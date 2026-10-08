//
// ImapRequestCallbackTests.cs
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
using System.Globalization;

using MimeKit;

using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapRequestCallbackTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		static readonly UniqueId[] Uids = { new UniqueId (1), new UniqueId (2), new UniqueId (3) };
		static readonly int[] Indexes = { 0, 1, 2 };

		class RecordingStoreFlagsRequest : StoreFlagsRequest
		{
			public readonly List<IList<UniqueId>> StartedUids = new List<IList<UniqueId>> ();
			public readonly List<IList<int>> StartedIndexes = new List<IList<int>> ();
			public readonly List<IList<UniqueId>> CompletedUids = new List<IList<UniqueId>> ();
			public readonly List<IList<UniqueId>> UnmodifiedUids = new List<IList<UniqueId>> ();
			public readonly List<IList<int>> CompletedIndexes = new List<IList<int>> ();
			public readonly List<IList<int>> UnmodifiedIndexes = new List<IList<int>> ();
			public readonly List<IMailFolder> Folders = new List<IMailFolder> ();
			public bool ThrowOnStarted;

			public RecordingStoreFlagsRequest (StoreAction action, MessageFlags flags) : base (action, flags)
			{
			}

			public override void OnStarted (IMailFolder folder, IList<UniqueId> uids)
			{
				if (ThrowOnStarted)
					throw new InvalidOperationException ("OnStarted");

				Folders.Add (folder);
				StartedUids.Add (uids.ToArray ());
			}

			public override void OnStarted (IMailFolder folder, IList<int> indexes)
			{
				Folders.Add (folder);
				StartedIndexes.Add (indexes.ToArray ());
			}

			public override void OnCompleted (IMailFolder folder, IList<UniqueId> uids, IList<UniqueId> unmodified)
			{
				CompletedUids.Add (uids.ToArray ());
				UnmodifiedUids.Add (unmodified.ToArray ());
			}

			public override void OnCompleted (IMailFolder folder, IList<int> indexes, IList<int> unmodified)
			{
				CompletedIndexes.Add (indexes.ToArray ());
				UnmodifiedIndexes.Add (unmodified.ToArray ());
			}
		}

		class RecordingCopyRequest : CopyRequest
		{
			public readonly List<IList<UniqueId>> StartedUids = new List<IList<UniqueId>> ();
			public readonly List<IList<int>> StartedIndexes = new List<IList<int>> ();
			public readonly List<UniqueIdMap> CompletedMaps = new List<UniqueIdMap> ();
			public readonly List<IList<int>> CompletedIndexes = new List<IList<int>> ();
			public readonly List<IMailFolder> Folders = new List<IMailFolder> ();

			public RecordingCopyRequest (IMailFolder destination) : base (destination)
			{
			}

			public override void OnStarted (IMailFolder folder, IList<UniqueId> uids)
			{
				Folders.Add (folder);
				StartedUids.Add (uids.ToArray ());
			}

			public override void OnStarted (IMailFolder folder, IList<int> indexes)
			{
				Folders.Add (folder);
				StartedIndexes.Add (indexes.ToArray ());
			}

			public override void OnCompleted (IMailFolder folder, UniqueIdMap map)
			{
				CompletedMaps.Add (map);
			}

			public override void OnCompleted (IMailFolder folder, IList<int> indexes)
			{
				CompletedIndexes.Add (indexes.ToArray ());
			}
		}

		class RecordingMoveRequest : MoveRequest
		{
			public readonly List<IList<UniqueId>> StartedUids = new List<IList<UniqueId>> ();
			public readonly List<IList<int>> StartedIndexes = new List<IList<int>> ();
			public readonly List<UniqueIdMap> CompletedMaps = new List<UniqueIdMap> ();
			public readonly List<IList<int>> CompletedIndexes = new List<IList<int>> ();

			public RecordingMoveRequest (IMailFolder destination) : base (destination)
			{
			}

			public override void OnStarted (IMailFolder folder, IList<UniqueId> uids)
			{
				StartedUids.Add (uids.ToArray ());
			}

			public override void OnStarted (IMailFolder folder, IList<int> indexes)
			{
				StartedIndexes.Add (indexes.ToArray ());
			}

			public override void OnCompleted (IMailFolder folder, UniqueIdMap map)
			{
				CompletedMaps.Add (map);
			}

			public override void OnCompleted (IMailFolder folder, IList<int> indexes)
			{
				CompletedIndexes.Add (indexes.ToArray ());
			}
		}

		class RecordingExpungeRequest : ExpungeRequest
		{
			public readonly List<IList<UniqueId>> Started = new List<IList<UniqueId>> ();
			public readonly List<IList<UniqueId>> Completed = new List<IList<UniqueId>> ();

			public override void OnStarted (IMailFolder folder, IList<UniqueId> uids)
			{
				Started.Add (uids.ToArray ());
			}

			public override void OnCompleted (IMailFolder folder, IList<UniqueId> uids)
			{
				Completed.Add (uids.ToArray ());
			}
		}

		class RecordingAppendRequest : AppendRequest
		{
			public readonly List<IMailFolder> Started = new List<IMailFolder> ();
			public readonly List<UniqueId?> Completed = new List<UniqueId?> ();

			public RecordingAppendRequest (MimeMessage message) : base (message)
			{
			}

			public override void OnStarted (IMailFolder folder)
			{
				Started.Add (folder);
			}

			public override void OnCompleted (IMailFolder folder, UniqueId? uid)
			{
				Completed.Add (uid);
			}
		}

		class RecordingReplaceRequest : ReplaceRequest
		{
			public readonly List<IMailFolder> Started = new List<IMailFolder> ();
			public readonly List<UniqueId?> Completed = new List<UniqueId?> ();

			public RecordingReplaceRequest (MimeMessage message) : base (message)
			{
			}

			public override void OnStarted (IMailFolder folder)
			{
				Started.Add (folder);
			}

			public override void OnCompleted (IMailFolder folder, UniqueId? uid)
			{
				Completed.Add (uid);
			}
		}

		static ImapReplayCommand Respond (string command, string response)
		{
			var tag = command.Substring (0, 9);

			return new ImapReplayCommand (command, Latin1.GetBytes (tag + " " + response + "\r\n"));
		}

		static List<ImapReplayCommand> CreateSession ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 LIST \"\" \"Archived Messages\"\r\n", "gmail.list-archived-messages.txt"),
			};
		}

		static async Task<(ImapClient client, IMailFolder inbox, IMailFolder archived)> ConnectAsync (List<ImapReplayCommand> commands, bool async)
		{
			var client = new ImapClient () { TagPrefix = 'A' };

			if (async) {
				await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				await client.AuthenticateAsync ("username", "password");
				await client.Inbox.OpenAsync (FolderAccess.ReadWrite);
			} else {
				client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				client.Authenticate ("username", "password");
				client.Inbox.Open (FolderAccess.ReadWrite);
			}

			var personal = client.GetFolder (client.PersonalNamespaces[0]);
			var archived = async ? await personal.GetSubfolderAsync ("Archived Messages") : personal.GetSubfolder ("Archived Messages");

			return (client, client.Inbox, archived);
		}

		static async Task DisconnectAsync (ImapClient client, bool async)
		{
			if (async)
				await client.DisconnectAsync (true);
			else
				client.Disconnect (true);

			client.Dispose ();
		}

		static string FormatAppendCommand (string tag, MimeMessage message)
		{
			string latin1;

			using (var stream = new MemoryStream ()) {
				var options = FormatOptions.Default.Clone ();
				options.NewLineFormat = NewLineFormat.Dos;
				options.EnsureNewLine = true;

				message.WriteTo (options, stream);

				latin1 = Latin1.GetString (stream.GetBuffer (), 0, (int) stream.Length);
			}

			return string.Format (CultureInfo.InvariantCulture, "{0} APPEND INBOX {{{1}+}}\r\n{2}\r\n", tag, Latin1.GetByteCount (latin1), latin1);
		}

		static MimeMessage CreateMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.To.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.MessageId = "callbacks@mimekit.net";
			message.Date = new DateTimeOffset (2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
			message.Subject = "Request callbacks";
			message.Body = new TextPart ("plain") { Text = "Hello" };

			return message;
		}

		[Test]
		public async Task TestStoreFlagsCallbacks ([Values] bool async)
		{
			var commands = CreateSession ();
			commands.Add (Respond ("A00000007 UID STORE 1:3 (UNCHANGEDSINCE 5) +FLAGS.SILENT (\\Deleted)\r\n", "OK [MODIFIED 2] Store completed"));
			commands.Add (Respond ("A00000008 STORE 1:3 (UNCHANGEDSINCE 5) +FLAGS.SILENT (\\Deleted)\r\n", "OK [MODIFIED 2] Store completed"));
			commands.Add (new ImapReplayCommand ("A00000009 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, _) = await ConnectAsync (commands, async);
			var request = new RecordingStoreFlagsRequest (StoreAction.Add, MessageFlags.Deleted) { Silent = true, UnchangedSince = 5 };

			var unmodifiedUids = async ? await inbox.StoreAsync (Uids, request) : inbox.Store (Uids, request);
			var unmodifiedIndexes = async ? await inbox.StoreAsync (Indexes, request) : inbox.Store (Indexes, request);

			Assert.That (request.Folders, Is.All.SameAs (inbox));
			Assert.That (request.StartedUids, Has.Count.EqualTo (1));
			Assert.That (request.StartedUids[0], Is.EqualTo (Uids));
			Assert.That (request.CompletedUids, Has.Count.EqualTo (1));
			Assert.That (request.CompletedUids[0], Is.EqualTo (Uids));
			Assert.That (request.UnmodifiedUids[0].Select (x => x.Id), Is.EqualTo (new uint[] { 2 }));
			Assert.That (unmodifiedUids.Select (x => x.Id), Is.EqualTo (new uint[] { 2 }));

			Assert.That (request.StartedIndexes, Has.Count.EqualTo (1));
			Assert.That (request.StartedIndexes[0], Is.EqualTo (Indexes));
			Assert.That (request.CompletedIndexes, Has.Count.EqualTo (1));
			Assert.That (request.CompletedIndexes[0], Is.EqualTo (Indexes));
			Assert.That (request.UnmodifiedIndexes[0], Is.EqualTo (new int[] { 1 }));
			Assert.That (unmodifiedIndexes, Is.EqualTo (new int[] { 1 }));

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestStoreRejectedDoesNotComplete ([Values] bool async)
		{
			var commands = CreateSession ();
			commands.Add (Respond ("A00000007 UID STORE 1:3 +FLAGS.SILENT (\\Deleted)\r\n", "NO Store failed"));
			commands.Add (new ImapReplayCommand ("A00000008 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, _) = await ConnectAsync (commands, async);
			var request = new RecordingStoreFlagsRequest (StoreAction.Add, MessageFlags.Deleted) { Silent = true };

			if (async)
				Assert.ThrowsAsync<ImapCommandException> (() => inbox.StoreAsync (Uids, request));
			else
				Assert.Throws<ImapCommandException> (() => inbox.Store (Uids, request));

			Assert.That (request.StartedUids, Has.Count.EqualTo (1));
			Assert.That (request.CompletedUids, Is.Empty);

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestThrowingCallbackDoesNotSendCommand ([Values] bool async)
		{
			var commands = CreateSession ();

			// Note: If the STORE command was left in the queue, it would be sent before the LOGOUT command and the tags would not match.
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, _) = await ConnectAsync (commands, async);
			var request = new RecordingStoreFlagsRequest (StoreAction.Add, MessageFlags.Deleted) { Silent = true, ThrowOnStarted = true };

			if (async)
				Assert.ThrowsAsync<InvalidOperationException> (() => inbox.StoreAsync (Uids, request));
			else
				Assert.Throws<InvalidOperationException> (() => inbox.Store (Uids, request));

			Assert.That (request.CompletedUids, Is.Empty);

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestCopyCallbacks ([Values] bool async)
		{
			var commands = CreateSession ();
			commands.Add (Respond ("A00000007 UID COPY 1:3 \"Archived Messages\"\r\n", "OK [COPYUID 85 1:3 101:103] Copy completed"));
			commands.Add (Respond ("A00000008 COPY 1:3 \"Archived Messages\"\r\n", "OK Copy completed"));
			commands.Add (new ImapReplayCommand ("A00000009 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, archived) = await ConnectAsync (commands, async);
			var request = new RecordingCopyRequest (archived);

			var map = async ? await inbox.CopyToAsync (Uids, request) : inbox.CopyTo (Uids, request);

			if (async)
				await inbox.CopyToAsync (Indexes, request);
			else
				inbox.CopyTo (Indexes, request);

			Assert.That (request.Folders, Is.All.SameAs (inbox));
			Assert.That (request.StartedUids, Has.Count.EqualTo (1));
			Assert.That (request.StartedUids[0], Is.EqualTo (Uids));
			Assert.That (request.CompletedMaps, Has.Count.EqualTo (1));
			Assert.That (request.CompletedMaps[0].Source.Select (x => x.Id), Is.EqualTo (new uint[] { 1, 2, 3 }));
			Assert.That (request.CompletedMaps[0].Destination.Select (x => x.Id), Is.EqualTo (new uint[] { 101, 102, 103 }));
			Assert.That (map.Destination.Select (x => x.Id), Is.EqualTo (new uint[] { 101, 102, 103 }));

			Assert.That (request.StartedIndexes, Has.Count.EqualTo (1));
			Assert.That (request.StartedIndexes[0], Is.EqualTo (Indexes));
			Assert.That (request.CompletedIndexes, Has.Count.EqualTo (1));
			Assert.That (request.CompletedIndexes[0], Is.EqualTo (Indexes));

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestMoveCallbacks ([Values] bool async)
		{
			var commands = CreateSession ();
			commands.Add (Respond ("A00000007 UID MOVE 1:3 \"Archived Messages\"\r\n", "OK [COPYUID 85 1:3 101:103] Move completed"));
			commands.Add (Respond ("A00000008 MOVE 1:3 \"Archived Messages\"\r\n", "OK Move completed"));
			commands.Add (new ImapReplayCommand ("A00000009 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, archived) = await ConnectAsync (commands, async);
			var request = new RecordingMoveRequest (archived);

			var map = async ? await inbox.MoveToAsync (Uids, request) : inbox.MoveTo (Uids, request);

			if (async)
				await inbox.MoveToAsync (Indexes, request);
			else
				inbox.MoveTo (Indexes, request);

			Assert.That (request.StartedUids, Has.Count.EqualTo (1));
			Assert.That (request.StartedUids[0], Is.EqualTo (Uids));
			Assert.That (request.CompletedMaps, Has.Count.EqualTo (1));
			Assert.That (request.CompletedMaps[0].Destination.Select (x => x.Id), Is.EqualTo (new uint[] { 101, 102, 103 }));
			Assert.That (map.Destination.Select (x => x.Id), Is.EqualTo (new uint[] { 101, 102, 103 }));

			Assert.That (request.StartedIndexes, Has.Count.EqualTo (1));
			Assert.That (request.CompletedIndexes, Has.Count.EqualTo (1));

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestMoveFallbackCallbacks ([Values] bool async)
		{
			var commands = CreateSession ();
			commands.Add (Respond ("A00000007 UID COPY 1:3 \"Archived Messages\"\r\n", "OK [COPYUID 85 1:3 101:103] Copy completed"));
			commands.Add (Respond ("A00000008 UID STORE 1:3 +FLAGS.SILENT (\\Deleted)\r\n", "OK Store completed"));
			commands.Add (Respond ("A00000009 UID EXPUNGE 1:3\r\n", "OK Expunge completed"));
			commands.Add (new ImapReplayCommand ("A00000010 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, archived) = await ConnectAsync (commands, async);
			var request = new RecordingMoveRequest (archived);

			client.Capabilities &= ~ImapCapabilities.Move;

			var map = async ? await inbox.MoveToAsync (Uids, request) : inbox.MoveTo (Uids, request);

			Assert.That (request.StartedUids, Has.Count.EqualTo (1));
			Assert.That (request.StartedUids[0], Is.EqualTo (Uids));
			Assert.That (request.CompletedMaps, Has.Count.EqualTo (1));
			Assert.That (request.CompletedMaps[0].Destination.Select (x => x.Id), Is.EqualTo (new uint[] { 101, 102, 103 }));
			Assert.That (map.Destination.Select (x => x.Id), Is.EqualTo (new uint[] { 101, 102, 103 }));

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestExpungeCallbacks ([Values] bool async)
		{
			var commands = CreateSession ();
			commands.Add (Respond ("A00000007 UID EXPUNGE 1:3\r\n", "OK Expunge completed"));
			commands.Add (new ImapReplayCommand ("A00000008 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, _) = await ConnectAsync (commands, async);
			var request = new RecordingExpungeRequest ();

			if (async)
				await inbox.ExpungeAsync (Uids, request);
			else
				inbox.Expunge (Uids, request);

			Assert.That (request.Started, Has.Count.EqualTo (1));
			Assert.That (request.Started[0], Is.EqualTo (Uids));
			Assert.That (request.Completed, Has.Count.EqualTo (1));
			Assert.That (request.Completed[0], Is.EqualTo (Uids));

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestAppendCallbacks ([Values] bool async)
		{
			using var message = CreateMessage ();
			var commands = CreateSession ();
			commands.Add (Respond (FormatAppendCommand ("A00000007", message), "OK [APPENDUID 81 42] Append completed"));
			commands.Add (Respond (FormatAppendCommand ("A00000008", message), "OK [APPENDUID 81 43] Append completed"));
			commands.Add (Respond (FormatAppendCommand ("A00000009", message), "OK [APPENDUID 81 44] Append completed"));
			commands.Add (new ImapReplayCommand ("A00000010 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, _) = await ConnectAsync (commands, async);
			var request = new RecordingAppendRequest (message);

			var uid = async ? await inbox.AppendAsync (request) : inbox.Append (request);

			Assert.That (uid?.Id, Is.EqualTo (42));
			Assert.That (request.Started, Has.Count.EqualTo (1));
			Assert.That (request.Started[0], Is.SameAs (inbox));
			Assert.That (request.Completed, Has.Count.EqualTo (1));
			Assert.That (request.Completed[0]?.Id, Is.EqualTo (42));

			// Note: gmail does not support MULTIAPPEND, so each request is appended with its own command.
			var requests = new RecordingAppendRequest[] { new RecordingAppendRequest (message), new RecordingAppendRequest (message) };
			var uids = async ? await inbox.AppendAsync (requests) : inbox.Append (requests);

			Assert.That (uids.Select (x => x.Id), Is.EqualTo (new uint[] { 43, 44 }));

			for (int i = 0; i < requests.Length; i++) {
				Assert.That (requests[i].Started, Has.Count.EqualTo (1), $"Started[{i}]");
				Assert.That (requests[i].Completed, Has.Count.EqualTo (1), $"Completed[{i}]");
				Assert.That (requests[i].Completed[0]?.Id, Is.EqualTo (43 + i), $"Completed[{i}] UID");
			}

			await DisconnectAsync (client, async);
		}

		[Test]
		public async Task TestReplaceFallbackCallbacks ([Values] bool async)
		{
			using var message = CreateMessage ();
			var commands = CreateSession ();
			commands.Add (Respond (FormatAppendCommand ("A00000007", message), "OK [APPENDUID 81 42] Append completed"));
			commands.Add (Respond ("A00000008 UID STORE 5 +FLAGS.SILENT (\\Deleted)\r\n", "OK Store completed"));
			commands.Add (Respond ("A00000009 UID EXPUNGE 5\r\n", "OK Expunge completed"));
			commands.Add (new ImapReplayCommand ("A00000010 LOGOUT\r\n", "gmail.logout.txt"));

			var (client, inbox, _) = await ConnectAsync (commands, async);
			var request = new RecordingReplaceRequest (message);

			var uid = async ? await inbox.ReplaceAsync (new UniqueId (5), request) : inbox.Replace (new UniqueId (5), request);

			Assert.That (uid?.Id, Is.EqualTo (42));
			Assert.That (request.Started, Has.Count.EqualTo (1));
			Assert.That (request.Started[0], Is.SameAs (inbox));
			Assert.That (request.Completed, Has.Count.EqualTo (1));
			Assert.That (request.Completed[0]?.Id, Is.EqualTo (42));

			await DisconnectAsync (client, async);
		}
	}
}
