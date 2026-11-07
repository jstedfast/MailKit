//
// ImapSession.cs
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

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Generic;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

using MimeKit;
using MimeKit.Text;

namespace ImapClientDemo
{
	// IMAP commands and IDLE share one connection and must not run concurrently.
	// UI requests enter a queue; only its worker accesses the client and live folders.
	// Returning snapshots instead of IMailFolder objects keeps that ownership boundary intact.
	sealed class ImapSession : IAsyncDisposable
	{
		sealed class WorkItem
		{
			public WorkItem (Func<Task> run, Action<Exception> fail)
			{
				Run = run;
				Fail = fail;
			}

			public Func<Task> Run { get; }
			public Action<Exception> Fail { get; }
		}

		readonly Channel<WorkItem> commands = Channel.CreateUnbounded<WorkItem> (new UnboundedChannelOptions { SingleReader = true });
		readonly CancellationTokenSource shutdown = new CancellationTokenSource ();
		readonly ImapClient client;
		readonly Task worker;
		IMailFolder? folder;
		bool fetchingPage;
		long changeVersion;
		int disposed;
		int sourceDisposed;

		public ImapSession () : this (new ImapClient ())
		{
		}

		internal ImapSession (ImapClient client)
		{
			this.client = client;
			worker = Task.Run (RunAsync);
		}

		// This is a local invalidation counter, not an IMAP MODSEQ. The UI can compare it
		// with its page snapshot without inspecting protocol state from another thread.
		public long ChangeVersion => Interlocked.Read (ref changeVersion);
		public Task Completion => worker;

		Task<T> EnqueueAsync<T> (Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
		{
			// Completing a request must not execute its caller's continuation inline on
			// the protocol worker, which still has connection housekeeping to do.
			var completion = new TaskCompletionSource<T> (TaskCreationOptions.RunContinuationsAsynchronously);

			var item = new WorkItem (
				async () => {
					try {
						// Rapid selection changes can leave obsolete requests in the queue.
						// Skip those before they consume any network bandwidth.
						cancellationToken.ThrowIfCancellationRequested ();

						using var timeout = CancellationTokenSource.CreateLinkedTokenSource (shutdown.Token);
						timeout.CancelAfter (TimeSpan.FromSeconds (30));

						// Selection cancellation discards results, not an in-flight IMAP response.
						// Aborting that response could unnecessarily disconnect the client.
						var result = await action (timeout.Token).ConfigureAwait (false);
						cancellationToken.ThrowIfCancellationRequested ();
						completion.TrySetResult (result);
					} catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || shutdown.IsCancellationRequested) {
						completion.TrySetCanceled ();
					} catch (OperationCanceledException ex) {
						completion.TrySetException (new TimeoutException ("The IMAP operation timed out.", ex));
					} catch (Exception ex) {
						completion.TrySetException (ex);
					}
				},
				ex => completion.TrySetException (ex));

			if (!commands.Writer.TryWrite (item))
				completion.TrySetException (new InvalidOperationException ("This IMAP session has ended. Connect again."));

			return completion.Task;
		}

		public Task<IReadOnlyList<FolderItem>> ConnectAsync (string host, int port, SecureSocketOptions security, string userName, string secret, bool oauth2, CancellationToken cancellationToken)
		{
			if (security != SecureSocketOptions.SslOnConnect && security != SecureSocketOptions.StartTls)
				throw new ArgumentException ("TLS is required.", nameof (security));

			return EnqueueAsync<IReadOnlyList<FolderItem>> (async token => {
				await client.ConnectAsync (host, port, security, token).ConfigureAwait (false);
				if (oauth2)
					await client.AuthenticateAsync (new SaslMechanismOAuth2 (userName, secret), token).ConfigureAwait (false);
				else
					await client.AuthenticateAsync (userName, secret, token).ConfigureAwait (false);

				// Include shared/other users' folders when advertised, not just personal mail.
				// Namespace listings may overlap, so deduplicate by the server's full folder name.
				var namespaces = client.PersonalNamespaces.Concat (client.SharedNamespaces).Concat (client.OtherNamespaces);
				var folders = new Dictionary<string, FolderItem> (StringComparer.Ordinal) {
					[client.Inbox.FullName] = new FolderItem (client.Inbox.FullName, true)
				};

				foreach (var ns in namespaces) {
					foreach (var item in await client.GetFoldersAsync (ns, cancellationToken: token).ConfigureAwait (false)) {
						if ((item.Attributes & (FolderAttributes.NoSelect | FolderAttributes.NonExistent)) == 0)
							folders[item.FullName] = new FolderItem (item.FullName, item == client.Inbox);
					}
				}

				return folders.Values.OrderByDescending (item => item.IsInbox).ThenBy (item => item.FullName, StringComparer.OrdinalIgnoreCase).ToArray ();
			}, cancellationToken);
		}

		public Task<MailboxPage> GetPageAsync (string folderName, int pageIndex, CancellationToken cancellationToken)
		{
			return EnqueueAsync (async token => {
				var selected = await client.GetFolderAsync (folderName, token).ConfigureAwait (false);
				if (folder != selected || !selected.IsOpen) {
					DetachFolder ();

					// Selecting a new folder replaces the old selection on this connection.
					// Read-only access keeps browsing from modifying flags such as \Seen.
					await selected.OpenAsync (FolderAccess.ReadOnly, token).ConfigureAwait (false);

					folder = selected;
					folder.CountChanged += OnFolderChanged;
					folder.MessageExpunged += OnFolderChanged;
					folder.MessagesVanished += OnFolderChanged;
					folder.MessageFlagsChanged += OnFolderChanged;
					folder.UidValidityChanged += OnFolderChanged;
					folder.MessageSummaryFetched += OnSummaryFetched;
				}

				IList<IMessageSummary> summaries = Array.Empty<IMessageSummary> ();
				// Capture before FETCH: a change arriving during the command must leave
				// this snapshot stale so the UI schedules another refresh.
				long version = ChangeVersion;
				int count = selected.Count;
				var (min, max) = MailboxPage.GetRange (count, pageIndex);

				if (count > 0) {
					// Keep list requests small; BODYSTRUCTURE and body content are fetched
					// only when a message is selected. An empty folder needs no FETCH.
					var request = new FetchRequest (MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags);

					fetchingPage = true;

					try {
						summaries = await selected.FetchAsync (min, max, request, token).ConfigureAwait (false);
					} finally {
						fetchingPage = false;
					}
				}

				return new MailboxPage (folderName, selected.UidValidity, count, pageIndex, version, summaries);
			}, cancellationToken);
		}

		public Task<string> GetPreviewAsync (MailboxPage page, UniqueId uid, CancellationToken cancellationToken)
		{
			return EnqueueAsync (async token => {
				// A UID identifies a message only within its folder and UIDVALIDITY.
				// After a folder switch or UIDVALIDITY reset, an old row is not a safe lookup.
				if (folder == null || !folder.IsOpen || folder.FullName != page.FolderName || folder.UidValidity != page.UidValidity)
					throw new InvalidOperationException ("The selected folder has changed. Refresh the message list.");

				var request = new FetchRequest (MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);
				var summaries = await folder.FetchAsync (new[] { uid }, request, token).ConfigureAwait (false);
				var summary = summaries.FirstOrDefault (item => item.UniqueId == uid);

				if (summary == null)
					return "This message is no longer available. Refresh the message list.";

				// BODYSTRUCTURE lets us locate the preferred text part without downloading
				// the entire message, including potentially large attachments.
				var body = summary.TextBody ?? summary.HtmlBody;
				if (body == null)
					return "This message has no text body. Attachment viewing is not included in this demo.";

				using var entity = await folder.GetBodyPartAsync (uid, body, token).ConfigureAwait (false);
				if (entity is not TextPart text)
					throw new InvalidOperationException ("The server returned a non-text body part.");

				// HTML is converted to inert text: no browser, script, remote images or link navigation.
				return text.IsHtml ? new HtmlToText ().Convert (text.Text) : text.Text;
			}, cancellationToken);
		}

		void OnFolderChanged (object? sender, EventArgs e)
		{
			Interlocked.Increment (ref changeVersion);
		}

		void OnSummaryFetched (object? sender, MessageSummaryFetchedEventArgs e)
		{
			// FETCH can include unsolicited flag updates. The page request already incorporates
			// them, but updates received during a preview request must invalidate the list.
			if (!fetchingPage && e.Message.Flags.HasValue)
				Interlocked.Increment (ref changeVersion);
		}

		void DetachFolder ()
		{
			if (folder == null)
				return;

			folder.CountChanged -= OnFolderChanged;
			folder.MessageExpunged -= OnFolderChanged;
			folder.MessagesVanished -= OnFolderChanged;
			folder.MessageFlagsChanged -= OnFolderChanged;
			folder.UidValidityChanged -= OnFolderChanged;
			folder.MessageSummaryFetched -= OnSummaryFetched;
			folder = null;
		}

		async Task WaitForWorkOrChangesAsync ()
		{
			using var stopWaiting = CancellationTokenSource.CreateLinkedTokenSource (shutdown.Token);
			var ready = commands.Reader.WaitToReadAsync (stopWaiting.Token).AsTask ();

			// A fresh session avoids retaining notifications from a previously selected folder.
			// ImapIdleSession uses IDLE when available and NOOP polling otherwise.
			var idle = client.CreateIdleSession ().WaitForChangesAsync (stopWaiting.Token);

			// Either a queued command or a server change wakes the worker. Cancel the
			// other wait, then finish leaving IDLE before sending any queued command.
			// Task.WhenAny alone is not enough: IDLE still owns the connection until DONE completes.
			try {
				await Task.WhenAny (ready, idle).ConfigureAwait (false);
			} finally {
				stopWaiting.Cancel ();
			}

			try {
				await idle.WaitAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (false);
			} catch (OperationCanceledException) when (stopWaiting.IsCancellationRequested) {
				// Cancelling an IDLE wait sends DONE and leaves the connection usable.
			} catch (TimeoutException) {
				// The server did not acknowledge DONE. Disconnect is the documented escape hatch.
				client.Disconnect (false);
				await idle.ConfigureAwait (false);
				throw;
			} finally {
				try {
					await ready.ConfigureAwait (false);
				} catch (OperationCanceledException) when (stopWaiting.IsCancellationRequested) {
				}
			}
		}

		async Task RunAsync ()
		{
			Exception ended = new OperationCanceledException ("The IMAP session has ended.");

			try {
				while (!shutdown.IsCancellationRequested) {
					if (commands.Reader.TryRead (out var command)) {
						bool wasConnected = client.IsConnected;

						await command.Run ().ConfigureAwait (false);

						// WorkItem reports command errors to its caller. If that error also
						// broke the connection, end the session rather than accept more work.
						if (wasConnected && !client.IsConnected && !shutdown.IsCancellationRequested)
							throw new IOException ("The IMAP connection was lost. Connect again.");
					} else if (client.IsConnected && client.IsAuthenticated && folder?.IsOpen == true) {
						await WaitForWorkOrChangesAsync ().ConfigureAwait (false);
					} else if (!await commands.Reader.WaitToReadAsync (shutdown.Token).ConfigureAwait (false)) {
						break;
					}
				}
			} catch (OperationCanceledException) when (shutdown.IsCancellationRequested) {
			} catch (Exception ex) {
				ended = ex;
				throw;
			} finally {
				commands.Writer.TryComplete ();

				// Every accepted request needs an outcome, even if the worker failed
				// before reaching it; otherwise its UI handler would wait indefinitely.
				while (commands.Reader.TryRead (out var pending))
					pending.Fail (ended);

				DetachFolder ();

				try {
					if (client.IsConnected) {
						using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (5));
						await client.DisconnectAsync (true, timeout.Token).ConfigureAwait (false);
					}
				} finally {
					client.Dispose ();
				}
			}
		}

		public async ValueTask DisposeAsync ()
		{
			// Shutdown may be requested by both Disconnect and window closing. Signal
			// it once, but let every caller await the worker's logout and client disposal.
			if (Interlocked.Exchange (ref disposed, 1) == 0) {
				commands.Writer.TryComplete ();
				shutdown.Cancel ();
			}

			try {
				await worker.ConfigureAwait (false);
			} finally {
				if (Interlocked.Exchange (ref sourceDisposed, 1) == 0)
					shutdown.Dispose ();
			}
		}
	}
}
