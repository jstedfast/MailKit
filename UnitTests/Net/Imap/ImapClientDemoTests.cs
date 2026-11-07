//
// ImapClientDemoTests.cs
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

using ImapClientDemo;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace UnitTests.Net.Imap
{
	[TestFixture]
	public class ImapClientDemoTests
	{
		const string Capabilities = "IMAP4rev1 IDLE";

		static ImapIdleSessionTests.ScriptedImapStream CreateStream (int count, params (string, string)[] commands)
		{
			var script = new List<(string, string)> {
				("A00000000 LOGIN username password\r\n", $"A00000000 OK [CAPABILITY {Capabilities}] Logged in\r\n"),
				("A00000001 LIST \"\" \"\"\r\n", "* LIST (\\Noselect) \"/\" \"\"\r\nA00000001 OK List completed\r\n"),
				("A00000002 LIST \"\" \"INBOX\"\r\n", "* LIST (\\HasNoChildren) \"/\" INBOX\r\nA00000002 OK List completed\r\n"),
				("A00000003 EXAMINE INBOX\r\n", $"* {count} EXISTS\r\n* 0 RECENT\r\n* OK [UIDVALIDITY 7] Valid\r\nA00000003 OK [READ-ONLY] Examined\r\n")
			};
			script.AddRange (commands);
			return new ImapIdleSessionTests.ScriptedImapStream (Capabilities, script.ToArray ());
		}

		static async Task<ImapSession> ConnectAsync (ImapIdleSessionTests.ScriptedImapStream stream)
		{
			var client = new ImapClient { TagPrefix = 'A' };
			try {
				await client.ConnectAsync (stream, "localhost", 143, SecureSocketOptions.None);
				await client.AuthenticateAsync ("username", "password");
				return new ImapSession (client);
			} catch {
				client.Dispose ();
				throw;
			}
		}

		static async Task WaitForCommandsAsync (ImapIdleSessionTests.ScriptedImapStream stream, int count)
		{
			using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (5));
			while (stream.CommandCount < count)
				await Task.Delay (10, timeout.Token);
		}

		static string Summary (int sequence, uint uid)
		{
			return $"* {sequence} FETCH (UID {uid} FLAGS () ENVELOPE (NIL \"Message {uid}\" ((\"Sender\" NIL \"sender\" \"example.com\")) NIL NIL NIL NIL NIL NIL \"<{uid}@example.com>\"))\r\n";
		}

		[TestCase (0, 0, 0, -1)]
		[TestCase (1, 0, 0, 0)]
		[TestCase (100, 0, 0, 99)]
		[TestCase (101, 0, 1, 100)]
		[TestCase (101, 1, 0, 0)]
		[TestCase (201, 1, 1, 100)]
		[TestCase (201, 2, 0, 0)]
		[TestCase (100, 2, 0, 99)]
		[TestCase (int.MaxValue, int.MaxValue, 0, 46)]
		public void TestPageBounds (int count, int pageIndex, int min, int max)
		{
			Assert.That (MailboxPage.GetRange (count, pageIndex), Is.EqualTo ((min, max)));
		}

		[Test]
		public async Task TestPagingInterruptsIdleAndSkipsCancelledRequestsAsync ()
		{
			using var stream = CreateStream (201,
				("A00000004 FETCH 102:201 (UID FLAGS ENVELOPE)\r\n", Summary (102, 102) + Summary (201, 201) + Summary (1, 1) + "A00000004 OK Fetched\r\n"),
				("A00000005 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", string.Empty),
				("A00000006 FETCH 2:101 (UID FLAGS ENVELOPE)\r\n", Summary (2, 2) + Summary (101, 101) + "A00000006 OK Fetched\r\n"),
				("A00000007 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000007 OK Idle finished\r\n"),
				("A00000008 LOGOUT\r\n", "* BYE Bye\r\nA00000008 OK Logged out\r\n"));
			await using var session = await ConnectAsync (stream);
			var newest = await session.GetPageAsync ("INBOX", 0, CancellationToken.None);
			Assert.That (newest.Messages.Select (message => message.UniqueId.Id), Is.EqualTo (new uint[] { 201, 102 }));
			Assert.That (newest.UidValidity, Is.EqualTo (7));
			Assert.That (newest.HasOlder, Is.True);
			Assert.That (newest.HasNewer, Is.False);
			await WaitForCommandsAsync (stream, 6);

			using var cancelled = new CancellationTokenSource ();
			cancelled.Cancel ();
			var skipped = session.GetPageAsync ("INBOX", 2, cancelled.Token);
			var older = session.GetPageAsync ("INBOX", 1, CancellationToken.None);
			await WaitForCommandsAsync (stream, 7);
			stream.Push ("A00000005 OK Idle finished\r\n");
			Assert.ThrowsAsync<TaskCanceledException> (async () => await skipped);
			var page = await older;
			Assert.That (page.Messages.Select (message => message.UniqueId.Id), Is.EqualTo (new uint[] { 101, 2 }));
			Assert.That (page.HasNewer, Is.True);
			await WaitForCommandsAsync (stream, 9);
			await session.DisposeAsync ();
			Assert.That (stream.Completed, Is.True);
		}

		[Test]
		public async Task TestEmptyFolderAndLiveArrivalAsync ()
		{
			using var stream = CreateStream (0,
				("A00000004 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000004 OK Idle finished\r\n"),
				("A00000005 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000005 OK Idle finished\r\n"),
				("A00000006 FETCH 1 (UID FLAGS ENVELOPE)\r\n", Summary (1, 42) + "A00000006 OK Fetched\r\n"),
				("A00000007 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000007 OK Idle finished\r\n"),
				("A00000008 LOGOUT\r\n", "* BYE Bye\r\nA00000008 OK Logged out\r\n"));
			await using var session = await ConnectAsync (stream);
			var empty = await session.GetPageAsync ("INBOX", 0, CancellationToken.None);
			Assert.That (empty.Messages, Is.Empty);
			Assert.That (empty.HasOlder, Is.False);
			await WaitForCommandsAsync (stream, 5);
			stream.Push ("* 1 EXISTS\r\n");
			await WaitForCommandsAsync (stream, 7);
			Assert.That (session.ChangeVersion, Is.GreaterThan (empty.ChangeVersion));

			var updated = await session.GetPageAsync ("INBOX", 0, CancellationToken.None);
			Assert.That (updated.Messages.Single ().UniqueId.Id, Is.EqualTo (42));
			await WaitForCommandsAsync (stream, 10);
			await session.DisposeAsync ();
			Assert.That (stream.Completed, Is.True);
		}

		[Test]
		public async Task TestStaleUidValidityDoesNotFetchPreviewAsync ()
		{
			using var stream = CreateStream (0,
				("A00000004 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000004 OK Idle finished\r\n"),
				("A00000005 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000005 OK Idle finished\r\n"),
				("A00000006 LOGOUT\r\n", "* BYE Bye\r\nA00000006 OK Logged out\r\n"));
			await using var session = await ConnectAsync (stream);
			await session.GetPageAsync ("INBOX", 0, CancellationToken.None);
			await WaitForCommandsAsync (stream, 5);
			var stale = new MailboxPage ("INBOX", 6, 0, 0, 0, Array.Empty<IMessageSummary> ());
			Assert.ThrowsAsync<InvalidOperationException> (async () => await session.GetPreviewAsync (stale, new UniqueId (1), CancellationToken.None));
			await WaitForCommandsAsync (stream, 7);
			await session.DisposeAsync ();
			Assert.That (stream.Completed, Is.True);
		}

		[TestCase (false)]
		[TestCase (true)]
		public async Task TestTextPreviewAndCancellationAsync (bool cancelPreview)
		{
			const string html = "<html><body><p>Hello from HTML</p></body></html>";
			const string mime = "Content-Type: text/html; charset=utf-8\r\n\r\n";
			string message = mime + html;
			string bodyResponse = $"* 1 FETCH (UID 42 BODY[] {{{message.Length}}}\r\n{message})\r\nA00000007 OK Fetched\r\n";
			using var stream = CreateStream (1,
				("A00000004 FETCH 1 (UID FLAGS ENVELOPE)\r\n", Summary (1, 42) + "A00000004 OK Fetched\r\n"),
				("A00000005 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000005 OK Idle finished\r\n"),
				("A00000006 UID FETCH 42 (UID BODYSTRUCTURE)\r\n",
					$"* 1 FETCH (UID 42 FLAGS (\\Seen) BODYSTRUCTURE (\"TEXT\" \"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"7BIT\" {html.Length} 1))\r\nA00000006 OK Fetched\r\n"),
				("A00000007 UID FETCH 42 (BODY.PEEK[])\r\n", string.Empty),
				("A00000008 FETCH 1 (UID FLAGS ENVELOPE)\r\n", Summary (1, 42) + "A00000008 OK Fetched\r\n"),
				("A00000009 IDLE\r\n", "+ Idling\r\n"),
				("DONE\r\n", "A00000009 OK Idle finished\r\n"),
				("A00000010 LOGOUT\r\n", "* BYE Bye\r\nA00000010 OK Logged out\r\n"));
			await using var session = await ConnectAsync (stream);
			var page = await session.GetPageAsync ("INBOX", 0, CancellationToken.None);
			await WaitForCommandsAsync (stream, 6);
			using var selection = new CancellationTokenSource ();
			var preview = session.GetPreviewAsync (page, new UniqueId (42), selection.Token);
			await WaitForCommandsAsync (stream, 9);
			Assert.That (session.ChangeVersion, Is.GreaterThan (page.ChangeVersion), "Unsolicited flags during preview should invalidate the page.");
			if (cancelPreview)
				selection.Cancel ();

			// A new command must wait for the in-flight response, even after selection changes.
			var refresh = session.GetPageAsync ("INBOX", 0, CancellationToken.None);
			Assert.That (refresh.IsCompleted, Is.False);
			stream.Push (bodyResponse);
			if (cancelPreview)
				Assert.ThrowsAsync<TaskCanceledException> (async () => await preview);
			else
				Assert.That ((await preview).Trim (), Is.EqualTo ("Hello from HTML"));

			Assert.That ((await refresh).Messages.Single ().UniqueId.Id, Is.EqualTo (42));
			await WaitForCommandsAsync (stream, 11);
			await session.DisposeAsync ();
			Assert.That (stream.Completed, Is.True);
		}

		[TestCase ((int) AuthenticationKind.Google, "imap.gmail.com")]
		[TestCase ((int) AuthenticationKind.Microsoft365, "outlook.office365.com")]
		public void TestProviderTokensAreRestrictedToTheirServer (int provider, string host)
		{
			var kind = (AuthenticationKind) provider;
			Assert.DoesNotThrow (() => OAuthSignIn.ValidateServer (kind, host, 993, SecureSocketOptions.SslOnConnect));
			Assert.Throws<ArgumentException> (() => OAuthSignIn.ValidateServer (kind, "example.com", 993, SecureSocketOptions.SslOnConnect));
			Assert.Throws<ArgumentException> (() => OAuthSignIn.ValidateServer (kind, host, 143, SecureSocketOptions.StartTls));
		}
	}
}
