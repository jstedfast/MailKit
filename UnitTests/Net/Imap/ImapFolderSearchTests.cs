//
// ImapFolderSearchTests.cs
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
using System.Net;
using System.Text;
using System.Collections.Generic;

using NUnit.Framework;

using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderSearchTests
	{
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

				// Search
				var searchOptions = SearchOptions.All | SearchOptions.Min | SearchOptions.Max | SearchOptions.Count;
				var orderBy = new OrderBy [] { OrderBy.Arrival };
				var emptyOrderBy = new OrderBy[0];

				Assert.Throws<ArgumentNullException> (() => inbox.Search ((SearchQuery) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync ((SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search ((IList<UniqueId>) null, SearchQuery.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync ((IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (UniqueIdRange.All, (SearchQuery) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync (UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, (IList<UniqueId>) null, SearchQuery.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, UniqueIdRange.All, (SearchQuery) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Search ((string) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SearchAsync ((string) null));
				Assert.Throws<ArgumentException> (() => inbox.Search (string.Empty));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SearchAsync (string.Empty));

				// Sort
				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((SearchQuery) null, orderBy));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (SearchQuery.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (SearchQuery.All, emptyOrderBy));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SortAsync (SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (UniqueIdRange.All, SearchQuery.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SortAsync (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, (SearchQuery) null, orderBy));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, SearchQuery.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (searchOptions, SearchQuery.All, emptyOrderBy));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SortAsync (searchOptions, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((string) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SortAsync ((string) null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (string.Empty));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SortAsync (string.Empty));

				// Thread
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Thread ((ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.ThreadAsync ((ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread (ThreadingAlgorithm.References, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ThreadAsync (ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread ((IList<UniqueId>) null, ThreadingAlgorithm.References, SearchQuery.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ThreadAsync ((IList<UniqueId>) null, ThreadingAlgorithm.References, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Thread (UniqueIdRange.All, (ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.ThreadAsync (UniqueIdRange.All, (ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread (UniqueIdRange.All, ThreadingAlgorithm.References, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.References, null));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestRawUnicodeSearch ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH SUBJECT {13+}\r\nComunicação\r\n", "dovecot.search-raw.txt"));

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

				var matches = inbox.Search ("SUBJECT {13+}\r\nComunicação");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestSearchStringWithSpaces ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "yahoo.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000001 CAPABILITY\r\n", "yahoo.capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "yahoo.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "yahoo.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 EXAMINE Inbox\r\n", "yahoo.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH SUBJECT \"Yahoo Mail\"\r\n", "yahoo.search.txt"));

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
				inbox.Open (FolderAccess.ReadOnly);

				var uids = inbox.Search (SearchQuery.SubjectContains ("Yahoo Mail"));
				Assert.AreEqual (14, uids.Count);
				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id);

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestSearchBadCharsetFallback ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH RETURN () CHARSET UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse));
			commands.Add (new ImapReplayCommand ("A00000006 UID SEARCH RETURN () SUBJECT {6+}\r\n?@825B\r\n", "dovecot.search-raw.txt"));

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

				var uids = inbox.Search (SearchQuery.SubjectContains ("привет"));
				Assert.AreEqual (14, uids.Count);
				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id);

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestSearchWithOptionsBadCharsetFallback ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) CHARSET UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse));
			commands.Add (new ImapReplayCommand ("A00000006 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) SUBJECT {6+}\r\n?@825B\r\n", "dovecot.search-uids-options.txt"));

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

				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var matches = inbox.Search (searchOptions, SearchQuery.SubjectContains ("привет"));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.AreEqual (10, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (13, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (2, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (10, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (expectedMatchedUids[i], matches.UniqueIds[i].Id);
				Assert.AreEqual (matches.Count, matches.Relevancy.Count, "Unexpected number of relevancy scores");

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestSortBadCharsetFallback ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SORT RETURN () (SUBJECT) UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse));
			commands.Add (new ImapReplayCommand ("A00000006 UID SORT RETURN () (SUBJECT) US-ASCII SUBJECT {6+}\r\n?@825B\r\n", "dovecot.sort-raw.txt"));

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

				var uids = inbox.Sort (SearchQuery.SubjectContains ("привет"), new OrderBy[] { OrderBy.Subject });
				var expected = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (expected[i], uids[i].Id, "Unexpected value for UniqueId[{0}]", i);

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestSortWithOptionsBadCharsetFallback ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (ARRIVAL) UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse));
			commands.Add (new ImapReplayCommand ("A00000006 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (ARRIVAL) US-ASCII SUBJECT {6+}\r\n?@825B\r\n", "dovecot.sort-uids-options.txt"));

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

				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var sorted = inbox.Sort (searchOptions, SearchQuery.SubjectContains ("привет"), new OrderBy[] { OrderBy.Arrival });
				Assert.AreEqual (14, sorted.UniqueIds.Count, "Unexpected number of UIDs");
				Assert.AreEqual (sorted.Count, sorted.Relevancy.Count, "Unexpected number of relevancy scores");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, sorted.UniqueIds[i].Id, "Unexpected value for UniqueId[{0}]", i);
				Assert.IsFalse (sorted.ModSeq.HasValue, "Expected the ModSeq property to be null");
				Assert.AreEqual (1, sorted.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (14, sorted.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (14, sorted.Count, "Unexpected Count");

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestThreadBadCharsetFallback ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			//commands.Add (new ImapReplayCommand ("A00000005 UID THREAD REFERENCES US-ASCII \r\n", "dovecot.thread-references.txt"));
			//commands.Add (new ImapReplayCommand ("A00000017 UID THREAD ORDEREDSUBJECT US-ASCII UID 1:* ALL\r\n", "dovecot.thread-orderedsubject.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID THREAD REFERENCES UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse));
			commands.Add (new ImapReplayCommand ("A00000006 UID THREAD REFERENCES US-ASCII SUBJECT {6+}\r\n?@825B\r\n", "dovecot.thread-references.txt"));

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

				Assert.IsTrue (inbox.Supports (FolderFeature.Threading), "Supports threading");
				Assert.IsTrue (inbox.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References), "Supports threading by References");

				var threaded = inbox.Thread (ThreadingAlgorithm.References, SearchQuery.SubjectContains ("привет"));
				Assert.AreEqual (2, threaded.Count, "Unexpected number of root nodes in threaded results");

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestThreadUidsBadCharsetFallback ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand (Encoding.UTF8, "A00000005 UID THREAD REFERENCES UTF-8 UID 1:* SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse));
			commands.Add (new ImapReplayCommand ("A00000006 UID THREAD REFERENCES US-ASCII UID 1:* SUBJECT {6+}\r\n?@825B\r\n", "dovecot.thread-references.txt"));

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

				Assert.IsTrue (inbox.Supports (FolderFeature.Threading), "Supports threading");
				Assert.IsTrue (inbox.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References), "Supports threading by References");

				var threaded = inbox.Thread (UniqueIdRange.All, ThreadingAlgorithm.References, SearchQuery.SubjectContains ("привет"));
				Assert.AreEqual (2, threaded.Count, "Unexpected number of root nodes in threaded results");

				client.Disconnect (false);
			}
		}
	}
}
