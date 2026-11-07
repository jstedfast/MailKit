//
// ImapIdleSessionTests.cs
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

using System.Text;

using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapIdleSessionTests
	{
		const string IdleCapabilities = "IMAP4rev1 IDLE";
		const string NoIdleCapabilities = "IMAP4rev1";

		// A scripted IMAP server stream whose reads block until the client has sent the next
		// expected command (or until the test pushes unsolicited data), which allows testing
		// time-based behavior such as IDLE refreshing, coalescing and cancellation.
		internal sealed class ScriptedImapStream : Stream
		{
			static readonly TimeSpan ReadTimeoutSpan = TimeSpan.FromSeconds (10);

			readonly List<(string Command, string Response)> script;
			readonly MemoryStream sent = new MemoryStream ();
			readonly object sync = new object ();
			byte[] pending = Array.Empty<byte> ();
			int pendingIndex;
			bool disposed;
			int index;

			public ScriptedImapStream (string capabilities, params (string Command, string Response)[] script)
			{
				this.script = new List<(string, string)> (script);
				Push ($"* OK [CAPABILITY {capabilities}] Ready\r\n");
			}

			public bool Completed {
				get { lock (sync) return index == script.Count; }
			}

			public int CommandCount {
				get { lock (sync) return index; }
			}

			public override bool CanRead => true;
			public override bool CanWrite => true;
			public override bool CanSeek => false;
			public override bool CanTimeout => false;
			public override long Length => throw new NotSupportedException ();
			public override long Position { get => throw new NotSupportedException (); set => throw new NotSupportedException (); }

			void Enqueue (string data)
			{
				var bytes = Encoding.ASCII.GetBytes (data);
				var buffer = new byte[pending.Length - pendingIndex + bytes.Length];

				Buffer.BlockCopy (pending, pendingIndex, buffer, 0, pending.Length - pendingIndex);
				Buffer.BlockCopy (bytes, 0, buffer, pending.Length - pendingIndex, bytes.Length);
				pending = buffer;
				pendingIndex = 0;

				Monitor.PulseAll (sync);
			}

			public void Push (string data)
			{
				lock (sync)
					Enqueue (data);
			}

			public override int Read (byte[] buffer, int offset, int count)
			{
				var deadline = DateTime.UtcNow + ReadTimeoutSpan;

				lock (sync) {
					while (pendingIndex == pending.Length) {
						if (disposed)
							return 0;

						var remaining = deadline - DateTime.UtcNow;

						if (remaining <= TimeSpan.Zero || !Monitor.Wait (sync, remaining))
							throw new IOException ($"Timed out waiting for the client. Expected: {(index < script.Count ? script[index].Command : "<end of script>")} Sent: {Encoding.ASCII.GetString (sent.GetBuffer (), 0, (int) sent.Length)}");
					}

					int n = Math.Min (count, pending.Length - pendingIndex);
					Buffer.BlockCopy (pending, pendingIndex, buffer, offset, n);
					pendingIndex += n;

					return n;
				}
			}

			public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				return Task.Run (() => Read (buffer, offset, count), cancellationToken);
			}

			public override void Write (byte[] buffer, int offset, int count)
			{
				lock (sync) {
					sent.Write (buffer, offset, count);

					while (index < script.Count && sent.Length >= script[index].Command.Length) {
						var expected = script[index].Command;
						var actual = Encoding.ASCII.GetString (sent.GetBuffer (), 0, expected.Length);

						if (actual != expected)
							throw new IOException ($"Unexpected command. Expected: {expected} Actual: {Encoding.ASCII.GetString (sent.GetBuffer (), 0, (int) sent.Length)}");

						var remaining = sent.GetBuffer ().AsSpan (expected.Length, (int) sent.Length - expected.Length).ToArray ();
						sent.SetLength (0);
						sent.Write (remaining, 0, remaining.Length);

						Enqueue (script[index].Response);
						index++;
					}
				}
			}

			public override Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				Write (buffer, offset, count);
				return Task.CompletedTask;
			}

			public override void Flush ()
			{
			}

			public override Task FlushAsync (CancellationToken cancellationToken)
			{
				return Task.CompletedTask;
			}

			public override long Seek (long offset, SeekOrigin origin) => throw new NotSupportedException ();

			public override void SetLength (long value) => throw new NotSupportedException ();

			protected override void Dispose (bool disposing)
			{
				lock (sync) {
					disposed = true;
					Monitor.PulseAll (sync);
				}

				base.Dispose (disposing);
			}
		}

		static (string, string)[] Script (string capabilities, params (string, string)[] commands)
		{
			var script = new List<(string, string)> {
				("A00000000 LOGIN username password\r\n", $"A00000000 OK [CAPABILITY {capabilities}] Logged in\r\n"),
				("A00000001 LIST \"\" \"\"\r\n", "* LIST (\\Noselect) \"/\" \"\"\r\nA00000001 OK List completed.\r\n"),
				("A00000002 LIST \"\" \"INBOX\"\r\n", "* LIST (\\HasNoChildren) \"/\" INBOX\r\nA00000002 OK List completed.\r\n"),
				("A00000003 SELECT INBOX\r\n", "* 3 EXISTS\r\n* 0 RECENT\r\n* OK [UIDVALIDITY 1] UIDs valid.\r\n* OK [UIDNEXT 4] Predicted next UID.\r\nA00000003 OK [READ-WRITE] Select completed.\r\n")
			};

			script.AddRange (commands);

			return script.ToArray ();
		}

		static ImapClient Connect (ScriptedImapStream stream)
		{
			var client = new ImapClient { TagPrefix = 'A' };

			client.Connect (stream, "localhost", 143, SecureSocketOptions.None);
			client.Authenticate ("username", "password");
			client.Inbox.Open (FolderAccess.ReadWrite);

			return client;
		}

		static async Task<ImapClient> ConnectAsync (ScriptedImapStream stream)
		{
			var client = new ImapClient { TagPrefix = 'A' };

			await client.ConnectAsync (stream, "localhost", 143, SecureSocketOptions.None);
			await client.AuthenticateAsync ("username", "password");
			await client.Inbox.OpenAsync (FolderAccess.ReadWrite);

			return client;
		}

		static ImapIdleOptions Immediate (TimeSpan? refresh = null)
		{
			var options = new ImapIdleOptions { CoalesceDelay = TimeSpan.Zero };

			if (refresh.HasValue)
				options.RefreshInterval = refresh.Value;

			return options;
		}

		[Test]
		public void TestOptionsDefaults ()
		{
			var options = new ImapIdleOptions ();

			Assert.That (options.RefreshInterval, Is.EqualTo (ImapIdleOptions.DefaultRefreshInterval), "RefreshInterval");
			Assert.That (options.PollInterval, Is.EqualTo (ImapIdleOptions.DefaultPollInterval), "PollInterval");
			Assert.That (options.CoalesceDelay, Is.EqualTo (ImapIdleOptions.DefaultCoalesceDelay), "CoalesceDelay");
			Assert.That (ImapIdleOptions.DefaultRefreshInterval, Is.EqualTo (TimeSpan.FromMinutes (9)), "DefaultRefreshInterval");
			Assert.That (ImapIdleOptions.DefaultPollInterval, Is.EqualTo (TimeSpan.FromMinutes (1)), "DefaultPollInterval");
			Assert.That (ImapIdleOptions.DefaultCoalesceDelay, Is.EqualTo (TimeSpan.FromMilliseconds (250)), "DefaultCoalesceDelay");
		}

		[Test]
		public void TestOptionsValidation ()
		{
			var options = new ImapIdleOptions ();
			var tooLarge = TimeSpan.FromMilliseconds ((double) int.MaxValue + 1);

			Assert.Throws<ArgumentOutOfRangeException> (() => options.RefreshInterval = TimeSpan.Zero);
			Assert.Throws<ArgumentOutOfRangeException> (() => options.RefreshInterval = TimeSpan.FromSeconds (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => options.RefreshInterval = tooLarge);
			Assert.Throws<ArgumentOutOfRangeException> (() => options.PollInterval = TimeSpan.Zero);
			Assert.Throws<ArgumentOutOfRangeException> (() => options.PollInterval = TimeSpan.FromSeconds (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => options.PollInterval = tooLarge);
			Assert.Throws<ArgumentOutOfRangeException> (() => options.CoalesceDelay = TimeSpan.FromSeconds (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => options.CoalesceDelay = tooLarge);

			options.RefreshInterval = TimeSpan.FromMinutes (5);
			options.PollInterval = TimeSpan.FromSeconds (30);
			options.CoalesceDelay = TimeSpan.Zero;

			Assert.That (options.RefreshInterval, Is.EqualTo (TimeSpan.FromMinutes (5)), "RefreshInterval");
			Assert.That (options.PollInterval, Is.EqualTo (TimeSpan.FromSeconds (30)), "PollInterval");
			Assert.That (options.CoalesceDelay, Is.EqualTo (TimeSpan.Zero), "CoalesceDelay");
		}

		[Test]
		public void TestEventsAndChangesValidation ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities));

			using (var client = Connect (stream)) {
				var inbox = client.Inbox;
				var uids = new UniqueId[] { new UniqueId (1) };
				var summary = new MessageSummary (0);

				Assert.Throws<ArgumentNullException> (() => new CountChangedIdleEvent (null, 0, 1));
				Assert.Throws<ArgumentOutOfRangeException> (() => new CountChangedIdleEvent (inbox, -1, 1));
				Assert.Throws<ArgumentOutOfRangeException> (() => new CountChangedIdleEvent (inbox, 0, -1));
				Assert.Throws<ArgumentNullException> (() => new MessageExpungedIdleEvent (null, 0));
				Assert.Throws<ArgumentOutOfRangeException> (() => new MessageExpungedIdleEvent (inbox, -1));
				Assert.Throws<ArgumentNullException> (() => new MessagesVanishedIdleEvent (null, uids, false));
				Assert.Throws<ArgumentNullException> (() => new MessagesVanishedIdleEvent (inbox, null, false));
				Assert.Throws<ArgumentNullException> (() => new MessageChangedIdleEvent (null, summary));
				Assert.Throws<ArgumentNullException> (() => new MessageChangedIdleEvent (inbox, null));
				Assert.Throws<ArgumentNullException> (() => new FolderStatusChangedIdleEvent (null, StatusItems.Unread));
				Assert.Throws<ArgumentException> (() => new FolderStatusChangedIdleEvent (inbox, StatusItems.None));

				var countChanged = new CountChangedIdleEvent (inbox, 3, 5);
				Assert.That (countChanged.Folder, Is.SameAs (inbox), "CountChanged.Folder");
				Assert.That (countChanged.PreviousCount, Is.EqualTo (3), "CountChanged.PreviousCount");
				Assert.That (countChanged.Count, Is.EqualTo (5), "CountChanged.Count");

				var expunged = new MessageExpungedIdleEvent (inbox, 2);
				Assert.That (expunged.Index, Is.EqualTo (2), "MessageExpunged.Index");

				var vanished = new MessagesVanishedIdleEvent (inbox, uids, true);
				Assert.That (vanished.UniqueIds, Is.EqualTo (uids), "MessagesVanished.UniqueIds");
				Assert.That (vanished.Earlier, Is.True, "MessagesVanished.Earlier");

				var changed = new MessageChangedIdleEvent (inbox, summary);
				Assert.That (changed.Message, Is.SameAs (summary), "MessageChanged.Message");

				var status = new FolderStatusChangedIdleEvent (inbox, StatusItems.Unread);
				Assert.That (status.Item, Is.EqualTo (StatusItems.Unread), "FolderStatusChanged.Item");

				Assert.Throws<ArgumentNullException> (() => new ImapIdleChanges (null));
				Assert.Throws<ArgumentException> (() => new ImapIdleChanges (Array.Empty<ImapIdleEvent> ()));
				Assert.Throws<ArgumentException> (() => new ImapIdleChanges (new ImapIdleEvent[] { expunged, null }));

				var changes = new ImapIdleChanges (new ImapIdleEvent[] { expunged, status });
				Assert.That (changes.Events, Is.EqualTo (new ImapIdleEvent[] { expunged, status }), "Events");
				Assert.That (changes.MessagesArrived, Is.False, "MessagesArrived");

				changes = new ImapIdleChanges (new ImapIdleEvent[] { new CountChangedIdleEvent (inbox, 5, 3) });
				Assert.That (changes.MessagesArrived, Is.False, "MessagesArrived (count decreased)");

				changes = new ImapIdleChanges (new ImapIdleEvent[] { expunged, countChanged });
				Assert.That (changes.MessagesArrived, Is.True, "MessagesArrived (count increased)");
			}
		}

		[Test]
		public void TestPreconditions ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities,
				("A00000000 LOGIN username password\r\n", $"A00000000 OK [CAPABILITY {IdleCapabilities}] Logged in\r\n"),
				("A00000001 LIST \"\" \"\"\r\n", "* LIST (\\Noselect) \"/\" \"\"\r\nA00000001 OK List completed.\r\n"),
				("A00000002 LIST \"\" \"INBOX\"\r\n", "* LIST (\\HasNoChildren) \"/\" INBOX\r\nA00000002 OK List completed.\r\n"));
			var client = new ImapClient { TagPrefix = 'A' };
			var session = client.CreateIdleSession ();

			Assert.That (session.Client, Is.SameAs (client), "Client");
			Assert.Throws<ServiceNotConnectedException> (() => session.WaitForChanges ());
			Assert.ThrowsAsync<ServiceNotConnectedException> (() => session.WaitForChangesAsync ());

			client.Connect (stream, "localhost", 143, SecureSocketOptions.None);
			Assert.Throws<ServiceNotAuthenticatedException> (() => session.WaitForChanges ());

			client.Authenticate ("username", "password");
			Assert.Throws<InvalidOperationException> (() => session.WaitForChanges ());
			Assert.ThrowsAsync<InvalidOperationException> (() => session.WaitForChangesAsync ());

			client.Dispose ();

			Assert.Throws<ObjectDisposedException> (() => client.CreateIdleSession ());
			Assert.Throws<ObjectDisposedException> (() => session.WaitForChanges ());
		}

		static (string, string)[] CreateIdleEventsScript ()
		{
			return Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n* 5 EXISTS\r\n* 1 RECENT\r\n* 1 EXPUNGE\r\n* 2 FETCH (UID 3 FLAGS (\\Seen))\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n"),
				("A00000005 NOOP\r\n", "A00000005 OK Noop completed.\r\n"));
		}

		static void AssertIdleEvents (ImapFolder inbox, ImapIdleChanges changes)
		{
			Assert.That (changes.MessagesArrived, Is.True, "MessagesArrived");
			Assert.That (changes.Events, Has.Count.EqualTo (5), "Events");

			var exists = (CountChangedIdleEvent) changes.Events[0];
			Assert.That (exists.Folder, Is.SameAs (inbox), "Folder");
			Assert.That (exists.PreviousCount, Is.EqualTo (3), "EXISTS PreviousCount");
			Assert.That (exists.Count, Is.EqualTo (5), "EXISTS Count");

			var recent = (FolderStatusChangedIdleEvent) changes.Events[1];
			Assert.That (recent.Item, Is.EqualTo (StatusItems.Recent), "RECENT");

			var expunged = (MessageExpungedIdleEvent) changes.Events[2];
			Assert.That (expunged.Index, Is.EqualTo (0), "EXPUNGE Index");

			var fetch = (MessageChangedIdleEvent) changes.Events[3];
			Assert.That (fetch.Message.Index, Is.EqualTo (1), "FETCH Index");
			Assert.That (fetch.Message.UniqueId, Is.EqualTo (new UniqueId (1, 3)), "FETCH UniqueId");
			Assert.That (fetch.Message.Flags, Is.EqualTo (MessageFlags.Seen), "FETCH Flags");

			var flushed = (CountChangedIdleEvent) changes.Events[4];
			Assert.That (flushed.PreviousCount, Is.EqualTo (5), "Flushed PreviousCount");
			Assert.That (flushed.Count, Is.EqualTo (4), "Flushed Count");

			Assert.That (inbox.Count, Is.EqualTo (4), "inbox.Count");
		}

		[Test]
		public void TestWaitForChanges ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, CreateIdleEventsScript ());

			using (var client = Connect (stream)) {
				var session = client.CreateIdleSession ();
				var changes = session.WaitForChanges ();

				AssertIdleEvents ((ImapFolder) client.Inbox, changes);

				// The client should no longer be idle.
				client.NoOp ();

				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestWaitForChangesAsync ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, CreateIdleEventsScript ());

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession ();
				var changes = await session.WaitForChangesAsync ();

				AssertIdleEvents ((ImapFolder) client.Inbox, changes);

				// The client should no longer be idle.
				await client.NoOpAsync ();

				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		static (string, string)[] CreateCancelScript (string capabilities)
		{
			return Script (capabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n"),
				("A00000005 NOOP\r\n", "A00000005 OK Noop completed.\r\n"));
		}

		[Test]
		public void TestCancelWaitForChanges ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, CreateCancelScript (IdleCapabilities));

			using (var client = Connect (stream)) {
				var session = client.CreateIdleSession ();

				using (var cts = new CancellationTokenSource (TimeSpan.FromMilliseconds (100)))
					Assert.Catch<OperationCanceledException> (() => session.WaitForChanges (cts.Token));

				Assert.That (client.IsConnected, Is.True, "IsConnected");
				Assert.That (client.IsIdle, Is.False, "IsIdle");

				client.NoOp ();

				Assert.That (stream.Completed, Is.True, "Completed");

				using (var cts = new CancellationTokenSource ()) {
					cts.Cancel ();

					Assert.Catch<OperationCanceledException> (() => session.WaitForChanges (cts.Token));
				}
			}
		}

		[Test]
		public async Task TestCancelWaitForChangesAsync ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, CreateCancelScript (IdleCapabilities));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession ();

				using (var cts = new CancellationTokenSource (TimeSpan.FromMilliseconds (100)))
					Assert.CatchAsync<OperationCanceledException> (() => session.WaitForChangesAsync (cts.Token));

				Assert.That (client.IsConnected, Is.True, "IsConnected");
				Assert.That (client.IsIdle, Is.False, "IsIdle");

				await client.NoOpAsync ();

				Assert.That (stream.Completed, Is.True, "Completed");

				using (var cts = new CancellationTokenSource ()) {
					cts.Cancel ();

					Assert.CatchAsync<OperationCanceledException> (() => session.WaitForChangesAsync (cts.Token));
				}
			}
		}

		static (string, string)[] CreateRefreshScript ()
		{
			return Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n"),
				("A00000005 IDLE\r\n", "+ idling\r\n"),
				("DONE\r\n", "* 1 RECENT\r\n* 4 EXISTS\r\nA00000005 OK Idle completed.\r\n"));
		}

		static void AssertRefreshChanges (ImapIdleChanges changes)
		{
			Assert.That (changes.Events, Has.Count.EqualTo (2), "Events");

			var recent = (FolderStatusChangedIdleEvent) changes.Events[0];
			Assert.That (recent.Item, Is.EqualTo (StatusItems.Recent), "RECENT");

			var exists = (CountChangedIdleEvent) changes.Events[1];
			Assert.That (exists.PreviousCount, Is.EqualTo (3), "PreviousCount");
			Assert.That (exists.Count, Is.EqualTo (4), "Count");
			Assert.That (changes.MessagesArrived, Is.True, "MessagesArrived");
		}

		[Test]
		public void TestRefresh ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, CreateRefreshScript ());

			using (var client = Connect (stream)) {
				var session = client.CreateIdleSession (Immediate (TimeSpan.FromMilliseconds (50)));
				var changes = session.WaitForChanges ();

				AssertRefreshChanges (changes);
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestRefreshAsync ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, CreateRefreshScript ());

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (Immediate (TimeSpan.FromMilliseconds (50)));
				var changes = await session.WaitForChangesAsync ();

				AssertRefreshChanges (changes);
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		static (string, string)[] CreatePollScript ()
		{
			return Script (NoIdleCapabilities,
				("A00000004 NOOP\r\n", "A00000004 OK Noop completed.\r\n"),
				("A00000005 NOOP\r\n", "* 2 FETCH (FLAGS (\\Flagged))\r\nA00000005 OK Noop completed.\r\n"));
		}

		static void AssertPollChanges (ImapIdleChanges changes)
		{
			Assert.That (changes.Events, Has.Count.EqualTo (1), "Events");
			Assert.That (changes.MessagesArrived, Is.False, "MessagesArrived");

			var fetch = (MessageChangedIdleEvent) changes.Events[0];
			Assert.That (fetch.Message.Index, Is.EqualTo (1), "Index");
			Assert.That (fetch.Message.Flags, Is.EqualTo (MessageFlags.Flagged), "Flags");
		}

		[Test]
		public void TestPolling ()
		{
			var stream = new ScriptedImapStream (NoIdleCapabilities, CreatePollScript ());

			using (var client = Connect (stream)) {
				var session = client.CreateIdleSession (new ImapIdleOptions { PollInterval = TimeSpan.FromMilliseconds (50) });

				AssertPollChanges (session.WaitForChanges ());
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public void TestPollingWithCancellableToken ()
		{
			var stream = new ScriptedImapStream (NoIdleCapabilities, CreatePollScript ());

			using (var client = Connect (stream)) {
				var session = client.CreateIdleSession (new ImapIdleOptions { PollInterval = TimeSpan.FromMilliseconds (50) });

				using (var cts = new CancellationTokenSource ())
					AssertPollChanges (session.WaitForChanges (cts.Token));

				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestPollingAsync ()
		{
			var stream = new ScriptedImapStream (NoIdleCapabilities, CreatePollScript ());

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (new ImapIdleOptions { PollInterval = TimeSpan.FromMilliseconds (50) });

				AssertPollChanges (await session.WaitForChangesAsync ());
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public void TestCancelPolling ()
		{
			var stream = new ScriptedImapStream (NoIdleCapabilities, Script (NoIdleCapabilities,
				("A00000004 NOOP\r\n", "A00000004 OK Noop completed.\r\n")));

			using (var client = Connect (stream)) {
				var session = client.CreateIdleSession (new ImapIdleOptions { PollInterval = TimeSpan.FromMinutes (5) });

				using (var cts = new CancellationTokenSource (TimeSpan.FromMilliseconds (100)))
					Assert.Catch<OperationCanceledException> (() => session.WaitForChanges (cts.Token));

				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestCancelPollingAsync ()
		{
			var stream = new ScriptedImapStream (NoIdleCapabilities, Script (NoIdleCapabilities,
				("A00000004 NOOP\r\n", "A00000004 OK Noop completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (new ImapIdleOptions { PollInterval = TimeSpan.FromMinutes (5) });

				using (var cts = new CancellationTokenSource (TimeSpan.FromMilliseconds (100)))
					Assert.CatchAsync<OperationCanceledException> (() => session.WaitForChangesAsync (cts.Token));

				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestConcurrentWaits ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession ();
				var other = client.CreateIdleSession ();

				using (var cts = new CancellationTokenSource ()) {
					var wait = session.WaitForChangesAsync (cts.Token);

					// Wait for the IDLE command to be accepted by the server.
					while (!client.IsIdle)
						await Task.Delay (10);

					Assert.ThrowsAsync<InvalidOperationException> (() => session.WaitForChangesAsync (), "same session");
					Assert.ThrowsAsync<InvalidOperationException> (() => other.WaitForChangesAsync (), "other session");

					cts.Cancel ();

					Assert.CatchAsync<OperationCanceledException> (() => wait);
				}

				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestChangesOutsideOfWaitAreNotRecorded ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 NOOP\r\n", "* 5 EXISTS\r\nA00000004 OK Noop completed.\r\n"),
				("A00000005 IDLE\r\n", "+ idling\r\n* 1 EXPUNGE\r\n"),
				("DONE\r\n", "A00000005 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (Immediate ());

				await client.NoOpAsync ();
				Assert.That (client.Inbox.Count, Is.EqualTo (5), "Count");

				var changes = await session.WaitForChangesAsync ();

				Assert.That (changes.Events, Has.Count.EqualTo (2), "Events");
				Assert.That (changes.Events[0], Is.InstanceOf<MessageExpungedIdleEvent> (), "Events[0]");

				var count = (CountChangedIdleEvent) changes.Events[1];
				Assert.That (count.PreviousCount, Is.EqualTo (5), "PreviousCount");
				Assert.That (count.Count, Is.EqualTo (4), "Count");
				Assert.That (changes.MessagesArrived, Is.False, "MessagesArrived");
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestChangesAreKeptWhenCancelled ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n"),
				("DONE\r\n", "* 4 EXISTS\r\nA00000004 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession ();

				using (var cts = new CancellationTokenSource (TimeSpan.FromMilliseconds (100)))
					Assert.CatchAsync<OperationCanceledException> (() => session.WaitForChangesAsync (cts.Token));

				// The EXISTS response that arrived while ending the cancelled IDLE command
				// should be returned immediately, without issuing another command.
				var changes = await session.WaitForChangesAsync ();

				Assert.That (changes.MessagesArrived, Is.True, "MessagesArrived");
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestVanished ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n* VANISHED 1:2\r\n* VANISHED (EARLIER) 3\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (Immediate ());
				var changes = await session.WaitForChangesAsync ();

				Assert.That (changes.Events, Has.Count.EqualTo (3), "Events");

				var vanished = (MessagesVanishedIdleEvent) changes.Events[0];
				Assert.That (vanished.Earlier, Is.False, "Earlier");
				Assert.That (vanished.UniqueIds, Is.EqualTo (new UniqueId[] { new UniqueId (1, 1), new UniqueId (1, 2) }), "UniqueIds");

				var count = (CountChangedIdleEvent) changes.Events[1];
				Assert.That (count.PreviousCount, Is.EqualTo (3), "PreviousCount");
				Assert.That (count.Count, Is.EqualTo (1), "Count");

				vanished = (MessagesVanishedIdleEvent) changes.Events[2];
				Assert.That (vanished.Earlier, Is.True, "Earlier (2)");
				Assert.That (vanished.UniqueIds, Is.EqualTo (new UniqueId[] { new UniqueId (1, 3) }), "UniqueIds (2)");
			}
		}

		[Test]
		public async Task TestReadChangesAsync ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n* 4 EXISTS\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n"),
				("A00000005 IDLE\r\n", "+ idling\r\n* 5 EXISTS\r\n"),
				("DONE\r\n", "A00000005 OK Idle completed.\r\n"),
				("A00000006 IDLE\r\n", "+ idling\r\n"),
				("DONE\r\n", "A00000006 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (Immediate ());
				var counts = new List<int> ();

				using (var cts = new CancellationTokenSource ()) {
					Assert.CatchAsync<OperationCanceledException> (async () => {
						await foreach (var changes in session.ReadChangesAsync (cts.Token)) {
							counts.Add (((CountChangedIdleEvent) changes.Events[0]).Count);

							if (counts.Count == 2)
								cts.CancelAfter (TimeSpan.FromMilliseconds (50));
						}
					});
				}

				Assert.That (counts, Is.EqualTo (new int[] { 4, 5 }), "Counts");
				Assert.That (stream.Completed, Is.True, "Completed");
			}
		}

		[Test]
		public async Task TestFolderStatusChanged ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n* STATUS INBOX (MESSAGES 3 UNSEEN 2 UIDNEXT 10 UIDVALIDITY 2 HIGHESTMODSEQ 5 SIZE 100 MAILBOXID (abc) DELETED 1)\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (Immediate ());
				var changes = await session.WaitForChangesAsync ();
				var items = new List<StatusItems> ();

				foreach (var e in changes.Events)
					items.Add (((FolderStatusChangedIdleEvent) e).Item);

				Assert.That (items, Is.EquivalentTo (new StatusItems[] { StatusItems.Unread, StatusItems.UidNext, StatusItems.UidValidity, StatusItems.HighestModSeq, StatusItems.Size, StatusItems.MailboxId, StatusItems.Deleted }), "Items");
				Assert.That (changes.MessagesArrived, Is.False, "MessagesArrived");
			}
		}

		[Test]
		public async Task TestExpungeFollowedByExists ()
		{
			var stream = new ScriptedImapStream (IdleCapabilities, Script (IdleCapabilities,
				("A00000004 IDLE\r\n", "+ idling\r\n* 1 EXPUNGE\r\n* 3 EXISTS\r\n"),
				("DONE\r\n", "A00000004 OK Idle completed.\r\n")));

			using (var client = await ConnectAsync (stream)) {
				var session = client.CreateIdleSession (Immediate ());
				var changes = await session.WaitForChangesAsync ();

				Assert.That (changes.Events, Has.Count.EqualTo (3), "Events");
				Assert.That (changes.Events[0], Is.InstanceOf<MessageExpungedIdleEvent> (), "Events[0]");

				var expunged = (CountChangedIdleEvent) changes.Events[1];
				Assert.That (expunged.PreviousCount, Is.EqualTo (3), "Expunged PreviousCount");
				Assert.That (expunged.Count, Is.EqualTo (2), "Expunged Count");

				var exists = (CountChangedIdleEvent) changes.Events[2];
				Assert.That (exists.PreviousCount, Is.EqualTo (2), "Exists PreviousCount");
				Assert.That (exists.Count, Is.EqualTo (3), "Exists Count");

				Assert.That (changes.MessagesArrived, Is.True, "MessagesArrived");
			}
		}
	}
}