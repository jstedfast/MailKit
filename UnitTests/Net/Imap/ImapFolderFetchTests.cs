//
// ImapFolderFetchTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
using System.Net.Sockets;
using System.Collections.Generic;
using System.Security.Cryptography;

using NUnit.Framework;

using MimeKit;

using MailKit.Net.Imap;
using MailKit.Security;
using MailKit.Search;
using MailKit;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderFetchTests
	{
		static void GetStreamsCallback (ImapFolder folder, int index, UniqueId uid, Stream stream)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = reader.ReadToEnd ();

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
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\"\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\"\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false, false));
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
				var headers = new HashSet<HeaderId> (new HeaderId [] { HeaderId.Subject });
				var fields = new HashSet<string> (new string [] { "SUBJECT" });
				var uids = new UniqueId [] { UniqueId.MinValue };
				var emptyHeaders = new HashSet<HeaderId> ();
				var emptyFields = new HashSet<string> ();
				var indexes = new int [] { 0 };

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>)null, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>)null, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>)null, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>)null, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>)null, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>)null, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>)null, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>)null, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>)null, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>)null, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>)null, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>)null, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, MessageSummaryItems.All, emptyFields));

				// Fetch + modseq
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>)null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>)null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>)null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>)null, 31337, MessageSummaryItems.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>)null, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>)null, 31337, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>)null, 31337, MessageSummaryItems.All, headers));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>)null, 31337, MessageSummaryItems.All, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None, headers));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None, headers));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<HeaderId>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, emptyHeaders));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, emptyHeaders));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (-1, -1, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (-1, -1, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (5, 1, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (5, 1, 31337, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (0, 5, 31337, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (0, 5, 31337, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<UniqueId>)null, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<UniqueId>)null, 31337, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (uids, 31337, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (uids, 31337, MessageSummaryItems.All, emptyFields));

				Assert.Throws<ArgumentNullException> (() => inbox.Fetch ((IList<int>)null, 31337, MessageSummaryItems.All, fields));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync ((IList<int>)null, 31337, MessageSummaryItems.All, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.None, fields));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.None, fields));
				Assert.Throws<ArgumentNullException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, (HashSet<string>)null));
				Assert.Throws<ArgumentException> (() => inbox.Fetch (indexes, 31337, MessageSummaryItems.All, emptyFields));
				Assert.Throws<ArgumentException> (async () => await inbox.FetchAsync (indexes, 31337, MessageSummaryItems.All, emptyFields));

				// GetHeaders
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetHeaders (-1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetHeadersAsync (-1));
				Assert.Throws<ArgumentException> (() => inbox.GetHeaders (UniqueId.Invalid));
				Assert.Throws<ArgumentException> (async () => await inbox.GetHeadersAsync (UniqueId.Invalid));

				var bodyPart = new BodyPartText ();

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetHeaders (-1, bodyPart));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetHeadersAsync (-1, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (0, (BodyPart)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetHeadersAsync (0, (BodyPart)null));

				Assert.Throws<ArgumentException> (() => inbox.GetHeaders (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentException> (async () => await inbox.GetHeadersAsync (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (UniqueId.MinValue, (BodyPart)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetHeadersAsync (UniqueId.MinValue, (BodyPart)null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetHeaders (-1, "1.2"));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetHeadersAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (0, (string)null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetHeadersAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetHeaders (UniqueId.Invalid, "1.2"));
				//Assert.Throws<ArgumentException> (async () => await inbox.GetHeadersAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetHeaders (UniqueId.MinValue, (string)null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetHeadersAsync (UniqueId.MinValue, (string) null));

				// GetMessage
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetMessage (-1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetMessageAsync (-1));
				Assert.Throws<ArgumentException> (() => inbox.GetMessage (UniqueId.Invalid));
				Assert.Throws<ArgumentException> (async () => await inbox.GetMessageAsync (UniqueId.Invalid));

				// GetBodyPart
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, bodyPart));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (BodyPart)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (BodyPart)null));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, bodyPart));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (BodyPart)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (BodyPart)null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetBodyPart (-1, "1.2"));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetBodyPartAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (0, (string)null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (0, (string) null));

				Assert.Throws<ArgumentException> (() => inbox.GetBodyPart (UniqueId.Invalid, "1.2"));
				//Assert.Throws<ArgumentException> (async () => await inbox.GetBodyPartAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetBodyPart (UniqueId.MinValue, (string)null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetBodyPartAsync (UniqueId.MinValue, (string) null));

				// GetStream
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2"));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string)null));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2"));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string)null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string)null));

				//Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart));
				//Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart));
				//Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart) null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart) null));

				//Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart));
				//Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart));
				//Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart) null));
				//Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart) null));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, 0, 1024));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, 0, -1));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, "1.2", 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, "1.2", 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (string)null, 0, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (string)null, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, "1.2", 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, "1.2", 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, "1.2", 0, 1024));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, "1.2", 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (string)null, 0, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (string)null, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, "1.2", -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, "1.2", 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, "1.2", 0, -1));

				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (-1, bodyPart, 0, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (-1, bodyPart, 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (0, (BodyPart)null, -1, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (0, (BodyPart)null, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (0, bodyPart, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (0, bodyPart, 0, -1));

				Assert.Throws<ArgumentException> (() => inbox.GetStream (UniqueId.Invalid, bodyPart, 0, 1024));
				Assert.Throws<ArgumentException> (async () => await inbox.GetStreamAsync (UniqueId.Invalid, bodyPart, 0, 1024));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStream (UniqueId.MinValue, (BodyPart)null, -1, 1024));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, (BodyPart)null, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, bodyPart, -1, 1024));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStream (UniqueId.MinValue, bodyPart, 0, -1));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamAsync (UniqueId.MinValue, bodyPart, 0, -1));

				// GetStreams
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStreams (-1, 0, GetStreamsCallback));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamsAsync (-1, 0, GetStreamsCallback));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.GetStreams (1, 0, GetStreamsCallback));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.GetStreamsAsync (1, 0, GetStreamsCallback));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams (0, -1, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamsAsync (0, -1, null));

				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams ((IList<int>)null, GetStreamsCallback));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamsAsync ((IList<int>)null, GetStreamsCallback));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams (new int [] { 0 }, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamsAsync (new int [] { 0 }, null));

				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams ((IList<UniqueId>)null, GetStreamsCallback));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamsAsync ((IList<UniqueId>)null, GetStreamsCallback));
				Assert.Throws<ArgumentNullException> (() => inbox.GetStreams (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetStreamsAsync (UniqueIdRange.All, null));

				client.Disconnect (false);
			}
		}
	}
}
