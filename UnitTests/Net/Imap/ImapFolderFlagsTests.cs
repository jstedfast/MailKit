//
// ImapFolderFlagsTests.cs
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
using System.Collections.Generic;

using NUnit.Framework;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderFlagsTests
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

				// AddFlags
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (-1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (0, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (0, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueId.MinValue, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (UniqueId.MinValue, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (new int [] { 0 }, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (new int [] { 0 }, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueIdRange.All, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (UniqueIdRange.All, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (new int [] { 0 }, 1, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (new int [] { 0 }, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueIdRange.All, 1, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddFlagsAsync (UniqueIdRange.All, 1, MessageFlags.None, true));

				// RemoveFlags
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (-1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (0, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (0, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueId.MinValue, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (UniqueId.MinValue, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (new int [] { 0 }, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (new int [] { 0 }, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueIdRange.All, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (UniqueIdRange.All, MessageFlags.None, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (new int [] { 0 }, 1, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (new int [] { 0 }, 1, MessageFlags.None, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueIdRange.All, 1, MessageFlags.None, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveFlagsAsync (UniqueIdRange.All, 1, MessageFlags.None, true));

				// SetFlags
				Assert.Throws<ArgumentException> (() => inbox.SetFlags (-1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SetFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));

				var labels = new string [] { "Label1", "Label2" };
				var emptyLabels = new string[0];

				// AddLabels
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (-1, labels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (0, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueId.MinValue, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<int>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<UniqueId>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (new int [] { 0 }, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync (new int [] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueIdRange.All, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (new int [] { 0 }, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddLabelsAsync (new int [] { 0 }, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (UniqueIdRange.All, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, emptyLabels, true));

				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<int>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (new int [] { 0 }, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync (new int [] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueIdRange.All, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (new int [] { 0 }, 1, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddLabelsAsync (new int [] { 0 }, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (UniqueIdRange.All, 1, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, 1, emptyLabels, true));

				// RemoveLabels
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (-1, labels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (0, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueId.MinValue, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<int>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<UniqueId>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (new int [] { 0 }, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (new int [] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueIdRange.All, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, null, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (new int [] { 0 }, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveLabelsAsync (new int [] { 0 }, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (UniqueIdRange.All, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, emptyLabels, true));

				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<int>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (new int [] { 0 }, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (new int [] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueIdRange.All, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, 1, null, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (new int [] { 0 }, 1, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveLabelsAsync (new int [] { 0 }, 1, emptyLabels, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (UniqueIdRange.All, 1, emptyLabels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, 1, emptyLabels, true));

				// SetLabels
				Assert.Throws<ArgumentException> (() => inbox.SetLabels (-1, labels, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.SetLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (0, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueId.MinValue, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<int>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<UniqueId>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (new int [] { 0 }, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync (new int [] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueIdRange.All, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync (UniqueIdRange.All, null, true));

				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<int>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (new int [] { 0 }, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync (new int [] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueIdRange.All, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetLabelsAsync (UniqueIdRange.All, 1, null, true));

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

				var indexes = new int[] { 0 };
				ulong modseq = 409601020304;

				// AddFlags
				Assert.Throws<NotSupportedException> (() => inbox.AddFlags (indexes, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AddFlagsAsync (indexes, modseq, MessageFlags.Seen, true));
				Assert.Throws<NotSupportedException> (() => inbox.AddFlags (UniqueIdRange.All, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AddFlagsAsync (UniqueIdRange.All, modseq, MessageFlags.Seen, true));

				// RemoveFlags
				Assert.Throws<NotSupportedException> (() => inbox.RemoveFlags (indexes, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveFlagsAsync (indexes, modseq, MessageFlags.Seen, true));
				Assert.Throws<NotSupportedException> (() => inbox.RemoveFlags (UniqueIdRange.All, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveFlagsAsync (UniqueIdRange.All, modseq, MessageFlags.Seen, true));

				// SetFlags
				Assert.Throws<NotSupportedException> (() => inbox.SetFlags (indexes, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetFlagsAsync (indexes, modseq, MessageFlags.Seen, true));
				Assert.Throws<NotSupportedException> (() => inbox.SetFlags (UniqueIdRange.All, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetFlagsAsync (UniqueIdRange.All, modseq, MessageFlags.Seen, true));

				var labels = new string[] { "Label1", "Label2" };

				// AddLabels
				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (indexes, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AddLabelsAsync (indexes, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, labels, true));

				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (indexes, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AddLabelsAsync (indexes, modseq, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (UniqueIdRange.All, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AddLabelsAsync (UniqueIdRange.All, modseq, labels, true));

				// RemoveLabels
				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (indexes, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveLabelsAsync (indexes, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, labels, true));

				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (indexes, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveLabelsAsync (indexes, modseq, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (UniqueIdRange.All, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveLabelsAsync (UniqueIdRange.All, modseq, labels, true));

				// SetLabels
				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (indexes, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetLabelsAsync (indexes, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetLabelsAsync (UniqueIdRange.All, labels, true));

				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (indexes, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetLabelsAsync (indexes, modseq, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (UniqueIdRange.All, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetLabelsAsync (UniqueIdRange.All, modseq, labels, true));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestChangingFlagsOnEmptyListOfMessages ()
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

				ulong modseq = 409601020304;
				var uids = new UniqueId[0];
				var indexes = new int[0];
				IList<UniqueId> unmodifiedUids;
				IList<int> unmodifiedIndexes;

				// AddFlags
				unmodifiedIndexes = inbox.AddFlags (indexes, modseq, MessageFlags.Seen, true);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.AddFlags (uids, modseq, MessageFlags.Seen, true);
				Assert.AreEqual (0, unmodifiedUids.Count);

				// RemoveFlags
				unmodifiedIndexes = inbox.RemoveFlags (indexes, modseq, MessageFlags.Seen, true);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.RemoveFlags (uids, modseq, MessageFlags.Seen, true);
				Assert.AreEqual (0, unmodifiedUids.Count);

				// SetFlags
				unmodifiedIndexes = inbox.SetFlags (indexes, modseq, MessageFlags.Seen, true);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.SetFlags (uids, modseq, MessageFlags.Seen, true);
				Assert.AreEqual (0, unmodifiedUids.Count);

				var labels = new string[] { "Label1", "Label2" };

				// AddLabels
				unmodifiedIndexes = inbox.AddLabels (indexes, modseq, labels, true);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.AddLabels (uids, modseq, labels, true);
				Assert.AreEqual (0, unmodifiedUids.Count);

				// RemoveLabels
				unmodifiedIndexes = inbox.RemoveLabels (indexes, modseq, labels, true);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.RemoveLabels (uids, modseq, labels, true);
				Assert.AreEqual (0, unmodifiedUids.Count);

				// SetLabels
				unmodifiedIndexes = inbox.SetLabels (indexes, modseq, labels, true);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.SetLabels (uids, modseq, labels, true);
				Assert.AreEqual (0, unmodifiedUids.Count);

				client.Disconnect (false);
			}
		}
	}
}
