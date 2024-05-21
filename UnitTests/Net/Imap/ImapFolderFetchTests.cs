//
// ImapFolderFetchTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using System.Net;
using System.Text;
using System.Security.Cryptography;

using MimeKit;

using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderFetchTests
	{
		static FolderAttributes GetSpecialFolderAttribute (SpecialFolder special)
		{
			switch (special) {
			case SpecialFolder.All:       return FolderAttributes.All;
			case SpecialFolder.Archive:   return FolderAttributes.Archive;
			case SpecialFolder.Drafts:    return FolderAttributes.Drafts;
			case SpecialFolder.Flagged:   return FolderAttributes.Flagged;
			case SpecialFolder.Important: return FolderAttributes.Important;
			case SpecialFolder.Junk:      return FolderAttributes.Junk;
			case SpecialFolder.Sent:      return FolderAttributes.Sent;
			case SpecialFolder.Trash:     return FolderAttributes.Trash;
			default: throw new ArgumentOutOfRangeException (nameof (special));
			}
		}

		static string HexEncode (byte [] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		static void GetStreamsCallback (ImapFolder folder, int index, UniqueId uid, Stream stream)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = reader.ReadToEnd ();

				Assert.That (text, Is.EqualTo (expected));
			}
		}

		static async Task GetStreamsAsyncCallback (ImapFolder folder, int index, UniqueId uid, Stream stream, CancellationToken cancellationToken)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = await reader.ReadToEndAsync ();

				Assert.That (text, Is.EqualTo (expected));
			}
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				// Fetch
				var invalidHeaderFields = new string[] { "Invalid Header Name" };
				var headerIds = new HeaderId [] { HeaderId.Subject };
				var headerFields = new string [] { "SUBJECT" };
				var uids = new UniqueId [] { UniqueId.MinValue };
				var indexes = new int [] { 0 };

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, -1, null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.FetchAsync (0, -1, null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.FetchAsync (uids, null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.FetchAsync (indexes, null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All, headerIds));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headerIds));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None, headers));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All, headerIds));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None, headers));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All, headerIds));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None, headers));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All, headerFields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headerFields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None, fields));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, invalidHeaderFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All, headerFields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None, fields));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, invalidHeaderFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All, headerFields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None, fields));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, invalidHeaderFields));

				// Fetch + modseq
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, headerIds));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headerIds));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerIds));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All, headerIds));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, headerFields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All, headerFields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, invalidHeaderFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerFields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, invalidHeaderFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All, headerFields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, invalidHeaderFields));

				// GetHeaders
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetHeaders (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetHeadersAsync (-1));
				Assert.Throws<ArgumentException> (() => inbox.GetHeaders (UniqueId.Invalid));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetHeadersAsync (UniqueId.Invalid));

				var bodyPart = new BodyPartText ();

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetHeaders (-1, bodyPart));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetHeadersAsync (-1, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (0, (BodyPart) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetHeadersAsync (0, (BodyPart) null));

				Assert.Throws<ArgumentException> (() => inbox.GetHeaders (UniqueId.Invalid, bodyPart));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetHeadersAsync (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (UniqueId.MinValue, (BodyPart) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetHeadersAsync (UniqueId.MinValue, (BodyPart) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetHeaders (-1, "1.2"));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetHeadersAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (0, (string) null));
				//Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetHeadersAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetHeaders (UniqueId.Invalid, "1.2"));
				//Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetHeadersAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (UniqueId.MinValue, (string) null));
				//Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetHeadersAsync (UniqueId.MinValue, (string) null));

				// GetMessage
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetMessage (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetMessageAsync (-1));
				Assert.Throws<ArgumentException> (() => inbox.GetMessage (UniqueId.Invalid));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetMessageAsync (UniqueId.Invalid));

				// GetBodyPart
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, bodyPart));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (BodyPart) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (BodyPart) null));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, bodyPart));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (BodyPart) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (BodyPart) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, "1.2"));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (string) null));
				//Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, "1.2"));
				//Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (string) null));
				//Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (string) null));

				// GetStream
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2"));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2"));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart) null));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, 0, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, -1, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, 0, -1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, 0, 1024));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, -1, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, 0, -1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, 0, -1));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2", 0, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2", 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string) null, 0, 1024));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string) null, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, "1.2", -1, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, "1.2", 0, -1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, "1.2", 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2", 0, 1024));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2", 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string) null, 0, 1024));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string) null, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, "1.2", -1, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, "1.2", 0, -1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, "1.2", 0, -1));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart, 0, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart, 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart) null, -1, 1024));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart) null, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, bodyPart, -1, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, bodyPart, 0, -1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, bodyPart, 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart, 0, 1024));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart, 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart) null, -1, 1024));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart) null, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, bodyPart, -1, 1024));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, bodyPart, 0, -1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, bodyPart, 0, -1));

				// GetStreams
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStreams (-1, 0, GetStreamsCallback));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamsAsync (-1, 0, GetStreamsAsyncCallback));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStreams (1, 0, GetStreamsCallback));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamsAsync (1, 0, GetStreamsAsyncCallback));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams (0, -1, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamsAsync (0, -1, null));

				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams ((IList<int>) null, GetStreamsCallback));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamsAsync ((IList<int>) null, GetStreamsAsyncCallback));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams (new int [] { 0 }, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamsAsync (new int [] { 0 }, null));

				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams ((IList<UniqueId>) null, GetStreamsCallback));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamsAsync ((IList<UniqueId>) null, GetStreamsAsyncCallback));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams (UniqueIdRange.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamsAsync (UniqueIdRange.All, null));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestNotSupportedExceptions ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX\r\n", "common.select-inbox-no-modseq.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				// disable all features
				client.Capabilities = ImapCapabilities.None;

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				// Fetch
				var headers = new HashSet<HeaderId> (new HeaderId[] { HeaderId.Subject });
				var fields = new HashSet<string> (new string[] { "SUBJECT" });
				var uids = new UniqueId[] { UniqueId.MinValue };
				var indexes = new int[] { 0 };
				ulong modseq = 409601020304;

				Assert.Throws<NotSupportedException> (() => inbox.Fetch (0, -1, modseq, MessageSummaryItems.All));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (0, -1, modseq, MessageSummaryItems.All));
				Assert.Throws<NotSupportedException> (() => inbox.Fetch (0, -1, modseq, MessageSummaryItems.All, headers));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (0, -1, modseq, MessageSummaryItems.All, headers));
				Assert.Throws<NotSupportedException> (() => inbox.Fetch (0, -1, modseq, MessageSummaryItems.All, fields));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (0, -1, modseq, MessageSummaryItems.All, fields));

				Assert.Throws<NotSupportedException> (() => inbox.Fetch (indexes, modseq, MessageSummaryItems.All));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (indexes, modseq, MessageSummaryItems.All));
				Assert.Throws<NotSupportedException> (() => inbox.Fetch (indexes, modseq, MessageSummaryItems.All, headers));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (indexes, modseq, MessageSummaryItems.All, headers));
				Assert.Throws<NotSupportedException> (() => inbox.Fetch (indexes, modseq, MessageSummaryItems.All, fields));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (indexes, modseq, MessageSummaryItems.All, fields));

				Assert.Throws<NotSupportedException> (() => inbox.Fetch (uids, modseq, MessageSummaryItems.All));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (uids, modseq, MessageSummaryItems.All));
				Assert.Throws<NotSupportedException> (() => inbox.Fetch (uids, modseq, MessageSummaryItems.All, headers));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (uids, modseq, MessageSummaryItems.All, headers));
				Assert.Throws<NotSupportedException> (() => inbox.Fetch (uids, modseq, MessageSummaryItems.All, fields));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.FetchAsync (uids, modseq, MessageSummaryItems.All, fields));

				client.Disconnect (false);
			}
		}

		static List<ImapReplayCommand> CreateEmptyFetchRequestCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
			};
		}

		[Test]
		public void TestEmptyFetchRequest ()
		{
			var commands = CreateEmptyFetchRequestCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				// First, test a non-empty requests with empty message sets
				var request = new FetchRequest (MessageSummaryItems.Flags);

				var messages = inbox.Fetch (Array.Empty<UniqueId> (), request);
				Assert.That (messages, Is.Empty, "UID FETCH (0 uids)");

				messages = inbox.Fetch (Array.Empty<int> (), request);
				Assert.That (messages, Is.Empty, "FETCH (0 indexes)");

				// Now make the FetchRequest empty
				request = new FetchRequest (MessageSummaryItems.None);

				messages = inbox.Fetch (UniqueIdRange.All, request);
				Assert.That (messages, Is.Empty, "UID FETCH (None)");

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				Assert.That (messages, Is.Empty, "FETCH (None)");

				messages = inbox.Fetch (0, -1, request);
				Assert.That (messages, Is.Empty, "FETCH min:max (None)");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestEmptyFetchRequestAsync ()
		{
			var commands = CreateEmptyFetchRequestCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				// First, test a non-empty requests with empty message sets
				var request = new FetchRequest (MessageSummaryItems.Flags);

				var messages = await inbox.FetchAsync (Array.Empty<UniqueId> (), request);
				Assert.That (messages, Is.Empty, "UID FETCH (0 uids)");

				messages = await inbox.FetchAsync (Array.Empty<int> (), request);
				Assert.That (messages, Is.Empty, "FETCH (0 indexes)");

				// Now make the FetchRequest empty
				request = new FetchRequest (MessageSummaryItems.None);

				messages = await inbox.FetchAsync (UniqueIdRange.All, request);
				Assert.That (messages, Is.Empty, "UID FETCH (None)");

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				Assert.That (messages, Is.Empty, "FETCH (None)");

				messages = await inbox.FetchAsync (0, -1, request);
				Assert.That (messages, Is.Empty, "FETCH min:max (None)");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateFetchAllHeadersCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1:* (UID FLAGS BODY.PEEK[HEADER])\r\n", "gmail.fetch-all-headers.txt"),
				new ImapReplayCommand ("A00000008 FETCH 1:6 (UID FLAGS BODY.PEEK[HEADER])\r\n", "gmail.fetch-all-headers.txt"),
				new ImapReplayCommand ("A00000009 FETCH 1:* (UID FLAGS BODY.PEEK[HEADER])\r\n", "gmail.fetch-all-headers.txt")
			};
		}

		[Test]
		public void TestFetchAllHeaders ()
		{
			var commands = CreateFetchAllHeadersCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var request = new FetchRequest (MessageSummaryItems.Flags | MessageSummaryItems.UniqueId) {
					Headers = HeaderSet.All
				};

				var messages = inbox.Fetch (UniqueIdRange.All, request);
				Assert.That (messages, Has.Count.EqualTo (6), "UID FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "UID FETCH fields");

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH fields");

				messages = inbox.Fetch (0, -1, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH min:max");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH min:max fields");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchAllHeadersAsync ()
		{
			var commands = CreateFetchAllHeadersCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var request = new FetchRequest (MessageSummaryItems.Flags | MessageSummaryItems.UniqueId) {
					Headers = HeaderSet.All
				};

				var messages = await inbox.FetchAsync (UniqueIdRange.All, request);
				Assert.That (messages, Has.Count.EqualTo (6), "UID FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "UID FETCH fields");

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH fields");

				messages = await inbox.FetchAsync (0, -1, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH min:max");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH min:max fields");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateFetchInvalidHeadersCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1:* (UID FLAGS BODY.PEEK[HEADER])\r\n", "gmail.fetch-invalid-headers.txt"),
				new ImapReplayCommand ("A00000008 FETCH 1:6 (UID FLAGS BODY.PEEK[HEADER])\r\n", "gmail.fetch-invalid-headers.txt"),
				new ImapReplayCommand ("A00000009 FETCH 1:* (UID FLAGS BODY.PEEK[HEADER])\r\n", "gmail.fetch-invalid-headers.txt")
			};
		}

		[Test]
		public void TestFetchInvalidHeaders ()
		{
			var commands = CreateFetchInvalidHeadersCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var request = new FetchRequest (MessageSummaryItems.Flags | MessageSummaryItems.UniqueId) {
					Headers = HeaderSet.All
				};

				var messages = inbox.Fetch (UniqueIdRange.All, request);
				Assert.That (messages, Has.Count.EqualTo (6), "UID FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "UID FETCH fields");

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH fields");

				messages = inbox.Fetch (0, -1, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH min:max");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH min:max fields");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchInvalidHeadersAsync ()
		{
			var commands = CreateFetchInvalidHeadersCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var request = new FetchRequest (MessageSummaryItems.Flags | MessageSummaryItems.UniqueId) {
					Headers = HeaderSet.All
				};

				var messages = await inbox.FetchAsync (UniqueIdRange.All, request);
				Assert.That (messages, Has.Count.EqualTo (6), "UID FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "UID FETCH fields");

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH fields");

				messages = await inbox.FetchAsync (0, -1, request);
				Assert.That (messages, Has.Count.EqualTo (6), "FETCH min:max");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Fields, Is.EqualTo (request.Items | MessageSummaryItems.Headers | MessageSummaryItems.References), "FETCH min:max fields");

				await client.DisconnectAsync (false);
			}
		}

		static readonly string[] PreviewTextValues = {
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver",
			"Don't miss our celebrity guest Monday evening",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver",
			"Don't miss our celebrity guest Monday evening",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver"
		};

		static List<ImapReplayCommand> CreateFetchPreviewTextCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+preview.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1:* (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE PREVIEW)\r\n", "gmail.fetch-preview.txt"),
				new ImapReplayCommand ("A00000008 FETCH 1:6 (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE PREVIEW)\r\n", "gmail.fetch-preview.txt"),
				new ImapReplayCommand ("A00000009 FETCH 1:* (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE PREVIEW)\r\n", "gmail.fetch-preview.txt")
			};
		}

		[Test]
		public void TestFetchPreviewText ()
		{
			var commands = CreateFetchPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (UniqueIdRange.All, MessageSummaryItems.All | MessageSummaryItems.PreviewText);
				Assert.That (messages, Has.Count.EqualTo (PreviewTextValues.Length), "UID FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5 }, MessageSummaryItems.All | MessageSummaryItems.PreviewText);
				Assert.That (messages, Has.Count.EqualTo (PreviewTextValues.Length), "FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = inbox.Fetch (0, -1, MessageSummaryItems.All | MessageSummaryItems.PreviewText);
				Assert.That (messages, Has.Count.EqualTo (PreviewTextValues.Length), "FETCH min:max");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchPreviewTextAsync ()
		{
			var commands = CreateFetchPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (UniqueIdRange.All, MessageSummaryItems.All | MessageSummaryItems.PreviewText);
				Assert.That (messages, Has.Count.EqualTo (PreviewTextValues.Length), "UID FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5 }, MessageSummaryItems.All | MessageSummaryItems.PreviewText);
				Assert.That (messages, Has.Count.EqualTo (PreviewTextValues.Length), "FETCH");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.All | MessageSummaryItems.PreviewText);
				Assert.That (messages, Has.Count.EqualTo (PreviewTextValues.Length), "FETCH min:max");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				await client.DisconnectAsync (false);
			}
		}

#if ENABLE_LAZY_PREVIEW_API
		static List<ImapReplayCommand> CreateFetchLazyPreviewTextCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+preview.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1:* (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE PREVIEW (LAZY))\r\n", "gmail.fetch-preview.txt"),
				new ImapReplayCommand ("A00000008 FETCH 1:6 (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE PREVIEW (LAZY))\r\n", "gmail.fetch-preview.txt"),
				new ImapReplayCommand ("A00000009 FETCH 1:* (FLAGS INTERNALDATE RFC822.SIZE ENVELOPE PREVIEW (LAZY))\r\n", "gmail.fetch-preview.txt")
			};
		}

		[Test]
		public void TestFetchLazyPreviewText ()
		{
			var commands = CreateFetchLazyPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var request = new FetchRequest (MessageSummaryItems.All | MessageSummaryItems.PreviewText) {
					PreviewOptions = PreviewOptions.Lazy
				};

				var messages = inbox.Fetch (UniqueIdRange.All, request);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = inbox.Fetch (0, -1, request);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchLazyPreviewTextAsync ()
		{
			var commands = CreateFetchLazyPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var request = new FetchRequest (MessageSummaryItems.All | MessageSummaryItems.PreviewText) {
					PreviewOptions = PreviewOptions.Lazy
				};

				var messages = await inbox.FetchAsync (UniqueIdRange.All, request);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5 }, request);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				messages = await inbox.FetchAsync (0, -1, request);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (PreviewTextValues[i]));

				await client.DisconnectAsync (false);
			}
		}
#endif

		static readonly string[] SimulatedPreviewTextValues = {
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			"Don’t miss our celebrity guest Monday evening",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			"Don’t miss our celebrity guest Monday evening",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			string.Empty
		};

		static List<ImapReplayCommand> CreateFetchSimulatedPreviewTextCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"),
				new ImapReplayCommand ("A00000008 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"),
				new ImapReplayCommand ("A00000009 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"),
				new ImapReplayCommand ("A00000010 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"),
				new ImapReplayCommand ("A00000011 FETCH 1:7 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"),
				new ImapReplayCommand ("A00000012 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"),
				new ImapReplayCommand ("A00000013 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"),
				new ImapReplayCommand ("A00000014 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"),
				new ImapReplayCommand ("A00000015 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"),
				new ImapReplayCommand ("A00000016 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"),
				new ImapReplayCommand ("A00000017 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"),
				new ImapReplayCommand ("A00000018 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt")
			};
		}

		[Test]
		public void TestFetchSimulatedPreviewText ()
		{
			var commands = CreateFetchSimulatedPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (SimulatedPreviewTextValues[i]));

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5, 6 }, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (SimulatedPreviewTextValues[i]));

				messages = inbox.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (SimulatedPreviewTextValues[i]));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchSimulatedPreviewTextAsync ()
		{
			var commands = CreateFetchSimulatedPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (SimulatedPreviewTextValues[i]));

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5, 6 }, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (SimulatedPreviewTextValues[i]));

				messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].PreviewText, Is.EqualTo (SimulatedPreviewTextValues[i]));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateFetchSimulatedKoreanPreviewTextCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1 (UID BODYSTRUCTURE)\r\n", "gmail.fetch-korean-previewtext-bodystructure.txt"),
				new ImapReplayCommand ("A00000008 UID FETCH 1 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-korean-previewtext-peek-text-only.txt"),
			};
		}

		[Test]
		public void TestFetchSimulatedKoreanPreviewText ()
		{
			const string koreanPreviewText = "내 주제는 여기 안녕하세요 여러분, 모두 괜찮기를 바랍니다. 이번 주말에 어떤 계획을 갖고 있나요? 다들 빨리 보시길 바랄게요! 여행을 계획하는 아이디어가 있습니다. 네 생각을 말해봐. 또 봐요!";
			var commands = CreateFetchSimulatedKoreanPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (new[] { new UniqueId (1) }, MessageSummaryItems.PreviewText);
				Assert.That (messages.Count, Is.EqualTo (1), "Expected 1 message to be fetched.");
				Assert.That (messages[0].PreviewText, Is.EqualTo (koreanPreviewText));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchSimulatedKoreanPreviewTextAsync ()
		{
			const string koreanPreviewText = "내 주제는 여기 안녕하세요 여러분, 모두 괜찮기를 바랍니다. 이번 주말에 어떤 계획을 갖고 있나요? 다들 빨리 보시길 바랄게요! 여행을 계획하는 아이디어가 있습니다. 네 생각을 말해봐. 또 봐요!";
			var commands = CreateFetchSimulatedKoreanPreviewTextCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (new[] { new UniqueId (1) }, MessageSummaryItems.PreviewText);
				Assert.That (messages.Count, Is.EqualTo (1), "Expected 1 message to be fetched.");
				Assert.That (messages[0].PreviewText, Is.EqualTo (koreanPreviewText));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateFetchQuotedStringCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 FETCH 1:* (UID BODYSTRUCTURE)\r\n", "gmail.fetch-quoted-string-bodystructure.txt"),
				new ImapReplayCommand ("A00000008 UID FETCH 1 (BODY.PEEK[1.TEXT]<0.512>)\r\n", "gmail.fetch-quoted-string.txt"),
			};
		}

		[Test]
		public void TestFetchQuotedString ()
		{
			var commands = CreateFetchQuotedStringCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);
				using (var stream = inbox.GetStream (messages[0].UniqueId, messages[0].TextBody.PartSpecifier + ".TEXT", 0, 512)) {
					var text = Encoding.UTF8.GetString (((MemoryStream) stream).ToArray ());

					Assert.That (text, Is.EqualTo ("This is the message body as a quoted-string."), "The message body does not match.");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchQuotedStringAsync ()
		{
			var commands = CreateFetchQuotedStringCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);
				using (var stream = await inbox.GetStreamAsync (messages[0].UniqueId, messages[0].TextBody.PartSpecifier + ".TEXT", 0, 512)) {
					var text = Encoding.UTF8.GetString (((MemoryStream) stream).ToArray ());

					Assert.That (text, Is.EqualTo ("This is the message body as a quoted-string."), "The message body does not match.");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateFetchNilCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 FETCH 1:* (UID BODYSTRUCTURE)\r\n", "gmail.fetch-nil-bodystructure.txt"),
				new ImapReplayCommand ("A00000008 UID FETCH 1 (BODY.PEEK[1.TEXT]<0.512>)\r\n", "gmail.fetch-nil.txt"),
			};
		}

		[Test]
		public void TestFetchNil ()
		{
			var commands = CreateFetchNilCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);
				using (var stream = inbox.GetStream (messages[0].UniqueId, messages[0].TextBody.PartSpecifier + ".TEXT", 0, 512)) {
					var text = Encoding.UTF8.GetString (((MemoryStream) stream).ToArray ());

					Assert.That (text, Is.EqualTo (string.Empty), "The message body does not match.");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchNilAsync ()
		{
			var commands = CreateFetchNilCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure);
				using (var stream = await inbox.GetStreamAsync (messages[0].UniqueId, messages[0].TextBody.PartSpecifier + ".TEXT", 0, 512)) {
					var text = Encoding.UTF8.GetString (((MemoryStream) stream).ToArray ());

					Assert.That (text, Is.EqualTo (string.Empty), "The message body does not match.");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateExpungeDuringFetchCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1:6 (UID INTERNALDATE ENVELOPE)\r\n", "gmail.expunge-during-fetch.txt")
			};
		}

		[Test]
		public void TestExpungeDuringFetch ()
		{
			var commands = CreateExpungeDuringFetchCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var range = new UniqueIdRange (0, 1, 6);
				var messages = inbox.Fetch (range, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate | MessageSummaryItems.Envelope);

				Assert.That (messages, Has.Count.EqualTo (4), "Count");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Index, Is.EqualTo (i), $"Index #{i}");
				Assert.That (messages[0].UniqueId.Id, Is.EqualTo ((uint) 1), "UniqueId #0");
				Assert.That (messages[1].UniqueId.Id, Is.EqualTo ((uint) 3), "UniqueId #1");
				Assert.That (messages[2].UniqueId.Id, Is.EqualTo ((uint) 4), "UniqueId #2");
				Assert.That (messages[3].UniqueId.Id, Is.EqualTo ((uint) 5), "UniqueId #3");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestExpungeDuringFetchAsync ()
		{
			var commands = CreateExpungeDuringFetchCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var range = new UniqueIdRange (0, 1, 6);
				var messages = await inbox.FetchAsync (range, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate | MessageSummaryItems.Envelope);

				Assert.That (messages, Has.Count.EqualTo (4), "Count");
				for (int i = 0; i < messages.Count; i++)
					Assert.That (messages[i].Index, Is.EqualTo (i), $"Index #{i}");
				Assert.That (messages[0].UniqueId.Id, Is.EqualTo ((uint) 1), "UniqueId #0");
				Assert.That (messages[1].UniqueId.Id, Is.EqualTo ((uint) 3), "UniqueId #1");
				Assert.That (messages[2].UniqueId.Id, Is.EqualTo ((uint) 4), "UniqueId #2");
				Assert.That (messages[3].UniqueId.Id, Is.EqualTo ((uint) 5), "UniqueId #3");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateExtractingPrecisePangolinAttachmentCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000007 FETCH 270 (BODY.PEEK[])\r\n", "gmail.precise-pangolin-message.txt")
			};
		}

		[Test]
		public void TestExtractingPrecisePangolinAttachment ()
		{
			var commands = CreateExtractingPrecisePangolinAttachmentCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				client.Inbox.Open (FolderAccess.ReadOnly);

				using (var message = client.Inbox.GetMessage (269)) {
					using (var jpeg = new MemoryStream ()) {
						var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

						attachment.Content.DecodeTo (jpeg);
						jpeg.Position = 0;

						using (var md5 = MD5.Create ()) {
							var md5sum = HexEncode (md5.ComputeHash (jpeg));

							Assert.That (md5sum, Is.EqualTo ("167a46aa81e881da2ea8a840727384d3"), "MD5 checksums do not match.");
						}
					}
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestExtractingPrecisePangolinAttachmentAsync ()
		{
			var commands = CreateExtractingPrecisePangolinAttachmentCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				await client.Inbox.OpenAsync (FolderAccess.ReadOnly);

				using (var message = await client.Inbox.GetMessageAsync (269)) {
					using (var jpeg = new MemoryStream ()) {
						var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

						attachment.Content.DecodeTo (jpeg);
						jpeg.Position = 0;

						using (var md5 = MD5.Create ()) {
							var md5sum = HexEncode (md5.ComputeHash (jpeg));

							Assert.That (md5sum, Is.EqualTo ("167a46aa81e881da2ea8a840727384d3"), "MD5 checksums do not match.");
						}
					}
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateFetchObjectIdAttributesCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000006 FETCH 1:* (UID EMAILID THREADID)\r\n", "gmail.fetch-objectid.txt"),
				new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestFetchObjectIdAttributes ()
		{
			var commands = CreateFetchObjectIdAttributesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.ObjectID), Is.True, "OBJECTID");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.EmailId | MessageSummaryItems.ThreadId);
				Assert.That (messages, Has.Count.EqualTo (4), "Count");
				Assert.That (messages[0].UniqueId.Id, Is.EqualTo (1), "UniqueId");
				Assert.That (messages[0].EmailId, Is.EqualTo ("M6d99ac3275bb4e"), "EmailId");
				Assert.That (messages[0].ThreadId, Is.EqualTo ("T64b478a75b7ea9"), "ThreadId");
				Assert.That (messages[1].UniqueId.Id, Is.EqualTo (2), "UniqueId");
				Assert.That (messages[1].EmailId, Is.EqualTo ("M288836c4c7a762"), "EmailId");
				Assert.That (messages[1].ThreadId, Is.EqualTo ("T64b478a75b7ea9"), "ThreadId");
				Assert.That (messages[2].UniqueId.Id, Is.EqualTo (3), "UniqueId");
				Assert.That (messages[2].EmailId, Is.EqualTo ("M5fdc09b49ea703"), "EmailId");
				Assert.That (messages[2].ThreadId, Is.EqualTo ("T11863d02dd95b5"), "ThreadId");
				Assert.That (messages[3].UniqueId.Id, Is.EqualTo (4), "UniqueId");
				Assert.That (messages[3].EmailId, Is.EqualTo ("M4fdc09b49ea629"), "EmailId");
				Assert.That (messages[3].ThreadId, Is.EqualTo (null), "ThreadId");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestFetchObjectIdAttributesAsync ()
		{
			var commands = CreateFetchObjectIdAttributesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.ObjectID), Is.True, "OBJECTID");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.EmailId | MessageSummaryItems.ThreadId);
				Assert.That (messages, Has.Count.EqualTo (4), "Count");
				Assert.That (messages[0].UniqueId.Id, Is.EqualTo (1), "UniqueId");
				Assert.That (messages[0].EmailId, Is.EqualTo ("M6d99ac3275bb4e"), "EmailId");
				Assert.That (messages[0].ThreadId, Is.EqualTo ("T64b478a75b7ea9"), "ThreadId");
				Assert.That (messages[1].UniqueId.Id, Is.EqualTo (2), "UniqueId");
				Assert.That (messages[1].EmailId, Is.EqualTo ("M288836c4c7a762"), "EmailId");
				Assert.That (messages[1].ThreadId, Is.EqualTo ("T64b478a75b7ea9"), "ThreadId");
				Assert.That (messages[2].UniqueId.Id, Is.EqualTo (3), "UniqueId");
				Assert.That (messages[2].EmailId, Is.EqualTo ("M5fdc09b49ea703"), "EmailId");
				Assert.That (messages[2].ThreadId, Is.EqualTo ("T11863d02dd95b5"), "ThreadId");
				Assert.That (messages[3].UniqueId.Id, Is.EqualTo (4), "UniqueId");
				Assert.That (messages[3].EmailId, Is.EqualTo ("M4fdc09b49ea629"), "EmailId");
				Assert.That (messages[3].ThreadId, Is.EqualTo (null), "ThreadId");

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateFetchSaveDateCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+savedate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"),
				new ImapReplayCommand ("A00000006 FETCH 1:* (UID SAVEDATE)\r\n", "gmail.fetch-savedate.txt"),
				new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestFetchSaveDate ()
		{
			var commands = CreateFetchSaveDateCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.SaveDate), Is.True, "SAVEDATE");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.SaveDate);
				var dto = new DateTimeOffset (2023, 9, 12, 13, 39, 01, new TimeSpan (-4, 0, 0));

				Assert.That (messages, Has.Count.EqualTo (4), "Count");
				Assert.That (messages[0].UniqueId.Id, Is.EqualTo (1), "UniqueId");
				Assert.That (messages[0].SaveDate, Is.EqualTo (dto), "SaveDate");
				Assert.That (messages[1].UniqueId.Id, Is.EqualTo (2), "UniqueId");
				Assert.That (messages[1].SaveDate, Is.EqualTo (dto), "SaveDate");
				Assert.That (messages[2].UniqueId.Id, Is.EqualTo (3), "UniqueId");
				Assert.That (messages[2].SaveDate, Is.EqualTo (dto), "SaveDate");
				Assert.That (messages[3].UniqueId.Id, Is.EqualTo (4), "UniqueId");
				Assert.That (messages[3].SaveDate, Is.EqualTo (null), "SaveDate");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestFetchSaveDateAsync ()
		{
			var commands = CreateFetchSaveDateCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.SaveDate), Is.True, "SAVEDATE");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.SaveDate);
				var dto = new DateTimeOffset (2023, 9, 12, 13, 39, 01, new TimeSpan (-4, 0, 0));

				Assert.That (messages, Has.Count.EqualTo (4), "Count");
				Assert.That (messages[0].UniqueId.Id, Is.EqualTo (1), "UniqueId");
				Assert.That (messages[0].SaveDate, Is.EqualTo (dto), "SaveDate");
				Assert.That (messages[1].UniqueId.Id, Is.EqualTo (2), "UniqueId");
				Assert.That (messages[1].SaveDate, Is.EqualTo (dto), "SaveDate");
				Assert.That (messages[2].UniqueId.Id, Is.EqualTo (3), "UniqueId");
				Assert.That (messages[2].SaveDate, Is.EqualTo (dto), "SaveDate");
				Assert.That (messages[3].UniqueId.Id, Is.EqualTo (4), "UniqueId");
				Assert.That (messages[3].SaveDate, Is.EqualTo (null), "SaveDate");

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateFetchAnnotationsCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-readonly.txt"),
				new ImapReplayCommand ("A00000005 FETCH 1:* (UID ANNOTATION (/* (value size)))\r\n", "common.fetch-annotations.txt"),
				new ImapReplayCommand ("A00000006 NOOP\r\n", "common.fetch-annotations.txt"),
			};
		}

		[Test]
		public void TestFetchAnnotations ()
		{
			var commands = CreateFetchAnnotationsCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.Annotate), Is.True, "ANNOTATE-EXPERIMENT-1");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadOnly), "AnnotationAccess");
				//Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				//Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Annotations);
				Assert.That (messages, Has.Count.EqualTo (3), "Count");

				IReadOnlyList<Annotation> annotations;

				Assert.That (messages[0].UniqueId.Id, Is.EqualTo (1), "UniqueId");
				annotations = messages[0].Annotations;
				Assert.That (annotations, Has.Count.EqualTo (1), "Count");
				Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "Entry");
				Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "Properties.Count");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "value.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "value.shared");

				Assert.That (messages[1].UniqueId.Id, Is.EqualTo (2), "UniqueId");
				annotations = messages[1].Annotations;
				Assert.That (annotations, Has.Count.EqualTo (2), "Count");
				Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
				Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "annotations[0].Properties.Count");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
				Assert.That (annotations[1].Entry, Is.EqualTo (AnnotationEntry.AltSubject), "annotations[1].Entry");
				Assert.That (annotations[1].Properties, Has.Count.EqualTo (2), "annotations[1].Properties.Count");
				Assert.That (annotations[1].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My subject"), "annotations[1] value.priv");
				Assert.That (annotations[1].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[1] value.shared");

				Assert.That (messages[2].UniqueId.Id, Is.EqualTo (3), "UniqueId");
				annotations = messages[2].Annotations;
				Assert.That (annotations, Has.Count.EqualTo (1), "Count");
				Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
				Assert.That (annotations[0].Properties, Has.Count.EqualTo (4), "annotations[0].Properties.Count");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateSize], Is.EqualTo ("10"), "annotations[0] size.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedSize], Is.EqualTo ("0"), "annotations[0] size.shared");

				var annotationsChanged = new List<AnnotationsChangedEventArgs> ();

				inbox.AnnotationsChanged += (sender, e) => {
					annotationsChanged.Add (e);
				};

				client.NoOp ();

				Assert.That (annotationsChanged, Has.Count.EqualTo (3), "# AnnotationsChanged events");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchAnnotationsAsync ()
		{
			var commands = CreateFetchAnnotationsCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.Annotate), Is.True, "ANNOTATE-EXPERIMENT-1");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadOnly), "AnnotationAccess");
				//Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				//Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Annotations);
				Assert.That (messages, Has.Count.EqualTo (3), "Count");

				IReadOnlyList<Annotation> annotations;

				Assert.That (messages[0].UniqueId.Id, Is.EqualTo (1), "UniqueId");
				annotations = messages[0].Annotations;
				Assert.That (annotations, Has.Count.EqualTo (1), "Count");
				Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "Entry");
				Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "Properties.Count");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "value.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "value.shared");

				Assert.That (messages[1].UniqueId.Id, Is.EqualTo (2), "UniqueId");
				annotations = messages[1].Annotations;
				Assert.That (annotations, Has.Count.EqualTo (2), "Count");
				Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
				Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "annotations[0].Properties.Count");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
				Assert.That (annotations[1].Entry, Is.EqualTo (AnnotationEntry.AltSubject), "annotations[1].Entry");
				Assert.That (annotations[1].Properties, Has.Count.EqualTo (2), "annotations[1].Properties.Count");
				Assert.That (annotations[1].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My subject"), "annotations[1] value.priv");
				Assert.That (annotations[1].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[1] value.shared");

				Assert.That (messages[2].UniqueId.Id, Is.EqualTo (3), "UniqueId");
				annotations = messages[2].Annotations;
				Assert.That (annotations, Has.Count.EqualTo (1), "Count");
				Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
				Assert.That (annotations[0].Properties, Has.Count.EqualTo (4), "annotations[0].Properties.Count");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
				Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateSize], Is.EqualTo ("10"), "annotations[0] size.priv");
				Assert.That (annotations[0].Properties[AnnotationAttribute.SharedSize], Is.EqualTo ("0"), "annotations[0] size.shared");

				var annotationsChanged = new List<AnnotationsChangedEventArgs> ();

				inbox.AnnotationsChanged += (sender, e) => {
					annotationsChanged.Add (e);
				};

				await client.NoOpAsync ();

				Assert.That (annotationsChanged, Has.Count.EqualTo (3), "# AnnotationsChanged events");

				client.Disconnect (false);
			}
		}

		static List<ImapReplayCommand> CreateDominoParenthesisWorkaroundCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* OK Domino IMAP4 Server Release 10.0.1FP3 ready Wed, 30 Oct 2019 09:28:06 +0100\r\n")),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "domino.capability.txt"),
				new ImapReplayCommand ("A00000001 LOGIN username password\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000002 CAPABILITY\r\n", "domino.capability.txt"),
				new ImapReplayCommand ("A00000003 NAMESPACE\r\n", "domino.namespace.txt"),
				new ImapReplayCommand ("A00000004 LIST \"\" \"INBOX\"\r\n", "domino.list-inbox.txt"),
				new ImapReplayCommand ("A00000005 SELECT Inbox\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 FETCH 1:* (UID ENVELOPE BODYSTRUCTURE)\r\n", "domino.fetch-extra-parens.txt")
			};
		}

		[Test]
		public void TestDominoParenthesisWorkaround ()
		{
			var commands = CreateDominoParenthesisWorkaroundCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.PersonalNamespaces, Has.Count.EqualTo (1), "Personal Count");
				Assert.That (client.PersonalNamespaces[0].Path, Is.EqualTo (""), "Personal Path");
				Assert.That (client.PersonalNamespaces[0].DirectorySeparator, Is.EqualTo ('\\'), "Personal DirectorySeparator");

				Assert.That (client.OtherNamespaces, Has.Count.EqualTo (1), "Other Count");
				Assert.That (client.OtherNamespaces[0].Path, Is.EqualTo ("Other"), "Other Path");
				Assert.That (client.OtherNamespaces[0].DirectorySeparator, Is.EqualTo ('\\'), "Other DirectorySeparator");

				Assert.That (client.SharedNamespaces, Has.Count.EqualTo (1), "Shared Count");
				Assert.That (client.SharedNamespaces[0].Path, Is.EqualTo ("Shared"), "Shared Path");
				Assert.That (client.SharedNamespaces[0].DirectorySeparator, Is.EqualTo ('\\'), "Shared DirectorySeparator");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure);
				Assert.That (messages, Has.Count.EqualTo (29), "Count");

				for (int i = 0; i < 29; i++) {
					Assert.That (messages[i].Index, Is.EqualTo (i), "MessageSummaryItems are out of order!");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestDominoParenthesisWorkaroundAsync ()
		{
			var commands = CreateDominoParenthesisWorkaroundCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.PersonalNamespaces, Has.Count.EqualTo (1), "Personal Count");
				Assert.That (client.PersonalNamespaces[0].Path, Is.EqualTo (""), "Personal Path");
				Assert.That (client.PersonalNamespaces[0].DirectorySeparator, Is.EqualTo ('\\'), "Personal DirectorySeparator");

				Assert.That (client.OtherNamespaces, Has.Count.EqualTo (1), "Other Count");
				Assert.That (client.OtherNamespaces[0].Path, Is.EqualTo ("Other"), "Other Path");
				Assert.That (client.OtherNamespaces[0].DirectorySeparator, Is.EqualTo ('\\'), "Other DirectorySeparator");

				Assert.That (client.SharedNamespaces, Has.Count.EqualTo (1), "Shared Count");
				Assert.That (client.SharedNamespaces[0].Path, Is.EqualTo ("Shared"), "Shared Path");
				Assert.That (client.SharedNamespaces[0].DirectorySeparator, Is.EqualTo ('\\'), "Shared DirectorySeparator");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure);
				Assert.That (messages, Has.Count.EqualTo (29), "Count");

				for (int i = 0; i < 29; i++) {
					Assert.That (messages[i].Index, Is.EqualTo (i), "MessageSummaryItems are out of order!");
				}

				client.Disconnect (false);
			}
		}

		static IList<ImapReplayCommand> CreateFetchStreamUnsolicitedInfoCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt"),
				new ImapReplayCommand ("A00000006 UID FETCH 1 (BODY.PEEK[HEADER])\r\n", "gmail.headers.1+unsolicited-info.txt"),
				new ImapReplayCommand ("A00000007 UID FETCH 1 (BODY.PEEK[])\r\n", "gmail.fetch.1+unsolicited-info.txt"),
				new ImapReplayCommand ("A00000008 UID FETCH 1 (BODY.PEEK[])\r\n", "gmail.fetch.1+unsolicited-info.txt")
			};
		}

		[Test]
		public void TestFetchStreamUnsolicitedInfo ()
		{
			var commands = CreateFetchStreamUnsolicitedInfoCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.Annotate), Is.True, "ANNOTATE-EXPERIMENT-1");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				//Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				//Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				// Keep track of various folder events
				var annotationsChanged = new List<AnnotationsChangedEventArgs> ();
				var flagsChanged = new List<MessageFlagsChangedEventArgs> ();
				var labelsChanged = new List<MessageLabelsChangedEventArgs> ();
				var modSeqChanged = new List<ModSeqChangedEventArgs> ();

				inbox.AnnotationsChanged += (sender, e) => {
					annotationsChanged.Add (e);
				};

				inbox.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				inbox.MessageLabelsChanged += (sender, e) => {
					labelsChanged.Add (e);
				};

				inbox.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				Assert.That (inbox.HighestModSeq, Is.EqualTo (2), "HIGHESTMODSEQ #1");

				var headers = inbox.GetHeaders (new UniqueId (1));
				var message = inbox.GetMessage (new UniqueId (1));
				var stream = inbox.GetStream (new UniqueId (1));

				Assert.That (inbox.HighestModSeq, Is.EqualTo (29233), "HIGHESTMODSEQ #2");

				Assert.That (annotationsChanged, Has.Count.EqualTo (3), "AnnotationsChanged");
				Assert.That (flagsChanged, Has.Count.EqualTo (3), "FlagsChanged");
				Assert.That (labelsChanged, Has.Count.EqualTo (3), "LabelsChanged");
				Assert.That (modSeqChanged, Has.Count.EqualTo (3), "ModSeqChanged");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchStreamUnsolicitedInfoAsync ()
		{
			var commands = CreateFetchStreamUnsolicitedInfoCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.Annotate), Is.True, "ANNOTATE-EXPERIMENT-1");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				//Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				//Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				// Keep track of various folder events
				var annotationsChanged = new List<AnnotationsChangedEventArgs> ();
				var flagsChanged = new List<MessageFlagsChangedEventArgs> ();
				var labelsChanged = new List<MessageLabelsChangedEventArgs> ();
				var modSeqChanged = new List<ModSeqChangedEventArgs> ();

				inbox.AnnotationsChanged += (sender, e) => {
					annotationsChanged.Add (e);
				};

				inbox.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				inbox.MessageLabelsChanged += (sender, e) => {
					labelsChanged.Add (e);
				};

				inbox.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				Assert.That (inbox.HighestModSeq, Is.EqualTo (2), "HIGHESTMODSEQ #1");

				var headers = await inbox.GetHeadersAsync (new UniqueId (1));
				var message = await inbox.GetMessageAsync (new UniqueId (1));
				var stream = await inbox.GetStreamAsync (new UniqueId (1));

				Assert.That (inbox.HighestModSeq, Is.EqualTo (29233), "HIGHESTMODSEQ #2");

				Assert.That (annotationsChanged, Has.Count.EqualTo (3), "AnnotationsChanged");
				Assert.That (flagsChanged, Has.Count.EqualTo (3), "FlagsChanged");
				Assert.That (labelsChanged, Has.Count.EqualTo (3), "LabelsChanged");
				Assert.That (modSeqChanged, Has.Count.EqualTo (3), "ModSeqChanged");

				client.Disconnect (false);
			}
		}

		static IList<ImapReplayCommand> CreateFetchNegativeModSeqResponseValuesCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "zoho.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "zoho.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "zoho.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "zoho.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "zoho.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "zoho.xlist.txt"),
				new ImapReplayCommand ("A00000005 EXAMINE Gesendet (CONDSTORE)\r\n", "zoho.examine-gesendet.txt"),
				new ImapReplayCommand ("A00000006 FETCH 1:74 (UID FLAGS MODSEQ)\r\n", "zoho.fetch-negative-modseq-values.txt")
			};
		}

		[Test]
		public void TestFetchNegativeModSeqResponseValues ()
		{
			var commands = CreateFetchNegativeModSeqResponseValuesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var gesendet = client.GetFolder (SpecialFolder.Sent);
				gesendet.Open (FolderAccess.ReadOnly);

				var messages = gesendet.Fetch (0, 73, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.That (messages, Has.Count.EqualTo (74), "Count");

				for (int i = 0; i < 15; i++) {
					Assert.That (messages[i].ModSeq, Is.EqualTo ((ulong) 0), $"MODSEQ {i}");
					Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen), $"FLAGS {i}");
				}

				for (int i = 15; i < 39; i++) {
					Assert.That (messages[i].ModSeq, Is.EqualTo ((ulong) 0), $"MODSEQ {i}");
					Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen | MessageFlags.Recent), $"FLAGS {i}");

					if (i == 35) {
						Assert.That (messages[i].Keywords, Has.Count.EqualTo (1), $"KEYWORDS {i}");
						Assert.That (messages[i].Keywords.Contains ("$FORWARDED"), Is.True, $"KEYWORDS {i}");
					}
				}

				for (int i = 39; i < 70; i++) {
					if (i == 53) {
						Assert.That (messages[i].ModSeq, Is.EqualTo (1538484935027010002), $"MODSEQ {i}");
						Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen | MessageFlags.Recent | MessageFlags.Answered), $"FLAGS {i}");
					} else {
						Assert.That (messages[i].ModSeq, Is.Null, $"MODSEQ {i}");
						Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen | MessageFlags.Recent), $"FLAGS {i}");
					}
				}
			}
		}

		[Test]
		public async Task TestFetchNegativeModSeqResponseValuesAsync ()
		{
			var commands = CreateFetchNegativeModSeqResponseValuesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var gesendet = client.GetFolder (SpecialFolder.Sent);
				await gesendet.OpenAsync (FolderAccess.ReadOnly);

				var messages = await gesendet.FetchAsync (0, 73, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.That (messages, Has.Count.EqualTo (74), "Count");

				for (int i = 0; i < 15; i++) {
					Assert.That (messages[i].ModSeq, Is.EqualTo ((ulong) 0), $"MODSEQ {i}");
					Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen), $"FLAGS {i}");
				}

				for (int i = 15; i < 39; i++) {
					Assert.That (messages[i].ModSeq, Is.EqualTo ((ulong) 0), $"MODSEQ {i}");
					Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen | MessageFlags.Recent), $"FLAGS {i}");

					if (i == 35) {
						Assert.That (messages[i].Keywords, Has.Count.EqualTo (1), $"KEYWORDS {i}");
						Assert.That (messages[i].Keywords.Contains ("$FORWARDED"), Is.True, $"KEYWORDS {i}");
					}
				}

				for (int i = 39; i < 70; i++) {
					if (i == 53) {
						Assert.That (messages[i].ModSeq, Is.EqualTo (1538484935027010002), $"MODSEQ {i}");
						Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen | MessageFlags.Recent | MessageFlags.Answered), $"FLAGS {i}");
					} else {
						Assert.That (messages[i].ModSeq, Is.Null, $"MODSEQ {i}");
						Assert.That (messages[i].Flags, Is.EqualTo (MessageFlags.Seen | MessageFlags.Recent), $"FLAGS {i}");
					}
				}
			}
		}

		static IList<ImapReplayCommand> CreateYandexGetBodyPartMissingContentCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "yandex.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "yandex.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("A00000001", "AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "yandex.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "yandex.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "yandex.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "yandex.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX\r\n", "yandex.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 UID FETCH 3016 (BODY.PEEK[2.MIME] BODY.PEEK[2])\r\n", "yandex.getbodypart-missing-content.txt")
			};
		}

		[Test]
		public void TestYandexGetBodyPartMissingContent ()
		{
			// IMAP4rev1 CHILDREN UNSELECT LITERAL+ NAMESPACE XLIST UIDPLUS ENABLE ID AUTH=PLAIN AUTH=XOAUTH2 IDLE MOVE
			const ImapCapabilities YandexGreetingCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Children | ImapCapabilities.Unselect |
				ImapCapabilities.LiteralPlus | ImapCapabilities.Namespace | ImapCapabilities.XList | ImapCapabilities.UidPlus | ImapCapabilities.Enable |
				ImapCapabilities.Id | ImapCapabilities.Idle | ImapCapabilities.Move | ImapCapabilities.Status;
			var commands = CreateYandexGetBodyPartMissingContentCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (YandexGreetingCapabilities), "Greeting Capabilities");
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (YandexGreetingCapabilities), "Greeting Capabilities");

				client.Inbox.Open (FolderAccess.ReadWrite);

				//var messages = client.Inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				//Assert.That (messages, Has.Count.EqualTo (74), "Count");

				var bodyPart = new BodyPartBasic {
					PartSpecifier = "2",
				};

				var body = client.Inbox.GetBodyPart (new UniqueId (3016), bodyPart);
				Assert.That (body, Is.Not.Null);
				Assert.That (body, Is.InstanceOf<MimePart> ());
				var part = (MimePart) body;
				Assert.That (part.ContentType.MimeType, Is.EqualTo ("application/pdf"), "Content-Type");
				Assert.That (part.ContentType.Name, Is.EqualTo ("empty.pdf"), "name");
				Assert.That (part.ContentDisposition.Disposition, Is.EqualTo (ContentDisposition.Attachment), "Content-Disposition");
				Assert.That (part.ContentDisposition.FileName, Is.EqualTo ("empty.pdf"), "filename");
				Assert.That (part.ContentTransferEncoding, Is.EqualTo (ContentEncoding.Base64), "Content-Transfer-Encoding");
				Assert.That (part.Content, Is.Null);
			}
		}

		[Test]
		public async Task TestYandexGetBodyPartMissingContentAsync ()
		{
			// IMAP4rev1 CHILDREN UNSELECT LITERAL+ NAMESPACE XLIST UIDPLUS ENABLE ID AUTH=PLAIN AUTH=XOAUTH2 IDLE MOVE
			const ImapCapabilities YandexGreetingCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Children | ImapCapabilities.Unselect |
				ImapCapabilities.LiteralPlus | ImapCapabilities.Namespace | ImapCapabilities.XList | ImapCapabilities.UidPlus | ImapCapabilities.Enable |
				ImapCapabilities.Id | ImapCapabilities.Idle | ImapCapabilities.Move | ImapCapabilities.Status;
			var commands = CreateYandexGetBodyPartMissingContentCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (YandexGreetingCapabilities), "Greeting Capabilities");
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (YandexGreetingCapabilities), "Greeting Capabilities");

				await client.Inbox.OpenAsync (FolderAccess.ReadWrite);

				//var messages = client.Inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				//Assert.That (messages, Has.Count.EqualTo (74), "Count");

				var bodyPart = new BodyPartBasic {
					PartSpecifier = "2",
				};

				var body = await client.Inbox.GetBodyPartAsync (new UniqueId (3016), bodyPart);
				Assert.That (body, Is.Not.Null);
				Assert.That (body, Is.InstanceOf<MimePart> ());
				var part = (MimePart) body;
				Assert.That (part.ContentType.MimeType, Is.EqualTo ("application/pdf"), "Content-Type");
				Assert.That (part.ContentType.Name, Is.EqualTo ("empty.pdf"), "name");
				Assert.That (part.ContentDisposition.Disposition, Is.EqualTo (ContentDisposition.Attachment), "Content-Disposition");
				Assert.That (part.ContentDisposition.FileName, Is.EqualTo ("empty.pdf"), "filename");
				Assert.That (part.ContentTransferEncoding, Is.EqualTo (ContentEncoding.Base64), "Content-Transfer-Encoding");
				Assert.That (part.Content, Is.Null);
			}
		}

		[TestCase ("ALL", MessageSummaryItems.All)]
		[TestCase ("FAST", MessageSummaryItems.Fast)]
		[TestCase ("FULL", MessageSummaryItems.Full)]
		public void TestFormatFetchSummaryItemsMacros (string expected, MessageSummaryItems items)
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (items);
				var command = ImapFolder.FormatSummaryItems (engine, request, out _);

				Assert.That (command, Is.EqualTo (expected));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsExcludeHeaders ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest () {
					Headers = new HeaderSet () {
						Exclude = true
					}
				};
				string command;

				command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER]"));

				request = new FetchRequest () {
					Headers = new HeaderSet (new[] { "FROM", "SUBJECT", "DATE" }) {
						Exclude = true
					}
				};

				command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS.NOT (FROM SUBJECT DATE)]"));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsReferences ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (MessageSummaryItems.References);
				string command;

				command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS (REFERENCES)]"));

				request = new FetchRequest () {
					Headers = new HeaderSet (new[] { "REFERENCES" })
				};

				command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS (REFERENCES)]"));

				request = new FetchRequest (MessageSummaryItems.References) {
					Headers = new HeaderSet (new[] { "REFERENCES" })
				};

				command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS (REFERENCES)]"));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsAllHeaders ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (MessageSummaryItems.Headers);

				var command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER]"));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsHeaderFieldsAndReferences ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (MessageSummaryItems.References) {
					Headers = new HeaderSet (new[] { HeaderId.InReplyTo })
				};

				var command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS (IN-REPLY-TO REFERENCES)]"));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsExcludeHeaderFieldsReferencesAndReferences ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (MessageSummaryItems.References) {
					Headers = new HeaderSet (new[] { HeaderId.References }) {
						Exclude = true
					}
				};

				var command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER]"));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsExcludeHeaderFieldsInReplyToAndReferences ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (MessageSummaryItems.References) {
					Headers = new HeaderSet (new[] { HeaderId.InReplyTo }) {
						Exclude = true
					}
				};

				var command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS.NOT (IN-REPLY-TO)]"));
			}
		}

		[Test]
		public void TestFormatFetchSummaryItemsExcludeHeaderFieldsInReplyToReferencesAndReferences ()
		{
			using (var engine = new ImapEngine (null)) {
				var request = new FetchRequest (MessageSummaryItems.References) {
					Headers = new HeaderSet (new[] { HeaderId.InReplyTo, HeaderId.References }) {
						Exclude = true
					}
				};

				var command = ImapFolder.FormatSummaryItems (engine, request, out _);
				Assert.That (command, Is.EqualTo ("BODY.PEEK[HEADER.FIELDS.NOT (IN-REPLY-TO)]"));
			}
		}
	}
}
