//
// IdleClient.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014-2020 Jeffrey Stedfast
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MailKit;
using MailKit.Net.Imap;

namespace ImapIdle
{
	public class IdleClient : IDisposable
	{
		List<IMessageSummary> messages;
		CancellationTokenSource cancel;
		CancellationTokenSource done;
		bool messagesArrived;
		ImapClient client;

		public IdleClient ()
		{
			client = new ImapClient (new ProtocolLogger (Console.OpenStandardError ()));
			messages = new List<IMessageSummary> ();
			cancel = new CancellationTokenSource ();
		}

		async Task ReconnectAsync ()
		{
			if (!client.IsConnected)
				await client.ConnectAsync (Program.Host, Program.Port, Program.SslOptions, cancel.Token);

			if (!client.IsAuthenticated) {
				await client.AuthenticateAsync (Program.Username, Program.Password, cancel.Token);

				await client.Inbox.OpenAsync (FolderAccess.ReadOnly, cancel.Token);
			}
		}

		async Task FetchMessageSummariesAsync (bool print)
		{
			IList<IMessageSummary> fetched;

			do {
				try {
					// fetch summary information for messages that we don't already have
					int startIndex = messages.Count;

					fetched = client.Inbox.Fetch (startIndex, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId, cancel.Token);
					break;
				} catch (ImapProtocolException) {
					// protocol exceptions often result in the client getting disconnected
					await ReconnectAsync ();
				} catch (IOException) {
					// I/O exceptions always result in the client getting disconnected
					await ReconnectAsync ();
				}
			} while (true);

			foreach (var message in fetched) {
				if (print)
					Console.WriteLine ("{0}: new message: {1}", client.Inbox, message.Envelope.Subject);
				messages.Add (message);
			}
		}

		async Task WaitForNewMessagesAsync ()
		{
			do {
				try {
					if (client.Capabilities.HasFlag (ImapCapabilities.Idle)) {
						// Note: IMAP servers are only supposed to drop the connection after 30 minutes, so normally
						// we'd IDLE for a max of, say, ~29 minutes... but GMail seems to drop idle connections after
						// about 10 minutes, so we'll only idle for 9 minutes.
						done = new CancellationTokenSource (new TimeSpan (0, 9, 0));
						try {
							await client.IdleAsync (done.Token, cancel.Token);
						} finally {
							done.Dispose ();
							done = null;
						}
					} else {
						// Note: we don't want to spam the IMAP server with NOOP commands, so lets wait a minute
						// between each NOOP command.
						await Task.Delay (new TimeSpan (0, 1, 0), cancel.Token);
						await client.NoOpAsync (cancel.Token);
					}
					break;
				} catch (ImapProtocolException) {
					// protocol exceptions often result in the client getting disconnected
					await ReconnectAsync ();
				} catch (IOException) {
					// I/O exceptions always result in the client getting disconnected
					await ReconnectAsync ();
				}
			} while (true);
		}

		async Task IdleAsync ()
		{
			do {
				try {
					await WaitForNewMessagesAsync ();

					if (messagesArrived) {
						await FetchMessageSummariesAsync (true);
						messagesArrived = false;
					}
				} catch (OperationCanceledException) {
					break;
				}
			} while (!cancel.IsCancellationRequested);
		}

		public async Task RunAsync ()
		{
			// connect to the IMAP server and get our initial list of messages
			try {
				await ReconnectAsync ();
				await FetchMessageSummariesAsync (false);
			} catch (OperationCanceledException) {
				await client.DisconnectAsync (true);
				return;
			}

			// Note: We capture client.Inbox here because cancelling IdleAsync() *may* require
			// disconnecting the IMAP client connection, and, if it does, the `client.Inbox`
			// property will no longer be accessible which means we won't be able to disconnect
			// our event handlers.
			var inbox = client.Inbox;

			// keep track of changes to the number of messages in the folder (this is how we'll tell if new messages have arrived).
			inbox.CountChanged += OnCountChanged;

			// keep track of messages being expunged so that when the CountChanged event fires, we can tell if it's
			// because new messages have arrived vs messages being removed (or some combination of the two).
			inbox.MessageExpunged += OnMessageExpunged;

			// keep track of flag changes
			inbox.MessageFlagsChanged += OnMessageFlagsChanged;

			await IdleAsync ();

			inbox.MessageFlagsChanged -= OnMessageFlagsChanged;
			inbox.MessageExpunged -= OnMessageExpunged;
			inbox.CountChanged -= OnCountChanged;

			await client.DisconnectAsync (true);
		}

		// Note: the CountChanged event will fire when new messages arrive in the folder and/or when messages are expunged.
		void OnCountChanged (object sender, EventArgs e)
		{
			var folder = (ImapFolder) sender;

			// Note: because we are keeping track of the MessageExpunged event and updating our
			// 'messages' list, we know that if we get a CountChanged event and folder.Count is
			// larger than messages.Count, then it means that new messages have arrived.
			if (folder.Count > messages.Count) {
				int arrived = folder.Count - messages.Count;

				if (arrived > 1)
					Console.WriteLine ("\t{0} new messages have arrived.", arrived);
				else
					Console.WriteLine ("\t1 new message has arrived.");

				// Note: your first instinct may be to fetch these new messages now, but you cannot do
				// that in this event handler (the ImapFolder is not re-entrant).
				//
				// Instead, cancel the `done` token and update our state so that we know new messages
				// have arrived. We'll fetch the summaries for these new messages later...
				messagesArrived = true;
				done?.Cancel ();
			}
		}

		void OnMessageExpunged (object sender, MessageEventArgs e)
		{
			var folder = (ImapFolder) sender;

			if (e.Index < messages.Count) {
				var message = messages[e.Index];

				Console.WriteLine ("{0}: message #{1} has been expunged: {2}", folder, e.Index, message.Envelope.Subject);

				// Note: If you are keeping a local cache of message information
				// (e.g. MessageSummary data) for the folder, then you'll need
				// to remove the message at e.Index.
				messages.RemoveAt (e.Index);
			} else {
				Console.WriteLine ("{0}: message #{1} has been expunged.", folder, e.Index);
			}
		}

		void OnMessageFlagsChanged (object sender, MessageFlagsChangedEventArgs e)
		{
			var folder = (ImapFolder) sender;

			Console.WriteLine ("{0}: flags have changed for message #{1} ({2}).", folder, e.Index, e.Flags);
		}

		public void Exit ()
		{
			cancel.Cancel ();
		}

		public void Dispose ()
		{
			client.Dispose ();
			cancel.Dispose ();
		}
	}
}
