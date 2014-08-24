//
// Program.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;

using MailKit.Net.Imap;
using MailKit;

namespace ImapIdle {
	class Program
	{
		public static void Main (string[] args)
		{
			var logger = new ProtocolLogger (Console.OpenStandardError ());

			using (var client = new ImapClient (logger)) {
				var credentials = new NetworkCredential ("username@gmail.com", "password");
				var uri = new Uri ("imaps://imap.gmail.com");

				client.Connect (uri);

				// Remove the XOAUTH2 authentication mechanism since we don't have an OAuth2 token.
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				client.Authenticate (credentials);

				client.Inbox.Open (FolderAccess.ReadOnly);

				// keep track of the messages
				var messages = client.Inbox.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId).ToList ();
				int count = messages.Count;

				// connect to some events...
				client.Inbox.CountChanged += (sender, e) => {
					// Note: the CountChanged event will fire when new messages arrive in the folder.
					var folder = (ImapFolder) sender;

					// New messages have arrived in the folder.
					Console.WriteLine ("{0}: {1} new messages have arrived.", folder, folder.Count - count);

					// Update our count so we can figure out how many new messages arrived the next time this
					// event fires.
					count = folder.Count;

					// Note: your first instict may be to fetch these new messages now, but you cannot do
					// that in an event handler (the ImapFolder is not re-entrant).
				};

				client.Inbox.MessageExpunged += (sender, e) => {
					var folder = (ImapFolder) sender;

					if (e.Index < messages.Count) {
						var message = messages[e.Index];

						Console.WriteLine ("{0}: expunged message {1}: Subject: {2}", folder, e.Index, message.Envelope.Subject);

						// Note: If you are keeping a local cache of message information
						// (e.g. MessageSummary data) for the folder, then you'll need
						// to remove the message at e.Index.
						messages.RemoveAt (e.Index);
					} else {
						Console.WriteLine ("{0}: expunged message {1}: Unknown message.", folder, e.Index);
					}
				};

				client.Inbox.MessageFlagsChanged += (sender, e) => {
					var folder = (ImapFolder) sender;

					Console.WriteLine ("{0}: flags for message {1} have changed to: {2}.", folder, e.Index, e.Flags);
				};

				Console.WriteLine ("Hit any key to end the IDLE loop.");
				using (var done = new CancellationTokenSource ()) {
					var thread = new Thread (IdleLoop);

					thread.Start (new IdleState (client, done.Token));

					Console.ReadKey ();
					done.Cancel ();
					thread.Join ();
				}

				if (client.Inbox.Count > messages.Count) {
					Console.WriteLine ("The new messages that arrived during IDLE are:");
					foreach (var message in client.Inbox.Fetch (messages.Count, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId))
						Console.WriteLine ("Subject: {0}", message.Envelope.Subject);
				}

				client.Disconnect (true);
			}
		}

		class IdleState
		{
			readonly object mutex = new object ();
			CancellationTokenSource timeout;

			/// <summary>
			/// Gets the cancellation token.
			/// </summary>
			/// <remarks>
			/// <para>The cancellation token is the brute-force approach to cancelling the IDLE and/or NOOP command.</para>
			/// <para>Using the cancellation token will typically drop the connection to the server and so should
			/// not be used unless the client is in the process of shutting down or otherwise needs to
			/// immediately abort communication with the server.</para>
			/// </remarks>
			/// <value>The cancellation token.</value>
			public CancellationToken CancellationToken { get; private set; }

			/// <summary>
			/// Gets the done token.
			/// </summary>
			/// <remarks>
			/// <para>The done token tells the <see cref="Program.IdleLoop"/> that the user has requested to end the loop.</para>
			/// <para>When the done token is cancelled, the <see cref="Program.IdleLoop"/> will gracefully come to an end by
			/// cancelling the timeout and then breaking out of the loop.</para>
			/// </remarks>
			/// <value>The done token.</value>
			public CancellationToken DoneToken { get; private set; }

			/// <summary>
			/// Gets the IMAP client.
			/// </summary>
			/// <value>The IMAP client.</value>
			public ImapClient Client { get; private set; }

			/// <summary>
			/// Checks whether or not either of the CancellationToken's have been cancelled.
			/// </summary>
			/// <value><c>true</c> if cancellation was requested; otherwise, <c>false</c>.</value>
			public bool IsCancellationRequested {
				get {
					return CancellationToken.IsCancellationRequested || DoneToken.IsCancellationRequested;
				}
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="IdleState"/> class.
			/// </summary>
			/// <param name="client">The IMAP client.</param>
			/// <param name="doneToken">The user-controlled 'done' token.</param>
			/// <param name="cancellationToken">The brute-force cancellation token.</param>
			public IdleState (ImapClient client, CancellationToken doneToken, CancellationToken cancellationToken = default (CancellationToken))
			{
				CancellationToken = cancellationToken;
				DoneToken = doneToken;
				Client = client;

				// When the user hits a key, end the current timeout as well
				doneToken.Register (CancelTimeout);
			}

			/// <summary>
			/// Cancels the timeout token source, forcing ImapClient.Idle() to gracefully exit.
			/// </summary>
			void CancelTimeout ()
			{
				lock (mutex) {
					if (timeout != null)
						timeout.Cancel ();
				}
			}

			public void SetTimeoutSource (CancellationTokenSource source)
			{
				lock (mutex) {
					timeout = source;

					if (timeout != null && IsCancellationRequested)
						timeout.Cancel ();
				}
			}
		}

		static void IdleLoop (object state)
		{
			var idle = (IdleState) state;

			lock (idle.Client.SyncRoot) {
				// Note: since the IMAP server will drop the connection after 30 minutes, we must loop sending IDLE commands that
				// last ~29 minutes or until the user has requested that they do not want to IDLE anymore.
				//
				// For GMail, we use a 9 minute interval because they do not seem to keep the connection alive for more than ~10 minutes.
				while (!idle.IsCancellationRequested) {
					// Note: In .NET 4.5, you can make this simpler by using the CancellationTokenSource .ctor that
					// takes a TimeSpan argument, thus eliminating the need to create a timer.
					using (var timeout = new CancellationTokenSource ()) {
						using (var timer = new System.Timers.Timer (9 * 60 * 1000)) {
							// End the IDLE command after 29 minutes... (most servers will disconnect the client after 30 minutes)
							timer.Elapsed += (sender, e) => timeout.Cancel ();
							timer.AutoReset = false;
							timer.Enabled = true;

							try {
								// We set the timeout source so that if the idle.DoneToken is cancelled, it can cancel the timeout
								idle.SetTimeoutSource (timeout);

								if (idle.Client.Capabilities.HasFlag (ImapCapabilities.Idle)) {
									// The Idle() method will not return until the timeout has elapsed or idle.CancellationToken is cancelled
									idle.Client.Idle (timeout.Token, idle.CancellationToken);
								} else {
									// The IMAP server does not support IDLE, so send a NOOP command instead
									idle.Client.NoOp (idle.CancellationToken);

									// Wait for the timeout to elapse or the cancellation token to be cancelled
									WaitHandle.WaitAny (new [] { timeout.Token.WaitHandle, idle.CancellationToken.WaitHandle });
								}
							} catch (OperationCanceledException) {
								// This means that idle.CancellationToken was cancelled, not the DoneToken nor the timeout.
								break;
							} catch (ImapProtocolException) {
								// The IMAP server sent garbage in a response and the ImapClient was unable to deal with it.
								// This should never happen in practice, but it's probably still a good idea to handle it.
								//
								// Note: an ImapProtocolException almost always results in the ImapClient getting disconnected.
								break;
							} catch (ImapCommandException) {
								// The IMAP server responded with "NO" or "BAD" to either the IDLE command or the NOOP command.
								// This should never happen... but again, we're catching it for the sake of completeness.
								break;
							} finally {
								// We're about to Dispose() the timeout source, so set it to null.
								idle.SetTimeoutSource (null);
							}
						}
					}
				}
			}
		}
	}
}
