//
// MainWindowViewModel.cs
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;

using MailKit.Security;

namespace ImapClientDemo
{
	// All view-model access stays on the UI context; the session owns all protocol access.
	// Unlike the session, awaits here intentionally retain the UI context so property
	// notifications and selection changes stay on WPF's dispatcher thread.
	sealed class MainWindowViewModel : INotifyPropertyChanged
	{
		ImapSession? session;
		CancellationTokenSource? connectionRequest;
		CancellationTokenSource? pageRequest;
		CancellationTokenSource? previewRequest;
		MailboxPage? page;
		bool connecting, disconnecting, loadingPage, loadingPreview;
		bool refreshPaused;
		string error = string.Empty;

		public event PropertyChangedEventHandler? PropertyChanged;

		public bool IsUpdatingBindings { get; private set; }
		public bool GoogleOAuthEnabled => OAuthSignIn.GoogleEnabled;
		public bool MicrosoftOAuthEnabled => OAuthSignIn.MicrosoftEnabled;
		public IReadOnlyList<FolderItem> Folders { get; private set; } = Array.Empty<FolderItem> ();
		public IReadOnlyList<MessageItem> Messages => page?.Messages ?? Array.Empty<MessageItem> ();
		public FolderItem? SelectedFolder { get; private set; }
		public MessageItem? SelectedMessage { get; private set; }
		public string Preview { get; private set; } = string.Empty;
		public bool CanConnect => session == null && !disconnecting;
		public bool CanDisconnect => session != null && !disconnecting;
		public bool IsConnected => session != null && !connecting;
		public bool CanRefresh => IsConnected && SelectedFolder != null && !loadingPage;
		public bool CanGoNewer => CanRefresh && page?.HasNewer == true;
		public bool CanGoOlder => CanRefresh && page?.HasOlder == true;
		public bool IsBusy => connecting || disconnecting || loadingPage || loadingPreview;
		public string PageDescription => page?.Description ?? "Select a folder";
		public string Error => error;
		public string Status => disconnecting ? "Disconnecting..." : connecting ? "Connecting..." :
			loadingPage ? "Loading messages..." : loadingPreview ? "Loading preview..." :
			IsConnected ? "Connected - read-only. Changes are monitored automatically." : "Not connected";

		void Notify ()
		{
			// Replacing ItemsSource can synchronously raise SelectionChanged. Those
			// binding-driven events must not be mistaken for a new user selection.
			IsUpdatingBindings = true;
			try {
				PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (null));
			} finally {
				IsUpdatingBindings = false;
			}
		}

		public void ReportError (Exception exception)
		{
			error = exception.Message;
			Notify ();
		}

		static void Cancel (ref CancellationTokenSource? source)
		{
			source?.Cancel ();
			source?.Dispose ();
			source = null;
		}

		public async Task ConnectAsync (string host, int port, SecureSocketOptions security, string userName, string secret, AuthenticationKind authentication)
		{
			if (!CanConnect)
				return;

			bool browserSignIn = authentication == AuthenticationKind.Google || authentication == AuthenticationKind.Microsoft365;

			if (string.IsNullOrWhiteSpace (host) || string.IsNullOrWhiteSpace (userName))
				throw new ArgumentException ("Enter a server and user name.");

			if (!browserSignIn && string.IsNullOrEmpty (secret))
				throw new ArgumentException ("Enter a password or access token.");

			if (port < 1 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port), "The port must be between 1 and 65535.");

			if (browserSignIn)
				OAuthSignIn.ValidateServer (authentication, host.Trim (), port, security);

			var current = new ImapSession ();
			session = current;

			connectionRequest = new CancellationTokenSource ();
			var token = connectionRequest.Token;

			connecting = true;
			error = string.Empty;

			Notify ();

			try {
				if (browserSignIn) {
					using var signInTimeout = CancellationTokenSource.CreateLinkedTokenSource (token);

					signInTimeout.CancelAfter (TimeSpan.FromMinutes (5));
					secret = await OAuthSignIn.GetAccessTokenAsync (authentication, userName.Trim (), signInTimeout.Token);
					token.ThrowIfCancellationRequested ();
				}

				var folders = await current.ConnectAsync (host.Trim (), port, security, userName.Trim (), secret, authentication != AuthenticationKind.Password, token);

				if (token.IsCancellationRequested)
					return;

				Folders = folders;
				connecting = false;
				Notify ();

				await SelectFolderAsync (folders.FirstOrDefault (folder => folder.IsInbox) ?? folders.FirstOrDefault ());
			} catch (OperationCanceledException) when (token.IsCancellationRequested) {
			} catch {
				if (session == current)
					await DisconnectAsync ();
				throw;
			} finally {
				if (session == current) {
					connecting = false;
					Notify ();
				}
			}
		}

		public async Task DisconnectAsync ()
		{
			var current = session;

			// Invalidate outstanding UI work before awaiting shutdown. Its continuations
			// may run while we wait, but must not repopulate the disconnected window.
			session = null;

			Cancel (ref connectionRequest);
			Cancel (ref pageRequest);
			Cancel (ref previewRequest);

			connecting = loadingPage = loadingPreview = false;
			Folders = Array.Empty<FolderItem> ();
			SelectedFolder = null;
			SelectedMessage = null;
			page = null;
			Preview = string.Empty;
			disconnecting = true;

			Notify ();

			try {
				if (current != null)
					await current.DisposeAsync ();
			} finally {
				disconnecting = false;
				Notify ();
			}
		}

		public async Task SelectFolderAsync (FolderItem? selected)
		{
			if (SelectedFolder == selected)
				return;

			Cancel (ref pageRequest);
			Cancel (ref previewRequest);
			SelectedFolder = selected;
			SelectedMessage = null;
			page = null;
			Preview = string.Empty;
			loadingPage = loadingPreview = false;
			Notify ();
			if (selected != null && IsConnected)
				await LoadPageAsync (0);
		}

		async Task LoadPageAsync (int pageIndex)
		{
			if (session == null || SelectedFolder == null)
				return;

			Cancel (ref pageRequest);

			pageRequest = new CancellationTokenSource ();

			var token = pageRequest.Token;
			var current = session;
			var folder = SelectedFolder;

			loadingPage = true;
			refreshPaused = false;
			error = string.Empty;

			Notify ();

			try {
				var result = await current.GetPageAsync (folder.FullName, pageIndex, token);

				// A newer selection or disconnect may have happened while awaiting FETCH.
				// Never let an obsolete result overwrite the current UI state.
				if (token.IsCancellationRequested)
					return;

				// Sequence indexes shift when messages are expunged. Preserve selection
				// by UID instead, and only while UIDVALIDITY still matches. Folder changes
				// clear the old page in SelectFolderAsync before reaching this point.
				var previous = SelectedMessage;
				var selection = page?.UidValidity == result.UidValidity && previous != null
					? result.Messages.FirstOrDefault (message => message.UniqueId == previous.UniqueId) : null;

				page = result;
				SelectedMessage = selection;

				if (selection == null) {
					Cancel (ref previewRequest);
					Preview = string.Empty;
					loadingPreview = false;
				}

				Notify ();
			} catch (OperationCanceledException) when (token.IsCancellationRequested) {
			} catch {
				// Do not retry a failing automatic refresh on every timer tick.
				// An explicit refresh or a new page/folder load resumes automatic updates.
				if (!token.IsCancellationRequested)
					refreshPaused = true;
				throw;
			} finally {
				// An obsolete request must not clear the loading state of its successor.
				if (!token.IsCancellationRequested) {
					loadingPage = false;
					Notify ();
				}
			}
		}

		public Task RefreshAsync ()
		{
			return LoadPageAsync (page?.PageIndex ?? 0);
		}

		public Task ChangePageAsync (int delta)
		{
			return LoadPageAsync ((page?.PageIndex ?? 0) + delta);
		}

		public async Task SelectMessageAsync (MessageItem? selected)
		{
			if (SelectedMessage == selected)
				return;

			Cancel (ref previewRequest);
			SelectedMessage = selected;
			Preview = string.Empty;
			loadingPreview = false;
			error = string.Empty;
			Notify ();

			if (selected == null || session == null || page == null)
				return;

			previewRequest = new CancellationTokenSource ();
			var token = previewRequest.Token;
			loadingPreview = true;
			Notify ();

			try {
				var text = await session.GetPreviewAsync (page, selected.UniqueId, token);
				// Cancellation leaves an in-flight FETCH free to finish safely, but its
				// text must not appear under a different message selected in the meantime.
				if (!token.IsCancellationRequested)
					Preview = text;
			} catch (OperationCanceledException) when (token.IsCancellationRequested) {
			} finally {
				if (!token.IsCancellationRequested) {
					loadingPreview = false;
					Notify ();
				}
			}
		}

		public async Task PollAsync ()
		{
			// This timer checks local state, not the server. IDLE/NOOP runs on the worker;
			// comparing versions coalesces a burst of notifications into a page refresh.
			if (session == null || connecting)
				return;

			if (session.Completion.IsCompleted) {
				// Dispose also observes and surfaces a failed background IDLE operation.
				await DisconnectAsync ();
				return;
			}

			if (!loadingPage && !refreshPaused && page != null && page.ChangeVersion != session.ChangeVersion)
				await RefreshAsync ();
		}
	}
}
