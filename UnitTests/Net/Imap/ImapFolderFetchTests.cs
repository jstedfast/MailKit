//
// ImapFolderFetchTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

using NUnit.Framework;

using MimeKit;

using MailKit;
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
			default: throw new ArgumentOutOfRangeException ();
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

				Assert.AreEqual (expected, text);
			}
		}

		static async Task GetStreamsAsyncCallback (ImapFolder folder, int index, UniqueId uid, Stream stream, CancellationToken cancellationToken)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = await reader.ReadToEndAsync ();

				Assert.AreEqual (expected, text);
			}
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

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
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None));

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
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, headerIds));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headerIds));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None, headers));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerIds));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None, headers));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All, headerIds));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All, headerIds));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None, headers));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, headerFields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All, headerFields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None, fields));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, invalidHeaderFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>) null, 31337, MessageSummaryItems.All, headerFields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None, fields));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<string>) null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, invalidHeaderFields));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, invalidHeaderFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>) null, 31337, MessageSummaryItems.All, headerFields));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>) null, 31337, MessageSummaryItems.All, headerFields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None, fields));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None, fields));
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
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2"));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2"));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string) null));

				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart));
				//Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart));
				//Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart) null));
				//Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart) null));

				//Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart));
				//Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart));
				//Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart) null));
				//Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart) null));

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
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX\r\n", "common.select-inbox-no-modseq.txt"));

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

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

		static readonly string[] PreviewTextValues = {
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			"Don’t miss our celebrity guest Monday evening",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…",
			"Don’t miss our celebrity guest Monday evening",
			"Planet Fitness https://view.email.planetfitness.com/?qs=9a098a031cabde68c0a4260051cd6fe473a2e997a53678ff26b4b199a711a9d2ad0536530d6f837c246b09f644d42016ecfb298f930b7af058e9e454b34f3d818ceb3052ae317b1ac4594aab28a2d788 View web ver…"
		};

		[Test]
		public void TestFetchPreviewText ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 FETCH 1:6 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"));
			commands.Add (new ImapReplayCommand ("A00000012 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (PreviewTextValues[i], messages[i].PreviewText);

				messages = inbox.Fetch (new int[] { 0, 1, 2, 3, 4, 5 }, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (PreviewTextValues[i], messages[i].PreviewText);

				messages = inbox.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (PreviewTextValues[i], messages[i].PreviewText);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchPreviewTextAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 FETCH 1:6 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"));
			commands.Add (new ImapReplayCommand ("A00000012 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE)\r\n", "gmail.fetch-previewtext-bodystructure.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 UID FETCH 1,4 (BODY.PEEK[TEXT]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-only.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 UID FETCH 3,6 (BODY.PEEK[1]<0.512>)\r\n", "gmail.fetch-previewtext-peek-text-alternative.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 UID FETCH 2,5 (BODY.PEEK[TEXT]<0.16384>)\r\n", "gmail.fetch-previewtext-peek-html-only.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (PreviewTextValues[i], messages[i].PreviewText);

				messages = await inbox.FetchAsync (new int[] { 0, 1, 2, 3, 4, 5 }, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (PreviewTextValues[i], messages[i].PreviewText);

				messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.PreviewText);
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (PreviewTextValues[i], messages[i].PreviewText);

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestExpungeDuringFetch ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 UID FETCH 1:6 (UID INTERNALDATE ENVELOPE)\r\n", "gmail.expunge-during-fetch.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadOnly);

				var range = new UniqueIdRange (0, 1, 6);
				var messages = inbox.Fetch (range, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate | MessageSummaryItems.Envelope);

				Assert.AreEqual (4, messages.Count, "Count");
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (i, messages[i].Index, "Index #{0}", i);
				Assert.AreEqual ((uint) 1, messages[0].UniqueId.Id, "UniqueId #0");
				Assert.AreEqual ((uint) 3, messages[1].UniqueId.Id, "UniqueId #1");
				Assert.AreEqual ((uint) 4, messages[2].UniqueId.Id, "UniqueId #2");
				Assert.AreEqual ((uint) 5, messages[3].UniqueId.Id, "UniqueId #3");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestExpungeDuringFetchAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 UID FETCH 1:6 (UID INTERNALDATE ENVELOPE)\r\n", "gmail.expunge-during-fetch.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var range = new UniqueIdRange (0, 1, 6);
				var messages = await inbox.FetchAsync (range, MessageSummaryItems.UniqueId | MessageSummaryItems.InternalDate | MessageSummaryItems.Envelope);

				Assert.AreEqual (4, messages.Count, "Count");
				for (int i = 0; i < messages.Count; i++)
					Assert.AreEqual (i, messages[i].Index, "Index #{0}", i);
				Assert.AreEqual ((uint) 1, messages[0].UniqueId.Id, "UniqueId #0");
				Assert.AreEqual ((uint) 3, messages[1].UniqueId.Id, "UniqueId #1");
				Assert.AreEqual ((uint) 4, messages[2].UniqueId.Id, "UniqueId #2");
				Assert.AreEqual ((uint) 5, messages[3].UniqueId.Id, "UniqueId #3");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestExtractingPrecisePangolinAttachment ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 FETCH 270 (BODY.PEEK[])\r\n", "gmail.precise-pangolin-message.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				client.Inbox.Open (FolderAccess.ReadOnly);

				var message = client.Inbox.GetMessage (269);

				using (var jpeg = new MemoryStream ()) {
					var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

					attachment.Content.DecodeTo (jpeg);
					jpeg.Position = 0;

					using (var md5 = new MD5CryptoServiceProvider ()) {
						var md5sum = HexEncode (md5.ComputeHash (jpeg));

						Assert.AreEqual ("167a46aa81e881da2ea8a840727384d3", md5sum, "MD5 checksums do not match.");
					}
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestExtractingPrecisePangolinAttachmentAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 FETCH 270 (BODY.PEEK[])\r\n", "gmail.precise-pangolin-message.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				await client.Inbox.OpenAsync (FolderAccess.ReadOnly);

				var message = await client.Inbox.GetMessageAsync (269);

				using (var jpeg = new MemoryStream ()) {
					var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

					attachment.Content.DecodeTo (jpeg);
					jpeg.Position = 0;

					using (var md5 = new MD5CryptoServiceProvider ()) {
						var md5sum = HexEncode (md5.ComputeHash (jpeg));

						Assert.AreEqual ("167a46aa81e881da2ea8a840727384d3", md5sum, "MD5 checksums do not match.");
					}
				}

				await client.DisconnectAsync (false);
			}
		}

		List<ImapReplayCommand> CreateFetchObjectIdAttributesCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 FETCH 1:* (UID EMAILID THREADID)\r\n", "gmail.fetch-objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestFetchObjectIdAttributes ()
		{
			var commands = CreateFetchObjectIdAttributesCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.ObjectID), "OBJECTID");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadOnly);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.EmailId | MessageSummaryItems.ThreadId);
				Assert.AreEqual (4, messages.Count, "Count");
				Assert.AreEqual (1, messages[0].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M6d99ac3275bb4e", messages[0].EmailId, "EmailId");
				Assert.AreEqual ("T64b478a75b7ea9", messages[0].ThreadId, "ThreadId");
				Assert.AreEqual (2, messages[1].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M288836c4c7a762", messages[1].EmailId, "EmailId");
				Assert.AreEqual ("T64b478a75b7ea9", messages[1].ThreadId, "ThreadId");
				Assert.AreEqual (3, messages[2].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M5fdc09b49ea703", messages[2].EmailId, "EmailId");
				Assert.AreEqual ("T11863d02dd95b5", messages[2].ThreadId, "ThreadId");
				Assert.AreEqual (4, messages[3].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M4fdc09b49ea629", messages[3].EmailId, "EmailId");
				Assert.AreEqual (null, messages[3].ThreadId, "ThreadId");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestFetchObjectIdAttributesAsync ()
		{
			var commands = CreateFetchObjectIdAttributesCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.ObjectID), "OBJECTID");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.EmailId | MessageSummaryItems.ThreadId);
				Assert.AreEqual (4, messages.Count, "Count");
				Assert.AreEqual (1, messages[0].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M6d99ac3275bb4e", messages[0].EmailId, "EmailId");
				Assert.AreEqual ("T64b478a75b7ea9", messages[0].ThreadId, "ThreadId");
				Assert.AreEqual (2, messages[1].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M288836c4c7a762", messages[1].EmailId, "EmailId");
				Assert.AreEqual ("T64b478a75b7ea9", messages[1].ThreadId, "ThreadId");
				Assert.AreEqual (3, messages[2].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M5fdc09b49ea703", messages[2].EmailId, "EmailId");
				Assert.AreEqual ("T11863d02dd95b5", messages[2].ThreadId, "ThreadId");
				Assert.AreEqual (4, messages[3].UniqueId.Id, "UniqueId");
				Assert.AreEqual ("M4fdc09b49ea629", messages[3].EmailId, "EmailId");
				Assert.AreEqual (null, messages[3].ThreadId, "ThreadId");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateFetchAnnotationsCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-readonly.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 FETCH 1:* (UID ANNOTATION (/* (value size)))\r\n", "common.fetch-annotations.txt"));

			return commands;
		}

		[Test]
		public void TestFetchAnnotations ()
		{
			var commands = CreateFetchAnnotationsCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.Annotate), "ANNOTATE-EXPERIMENT-1");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				Assert.AreEqual (AnnotationAccess.ReadOnly, inbox.AnnotationAccess, "AnnotationAccess");
				//Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				//Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Annotations);
				Assert.AreEqual (3, messages.Count, "Count");

				IList<Annotation> annotations;

				Assert.AreEqual (1, messages[0].UniqueId.Id, "UniqueId");
				annotations = messages[0].Annotations;
				Assert.AreEqual (1, annotations.Count, "Count");
				Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "Entry");
				Assert.AreEqual (2, annotations[0].Properties.Count, "Properties.Count");
				Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "value.priv");
				Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "value.shared");

				Assert.AreEqual (2, messages[1].UniqueId.Id, "UniqueId");
				annotations = messages[1].Annotations;
				Assert.AreEqual (2, annotations.Count, "Count");
				Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
				Assert.AreEqual (2, annotations[0].Properties.Count, "annotations[0].Properties.Count");
				Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
				Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
				Assert.AreEqual (AnnotationEntry.AltSubject, annotations[1].Entry, "annotations[1].Entry");
				Assert.AreEqual (2, annotations[1].Properties.Count, "annotations[1].Properties.Count");
				Assert.AreEqual ("My subject", annotations[1].Properties[AnnotationAttribute.PrivateValue], "annotations[1] value.priv");
				Assert.AreEqual (null, annotations[1].Properties[AnnotationAttribute.SharedValue], "annotations[1] value.shared");

				Assert.AreEqual (3, messages[2].UniqueId.Id, "UniqueId");
				annotations = messages[2].Annotations;
				Assert.AreEqual (1, annotations.Count, "Count");
				Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
				Assert.AreEqual (4, annotations[0].Properties.Count, "annotations[0].Properties.Count");
				Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
				Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
				Assert.AreEqual ("10", annotations[0].Properties[AnnotationAttribute.PrivateSize], "annotations[0] size.priv");
				Assert.AreEqual ("0", annotations[0].Properties[AnnotationAttribute.SharedSize], "annotations[0] size.shared");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestFetchAnnotationsAsync ()
		{
			var commands = CreateFetchAnnotationsCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.Annotate), "ANNOTATE-EXPERIMENT-1");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.AreEqual (AnnotationAccess.ReadOnly, inbox.AnnotationAccess, "AnnotationAccess");
				//Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				//Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Annotations);
				Assert.AreEqual (3, messages.Count, "Count");

				IList<Annotation> annotations;

				Assert.AreEqual (1, messages[0].UniqueId.Id, "UniqueId");
				annotations = messages[0].Annotations;
				Assert.AreEqual (1, annotations.Count, "Count");
				Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "Entry");
				Assert.AreEqual (2, annotations[0].Properties.Count, "Properties.Count");
				Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "value.priv");
				Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "value.shared");

				Assert.AreEqual (2, messages[1].UniqueId.Id, "UniqueId");
				annotations = messages[1].Annotations;
				Assert.AreEqual (2, annotations.Count, "Count");
				Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
				Assert.AreEqual (2, annotations[0].Properties.Count, "annotations[0].Properties.Count");
				Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
				Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
				Assert.AreEqual (AnnotationEntry.AltSubject, annotations[1].Entry, "annotations[1].Entry");
				Assert.AreEqual (2, annotations[1].Properties.Count, "annotations[1].Properties.Count");
				Assert.AreEqual ("My subject", annotations[1].Properties[AnnotationAttribute.PrivateValue], "annotations[1] value.priv");
				Assert.AreEqual (null, annotations[1].Properties[AnnotationAttribute.SharedValue], "annotations[1] value.shared");

				Assert.AreEqual (3, messages[2].UniqueId.Id, "UniqueId");
				annotations = messages[2].Annotations;
				Assert.AreEqual (1, annotations.Count, "Count");
				Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
				Assert.AreEqual (4, annotations[0].Properties.Count, "annotations[0].Properties.Count");
				Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
				Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
				Assert.AreEqual ("10", annotations[0].Properties[AnnotationAttribute.PrivateSize], "annotations[0] size.priv");
				Assert.AreEqual ("0", annotations[0].Properties[AnnotationAttribute.SharedSize], "annotations[0] size.shared");

				client.Disconnect (false);
			}
		}

		List<ImapReplayCommand> CreateDominoParenthesisWorkaroundCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* OK Domino IMAP4 Server Release 10.0.1FP3 ready Wed, 30 Oct 2019 09:28:06 +0100\r\n")));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "domino.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN username password\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000002 CAPABILITY\r\n", "domino.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 NAMESPACE\r\n", "domino.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 LIST \"\" \"INBOX\"\r\n", "domino.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT Inbox\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 FETCH 1:* (UID ENVELOPE BODYSTRUCTURE)\r\n", "domino.fetch-extra-parens.txt"));

			return commands;
		}

		[Test]
		public void TestDominoParenthesisWorkaround ()
		{
			var commands = CreateDominoParenthesisWorkaroundCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, client.PersonalNamespaces.Count, "Personal Count");
				Assert.AreEqual ("", client.PersonalNamespaces[0].Path, "Personal Path");
				Assert.AreEqual ('\\', client.PersonalNamespaces[0].DirectorySeparator, "Personal DirectorySeparator");

				Assert.AreEqual (1, client.OtherNamespaces.Count, "Other Count");
				Assert.AreEqual ("Other", client.OtherNamespaces[0].Path, "Other Path");
				Assert.AreEqual ('\\', client.OtherNamespaces[0].DirectorySeparator, "Other DirectorySeparator");

				Assert.AreEqual (1, client.SharedNamespaces.Count, "Shared Count");
				Assert.AreEqual ("Shared", client.SharedNamespaces[0].Path, "Shared Path");
				Assert.AreEqual ('\\', client.SharedNamespaces[0].DirectorySeparator, "Shared DirectorySeparator");

				var inbox = client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				var messages = inbox.Fetch (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure);
				Assert.AreEqual (29, messages.Count, "Count");

				for (int i = 0; i < 29; i++) {
					Assert.AreEqual (i, messages[i].Index, "MessageSummaryItems are out of order!");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestDominoParenthesisWorkaroundAsync ()
		{
			var commands = CreateDominoParenthesisWorkaroundCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, client.PersonalNamespaces.Count, "Personal Count");
				Assert.AreEqual ("", client.PersonalNamespaces[0].Path, "Personal Path");
				Assert.AreEqual ('\\', client.PersonalNamespaces[0].DirectorySeparator, "Personal DirectorySeparator");

				Assert.AreEqual (1, client.OtherNamespaces.Count, "Other Count");
				Assert.AreEqual ("Other", client.OtherNamespaces[0].Path, "Other Path");
				Assert.AreEqual ('\\', client.OtherNamespaces[0].DirectorySeparator, "Other DirectorySeparator");

				Assert.AreEqual (1, client.SharedNamespaces.Count, "Shared Count");
				Assert.AreEqual ("Shared", client.SharedNamespaces[0].Path, "Shared Path");
				Assert.AreEqual ('\\', client.SharedNamespaces[0].DirectorySeparator, "Shared DirectorySeparator");

				var inbox = client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var messages = await inbox.FetchAsync (0, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure);
				Assert.AreEqual (29, messages.Count, "Count");

				for (int i = 0; i < 29; i++) {
					Assert.AreEqual (i, messages[i].Index, "MessageSummaryItems are out of order!");
				}

				client.Disconnect (false);
			}
		}
	}
}
