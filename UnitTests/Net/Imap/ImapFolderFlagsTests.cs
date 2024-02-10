//
// ImapFolderFlagsTests.cs
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

using MailKit;
using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderFlagsTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var keywords = new HashSet<string> (new string[] { "$Forwarded", "$Junk" });
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

				// AddFlags
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (-1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.AddFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueId.Invalid, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.AddFlagsAsync (UniqueId.Invalid, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (-1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.AddFlagsAsync (-1, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentException> (() => inbox.AddFlags (UniqueId.Invalid, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.AddFlagsAsync (UniqueId.Invalid, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<int>) null, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<int>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, keywords, true));

				// RemoveFlags
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (-1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.RemoveFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueId.Invalid, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.RemoveFlagsAsync (UniqueId.Invalid, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (-1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.RemoveFlagsAsync (-1, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentException> (() => inbox.RemoveFlags (UniqueId.Invalid, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.RemoveFlagsAsync (UniqueId.Invalid, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<int>) null, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<int>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, keywords, true));

				// SetFlags
				Assert.Throws<ArgumentException> (() => inbox.SetFlags (-1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.SetFlagsAsync (-1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.SetFlags (UniqueId.Invalid, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.SetFlagsAsync (UniqueId.Invalid, MessageFlags.Seen, true));
				Assert.Throws<ArgumentException> (() => inbox.SetFlags (-1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.SetFlagsAsync (-1, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentException> (() => inbox.SetFlags (UniqueId.Invalid, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.SetFlagsAsync (UniqueId.Invalid, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<int>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<int>) null, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<UniqueId>) null, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<int>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<int>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetFlags ((IList<UniqueId>) null, 1, MessageFlags.Seen, keywords, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetFlagsAsync ((IList<UniqueId>) null, 1, MessageFlags.Seen, keywords, true));

				// Store Flags
				var addSeen = new StoreFlagsRequest (StoreAction.Add, MessageFlags.Seen) { Silent = true };
				Assert.Throws<ArgumentException> (() => inbox.Store (UniqueId.Invalid, addSeen));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.StoreAsync (UniqueId.Invalid, addSeen));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueId.MinValue, (StoreFlagsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueId.MinValue, (StoreFlagsRequest) null));
				Assert.Throws<ArgumentException> (() => inbox.Store (-1, addSeen));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.StoreAsync (-1, addSeen));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (0, (StoreFlagsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (0, (StoreFlagsRequest) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<UniqueId>) null, addSeen));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<UniqueId>) null, addSeen));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueIdRange.All, (StoreFlagsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueIdRange.All, (StoreFlagsRequest) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<int>) null, addSeen));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<int>) null, addSeen));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (new int[] { 0 }, (StoreFlagsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (new int[] { 0 }, (StoreFlagsRequest) null));

				var labels = new string [] { "Label1", "Label2" };
				var emptyLabels = Array.Empty<string> ();

				// AddLabels
				Assert.Throws<ArgumentException> (() => inbox.AddLabels (-1, labels, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.AddLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (0, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueId.MinValue, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<int>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<UniqueId>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (new int [] { 0 }, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync (new int [] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueIdRange.All, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync (UniqueIdRange.All, null, true));

				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<int>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (new int [] { 0 }, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync (new int [] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.AddLabels (UniqueIdRange.All, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.AddLabelsAsync (UniqueIdRange.All, 1, null, true));

				// RemoveLabels
				Assert.Throws<ArgumentException> (() => inbox.RemoveLabels (-1, labels, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.RemoveLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (0, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueId.MinValue, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<int>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<UniqueId>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (new int [] { 0 }, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync (new int [] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueIdRange.All, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync (UniqueIdRange.All, null, true));

				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<int>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (new int [] { 0 }, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync (new int [] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.RemoveLabels (UniqueIdRange.All, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.RemoveLabelsAsync (UniqueIdRange.All, 1, null, true));

				// SetLabels
				Assert.Throws<ArgumentException> (() => inbox.SetLabels (-1, labels, true));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.SetLabelsAsync (-1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (0, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync (0, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueId.MinValue, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync (UniqueId.MinValue, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<int>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync ((IList<int>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<UniqueId>) null, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync ((IList<UniqueId>) null, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (new int [] { 0 }, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync (new int [] { 0 }, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueIdRange.All, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync (UniqueIdRange.All, null, true));

				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<int>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync ((IList<int>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels ((IList<UniqueId>) null, 1, labels, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync ((IList<UniqueId>) null, 1, labels, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (new int [] { 0 }, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync (new int [] { 0 }, 1, null, true));
				Assert.Throws<ArgumentNullException> (() => inbox.SetLabels (UniqueIdRange.All, 1, null, true));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.SetLabelsAsync (UniqueIdRange.All, 1, null, true));

				// Store Labels
				var addLabel = new StoreLabelsRequest (StoreAction.Add, new string[] { "Label1" }) { Silent = true };
				Assert.Throws<ArgumentException> (() => inbox.Store (UniqueId.Invalid, addLabel));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.StoreAsync (UniqueId.Invalid, addLabel));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueId.MinValue, (StoreLabelsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueId.MinValue, (StoreLabelsRequest) null));
				Assert.Throws<ArgumentException> (() => inbox.Store (-1, addLabel));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.StoreAsync (-1, addLabel));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (0, (StoreLabelsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (0, (StoreLabelsRequest) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<UniqueId>) null, addLabel));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<UniqueId>) null, addLabel));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueIdRange.All, (StoreLabelsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueIdRange.All, (StoreLabelsRequest) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<int>) null, addLabel));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<int>) null, addLabel));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (new int[] { 0 }, (StoreLabelsRequest) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (new int[] { 0 }, (StoreLabelsRequest) null));

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

				var indexes = new int[] { 0 };
				ulong modseq = 409601020304;

				// AddFlags
				Assert.Throws<NotSupportedException> (() => inbox.AddFlags (indexes, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.AddFlagsAsync (indexes, modseq, MessageFlags.Seen, true));
				Assert.Throws<NotSupportedException> (() => inbox.AddFlags (UniqueIdRange.All, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.AddFlagsAsync (UniqueIdRange.All, modseq, MessageFlags.Seen, true));

				// RemoveFlags
				Assert.Throws<NotSupportedException> (() => inbox.RemoveFlags (indexes, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.RemoveFlagsAsync (indexes, modseq, MessageFlags.Seen, true));
				Assert.Throws<NotSupportedException> (() => inbox.RemoveFlags (UniqueIdRange.All, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.RemoveFlagsAsync (UniqueIdRange.All, modseq, MessageFlags.Seen, true));

				// SetFlags
				Assert.Throws<NotSupportedException> (() => inbox.SetFlags (indexes, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SetFlagsAsync (indexes, modseq, MessageFlags.Seen, true));
				Assert.Throws<NotSupportedException> (() => inbox.SetFlags (UniqueIdRange.All, modseq, MessageFlags.Seen, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SetFlagsAsync (UniqueIdRange.All, modseq, MessageFlags.Seen, true));

				var labels = new string[] { "Label1", "Label2" };

				// AddLabels
				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (indexes, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.AddLabelsAsync (indexes, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.AddLabelsAsync (UniqueIdRange.All, labels, true));

				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (indexes, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.AddLabelsAsync (indexes, modseq, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.AddLabels (UniqueIdRange.All, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.AddLabelsAsync (UniqueIdRange.All, modseq, labels, true));

				// RemoveLabels
				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (indexes, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.RemoveLabelsAsync (indexes, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.RemoveLabelsAsync (UniqueIdRange.All, labels, true));

				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (indexes, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.RemoveLabelsAsync (indexes, modseq, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.RemoveLabels (UniqueIdRange.All, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.RemoveLabelsAsync (UniqueIdRange.All, modseq, labels, true));

				// SetLabels
				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (indexes, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SetLabelsAsync (indexes, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SetLabelsAsync (UniqueIdRange.All, labels, true));

				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (indexes, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SetLabelsAsync (indexes, modseq, labels, true));
				Assert.Throws<NotSupportedException> (() => inbox.SetLabels (UniqueIdRange.All, modseq, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SetLabelsAsync (UniqueIdRange.All, modseq, labels, true));

				client.Disconnect (false);
			}
		}

		static IList<ImapReplayCommand> CreateChangingFlagsOnEmptyListOfMessagesCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt")
			};
		}

		[Test]
		public void TestChangingFlagsOnEmptyListOfMessages ()
		{
			var commands = CreateChangingFlagsOnEmptyListOfMessagesCommands ();

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

				ulong modseq = 409601020304;
				var uids = Array.Empty<UniqueId> ();
				var indexes = Array.Empty<int> ();
				IList<UniqueId> unmodifiedUids;
				IList<int> unmodifiedIndexes;

				// AddFlags
				unmodifiedIndexes = inbox.AddFlags (indexes, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.AddFlags (uids, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// RemoveFlags
				unmodifiedIndexes = inbox.RemoveFlags (indexes, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.RemoveFlags (uids, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// SetFlags
				unmodifiedIndexes = inbox.SetFlags (indexes, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.SetFlags (uids, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedUids, Is.Empty);

				var labels = new string[] { "Label1", "Label2" };

				// AddLabels
				unmodifiedIndexes = inbox.AddLabels (indexes, modseq, labels, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.AddLabels (uids, modseq, labels, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// RemoveLabels
				unmodifiedIndexes = inbox.RemoveLabels (indexes, modseq, labels, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.RemoveLabels (uids, modseq, labels, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// SetLabels
				unmodifiedIndexes = inbox.SetLabels (indexes, modseq, labels, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.SetLabels (uids, modseq, labels, true);
				Assert.That (unmodifiedUids, Is.Empty);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestChangingFlagsOnEmptyListOfMessagesAsync ()
		{
			var commands = CreateChangingFlagsOnEmptyListOfMessagesCommands ();

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

				ulong modseq = 409601020304;
				var uids = Array.Empty<UniqueId> ();
				var indexes = Array.Empty<int> ();
				IList<UniqueId> unmodifiedUids;
				IList<int> unmodifiedIndexes;

				// AddFlags
				unmodifiedIndexes = await inbox.AddFlagsAsync (indexes, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = await inbox.AddFlagsAsync (uids, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// RemoveFlags
				unmodifiedIndexes = await inbox.RemoveFlagsAsync (indexes, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = await inbox.RemoveFlagsAsync (uids, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// SetFlags
				unmodifiedIndexes = await inbox.SetFlagsAsync (indexes, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = await inbox.SetFlagsAsync (uids, modseq, MessageFlags.Seen, true);
				Assert.That (unmodifiedUids, Is.Empty);

				var labels = new string[] { "Label1", "Label2" };

				// AddLabels
				unmodifiedIndexes = await inbox.AddLabelsAsync (indexes, modseq, labels, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = await inbox.AddLabelsAsync (uids, modseq, labels, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// RemoveLabels
				unmodifiedIndexes = await inbox.RemoveLabelsAsync (indexes, modseq, labels, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = await inbox.RemoveLabelsAsync (uids, modseq, labels, true);
				Assert.That (unmodifiedUids, Is.Empty);

				// SetLabels
				unmodifiedIndexes = await inbox.SetLabelsAsync (indexes, modseq, labels, true);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = await inbox.SetLabelsAsync (uids, modseq, labels, true);
				Assert.That (unmodifiedUids, Is.Empty);

				await client.DisconnectAsync (false);
			}
		}
	}
}
