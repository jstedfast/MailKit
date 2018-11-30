//
// ImapFolderSearchTests.cs
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
using System.Net;
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
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search ((IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, (IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Search (searchOptions, UniqueIdRange.All, (SearchQuery) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Search ((string) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SearchAsync ((string) null));
				Assert.Throws<ArgumentException> (() => inbox.Search (string.Empty));
				Assert.Throws<ArgumentException> (async () => await inbox.SearchAsync (string.Empty));

				// Sort
				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync ((SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync ((IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (searchOptions, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (searchOptions, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, (IList<UniqueId>) null, SearchQuery.All, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, (SearchQuery) null, orderBy));
				Assert.Throws<ArgumentNullException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, null));
				Assert.Throws<ArgumentException> (() => inbox.Sort (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));
				Assert.Throws<ArgumentException> (async () => await inbox.SortAsync (searchOptions, UniqueIdRange.All, SearchQuery.All, emptyOrderBy));

				Assert.Throws<ArgumentNullException> (() => inbox.Sort ((string) null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SortAsync ((string) null));

				// Thread
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Thread ((ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.ThreadAsync ((ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread (ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ThreadAsync (ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread ((IList<UniqueId>) null, ThreadingAlgorithm.References, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ThreadAsync ((IList<UniqueId>) null, ThreadingAlgorithm.References, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Thread (UniqueIdRange.All, (ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.ThreadAsync (UniqueIdRange.All, (ThreadingAlgorithm) 500, SearchQuery.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Thread (UniqueIdRange.All, ThreadingAlgorithm.References, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.References, null));

				client.Disconnect (false);
			}
		}
	}
}
