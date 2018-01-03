//
// LoginViewController.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
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

using MonoTouch.Dialog;

using UIKit;

namespace ImapClientDemo.iOS
{
	public class LoginViewController : DialogViewController
	{
		readonly EntryElement hostEntry, portEntry, userEntry, passwordEntry;
		readonly FoldersViewController foldersViewController;
		readonly CheckboxElement sslCheckbox;

		public LoginViewController () : base (UITableViewStyle.Grouped, null)
		{
			hostEntry = new EntryElement ("Host", "imap.gmail.com", "imap.gmail.com");
			portEntry = new EntryElement ("Port", "993", "993") {
				KeyboardType = UIKeyboardType.NumberPad
			};
			sslCheckbox = new CheckboxElement ("Use SSL", true);

			userEntry = new EntryElement ("Username", "Email / Username", "");
			passwordEntry = new EntryElement ("Password", "password", "", true);

			Root = new RootElement ("IMAP Login") {
				new Section ("Server") {
					hostEntry,
					portEntry,
					sslCheckbox
				},
				new Section ("Account") {
					userEntry,
					passwordEntry
				},
				new Section {
					new StyledStringElement ("Login", Login)
				}
			};

			foldersViewController = new FoldersViewController ();
		}

		async void Login ()
		{
			int port;

			hostEntry.FetchValue ();
			portEntry.FetchValue ();
			userEntry.FetchValue ();
			passwordEntry.FetchValue ();

			int.TryParse (portEntry.Value, out port);

			try {
				if (Mail.Client.IsConnected)
					await Mail.Client.DisconnectAsync (true);

				// Note: for demo purposes, we're ignoring SSL validation errors (don't do this in production code)
				Mail.Client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

				// Connect to server
				await Mail.Client.ConnectAsync (hostEntry.Value, port, sslCheckbox.Value);

				try {
					// Authenticate now that we're connected
					await Mail.Client.AuthenticateAsync (userEntry.Value, passwordEntry.Value);

					// Show the folders view controller
					NavigationController.PushViewController (foldersViewController, true);
				} catch (Exception aex) {
					Console.WriteLine (aex);
					Mail.MessageBox ("Authentication Error",
						"Failed to Authenticate to server. If you are using GMail, then you probably " +
						"need to go into your GMail settings to enable \"less secure apps\" in order " + 
						"to get this demo to work.\n\n" +
						"For a real Mail application, you'll want to add support for obtaining the " +
						"user's OAuth2 credentials to prevent the need for user's to enable this, but " +
						"that is beyond the scope of this demo."
					);
				}
			} catch (Exception ex) {
				Console.WriteLine (ex);
				Mail.MessageBox ("Connection Error", "Failed to connect to server.");
			}
		}
	}
}
