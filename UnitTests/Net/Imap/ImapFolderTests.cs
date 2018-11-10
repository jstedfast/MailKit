//
// ImapFolderTests.cs
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
	public class ImapFolderTests
	{
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
				inbox.Open (FolderAccess.ReadWrite);

				// ImapFolder .ctor
				Assert.Throws<ArgumentNullException> (() => new ImapFolder (null));

				// Open
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Open ((FolderAccess) 500));
				Assert.Throws<ArgumentOutOfRangeException> (() => inbox.Open ((FolderAccess) 500, 0, 0, UniqueIdRange.All));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.OpenAsync ((FolderAccess) 500));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await inbox.OpenAsync ((FolderAccess) 500, 0, 0, UniqueIdRange.All));

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
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync (null, true));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, true));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", true));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync (null, SpecialFolder.All));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, SpecialFolder.All));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", SpecialFolder.All));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync (null, new SpecialFolder [] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync (string.Empty, new SpecialFolder [] { SpecialFolder.All }));
				Assert.Throws<ArgumentException> (async () => await inbox.CreateAsync ("Folder./Name", new SpecialFolder [] { SpecialFolder.All }));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CreateAsync ("ValidName", null));
				Assert.Throws<NotSupportedException> (async () => await inbox.CreateAsync ("ValidName", SpecialFolder.All));

				// Rename
				Assert.Throws<ArgumentNullException> (() => inbox.Rename (null, "NewName"));
				Assert.Throws<ArgumentNullException> (() => inbox.Rename (personal, null));
				Assert.Throws<ArgumentException> (() => inbox.Rename (personal, string.Empty));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RenameAsync (null, "NewName"));
				Assert.Throws<ArgumentNullException> (async () => await inbox.RenameAsync (personal, null));
				Assert.Throws<ArgumentException> (async () => await inbox.RenameAsync (personal, string.Empty));

				// GetSubfolder
				Assert.Throws<ArgumentNullException> (() => inbox.GetSubfolder (null));
				Assert.Throws<ArgumentException> (() => inbox.GetSubfolder (string.Empty));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetSubfolderAsync (null));
				Assert.Throws<ArgumentException> (async () => await inbox.GetSubfolderAsync (string.Empty));

				// GetMetadata
				Assert.Throws<ArgumentNullException> (() => client.GetMetadata (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (() => client.GetMetadata (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (async () => await client.GetMetadataAsync (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (async () => await client.GetMetadataAsync (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (() => inbox.GetMetadata (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (() => inbox.GetMetadata (new MetadataOptions (), null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetMetadataAsync (null, new MetadataTag [] { MetadataTag.PrivateComment }));
				Assert.Throws<ArgumentNullException> (async () => await inbox.GetMetadataAsync (new MetadataOptions (), null));

				// SetMetadata
				Assert.Throws<ArgumentNullException> (() => client.SetMetadata (null));
				Assert.Throws<ArgumentNullException> (async () => await client.SetMetadataAsync (null));
				Assert.Throws<ArgumentNullException> (() => inbox.SetMetadata (null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.SetMetadataAsync (null));

				// Expunge
				Assert.Throws<ArgumentNullException> (() => inbox.Expunge (null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.ExpungeAsync (null));

				// Append
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages[0]));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages[0]));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages[0], MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, MessageFlags.None, DateTimeOffset.Now));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, MessageFlags.None, DateTimeOffset.Now));

				// MultiAppend
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, flags));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (messages, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages, flags));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, flags));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, flags));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, flags, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, null, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (messages, null, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (messages, flags, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (messages, flags, null));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (null, messages, flags, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (null, messages, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, null, flags, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, null, flags, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, null, dates));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, null, dates));
				Assert.Throws<ArgumentNullException> (() => inbox.Append (FormatOptions.Default, messages, flags, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.AppendAsync (FormatOptions.Default, messages, flags, null));

				// CopyTo
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.CopyTo (new int [] { 0 }, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.CopyToAsync (new int [] { 0 }, null));

				// MoveTo
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync ((IList<UniqueId>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync (UniqueIdRange.All, null));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync ((IList<int>) null, inbox));
				Assert.Throws<ArgumentNullException> (() => inbox.MoveTo (new int [] { 0 }, null));
				Assert.Throws<ArgumentNullException> (async () => await inbox.MoveToAsync (new int [] { 0 }, null));

				client.Disconnect (false);
			}
		}
	}
}
