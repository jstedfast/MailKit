//
// ImapFolderTests.cs
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
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;

using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);

		static MimeMessage CreateThreadableMessage (string subject, string msgid, string references, DateTimeOffset date)
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.To.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.MessageId = msgid;
			message.Subject = subject;
			message.Date = date;

			if (references != null) {
				foreach (var reference in references.Split (' '))
					message.References.Add (reference);
			}

			message.Body = new TextPart ("plain") { Text = "This is the message body.\r\n" };

			return message;
		}

		Stream GetResourceStream (string name)
		{
			return GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
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

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var dates = new List<DateTimeOffset> ();
				var messages = new List<MimeMessage> ();
				var flags = new List<MessageFlags> ();
				var now = DateTimeOffset.Now;
				var uid = new UniqueId (1);

				messages.Add (CreateThreadableMessage ("A", "<a@mimekit.net>", null, now.AddMinutes (-7)));
				messages.Add (CreateThreadableMessage ("B", "<b@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-6)));
				messages.Add (CreateThreadableMessage ("C", "<c@mimekit.net>", "<a@mimekit.net> <b@mimekit.net>", now.AddMinutes (-5)));
				messages.Add (CreateThreadableMessage ("D", "<d@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-4)));
				messages.Add (CreateThreadableMessage ("E", "<e@mimekit.net>", "<c@mimekit.net> <x@mimekit.net> <y@mimekit.net> <z@mimekit.net>", now.AddMinutes (-3)));
				messages.Add (CreateThreadableMessage ("F", "<f@mimekit.net>", "<b@mimekit.net>", now.AddMinutes (-2)));
				messages.Add (CreateThreadableMessage ("G", "<g@mimekit.net>", null, now.AddMinutes (-1)));
				messages.Add (CreateThreadableMessage ("H", "<h@mimekit.net>", null, now));

				for (int i = 0; i < messages.Count; i++) {
					dates.Add (DateTimeOffset.Now);
					flags.Add (MessageFlags.Seen);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				inbox.Open (FolderAccess.ReadWrite);

				// ImapFolder .ctor
				Assert.Throws<ArgumentNullException> (() => new ImapFolder (null));

				// Open
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Open ((FolderAccess) 500));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Open ((FolderAccess) 500, 0, 0, UniqueIdRange.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Open (FolderAccess.ReadOnly, 0, 0, null));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.OpenAsync ((FolderAccess) 500));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.OpenAsync ((FolderAccess) 500, 0, 0, UniqueIdRange.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.OpenAsync (FolderAccess.ReadOnly, 0, 0, null));

				// Create
				Assert.Throws<ArgumentNullException> (() => inbox.Create (null, true));
				Assert.Throws<ArgumentException> (() => inbox.Create (string.Empty, true));
				Assert.Throws<ArgumentException> (() => inbox.Create ("Folder./Name", true));
				Assert.Throws<ArgumentNullException> (() => inbox.Create (null, SpecialFolder.All));
				Assert.Throws<ArgumentException> (() => inbox.Create (string.Empty, SpecialFolder.All));
				Assert.Throws<ArgumentException> (() => inbox.Create ("Folder./Name", SpecialFolder.All));
				Assert.Throws<ArgumentNullException> (() => inbox.Create (null, new SpecialFolder [] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (() => inbox.Create (string.Empty, new SpecialFolder [] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (() => inbox.Create ("Folder./Name", new SpecialFolder [] { SpecialFolder.All }));
				Assert.Throws<ArgumentNullException> (() => inbox.Create ("ValidName", null));
				Assert.Throws<NotSupportedException> (() => inbox.Create ("ValidName", SpecialFolder.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CreateAsync (null, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, true));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", true));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CreateAsync (null, SpecialFolder.All));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, SpecialFolder.All));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", SpecialFolder.All));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CreateAsync (null, new SpecialFolder [] { SpecialFolder.All }));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, new SpecialFolder [] { SpecialFolder.All }));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", new SpecialFolder [] { SpecialFolder.All }));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CreateAsync ("ValidName", null));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.CreateAsync ("ValidName", SpecialFolder.All));

				// Rename
				Assert.Throws<ArgumentNullException> (() => inbox.Rename (null, "NewName"));
				Assert.Throws<ArgumentNullException> (() => inbox.Rename (personal, null));
				Assert.Throws<ArgumentException> (() => inbox.Rename (personal, string.Empty));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RenameAsync (null, "NewName"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.RenameAsync (personal, null));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.RenameAsync (personal, string.Empty));

				// GetSubfolder
				Assert.Throws<ArgumentNullException> (() => inbox.GetSubfolder (null));
				Assert.Throws<ArgumentException> (() => inbox.GetSubfolder (string.Empty));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetSubfolderAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.GetSubfolderAsync (string.Empty));

				// GetMetadata
				Assert.Throws<ArgumentNullException> (() => client.GetMetadata (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (() => client.GetMetadata (new MetadataOptions (), null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetMetadataAsync (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetMetadataAsync (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (() => inbox.GetMetadata (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (() => inbox.GetMetadata (new MetadataOptions (), null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetMetadataAsync (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.GetMetadataAsync (new MetadataOptions (), null));

				// SetMetadata
				Assert.Throws<ArgumentNullException> (() => client.SetMetadata (null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SetMetadataAsync (null));
				Assert.Throws<ArgumentNullException> (() => inbox.SetMetadata (null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.SetMetadataAsync (null));

				// Expunge
				Assert.Throws<ArgumentNullException> (() => inbox.Expunge (null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ExpungeAsync (null));

				// Append
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages[0]));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages[0]));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, MessageFlags.None, DateTimeOffset.Now));

				// MultiAppend
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, flags));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, flags));
				Assert.Throws<ArgumentException> (() => inbox.Append (new MimeMessage[] { null }, flags));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AppendAsync (new MimeMessage[] { null }, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (messages, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages, flags));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, flags));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, flags));
				Assert.Throws<ArgumentException> (() => inbox.Append (FormatOptions.Default, new MimeMessage[] { null }, flags));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AppendAsync (FormatOptions.Default, new MimeMessage[] { null }, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, flags, dates));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, flags, dates));
				Assert.Throws<ArgumentException> (() => inbox.Append (new MimeMessage[] { null }, flags, dates));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AppendAsync (new MimeMessage[] { null }, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, null, dates));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (messages, null, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, flags, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (messages, flags, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages, flags, dates));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, flags, dates));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, flags, dates));
				Assert.Throws<ArgumentException> (() => inbox.Append (FormatOptions.Default, new MimeMessage[] { null }, flags, dates));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.AppendAsync (FormatOptions.Default, new MimeMessage[] { null }, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, null, dates));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, null, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, flags, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, flags, null));

				// Replace
				Assert.Throws<ArgumentException> (() => inbox.Replace (UniqueId.Invalid, messages[0]));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.ReplaceAsync (UniqueId.Invalid, messages[0]));
				Assert.Throws<ArgumentException> (() => inbox.Replace (UniqueId.Invalid, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.ReplaceAsync (UniqueId.Invalid, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (uid, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (uid, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (uid, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (uid, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (null, uid, messages[0]));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (null, uid, messages[0]));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (null, uid, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (null, uid, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Replace (-1, messages[0]));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.ReplaceAsync (-1, messages[0]));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Replace (-1, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await inbox.ReplaceAsync (-1, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (0, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (0, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (0, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (0, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (null, 0, messages[0]));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (null, 0, messages[0]));
				Assert.Throws<ArgumentNullException> (() => inbox.Replace (null, 0, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.ReplaceAsync (null, 0, messages[0], MessageFlags.None, DateTimeOffset.Now));

				// CopyTo
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo ((IList<UniqueId>) null, inbox));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CopyToAsync ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo (UniqueIdRange.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CopyToAsync (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo ((IList<int>) null, inbox));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CopyToAsync ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo (new int [] { 0 }, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.CopyToAsync (new int [] { 0 }, null));

				// MoveTo
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo ((IList<UniqueId>) null, inbox));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.MoveToAsync ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo (UniqueIdRange.All, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.MoveToAsync (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo ((IList<int>) null, inbox));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.MoveToAsync ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo (new int [] { 0 }, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.MoveToAsync (new int [] { 0 }, null));

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
			//commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX\r\n", "common.select-inbox.txt"));

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

				// disable all features
				client.Capabilities = ImapCapabilities.None;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var dates = new List<DateTimeOffset> ();
				var messages = new List<MimeMessage> ();
				var flags = new List<MessageFlags> ();
				var now = DateTimeOffset.Now;

				messages.Add (CreateThreadableMessage ("A", "<a@mimekit.net>", null, now.AddMinutes (-7)));
				messages.Add (CreateThreadableMessage ("B", "<b@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-6)));
				messages.Add (CreateThreadableMessage ("C", "<c@mimekit.net>", "<a@mimekit.net> <b@mimekit.net>", now.AddMinutes (-5)));
				messages.Add (CreateThreadableMessage ("D", "<d@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-4)));
				messages.Add (CreateThreadableMessage ("E", "<e@mimekit.net>", "<c@mimekit.net> <x@mimekit.net> <y@mimekit.net> <z@mimekit.net>", now.AddMinutes (-3)));
				messages.Add (CreateThreadableMessage ("F", "<f@mimekit.net>", "<b@mimekit.net>", now.AddMinutes (-2)));
				messages.Add (CreateThreadableMessage ("G", "<g@mimekit.net>", null, now.AddMinutes (-1)));
				messages.Add (CreateThreadableMessage ("H", "<h@mimekit.net>", null, now));

				for (int i = 0; i < messages.Count; i++) {
					dates.Add (DateTimeOffset.Now);
					flags.Add (MessageFlags.Seen);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;

				// Open
				Assert.Throws<NotSupportedException> (() => inbox.Open (FolderAccess.ReadOnly, 0, 0, UniqueIdRange.All));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.OpenAsync (FolderAccess.ReadOnly, 0, 0, UniqueIdRange.All));

				// Create
				Assert.Throws<NotSupportedException> (() => inbox.Create ("Folder", SpecialFolder.All));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.CreateAsync ("Folder", SpecialFolder.All));

				// Rename - TODO

				// Append
				var international = FormatOptions.Default.Clone ();
				international.International = true;
				Assert.Throws<NotSupportedException> (() => inbox.Append (international, messages[0]));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AppendAsync (international, messages[0]));
				Assert.Throws<NotSupportedException> (() => inbox.Append (international, messages[0], flags[0]));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AppendAsync (international, messages[0], flags[0]));
				Assert.Throws<NotSupportedException> (() => inbox.Append (international, messages[0], flags[0], dates[0]));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AppendAsync (international, messages[0], flags[0], dates[0]));

				// MultiAppend
				//Assert.Throws<NotSupportedException> (() => inbox.Append (international, messages));
				//Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AppendAsync (international, messages));
				Assert.Throws<NotSupportedException> (() => inbox.Append (international, messages, flags));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AppendAsync (international, messages, flags));
				Assert.Throws<NotSupportedException> (() => inbox.Append (international, messages, flags, dates));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.AppendAsync (international, messages, flags, dates));

				// Status
				Assert.Throws<NotSupportedException> (() => inbox.Status (StatusItems.Count));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StatusAsync (StatusItems.Count));

				// GetAccessControlList
				Assert.Throws<NotSupportedException> (() => inbox.GetAccessControlList ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.GetAccessControlListAsync ());

				// GetAccessRights
				Assert.Throws<NotSupportedException> (() => inbox.GetAccessRights ("name"));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.GetAccessRightsAsync ("name"));

				// GetMyAccessRights
				Assert.Throws<NotSupportedException> (() => inbox.GetMyAccessRights ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.GetMyAccessRightsAsync ());

				// RemoveAccess
				Assert.Throws<NotSupportedException> (() => inbox.RemoveAccess ("name"));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.RemoveAccessAsync ("name"));

				// GetMetadata
				Assert.Throws<NotSupportedException> (() => client.GetMetadata (MetadataTag.PrivateComment));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.GetMetadataAsync (MetadataTag.PrivateComment));
				Assert.Throws<NotSupportedException> (() => inbox.GetMetadata (MetadataTag.PrivateComment));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.GetMetadataAsync (MetadataTag.PrivateComment));
				Assert.Throws<NotSupportedException> (() => client.GetMetadata (new MetadataOptions (), new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.GetMetadataAsync (new MetadataOptions (), new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<NotSupportedException> (() => inbox.GetMetadata (new MetadataOptions (), new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.GetMetadataAsync (new MetadataOptions (), new MetadataTag[] { MetadataTag.PrivateComment }));

				// SetMetadata
				Assert.Throws<NotSupportedException> (() => client.SetMetadata (new MetadataCollection ()));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.SetMetadataAsync (new MetadataCollection ()));
				Assert.Throws<NotSupportedException> (() => inbox.SetMetadata (new MetadataCollection ()));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetMetadataAsync (new MetadataCollection ()));

				// GetQuota
				Assert.Throws<NotSupportedException> (() => inbox.GetQuota ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.GetQuotaAsync ());

				// SetQuota
				Assert.Throws<NotSupportedException> (() => inbox.SetQuota (5, 10));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SetQuotaAsync (5, 10));

				client.Disconnect (false);
			}
		}

		List<ImapReplayCommand> CreateAppendCommands (bool withInternalDates, out List<MimeMessage> messages, out List<MessageFlags> flags, out List<DateTimeOffset> internalDates)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			internalDates = withInternalDates ? new List<DateTimeOffset> () : null;
			messages = new List<MimeMessage> ();
			flags = new List<MessageFlags> ();
			var command = new StringBuilder ();
			int id = 5;

			for (int i = 0; i < 8; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				messages.Add (message);
				flags.Add (MessageFlags.Seen);
				if (withInternalDates)
					internalDates.Add (message.Date);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;
					options.EnsureNewLine = true;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				var tag = string.Format ("A{0:D8}", id++);
				command.Clear ();

				command.AppendFormat ("{0} APPEND INBOX (\\Seen) ", tag);

				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (message.Date));

				if (length > 4096) {
					command.Append ('{').Append (length.ToString ()).Append ("}\r\n");
					commands.Add (new ImapReplayCommand (command.ToString (), ImapReplayCommandResponse.Plus));
					commands.Add (new ImapReplayCommand (tag, latin1 + "\r\n", string.Format ("dovecot.append.{0}.txt", i + 1)));
				} else {
					command.Append ('{').Append (length.ToString ()).Append ("+}\r\n").Append (latin1).Append ("\r\n");
					commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
				}
			}

			commands.Add (new ImapReplayCommand (string.Format ("A{0:D8} LOGOUT\r\n", id), "gmail.logout.txt"));

			return commands;
		}

		[TestCase (true, TestName = "TestAppendWithInternalDates")]
		[TestCase (false, TestName = "TestAppendWithoutInternalDates")]
		public void TestAppend (bool withInternalDates)
		{
			var commands = CreateAppendCommands (withInternalDates, out var messages, out var flags, out var internalDates);

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

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = client.Inbox.Append (messages[i], flags[i], internalDates[i]);
					else
						uid = client.Inbox.Append (messages[i], flags[i]);

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				client.Disconnect (true);
			}
		}

		[TestCase (true, TestName = "TestAppendWithInternalDatesAsync")]
		[TestCase (false, TestName = "TestAppendWithoutInternalDatesAsync")]
		public async Task TestAppendAsync (bool withInternalDates)
		{
			var commands = CreateAppendCommands (withInternalDates, out var messages, out var flags, out var internalDates);

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

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = await client.Inbox.AppendAsync (messages[i], flags[i], internalDates[i]);
					else
						uid = await client.Inbox.AppendAsync (messages[i], flags[i]);

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateMultiAppendCommands (bool withInternalDates, out List<MimeMessage> messages, out List<MessageFlags> flags, out List<DateTimeOffset> internalDates)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));

			var command = new StringBuilder ("A00000004 APPEND INBOX");
			var now = DateTimeOffset.Now;

			internalDates = withInternalDates ? new List<DateTimeOffset> () : null;
			messages = new List<MimeMessage> ();
			flags = new List<MessageFlags> ();

			messages.Add (CreateThreadableMessage ("A", "<a@mimekit.net>", null, now.AddMinutes (-7)));
			messages.Add (CreateThreadableMessage ("B", "<b@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-6)));
			messages.Add (CreateThreadableMessage ("C", "<c@mimekit.net>", "<a@mimekit.net> <b@mimekit.net>", now.AddMinutes (-5)));
			messages.Add (CreateThreadableMessage ("D", "<d@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-4)));
			messages.Add (CreateThreadableMessage ("E", "<e@mimekit.net>", "<c@mimekit.net> <x@mimekit.net> <y@mimekit.net> <z@mimekit.net>", now.AddMinutes (-3)));
			messages.Add (CreateThreadableMessage ("F", "<f@mimekit.net>", "<b@mimekit.net>", now.AddMinutes (-2)));
			messages.Add (CreateThreadableMessage ("G", "<g@mimekit.net>", null, now.AddMinutes (-1)));
			messages.Add (CreateThreadableMessage ("H", "<h@mimekit.net>", null, now));

			for (int i = 0; i < messages.Count; i++) {
				var message = messages[i];
				string latin1;
				long length;

				if (withInternalDates)
					internalDates.Add (messages[i].Date);
				flags.Add (MessageFlags.Seen);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				command.Append (" (\\Seen) ");
				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (message.Date));
				command.Append ('{');
				command.AppendFormat ("{0}+", length);
				command.Append ("}\r\n");
				command.Append (latin1);
			}
			command.Append ("\r\n");
			commands.Add (new ImapReplayCommand (command.ToString (), "dovecot.multiappend.txt"));

			for (int i = 0; i < messages.Count; i++) {
				var message = messages[i];
				string latin1;
				long length;

				command.Clear ();
				command.AppendFormat ("A{0:D8} APPEND INBOX", i + 5);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				command.Append (" (\\Seen) ");
				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (message.Date));
				command.Append ('{');
				command.AppendFormat ("{0}+", length);
				command.Append ("}\r\n");
				command.Append (latin1);
				command.Append ("\r\n");
				commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
			}

			commands.Add (new ImapReplayCommand ("A00000013 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[TestCase (true, TestName = "TestMultiAppendWithInternalDates")]
		[TestCase (false, TestName = "TestMultiAppendWithoutInternalDates")]
		public void TestMultiAppend (bool withInternalDates)
		{
			var commands = CreateMultiAppendCommands (withInternalDates, out var messages, out var flags, out var internalDates);
			IList<UniqueId> uids;

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

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.MultiAppend), "MULTIAPPEND");

				// Use MULTIAPPEND to append some test messages
				if (withInternalDates)
					uids = client.Inbox.Append (messages, flags, internalDates);
				else
					uids = client.Inbox.Append (messages, flags);
				Assert.AreEqual (8, uids.Count, "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected UID");

				// Disable the MULTIAPPEND extension and do it again
				client.Capabilities &= ~ImapCapabilities.MultiAppend;
				if (withInternalDates)
					uids = client.Inbox.Append (messages, flags, internalDates);
				else
					uids = client.Inbox.Append (messages, flags);

				Assert.AreEqual (8, uids.Count, "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected UID");

				client.Disconnect (true);
			}
		}

		[TestCase (true, TestName = "TestMultiAppendWithInternalDatesAsync")]
		[TestCase (false, TestName = "TestMultiAppendWithoutInternalDatesAsync")]
		public async Task TestMultiAppendAsync (bool withInternalDates)
		{
			var commands = CreateMultiAppendCommands (withInternalDates, out var messages, out var flags, out var internalDates);
			IList<UniqueId> uids;

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

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.MultiAppend), "MULTIAPPEND");

				// Use MULTIAPPEND to append some test messages
				if (withInternalDates)
					uids = await client.Inbox.AppendAsync (messages, flags, internalDates);
				else
					uids = await client.Inbox.AppendAsync (messages, flags);
				Assert.AreEqual (8, uids.Count, "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected UID");

				// Disable the MULTIAPPEND extension and do it again
				client.Capabilities &= ~ImapCapabilities.MultiAppend;
				if (withInternalDates)
					uids = await client.Inbox.AppendAsync (messages, flags, internalDates);
				else
					uids = await client.Inbox.AppendAsync (messages, flags);

				Assert.AreEqual (8, uids.Count, "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected UID");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateReplaceCommands (bool clientSide, bool withInternalDates, out List<MimeMessage> messages, out List<MessageFlags> flags, out List<DateTimeOffset> internalDates)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+replace.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));

			internalDates = withInternalDates ? new List<DateTimeOffset> () : null;
			messages = new List<MimeMessage> ();
			flags = new List<MessageFlags> ();
			var command = new StringBuilder ();
			int id = 5;

			for (int i = 0; i < 8; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				messages.Add (message);
				flags.Add (MessageFlags.Seen);
				if (withInternalDates)
					internalDates.Add (message.Date);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;
					options.EnsureNewLine = true;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				var tag = string.Format ("A{0:D8}", id++);
				command.Clear ();

				if (clientSide)
					command.AppendFormat ("{0} APPEND INBOX (\\Seen) ", tag);
				else
					command.AppendFormat ("{0} REPLACE {1} INBOX (\\Seen) ", tag, i + 1);

				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (message.Date));

				//if (length > 4096) {
				//	command.Append ('{').Append (length.ToString ()).Append ("}\r\n");
				//	commands.Add (new ImapReplayCommand (command.ToString (), ImapReplayCommandResponse.Plus));
				//	commands.Add (new ImapReplayCommand (tag, latin1 + "\r\n", string.Format ("dovecot.append.{0}.txt", i + 1)));
				//} else {
					command.Append ('{').Append (length.ToString ()).Append ("+}\r\n").Append (latin1).Append ("\r\n");
					commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
				//}

				if (clientSide) {
					tag = string.Format ("A{0:D8}", id++);
					commands.Add (new ImapReplayCommand ($"{tag} STORE {i + 1} +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK));
				}
			}

			commands.Add (new ImapReplayCommand (string.Format ("A{0:D8} LOGOUT\r\n", id), "gmail.logout.txt"));

			return commands;
		}

		[TestCase (false, true, TestName = "TestReplaceWithInternalDates")]
		[TestCase (false, false, TestName = "TestReplaceWithoutInternalDates")]
		[TestCase (true, true, TestName = "TestClientSideReplaceWithInternalDates")]
		[TestCase (true, false, TestName = "TestClientSideReplaceWithoutInternalDates")]
		public void TestReplace (bool clientSide, bool withInternalDates)
		{
			var commands = CreateReplaceCommands (clientSide, withInternalDates, out var messages, out var flags, out var internalDates);

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

				if (clientSide)
					client.Capabilities &= ~ImapCapabilities.Replace;
				else
					Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.Replace), "REPLACE");

				client.Inbox.Open (FolderAccess.ReadWrite);

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = client.Inbox.Replace (i, messages[i], flags[i], internalDates[i]);
					else
						uid = client.Inbox.Replace (i, messages[i], flags[i]);

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				client.Disconnect (true);
			}
		}

		[TestCase (false, true, TestName = "TestReplaceWithInternalDatesAsync")]
		[TestCase (false, false, TestName = "TestReplaceWithoutInternalDatesAsync")]
		[TestCase (true, true, TestName = "TestClientSideReplaceWithInternalDatesAsync")]
		[TestCase (true, false, TestName = "TestClientSideReplaceWithoutInternalDatesAsync")]
		public async Task TestReplaceAsync (bool clientSide, bool withInternalDates)
		{
			var commands = CreateReplaceCommands (clientSide, withInternalDates, out var messages, out var flags, out var internalDates);

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

				if (clientSide)
					client.Capabilities &= ~ImapCapabilities.Replace;
				else
					Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.Replace), "REPLACE");

				await client.Inbox.OpenAsync (FolderAccess.ReadWrite);

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = await client.Inbox.ReplaceAsync (i, messages[i], flags[i], internalDates[i]);
					else
						uid = await client.Inbox.ReplaceAsync (i, messages[i], flags[i]);

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateReplaceByUidCommands (bool clientSide, bool withInternalDates, out List<MimeMessage> messages, out List<MessageFlags> flags, out List<DateTimeOffset> internalDates)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+replace.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE)\r\n", "common.select-inbox.txt"));

			internalDates = withInternalDates ? new List<DateTimeOffset> () : null;
			messages = new List<MimeMessage> ();
			flags = new List<MessageFlags> ();
			var command = new StringBuilder ();
			int id = 5;

			for (int i = 0; i < 8; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				messages.Add (message);
				flags.Add (MessageFlags.Seen);
				if (withInternalDates)
					internalDates.Add (message.Date);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;
					options.EnsureNewLine = true;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				var tag = string.Format ("A{0:D8}", id++);
				command.Clear ();

				if (clientSide)
					command.AppendFormat ("{0} APPEND INBOX (\\Seen) ", tag);
				else
					command.AppendFormat ("{0} UID REPLACE {1} INBOX (\\Seen) ", tag, i + 1);

				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (message.Date));

				//if (length > 4096) {
				//	command.Append ('{').Append (length.ToString ()).Append ("}\r\n");
				//	commands.Add (new ImapReplayCommand (command.ToString (), ImapReplayCommandResponse.Plus));
				//	commands.Add (new ImapReplayCommand (tag, latin1 + "\r\n", string.Format ("dovecot.append.{0}.txt", i + 1)));
				//} else {
				command.Append ('{').Append (length.ToString ()).Append ("+}\r\n").Append (latin1).Append ("\r\n");
				commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
				//}

				if (clientSide) {
					tag = string.Format ("A{0:D8}", id++);
					commands.Add (new ImapReplayCommand ($"{tag} UID STORE {i + 1} +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK));

					tag = string.Format ("A{0:D8}", id++);
					commands.Add (new ImapReplayCommand ($"{tag} UID EXPUNGE {i + 1}\r\n", ImapReplayCommandResponse.OK));
				}
			}

			commands.Add (new ImapReplayCommand (string.Format ("A{0:D8} LOGOUT\r\n", id), "gmail.logout.txt"));

			return commands;
		}

		[TestCase (false, true, TestName = "TestReplaceByUidWithInternalDates")]
		[TestCase (false, false, TestName = "TestReplaceByUidWithoutInternalDates")]
		[TestCase (true, true, TestName = "TestClientSideReplaceByUidWithInternalDates")]
		[TestCase (true, false, TestName = "TestClientSideReplaceByUidWithoutInternalDates")]
		public void TestReplaceByUid (bool clientSide, bool withInternalDates)
		{
			var commands = CreateReplaceByUidCommands (clientSide, withInternalDates, out var messages, out var flags, out var internalDates);

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

				if (clientSide)
					client.Capabilities &= ~ImapCapabilities.Replace;
				else
					Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.Replace), "REPLACE");

				client.Inbox.Open (FolderAccess.ReadWrite);

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = client.Inbox.Replace (new UniqueId ((uint) i + 1), messages[i], flags[i], internalDates[i]);
					else
						uid = client.Inbox.Replace (new UniqueId ((uint) i + 1), messages[i], flags[i]);

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				client.Disconnect (true);
			}
		}

		[TestCase (false, true, TestName = "TestReplaceByUidWithInternalDatesAsync")]
		[TestCase (false, false, TestName = "TestReplaceByUidWithoutInternalDatesAsync")]
		[TestCase (true, true, TestName = "TestClientSideReplaceByUidWithInternalDatesAsync")]
		[TestCase (true, false, TestName = "TestClientSideReplaceByUidWithoutInternalDatesAsync")]
		public async Task TestReplaceByUidAsync (bool clientSide, bool withInternalDates)
		{
			var commands = CreateReplaceByUidCommands (clientSide, withInternalDates, out var messages, out var flags, out var internalDates);

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

				if (clientSide)
					client.Capabilities &= ~ImapCapabilities.Replace;
				else
					Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.Replace), "REPLACE");

				await client.Inbox.OpenAsync (FolderAccess.ReadWrite);

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = await client.Inbox.ReplaceAsync (new UniqueId ((uint) i + 1), messages[i], flags[i], internalDates[i]);
					else
						uid = await client.Inbox.ReplaceAsync (new UniqueId ((uint) i + 1), messages[i], flags[i]);

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateCreateRenameDeleteCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 CREATE TopLevel1\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" TopLevel1\r\n", "gmail.list-toplevel1.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 CREATE TopLevel2\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000008 LIST \"\" TopLevel2\r\n", "gmail.list-toplevel2.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 CREATE TopLevel1/SubLevel1\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000010 LIST \"\" TopLevel1/SubLevel1\r\n", "gmail.list-sublevel1.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 CREATE TopLevel2/SubLevel2\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000012 LIST \"\" TopLevel2/SubLevel2\r\n", "gmail.list-sublevel2.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 SELECT TopLevel1/SubLevel1 (CONDSTORE)\r\n", "gmail.select-sublevel1.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 RENAME TopLevel1/SubLevel1 TopLevel2/SubLevel1\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000015 DELETE TopLevel1\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000016 SELECT TopLevel2/SubLevel2 (CONDSTORE)\r\n", "gmail.select-sublevel2.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 RENAME TopLevel2 TopLevel\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000018 SELECT TopLevel (CONDSTORE)\r\n", "gmail.select-toplevel.txt"));
			commands.Add (new ImapReplayCommand ("A00000019 DELETE TopLevel\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000020 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestCreateRenameDelete ()
		{
			var commands = CreateCreateRenameDeleteCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				int top1Renamed = 0, top2Renamed = 0, sub1Renamed = 0, sub2Renamed = 0;
				int top1Deleted = 0, top2Deleted = 0, sub1Deleted = 0, sub2Deleted = 0;
				int top1Closed = 0, top2Closed = 0, sub1Closed = 0, sub2Closed = 0;
				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var toplevel1 = personal.Create ("TopLevel1", false);
				var toplevel2 = personal.Create ("TopLevel2", false);
				var sublevel1 = toplevel1.Create ("SubLevel1", true);
				var sublevel2 = toplevel2.Create ("SubLevel2", true);

				toplevel1.Renamed += (o, e) => { top1Renamed++; };
				toplevel2.Renamed += (o, e) => { top2Renamed++; };
				sublevel1.Renamed += (o, e) => { sub1Renamed++; };
				sublevel2.Renamed += (o, e) => { sub2Renamed++; };

				toplevel1.Deleted += (o, e) => { top1Deleted++; };
				toplevel2.Deleted += (o, e) => { top2Deleted++; };
				sublevel1.Deleted += (o, e) => { sub1Deleted++; };
				sublevel2.Deleted += (o, e) => { sub2Deleted++; };

				toplevel1.Closed += (o, e) => { top1Closed++; };
				toplevel2.Closed += (o, e) => { top2Closed++; };
				sublevel1.Closed += (o, e) => { sub1Closed++; };
				sublevel2.Closed += (o, e) => { sub2Closed++; };

				sublevel1.Open (FolderAccess.ReadWrite);
				sublevel1.Rename (toplevel2, "SubLevel1");

				Assert.AreEqual (1, sub1Renamed, "SubLevel1 folder should have received a Renamed event");
				Assert.AreEqual (1, sub1Closed, "SubLevel1 should have received a Closed event");
				Assert.IsFalse (sublevel1.IsOpen, "SubLevel1 should be closed after being renamed");

				toplevel1.Delete ();
				Assert.AreEqual (1, top1Deleted, "TopLevel1 should have received a Deleted event");
				Assert.IsFalse (toplevel1.Exists, "TopLevel1.Exists");

				sublevel2.Open (FolderAccess.ReadWrite);
				toplevel2.Rename (personal, "TopLevel");

				Assert.AreEqual (2, sub1Renamed, "SubLevel1 folder should have received a Renamed event");
				Assert.AreEqual (1, sub2Renamed, "SubLevel2 folder should have received a Renamed event");
				Assert.AreEqual (1, sub2Closed, "SubLevel2 should have received a Closed event");
				Assert.IsFalse (sublevel2.IsOpen, "SubLevel2 should be closed after being renamed");
				Assert.AreEqual (1, top2Renamed, "TopLevel2 folder should have received a Renamed event");

				toplevel2.Open (FolderAccess.ReadWrite);
				toplevel2.Delete ();
				Assert.AreEqual (1, top2Closed, "TopLevel2 should have received a Closed event");
				Assert.IsFalse (toplevel2.IsOpen, "TopLevel2 should be closed after being deleted");
				Assert.AreEqual (1, top2Deleted, "TopLevel2 should have received a Deleted event");
				Assert.IsFalse (toplevel2.Exists, "TopLevel2.Exists");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCreateRenameDeleteAsync ()
		{
			var commands = CreateCreateRenameDeleteCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				int top1Renamed = 0, top2Renamed = 0, sub1Renamed = 0, sub2Renamed = 0;
				int top1Deleted = 0, top2Deleted = 0, sub1Deleted = 0, sub2Deleted = 0;
				int top1Closed = 0, top2Closed = 0, sub1Closed = 0, sub2Closed = 0;
				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var toplevel1 = await personal.CreateAsync ("TopLevel1", false);
				var toplevel2 = await personal.CreateAsync ("TopLevel2", false);
				var sublevel1 = await toplevel1.CreateAsync ("SubLevel1", true);
				var sublevel2 = await toplevel2.CreateAsync ("SubLevel2", true);

				toplevel1.Renamed += (o, e) => { top1Renamed++; };
				toplevel2.Renamed += (o, e) => { top2Renamed++; };
				sublevel1.Renamed += (o, e) => { sub1Renamed++; };
				sublevel2.Renamed += (o, e) => { sub2Renamed++; };

				toplevel1.Deleted += (o, e) => { top1Deleted++; };
				toplevel2.Deleted += (o, e) => { top2Deleted++; };
				sublevel1.Deleted += (o, e) => { sub1Deleted++; };
				sublevel2.Deleted += (o, e) => { sub2Deleted++; };

				toplevel1.Closed += (o, e) => { top1Closed++; };
				toplevel2.Closed += (o, e) => { top2Closed++; };
				sublevel1.Closed += (o, e) => { sub1Closed++; };
				sublevel2.Closed += (o, e) => { sub2Closed++; };

				await sublevel1.OpenAsync (FolderAccess.ReadWrite);
				await sublevel1.RenameAsync (toplevel2, "SubLevel1");

				Assert.AreEqual (1, sub1Renamed, "SubLevel1 folder should have received a Renamed event");
				Assert.AreEqual (1, sub1Closed, "SubLevel1 should have received a Closed event");
				Assert.IsFalse (sublevel1.IsOpen, "SubLevel1 should be closed after being renamed");

				await toplevel1.DeleteAsync ();
				Assert.AreEqual (1, top1Deleted, "TopLevel1 should have received a Deleted event");
				Assert.IsFalse (toplevel1.Exists, "TopLevel1.Exists");

				await sublevel2.OpenAsync (FolderAccess.ReadWrite);
				await toplevel2.RenameAsync (personal, "TopLevel");

				Assert.AreEqual (2, sub1Renamed, "SubLevel1 folder should have received a Renamed event");
				Assert.AreEqual (1, sub2Renamed, "SubLevel2 folder should have received a Renamed event");
				Assert.AreEqual (1, sub2Closed, "SubLevel2 should have received a Closed event");
				Assert.IsFalse (sublevel2.IsOpen, "SubLevel2 should be closed after being renamed");
				Assert.AreEqual (1, top2Renamed, "TopLevel2 folder should have received a Renamed event");

				await toplevel2.OpenAsync (FolderAccess.ReadWrite);
				await toplevel2.DeleteAsync ();
				Assert.AreEqual (1, top2Closed, "TopLevel2 should have received a Closed event");
				Assert.IsFalse (toplevel2.IsOpen, "TopLevel2 should be closed after being deleted");
				Assert.AreEqual (1, top2Deleted, "TopLevel2 should have received a Deleted event");
				Assert.IsFalse (toplevel2.Exists, "TopLevel2.Exists");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateCreateMailboxIdCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+create-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 CREATE TopLevel1\r\n", "gmail.create-mailboxid.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" TopLevel1\r\n", "gmail.list-toplevel1.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestCreateMailboxId ()
		{
			var commands = CreateCreateMailboxIdCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.ObjectID), "OBJECTID");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var toplevel1 = personal.Create ("TopLevel1", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, toplevel1.Attributes);
				Assert.AreEqual ("25dcfa84-fd65-41c3-abc3-633c8f10923f", toplevel1.Id);

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCreateMailboxIdAsync ()
		{
			var commands = CreateCreateMailboxIdCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.ObjectID), "OBJECTID");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var toplevel1 = await personal.CreateAsync ("TopLevel1", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, toplevel1.Attributes);
				Assert.AreEqual ("25dcfa84-fd65-41c3-abc3-633c8f10923f", toplevel1.Id);

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateCreateSpecialUseCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+create-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 CREATE \"[Gmail]/Archives\" (USE (\\Archive))\r\n", "gmail.create-mailboxid.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" \"[Gmail]/Archives\"\r\n", "gmail.list-archives.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestCreateSpecialUse ()
		{
			var commands = CreateCreateSpecialUseCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.CreateSpecialUse), "CREATE-SPECIAL-USE");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var gmail = personal.GetSubfolder ("[Gmail]");

				var archive = gmail.Create ("Archives", SpecialFolder.Archive);
				Assert.AreEqual (FolderAttributes.HasNoChildren | FolderAttributes.Archive, archive.Attributes);
				Assert.AreEqual (archive, client.GetFolder (SpecialFolder.Archive));
				Assert.AreEqual ("25dcfa84-fd65-41c3-abc3-633c8f10923f", archive.Id);

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCreateSpecialUseAsync ()
		{
			var commands = CreateCreateSpecialUseCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.CreateSpecialUse), "CREATE-SPECIAL-USE");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var gmail = await personal.GetSubfolderAsync ("[Gmail]");

				var archive = await gmail.CreateAsync ("Archives", SpecialFolder.Archive);
				Assert.AreEqual (FolderAttributes.HasNoChildren | FolderAttributes.Archive, archive.Attributes);
				Assert.AreEqual (archive, client.GetFolder (SpecialFolder.Archive));
				Assert.AreEqual ("25dcfa84-fd65-41c3-abc3-633c8f10923f", archive.Id);

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateCreateSpecialUseMultipleCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+create-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 CREATE \"[Gmail]/Archives\" (USE (\\All \\Archive \\Drafts \\Flagged \\Important \\Junk \\Sent \\Trash))\r\n", "gmail.create-mailboxid.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" \"[Gmail]/Archives\"\r\n", "gmail.list-archives.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 CREATE \"[Gmail]/MyImportant\" (USE (\\Important))\r\n", Encoding.ASCII.GetBytes ("A00000007 NO [USEATTR] An \\Important mailbox already exists\r\n")));
			commands.Add (new ImapReplayCommand ("A00000008 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestCreateSpecialUseMultiple ()
		{
			var commands = CreateCreateSpecialUseMultipleCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.CreateSpecialUse), "CREATE-SPECIAL-USE");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var gmail = personal.GetSubfolder ("[Gmail]");

				var uses = new List<SpecialFolder> ();
				uses.Add (SpecialFolder.All);
				uses.Add (SpecialFolder.Archive);
				uses.Add (SpecialFolder.Drafts);
				uses.Add (SpecialFolder.Flagged);
				uses.Add (SpecialFolder.Important);
				uses.Add (SpecialFolder.Junk);
				uses.Add (SpecialFolder.Sent);
				uses.Add (SpecialFolder.Trash);

				// specifically duplicate some special uses
				uses.Add (SpecialFolder.All);
				uses.Add (SpecialFolder.Flagged);

				// and add one that is invalid
				uses.Add ((SpecialFolder) 15);

				var archive = gmail.Create ("Archives", uses);
				Assert.AreEqual (FolderAttributes.HasNoChildren | FolderAttributes.Archive, archive.Attributes);
				Assert.AreEqual (archive, client.GetFolder (SpecialFolder.Archive));
				Assert.AreEqual ("25dcfa84-fd65-41c3-abc3-633c8f10923f", archive.Id);

				try {
					gmail.Create ("MyImportant", new[] { SpecialFolder.Important });
					Assert.Fail ("Creating the MyImportamnt folder should have thrown an ImapCommandException");
				} catch (ImapCommandException ex) {
					Assert.AreEqual (ImapCommandResponse.No, ex.Response);
					Assert.AreEqual ("An \\Important mailbox already exists", ex.ResponseText);
				} catch (Exception ex) {
					Assert.Fail ("Unexpected exception: {0}", ex);
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCreateSpecialUseMultipleAsync ()
		{
			var commands = CreateCreateSpecialUseMultipleCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.CreateSpecialUse), "CREATE-SPECIAL-USE");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var gmail = await personal.GetSubfolderAsync ("[Gmail]");

				var uses = new List<SpecialFolder> ();
				uses.Add (SpecialFolder.All);
				uses.Add (SpecialFolder.Archive);
				uses.Add (SpecialFolder.Drafts);
				uses.Add (SpecialFolder.Flagged);
				uses.Add (SpecialFolder.Important);
				uses.Add (SpecialFolder.Junk);
				uses.Add (SpecialFolder.Sent);
				uses.Add (SpecialFolder.Trash);

				// specifically duplicate some special uses
				uses.Add (SpecialFolder.All);
				uses.Add (SpecialFolder.Flagged);

				// and add one that is invalid
				uses.Add ((SpecialFolder) 15);

				var archive = await gmail.CreateAsync ("Archives", uses);
				Assert.AreEqual (FolderAttributes.HasNoChildren | FolderAttributes.Archive, archive.Attributes);
				Assert.AreEqual (archive, client.GetFolder (SpecialFolder.Archive));
				Assert.AreEqual ("25dcfa84-fd65-41c3-abc3-633c8f10923f", archive.Id);

				try {
					await gmail.CreateAsync ("MyImportant", new[] { SpecialFolder.Important });
					Assert.Fail ("Creating the MyImportamnt folder should have thrown an ImapCommandException");
				} catch (ImapCommandException ex) {
					Assert.AreEqual (ImapCommandResponse.No, ex.Response);
					Assert.AreEqual ("An \\Important mailbox already exists", ex.ResponseText);
				} catch (Exception ex) {
					Assert.Fail ("Unexpected exception: {0}", ex);
				}

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateCopyToCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 UID SEARCH RETURN () ALL\r\n", "gmail.search.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" \"Archived Messages\"\r\n", "gmail.list-archived-messages.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 UID COPY 1:3,5,7:9,11:14,26:29,31,34,41:43,50 \"Archived Messages\"\r\n", "gmail.uid-copy.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 SEARCH UID 1:3,5,7:9,11:14,26:29,31,34,41:43,50\r\n", "gmail.get-indexes.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 COPY 1:21 \"Archived Messages\"\r\n", "gmail.uid-copy.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestCopyTo ()
		{
			var commands = CreateCopyToCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.UidPlus), "Expected UIDPLUS extension");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);
				var uids = inbox.Search (SearchQuery.All);

				var archived = personal.GetSubfolder ("Archived Messages");

				// Test copying using the UIDPLUS extension
				var copied = inbox.CopyTo (uids, archived);

				Assert.AreEqual (copied.Source.Count, copied.Destination.Count, "Source and Destination UID counts do not match");

				// Disable UIDPLUS and try again (to test GetIndexesAsync() and CopyTo(IList<int>, IMailFolder)
				client.Capabilities &= ~ImapCapabilities.UidPlus;
				copied = inbox.CopyTo (uids, archived);

				Assert.AreEqual (copied.Source.Count, copied.Destination.Count, "Source and Destination UID counts do not match");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCopyToAsync ()
		{
			var commands = CreateCopyToCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.UidPlus), "Expected UIDPLUS extension");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);
				var uids = await inbox.SearchAsync (SearchQuery.All);

				var archived = await personal.GetSubfolderAsync ("Archived Messages");

				// Test copying using the UIDPLUS extension
				var copied = await inbox.CopyToAsync (uids, archived);

				Assert.AreEqual (copied.Source.Count, copied.Destination.Count, "Source and Destination UID counts do not match");

				// Disable UIDPLUS and try again (to test GetIndexesAsync() and CopyTo(IList<int>, IMailFolder)
				client.Capabilities &= ~ImapCapabilities.UidPlus;
				copied = await inbox.CopyToAsync (uids, archived);

				Assert.AreEqual (copied.Source.Count, copied.Destination.Count, "Source and Destination UID counts do not match");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateMoveToCommands (bool disableMove)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 UID SEARCH RETURN () ALL\r\n", "gmail.search.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" \"Archived Messages\"\r\n", "gmail.list-archived-messages.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 UID MOVE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 \"Archived Messages\"\r\n", "gmail.uid-move.txt"));
			if (disableMove) {
				commands.Add (new ImapReplayCommand ("A00000009 UID COPY 1:3,5,7:9,11:14,26:29,31,34,41:43,50 \"Archived Messages\"\r\n", "gmail.uid-copy.txt"));
				commands.Add (new ImapReplayCommand ("A00000010 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK));
				commands.Add (new ImapReplayCommand ("A00000011 UID EXPUNGE 1:3,5,7:9,11:14,26:29,31,34,41:43,50\r\n", "gmail.uid-expunge.txt"));
				commands.Add (new ImapReplayCommand ("A00000012 LOGOUT\r\n", "gmail.logout.txt"));
			} else {
				commands.Add (new ImapReplayCommand ("A00000009 SEARCH UID 1:3,5,7:9,11:14,26:29,31,34,41:43,50\r\n", "gmail.get-indexes.txt"));
				commands.Add (new ImapReplayCommand ("A00000010 MOVE 1:21 \"Archived Messages\"\r\n", "gmail.uid-move.txt"));
				commands.Add (new ImapReplayCommand ("A00000011 LOGOUT\r\n", "gmail.logout.txt"));
			}

			return commands;
		}

		[TestCase (true, TestName = "TestUidMoveToDisableMove")]
		[TestCase (false, TestName = "TestUidMoveToDisableUidPlus")]
		public void TestUidMoveTo (bool disableMove)
		{
			var commands = CreateMoveToCommands (disableMove);

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.UidPlus), "Expected UIDPLUS extension");

				var personal = client.GetFolder (client.PersonalNamespaces [0]);
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);
				var uids = inbox.Search (SearchQuery.All);

				var archived = personal.GetSubfolder ("Archived Messages");
				int changed = 0, expunged = 0;

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.AreEqual (0, e.Index, "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				// Test copying using the MOVE & UIDPLUS extensions
				var moved = inbox.MoveTo (uids, archived);

				Assert.AreEqual (moved.Source.Count, moved.Destination.Count, "Source and Destination UID counts do not match");
				Assert.AreEqual (21, expunged, "Expunged event");
				Assert.AreEqual (22, changed, "CountChanged event"); // FIXME: should we work more like IMAP and only emit this once?

				if (disableMove)
					client.Capabilities &= ~ImapCapabilities.Move;
				else
					client.Capabilities &= ~ImapCapabilities.UidPlus;

				expunged = changed = 0;

				moved = inbox.MoveTo (uids, archived);

				Assert.AreEqual (moved.Source.Count, moved.Destination.Count, "Source and Destination UID counts do not match");
				Assert.AreEqual (21, expunged, "Expunged event");
				Assert.AreEqual (22, changed, "CountChanged event");

				client.Disconnect (true);
			}
		}

		[TestCase (true, TestName = "TestUidMoveToDisableMoveAsync")]
		[TestCase (false, TestName = "TestUidMoveToDisableUidPlusAsync")]
		public async Task TestUidMoveToAsync (bool disableMove)
		{
			var commands = CreateMoveToCommands (disableMove);

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (ImapCapabilities.UidPlus), "Expected UIDPLUS extension");

				var personal = client.GetFolder (client.PersonalNamespaces [0]);
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);
				var uids = await inbox.SearchAsync (SearchQuery.All);

				var archived = await personal.GetSubfolderAsync ("Archived Messages");
				int changed = 0, expunged = 0;

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.AreEqual (0, e.Index, "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				// Test moving using the MOVE & UIDPLUS extensions
				var moved = await inbox.MoveToAsync (uids, archived);

				Assert.AreEqual (moved.Source.Count, moved.Destination.Count, "Source and Destination UID counts do not match");
				Assert.AreEqual (21, expunged, "Expunged event");
				Assert.AreEqual (22, changed, "CountChanged event");

				if (disableMove)
					client.Capabilities &= ~ImapCapabilities.Move;
				else
					client.Capabilities &= ~ImapCapabilities.UidPlus;

				expunged = changed = 0;

				moved = await inbox.MoveToAsync (uids, archived);

				Assert.AreEqual (moved.Source.Count, moved.Destination.Count, "Source and Destination UID counts do not match");
				Assert.AreEqual (21, expunged, "Expunged event");
				Assert.AreEqual (22, changed, "CountChanged event");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateUidExpungeCommands (bool disableUidPlus)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 UID SEARCH RETURN () ALL\r\n", "gmail.search.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK));
			if (!disableUidPlus) {
				commands.Add (new ImapReplayCommand ("A00000008 UID EXPUNGE 1:3\r\n", "gmail.expunge.txt"));
				commands.Add (new ImapReplayCommand ("A00000009 LOGOUT\r\n", "gmail.logout.txt"));
			} else {
				commands.Add (new ImapReplayCommand ("A00000008 UID SEARCH RETURN () DELETED NOT UID 1:3\r\n", "gmail.search-deleted-not-1-3.txt"));
				commands.Add (new ImapReplayCommand ("A00000009 UID STORE 5,7:9,11:14,26:29,31,34,41:43,50 -FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK));
				commands.Add (new ImapReplayCommand ("A00000010 EXPUNGE\r\n", "gmail.expunge.txt"));
				commands.Add (new ImapReplayCommand ("A00000011 UID STORE 5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK));
				commands.Add (new ImapReplayCommand ("A00000012 LOGOUT\r\n", "gmail.logout.txt"));
			}

			return commands;
		}

		[TestCase (false, TestName = "TestUidExpunge")]
		[TestCase (true, TestName = "TestUidExpungeDisableUidPlus")]
		public void TestUidExpunge (bool disableUidPlus)
		{
			var commands = CreateUidExpungeCommands (disableUidPlus);

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				int changed = 0, expunged = 0;
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.AreEqual (0, e.Index, "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				var uids = inbox.Search (SearchQuery.All);
				inbox.AddFlags (uids, MessageFlags.Deleted, true);

				if (disableUidPlus)
					client.Capabilities &= ~ImapCapabilities.UidPlus;

				uids = new UniqueIdRange (0, 1, 3);
				inbox.Expunge (uids);

				Assert.AreEqual (3, expunged, "Unexpected number of Expunged events");
				Assert.AreEqual (4, changed, "Unexpected number of CountChanged events");
				Assert.AreEqual (18, inbox.Count, "Count");

				client.Disconnect (true);
			}
		}

		[TestCase (false, TestName = "TestUidExpungeAsync")]
		[TestCase (true, TestName = "TestUidExpungeDisableUidPlusAsync")]
		public async Task TestUidExpungeAsync (bool disableUidPlus)
		{
			var commands = CreateUidExpungeCommands (disableUidPlus);

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				int changed = 0, expunged = 0;
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.AreEqual (0, e.Index, "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				var uids = await inbox.SearchAsync (SearchQuery.All);
				await inbox.AddFlagsAsync (uids, MessageFlags.Deleted, true);

				if (disableUidPlus)
					client.Capabilities &= ~ImapCapabilities.UidPlus;

				uids = new UniqueIdRange (0, 1, 3);
				await inbox.ExpungeAsync (uids);

				Assert.AreEqual (3, expunged, "Unexpected number of Expunged events");
				Assert.AreEqual (4, changed, "Unexpected number of CountChanged events");
				Assert.AreEqual (18, inbox.Count, "Count");

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestCountChanged ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			// INBOX has 1 message present in this test
			commands.Add (new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.count.examine.txt"));
			// next command simulates one expunge + one new message
			commands.Add (new ImapReplayCommand ("A00000006 NOOP\r\n", "gmail.count.noop.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var count = -1;

				client.Inbox.Open (FolderAccess.ReadOnly);

				client.Inbox.CountChanged += delegate {
					count = client.Inbox.Count;
				};

				client.NoOp ();

				Assert.AreEqual (1, count, "Count is not correct");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCountChangedAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			// INBOX has 1 message present in this test
			commands.Add (new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.count.examine.txt"));
			// next command simulates one expunge + one new message
			commands.Add (new ImapReplayCommand ("A00000006 NOOP\r\n", "gmail.count.noop.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var count = -1;

				await client.Inbox.OpenAsync (FolderAccess.ReadOnly);

				client.Inbox.CountChanged += delegate {
					count = client.Inbox.Count;
				};

				await client.NoOpAsync ();

				Assert.AreEqual (1, count, "Count is not correct");

				await client.DisconnectAsync (true);
			}
		}

		static void AssertFolder (IMailFolder folder, string fullName, FolderAttributes attributes, bool subscribed, ulong highestmodseq, int count, int recent, uint uidnext, uint validity, int unread)
		{
			if (subscribed)
				attributes |= FolderAttributes.Subscribed;

			Assert.AreEqual (fullName, folder.FullName, "FullName");
			Assert.AreEqual (attributes, folder.Attributes, "Attributes");
			Assert.AreEqual (subscribed, folder.IsSubscribed, "IsSubscribed");
			Assert.AreEqual (highestmodseq, folder.HighestModSeq, "HighestModSeq");
			Assert.AreEqual (count, folder.Count, "Count");
			Assert.AreEqual (recent, folder.Recent, "Recent");
			Assert.AreEqual (unread, folder.Unread, "Unread");
			Assert.AreEqual (uidnext, folder.UidNext.HasValue ? folder.UidNext.Value.Id : (uint)0, "UidNext");
			Assert.AreEqual (validity, folder.UidValidity, "UidValidity");
		}

		List<ImapReplayCommand> CreateGetSubfoldersWithStatusItemsCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			//commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"[Gmail]\"\r\n", "gmail.list-gmail.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST (SUBSCRIBED) \"\" \"[Gmail]/%\" RETURN (CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ))\r\n", "gmail.list-gmail-subfolders.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" \"[Gmail]/%\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-gmail-subfolders-no-status.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 STATUS \"[Gmail]/All Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-all-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 STATUS \"[Gmail]/Drafts\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-drafts.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 STATUS \"[Gmail]/Important\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-important.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 STATUS \"[Gmail]/Sent Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-all-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 STATUS \"[Gmail]/Spam\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-drafts.txt"));
			commands.Add (new ImapReplayCommand ("A00000012 STATUS \"[Gmail]/Starred\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-important.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 STATUS \"[Gmail]/Trash\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "gmail.status-all-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestGetSubfoldersWithStatusItems ()
		{
			var commands = CreateGetSubfoldersWithStatusItemsCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var gmail = personal.GetSubfolder ("[Gmail]");
				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;
				var folders = gmail.GetSubfolders (all, true);
				Assert.AreEqual (7, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (folders[1], "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (folders[2], "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (folders[3], "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (folders[4], "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (folders[5], "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (folders[6], "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				// Now make the same query but disable LIST-STATUS
				client.Capabilities &= ~ImapCapabilities.ListStatus;
				folders = gmail.GetSubfolders (all, false);
				Assert.AreEqual (7, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (folders[1], "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (folders[2], "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (folders[3], "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (folders[4], "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (folders[5], "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (folders[6], "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestGetSuboldersWithStatusItemsAsync ()
		{
			var commands = CreateGetSubfoldersWithStatusItemsCommands ();

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

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var gmail = await personal.GetSubfolderAsync ("[Gmail]");
				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;
				var folders = await gmail.GetSubfoldersAsync (all, true);
				Assert.AreEqual (7, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (folders[1], "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (folders[2], "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (folders[3], "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (folders[4], "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (folders[5], "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (folders[6], "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				// Now make the same query but disable LIST-STATUS
				client.Capabilities &= ~ImapCapabilities.ListStatus;
				folders = await gmail.GetSubfoldersAsync (all, false);
				Assert.AreEqual (7, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (folders[1], "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (folders[2], "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (folders[3], "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (folders[4], "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (folders[5], "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (folders[6], "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				await client.DisconnectAsync (true);
			}
		}
	}
}
