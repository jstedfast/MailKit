//
// ImapFolderSearchTests.cs
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

using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderSearchTests
	{
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

				// Search
				var searchOptions = SearchOptions.All | SearchOptions.Min | SearchOptions.Max | SearchOptions.Count;
				var orderBy = new OrderBy [] { OrderBy.Arrival };
				var emptyOrderBy = Array.Empty<OrderBy> ();

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

		static IList<ImapReplayCommand> CreateSearchFilterCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+filters.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand ("A00000005 UID SEARCH RETURN (ALL) FILTER MyFilter\r\n", "dovecot.search-all.txt"),
				new ImapReplayCommand ("A00000006 UID SEARCH RETURN (ALL) FILTER MyUndefinedFilter\r\n", Encoding.ASCII.GetBytes ("A00000006 NO [UNDEFINED-FILTER MyUndefinedFilter] THe specified filter is undefined.\r\n")),
			};
		}

		[Test]
		public void TestSearchFilter ()
		{
			var commands = CreateSearchFilterCommands ();

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

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.Filters), Is.True, "ImapCapabilities.Filters");

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				var uids = inbox.Search (SearchQuery.Filter ("MyFilter"));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				Assert.Throws<ImapCommandException> (() => inbox.Search (SearchQuery.Filter ("MyUndefinedFilter")));

				// Now disable the FILTERS extension and try again...
				client.Capabilities &= ~ImapCapabilities.Filters;
				Assert.Throws<NotSupportedException> (() => inbox.Search (SearchQuery.Filter ("MyFilter")));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchFilterAsync ()
		{
			var commands = CreateSearchFilterCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.Filters), Is.True, "ImapCapabilities.Filters");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var uids = await inbox.SearchAsync (SearchQuery.Filter ("MyFilter"));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				Assert.ThrowsAsync<ImapCommandException> (() => inbox.SearchAsync (SearchQuery.Filter ("MyUndefinedFilter")));

				// Now disable the SAVEDATE extension and try again...
				client.Capabilities &= ~ImapCapabilities.Filters;
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (SearchQuery.Filter ("MyFilter")));

				await client.DisconnectAsync (false);
			}
		}

		static IList<ImapReplayCommand> CreateSearchFuzzyCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+fuzzy.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand ("A00000005 UID SEARCH RETURN (ALL) FUZZY BODY fuzzy-match\r\n", "dovecot.search-all.txt"),
			};
		}

		[Test]
		public void TestSearchFuzzy ()
		{
			var commands = CreateSearchFuzzyCommands ();

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

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.FuzzySearch), Is.True, "ImapCapabilities.FuzzySearch");

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				var uids = inbox.Search (SearchQuery.Fuzzy (SearchQuery.BodyContains ("fuzzy-match")));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				// Now disable the FUZZY extension and try again...
				client.Capabilities &= ~ImapCapabilities.FuzzySearch;
				Assert.Throws<NotSupportedException> (() => inbox.Search (SearchQuery.Fuzzy (SearchQuery.BodyContains ("fuzzy-match"))));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchFuzzyAsync ()
		{
			var commands = CreateSearchFuzzyCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.FuzzySearch), Is.True, "ImapCapabilities.FuzzySearch");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var uids = await inbox.SearchAsync (SearchQuery.Fuzzy (SearchQuery.BodyContains ("fuzzy-match")));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				// Now disable the FUZZY extension and try again...
				client.Capabilities &= ~ImapCapabilities.FuzzySearch;
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (SearchQuery.Fuzzy (SearchQuery.BodyContains ("fuzzy-match"))));

				await client.DisconnectAsync (false);
			}
		}

		static IList<ImapReplayCommand> CreateSearchSaveDateCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+savedate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand ("A00000005 UID SEARCH RETURN (ALL) SAVEDATESUPPORTED\r\n", "dovecot.search-all.txt"),
				new ImapReplayCommand ("A00000006 UID SEARCH RETURN (ALL) SAVEDBEFORE 12-Oct-2016\r\n", "dovecot.search-all.txt"),
				new ImapReplayCommand ("A00000007 UID SEARCH RETURN (ALL) SAVEDON 12-Oct-2016\r\n", "dovecot.search-all.txt"),
				new ImapReplayCommand ("A00000008 UID SEARCH RETURN (ALL) SAVEDSINCE 12-Oct-2016\r\n", "dovecot.search-all.txt"),
			};
		}

		[Test]
		public void TestSearchSaveDate ()
		{
			var commands = CreateSearchSaveDateCommands ();

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

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.SaveDate), Is.True, "ImapCapabilities.SaveDate");

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				var uids = inbox.Search (SearchQuery.SaveDateSupported);
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				uids = inbox.Search (SearchQuery.SavedBefore (new DateTime (2016, 10, 12)));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				uids = inbox.Search (SearchQuery.SavedOn (new DateTime (2016, 10, 12)));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				uids = inbox.Search (SearchQuery.SavedSince (new DateTime (2016, 10, 12)));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				// Now disable the SAVEDATE extension and try again...
				client.Capabilities &= ~ImapCapabilities.SaveDate;
				Assert.Throws<NotSupportedException> (() => inbox.Search (SearchQuery.SaveDateSupported));
				Assert.Throws<NotSupportedException> (() => inbox.Search (SearchQuery.SavedBefore (new DateTime (2016, 10, 12))));
				Assert.Throws<NotSupportedException> (() => inbox.Search (SearchQuery.SavedOn (new DateTime (2016, 10, 12))));
				Assert.Throws<NotSupportedException> (() => inbox.Search (SearchQuery.SavedSince (new DateTime (2016, 10, 12))));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchSaveDateAsync ()
		{
			var commands = CreateSearchSaveDateCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities.HasFlag (ImapCapabilities.SaveDate), Is.True, "ImapCapabilities.SaveDate");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var uids = await inbox.SearchAsync (SearchQuery.SaveDateSupported);
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				uids = await inbox.SearchAsync (SearchQuery.SavedBefore (new DateTime (2016, 10, 12)));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				uids = await inbox.SearchAsync (SearchQuery.SavedOn (new DateTime (2016, 10, 12)));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				uids = await inbox.SearchAsync (SearchQuery.SavedSince (new DateTime (2016, 10, 12)));
				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				// Now disable the SAVEDATE extension and try again...
				client.Capabilities &= ~ImapCapabilities.SaveDate;
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (SearchQuery.SaveDateSupported));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (SearchQuery.SavedBefore (new DateTime (2016, 10, 12))));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (SearchQuery.SavedOn (new DateTime (2016, 10, 12))));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (SearchQuery.SavedSince (new DateTime (2016, 10, 12))));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateRawUnicodeSearchCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH SUBJECT {13+}\r\nComunicação\r\n", "dovecot.search-raw.txt")
			};
		}

		[Test]
		public void TestRawUnicodeSearch ()
		{
			var commands = CreateRawUnicodeSearchCommands ();

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

				var matches = inbox.Search ("SUBJECT {13+}\r\nComunicação");
				Assert.That (matches.Max.HasValue, Is.True, "MAX should always be set");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX value");
				Assert.That (matches.Min.HasValue, Is.True, "MIN should always be set");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN value");
				Assert.That (matches.Count, Is.EqualTo (14), "COUNT should always be set");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14));
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (i + 1));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestRawUnicodeSearchAsync ()
		{
			var commands = CreateRawUnicodeSearchCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var matches = await inbox.SearchAsync ("SUBJECT {13+}\r\nComunicação");
				Assert.That (matches.Max.HasValue, Is.True, "MAX should always be set");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX value");
				Assert.That (matches.Min.HasValue, Is.True, "MIN should always be set");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN value");
				Assert.That (matches.Count, Is.EqualTo (14), "COUNT should always be set");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14));
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (i + 1));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSearchStringWithSpacesCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "yahoo.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000001 CAPABILITY\r\n", "yahoo.capabilities.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "yahoo.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "yahoo.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 EXAMINE Inbox\r\n", "yahoo.examine-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH SUBJECT \"Yahoo Mail\"\r\n", "yahoo.search.txt")
			};
		}

		[Test]
		public void TestSearchStringWithSpaces ()
		{
			var commands = CreateSearchStringWithSpacesCommands ();

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
				inbox.Open (FolderAccess.ReadOnly);

				var uids = inbox.Search (SearchQuery.SubjectContains ("Yahoo Mail"));
				Assert.That (uids, Has.Count.EqualTo (14));
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchStringWithSpacesAsync ()
		{
			var commands = CreateSearchStringWithSpacesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadOnly);

				var uids = await inbox.SearchAsync (SearchQuery.SubjectContains ("Yahoo Mail"));
				Assert.That (uids, Has.Count.EqualTo (14));
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSearchBadCharsetFallbackCommands ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			return  new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH RETURN (ALL) CHARSET UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse),
				new ImapReplayCommand ("A00000006 UID SEARCH RETURN (ALL) SUBJECT {6+}\r\n?@825B\r\n", "dovecot.search-raw.txt")
			};
		}

		[Test]
		public void TestSearchBadCharsetFallback ()
		{
			var commands = CreateSearchBadCharsetFallbackCommands ();

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

				var uids = inbox.Search (SearchQuery.SubjectContains ("привет"));
				Assert.That (uids, Has.Count.EqualTo (14));
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchBadCharsetFallbackAsync ()
		{
			var commands = CreateSearchBadCharsetFallbackCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var uids = await inbox.SearchAsync (SearchQuery.SubjectContains ("привет"));
				Assert.That (uids, Has.Count.EqualTo (14));
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSearchWithOptionsBadCharsetFallbackCommands ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) CHARSET UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse),
				new ImapReplayCommand ("A00000006 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) SUBJECT {6+}\r\n?@825B\r\n", "dovecot.search-uids-options.txt")
			};
		}

		[Test]
		public void TestSearchWithOptionsBadCharsetFallback ()
		{
			var commands = CreateSearchWithOptionsBadCharsetFallbackCommands ();

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

				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var matches = inbox.Search (searchOptions, SearchQuery.SubjectContains ("привет"));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.That (matches.Count, Is.EqualTo (10), "Unexpected COUNT");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (13), "Unexpected MAX");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (2), "Unexpected MIN");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (10), "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (expectedMatchedUids[i]));
				Assert.That (matches.Relevancy, Has.Count.EqualTo (matches.Count), "Unexpected number of relevancy scores");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchWithOptionsBadCharsetFallbackAsync ()
		{
			var commands = CreateSearchWithOptionsBadCharsetFallbackCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var matches = await inbox.SearchAsync (searchOptions, SearchQuery.SubjectContains ("привет"));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.That (matches.Count, Is.EqualTo (10), "Unexpected COUNT");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (13), "Unexpected MAX");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (2), "Unexpected MIN");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (10), "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (expectedMatchedUids[i]));
				Assert.That (matches.Relevancy, Has.Count.EqualTo (matches.Count), "Unexpected number of relevancy scores");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSortBadCharsetFallbackCommands ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SORT RETURN (ALL) (SUBJECT) UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse),
				new ImapReplayCommand ("A00000006 UID SORT RETURN (ALL) (SUBJECT) US-ASCII SUBJECT {6+}\r\n?@825B\r\n", "dovecot.sort-raw.txt")
			};
		}

		[Test]
		public void TestSortBadCharsetFallback ()
		{
			var commands = CreateSortBadCharsetFallbackCommands ();

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

				var uids = inbox.Sort (SearchQuery.SubjectContains ("привет"), new OrderBy[] { OrderBy.Subject });
				var expected = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (expected[i]), $"Unexpected value for UniqueId[{i}]");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSortBadCharsetFallbackAsync ()
		{
			var commands = CreateSortBadCharsetFallbackCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var uids = await inbox.SortAsync (SearchQuery.SubjectContains ("привет"), new OrderBy[] { OrderBy.Subject });
				var expected = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (expected[i]), $"Unexpected value for UniqueId[{i}]");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSortWithOptionsBadCharsetFallbackCommands ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (ARRIVAL) UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse),
				new ImapReplayCommand ("A00000006 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (ARRIVAL) US-ASCII SUBJECT {6+}\r\n?@825B\r\n", "dovecot.sort-uids-options.txt")
			};
		}

		[Test]
		public void TestSortWithOptionsBadCharsetFallback ()
		{
			var commands = CreateSortWithOptionsBadCharsetFallbackCommands ();

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

				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var sorted = inbox.Sort (searchOptions, SearchQuery.SubjectContains ("привет"), new OrderBy[] { OrderBy.Arrival });
				Assert.That (sorted.UniqueIds, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				Assert.That (sorted.Relevancy, Has.Count.EqualTo (sorted.Count), "Unexpected number of relevancy scores");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.That (sorted.UniqueIds[i].Id, Is.EqualTo (i + 1), $"Unexpected value for UniqueId[{i}]");
				Assert.That (sorted.ModSeq.HasValue, Is.False, "Expected the ModSeq property to be null");
				Assert.That (sorted.Min.Value.Id, Is.EqualTo (1), "Unexpected Min");
				Assert.That (sorted.Max.Value.Id, Is.EqualTo (14), "Unexpected Max");
				Assert.That (sorted.Count, Is.EqualTo (14), "Unexpected Count");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSortWithOptionsBadCharsetFallbackAsync ()
		{
			var commands = CreateSortWithOptionsBadCharsetFallbackCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var sorted = await inbox.SortAsync (searchOptions, SearchQuery.SubjectContains ("привет"), new OrderBy[] { OrderBy.Arrival });
				Assert.That (sorted.UniqueIds, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				Assert.That (sorted.Relevancy, Has.Count.EqualTo (sorted.Count), "Unexpected number of relevancy scores");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.That (sorted.UniqueIds[i].Id, Is.EqualTo (i + 1), $"Unexpected value for UniqueId[{i}]");
				Assert.That (sorted.ModSeq.HasValue, Is.False, "Expected the ModSeq property to be null");
				Assert.That (sorted.Min.Value.Id, Is.EqualTo (1), "Unexpected Min");
				Assert.That (sorted.Max.Value.Id, Is.EqualTo (14), "Unexpected Max");
				Assert.That (sorted.Count, Is.EqualTo (14), "Unexpected Count");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateThreadBadCharsetFallbackCommands ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				//new ImapReplayCommand ("A00000005 UID THREAD REFERENCES US-ASCII \r\n", "dovecot.thread-references.txt"),
				//(new ImapReplayCommand ("A00000017 UID THREAD ORDEREDSUBJECT US-ASCII UID 1:* ALL\r\n", "dovecot.thread-orderedsubject.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID THREAD REFERENCES UTF-8 SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse),
				new ImapReplayCommand ("A00000006 UID THREAD REFERENCES US-ASCII SUBJECT {6+}\r\n?@825B\r\n", "dovecot.thread-references.txt")
			};
		}

		[Test]
		public void TestThreadBadCharsetFallback ()
		{
			var commands = CreateThreadBadCharsetFallbackCommands ();

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

				Assert.That (inbox.Supports (FolderFeature.Threading), Is.True, "Supports threading");
				Assert.That (inbox.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Supports threading by References");

				var threaded = inbox.Thread (ThreadingAlgorithm.References, SearchQuery.SubjectContains ("привет"));
				Assert.That (threaded, Has.Count.EqualTo (2), "Unexpected number of root nodes in threaded results");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestThreadBadCharsetFallbackAsync ()
		{
			var commands = CreateThreadBadCharsetFallbackCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.That (inbox.Supports (FolderFeature.Threading), Is.True, "Supports threading");
				Assert.That (inbox.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Supports threading by References");

				var threaded = await inbox.ThreadAsync (ThreadingAlgorithm.References, SearchQuery.SubjectContains ("привет"));
				Assert.That (threaded, Has.Count.EqualTo (2), "Unexpected number of root nodes in threaded results");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateThreadUidsBadCharsetFallbackCommands ()
		{
			var badCharsetResponse = Encoding.ASCII.GetBytes ("A00000005 NO [BADCHARSET (US-ASCII)] The specified charset is not supported.\r\n");

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand (Encoding.UTF8, "A00000005 UID THREAD REFERENCES UTF-8 UID 1:* SUBJECT {12+}\r\nпривет\r\n", badCharsetResponse),
				new ImapReplayCommand ("A00000006 UID THREAD REFERENCES US-ASCII UID 1:* SUBJECT {6+}\r\n?@825B\r\n", "dovecot.thread-references.txt")
			};
		}

		[Test]
		public void TestThreadUidsBadCharsetFallback ()
		{
			var commands = CreateThreadUidsBadCharsetFallbackCommands ();

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

				Assert.That (inbox.Supports (FolderFeature.Threading), Is.True, "Supports threading");
				Assert.That (inbox.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Supports threading by References");

				var threaded = inbox.Thread (UniqueIdRange.All, ThreadingAlgorithm.References, SearchQuery.SubjectContains ("привет"));
				Assert.That (threaded, Has.Count.EqualTo (2), "Unexpected number of root nodes in threaded results");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestThreadUidsBadCharsetFallbackAsync ()
		{
			var commands = CreateThreadUidsBadCharsetFallbackCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Inbox.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.That (inbox.Supports (FolderFeature.Threading), Is.True, "Supports threading");
				Assert.That (inbox.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Supports threading by References");

				var threaded = await inbox.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.References, SearchQuery.SubjectContains ("привет"));
				Assert.That (threaded, Has.Count.EqualTo (2), "Unexpected number of root nodes in threaded results");

				await client.DisconnectAsync (false);
			}
		}
	}
}
