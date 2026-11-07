//
// MainWindow.xaml.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using MailKit.Security;

namespace ImapClientDemo
{
	public partial class MainWindow : Window
	{
		readonly MainWindowViewModel model = new MainWindowViewModel ();
		readonly DispatcherTimer timer;
		bool closing, closed;

		public MainWindow ()
		{
			InitializeComponent ();
			DataContext = model;
			timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds (500) };
			timer.Tick += OnTick;
			timer.Start ();
		}

		async Task RunAsync (Func<Task> action)
		{
			// WPF event handlers are async void, so their exceptions cannot be awaited
			// by WPF. Keep that boundary here and surface failures in the window.
			try {
				await action ();
			} catch (Exception ex) {
				Trace.TraceError ("{0}", ex);
				model.ReportError (ex);
			}
		}

		async void OnConnect (object sender, RoutedEventArgs e)
		{
			await RunAsync (async () => {
				if (!int.TryParse (Port.Text, out int port))
					throw new ArgumentException ("Enter a numeric port between 1 and 65535.");

				var security = Encryption.SelectedIndex == 0 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
				var secret = Secret.Password;

				Secret.Clear ();

				await model.ConnectAsync (Server.Text, port, security, UserName.Text, secret, (AuthenticationKind) Authentication.SelectedValue);
			});
		}

		void OnEncryptionChanged (object sender, SelectionChangedEventArgs e)
		{
			if (Port != null && (Port.Text == "993" || Port.Text == "143"))
				Port.Text = Encryption.SelectedIndex == 0 ? "993" : "143";
		}

		void OnAuthenticationChanged (object sender, SelectionChangedEventArgs e)
		{
			if (Secret == null || Authentication.SelectedValue is not AuthenticationKind kind)
				return;

			bool browserSignIn = kind == AuthenticationKind.Google || kind == AuthenticationKind.Microsoft365;

			Secret.IsEnabled = !browserSignIn;

			if (browserSignIn) {
				Secret.Clear ();
				Server.Text = kind == AuthenticationKind.Google ? "imap.gmail.com" : "outlook.office365.com";
				Encryption.SelectedIndex = 0;
				Port.Text = "993";
			}
		}

		async void OnDisconnect (object sender, RoutedEventArgs e)
		{
			await RunAsync (model.DisconnectAsync);
		}

		async void OnFolderSelected (object sender, SelectionChangedEventArgs e)
		{
			if (model.IsUpdatingBindings)
				return;

			await RunAsync (() => model.SelectFolderAsync (((ListBox) sender).SelectedItem as FolderItem));
		}

		async void OnMessageSelected (object sender, SelectionChangedEventArgs e)
		{
			if (model.IsUpdatingBindings)
				return;

			await RunAsync (() => model.SelectMessageAsync (((DataGrid) sender).SelectedItem as MessageItem));
		}

		async void OnRefresh (object sender, RoutedEventArgs e)
		{
			await RunAsync (model.RefreshAsync);
		}

		async void OnNewer (object sender, RoutedEventArgs e)
		{
			await RunAsync (() => model.ChangePageAsync (-1));
		}

		async void OnOlder (object sender, RoutedEventArgs e)
		{
			await RunAsync (() => model.ChangePageAsync (1));
		}

		async void OnTick (object? sender, EventArgs e)
		{
			await RunAsync (model.PollAsync);
		}

		async void OnClosing (object? sender, CancelEventArgs e)
		{
			if (closed)
				return;

			// WPF cannot await Closing. Keep the window alive until the session has
			// stopped its worker and disposed the connection, then request close again.
			e.Cancel = true;
			if (closing)
				return;

			closing = true;
			IsEnabled = false;
			timer.Stop ();

			try {
				await model.DisconnectAsync ();
			} catch (Exception ex) {
				Trace.TraceError ("{0}", ex);
				MessageBox.Show (this, ex.Message, "Disconnect failed", MessageBoxButton.OK, MessageBoxImage.Warning);
			} finally {
				closed = true;
				// Disconnect can finish synchronously when already disconnected.
				// Defer Close so we never call it recursively from the current Closing event.
				_ = Dispatcher.BeginInvoke (new Action (Close));
			}
		}
	}
}
