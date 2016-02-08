//
// ImapClientTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
using System.Collections.Generic;
using System.Security.Cryptography;

using NUnit.Framework;

using MimeKit;

using MailKit.Net.Imap;
using MailKit.Security;
using MailKit.Search;
using MailKit;

namespace UnitTests.Net.Imap {

	public class ImapClientTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		static readonly ImapCapabilities GreetingCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Namespace | ImapCapabilities.Unselect;
		static readonly ImapCapabilities GMailInitialCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Quota | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Id |
			ImapCapabilities.Children | ImapCapabilities.Unselect | ImapCapabilities.SaslIR | ImapCapabilities.XList |
			ImapCapabilities.GMailExt1;
		static readonly ImapCapabilities GMailAuthenticatedCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Quota | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Id |
			ImapCapabilities.Children | ImapCapabilities.Unselect | ImapCapabilities.UidPlus | ImapCapabilities.CondStore |
			ImapCapabilities.ESearch | ImapCapabilities.Compress | ImapCapabilities.Enable | ImapCapabilities.ListExtended |
			ImapCapabilities.ListStatus | ImapCapabilities.Move | ImapCapabilities.UTF8Accept | ImapCapabilities.XList |
			ImapCapabilities.GMailExt1;
		static readonly ImapCapabilities AclInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities AclAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Acl;

		static FolderAttributes GetSpecialFolderAttribute (SpecialFolder special)
		{
			switch (special) {
			case SpecialFolder.All:     return FolderAttributes.All;
			case SpecialFolder.Archive: return FolderAttributes.Archive;
			case SpecialFolder.Drafts:  return FolderAttributes.Drafts;
			case SpecialFolder.Flagged: return FolderAttributes.Flagged;
			case SpecialFolder.Junk:    return FolderAttributes.Junk;
			case SpecialFolder.Sent:    return FolderAttributes.Sent;
			case SpecialFolder.Trash:   return FolderAttributes.Trash;
			default: throw new ArgumentOutOfRangeException ();
			}
		}

		Stream GetResourceStream (string name)
		{
			return GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
		}

		static string HexEncode (byte[] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 143, false));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 143, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 143, false));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 143, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				// Authenticate
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ("username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ("username", null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, credentials));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, credentials));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, "username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, "username", null));
			}
		}

		[Test]
		public void TestImapClientGreetingCapabilities ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GreetingCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
			}
		}

		[Test]
		public async void TestImapClientFeatures ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\")\r\n", "common.id.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 GETQUOTAROOT INBOX\r\n", "common.getquota.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var implementation = new ImapImplementation {
					Name = "MailKit", Version = "1.0", Vendor = "Xamarin Inc."
				};

				implementation = await client.IdentifyAsync (implementation);
				Assert.IsNotNull (implementation, "Expected a non-null ID response.");
				Assert.AreEqual ("GImap", implementation.Name);
				Assert.AreEqual ("Google, Inc.", implementation.Vendor);
				Assert.AreEqual ("http://support.google.com/mail", implementation.SupportUrl);
				Assert.AreEqual ("gmail_imap_150623.03_p1", implementation.Version);
				Assert.AreEqual ("127.0.0.1", implementation.Properties["remote-host"]);

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = await inbox.GetQuotaAsync ();
				Assert.IsNotNull (quota, "Expected a non-null GETQUOTAROOT response.");
				Assert.AreEqual (personal.FullName, quota.QuotaRoot.FullName);
				Assert.AreEqual (personal, quota.QuotaRoot);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (15728640, quota.StorageLimit.Value);
				Assert.IsFalse (quota.CurrentMessageCount.HasValue);
				Assert.IsFalse (quota.MessageLimit.HasValue);

				await client.DisconnectAsync (false);
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
			Assert.AreEqual (uidnext, folder.UidNext.HasValue ? folder.UidNext.Value.Id : (uint) 0, "UidNext");
			Assert.AreEqual (validity, folder.UidValidity, "UidValidity");
		}

		[Test]
		public async void TestImapClientGetFolders ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST (SUBSCRIBED) \"\" \"*\" RETURN (CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ))\r\n", "gmail.list-all.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;
				var folders = (await client.GetFoldersAsync (client.PersonalNamespaces[0], all, true)).ToList ();
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0);
				AssertFolder (folders[1], "[Gmail]", FolderAttributes.HasChildren | FolderAttributes.NonExistent | FolderAttributes.NoSelect, true, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (folders[3], "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				AssertFolder (folders[4], "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (folders[6], "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (folders[7], "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (folders[8], "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				AssertFolder (client.Inbox, "INBOX", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0);
				//AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Important", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 58, 0, 307, 9, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0);

				client.Disconnect (false);
			}
		}

		[Test]
		public async void TestImapClientGMail ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 CREATE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "gmail.list-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SELECT UnitTests (CONDSTORE)\r\n", "gmail.select-unittests.txt"));

			for (int i = 0; i < 50; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				var command = string.Format ("A{0:D8} APPEND UnitTests (\\Seen) ", i + 9);
				command += "{" + length + "}\r\n";

				commands.Add (new ImapReplayCommand (command, "gmail.go-ahead.txt"));
				commands.Add (new ImapReplayCommand (latin1 + "\r\n", string.Format ("gmail.append.{0}.txt", i + 1)));
			}

			commands.Add (new ImapReplayCommand ("A00000059 UID SEARCH RETURN () CHARSET US-ASCII OR TO nsb CC nsb\r\n", "gmail.search.txt"));
			commands.Add (new ImapReplayCommand ("A00000060 UID FETCH 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODY)\r\n", "gmail.search-summary.txt"));
			commands.Add (new ImapReplayCommand ("A00000061 UID FETCH 1 (BODY.PEEK[])\r\n", "gmail.fetch.1.txt"));
			commands.Add (new ImapReplayCommand ("A00000062 UID FETCH 2 (BODY.PEEK[])\r\n", "gmail.fetch.2.txt"));
			commands.Add (new ImapReplayCommand ("A00000063 UID FETCH 3 (BODY.PEEK[])\r\n", "gmail.fetch.3.txt"));
			commands.Add (new ImapReplayCommand ("A00000064 UID FETCH 5 (BODY.PEEK[])\r\n", "gmail.fetch.5.txt"));
			commands.Add (new ImapReplayCommand ("A00000065 UID FETCH 7 (BODY.PEEK[])\r\n", "gmail.fetch.7.txt"));
			commands.Add (new ImapReplayCommand ("A00000066 UID FETCH 8 (BODY.PEEK[])\r\n", "gmail.fetch.8.txt"));
			commands.Add (new ImapReplayCommand ("A00000067 UID FETCH 9 (BODY.PEEK[])\r\n", "gmail.fetch.9.txt"));
			commands.Add (new ImapReplayCommand ("A00000068 UID FETCH 11 (BODY.PEEK[])\r\n", "gmail.fetch.11.txt"));
			commands.Add (new ImapReplayCommand ("A00000069 UID FETCH 12 (BODY.PEEK[])\r\n", "gmail.fetch.12.txt"));
			commands.Add (new ImapReplayCommand ("A00000070 UID FETCH 13 (BODY.PEEK[])\r\n", "gmail.fetch.13.txt"));
			commands.Add (new ImapReplayCommand ("A00000071 UID FETCH 14 (BODY.PEEK[])\r\n", "gmail.fetch.14.txt"));
			commands.Add (new ImapReplayCommand ("A00000072 UID FETCH 26 (BODY.PEEK[])\r\n", "gmail.fetch.26.txt"));
			commands.Add (new ImapReplayCommand ("A00000073 UID FETCH 27 (BODY.PEEK[])\r\n", "gmail.fetch.27.txt"));
			commands.Add (new ImapReplayCommand ("A00000074 UID FETCH 28 (BODY.PEEK[])\r\n", "gmail.fetch.28.txt"));
			commands.Add (new ImapReplayCommand ("A00000075 UID FETCH 29 (BODY.PEEK[])\r\n", "gmail.fetch.29.txt"));
			commands.Add (new ImapReplayCommand ("A00000076 UID FETCH 31 (BODY.PEEK[])\r\n", "gmail.fetch.31.txt"));
			commands.Add (new ImapReplayCommand ("A00000077 UID FETCH 34 (BODY.PEEK[])\r\n", "gmail.fetch.34.txt"));
			commands.Add (new ImapReplayCommand ("A00000078 UID FETCH 41 (BODY.PEEK[])\r\n", "gmail.fetch.41.txt"));
			commands.Add (new ImapReplayCommand ("A00000079 UID FETCH 42 (BODY.PEEK[])\r\n", "gmail.fetch.42.txt"));
			commands.Add (new ImapReplayCommand ("A00000080 UID FETCH 43 (BODY.PEEK[])\r\n", "gmail.fetch.43.txt"));
			commands.Add (new ImapReplayCommand ("A00000081 UID FETCH 50 (BODY.PEEK[])\r\n", "gmail.fetch.50.txt"));
			commands.Add (new ImapReplayCommand ("A00000082 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 FLAGS (\\Answered \\Seen)\r\n", "gmail.set-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000083 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -FLAGS.SILENT (\\Answered)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000084 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", "gmail.add-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000085 UNSELECT\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000086 SUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000087 LSUB \"\" \"%\"\r\n", "gmail.lsub-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000088 UNSUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000089 CREATE UnitTests/Dummy\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000090 LIST \"\" UnitTests/Dummy\r\n", "gmail.list-unittests-dummy.txt"));
			commands.Add (new ImapReplayCommand ("A00000091 RENAME UnitTests RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000092 DELETE RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000093 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

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
				var folders = (await personal.GetSubfoldersAsync ()).ToList ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = await personal.CreateAsync ("UnitTests", true);
				Assert.IsNotNull (created, "Expected a non-null created folder.");
				Assert.AreEqual (FolderAttributes.HasNoChildren, created.Attributes);

				Assert.IsNotNull (created.ParentFolder, "The ParentFolder property should not be null.");

				const MessageFlags ExpectedPermanentFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.UserDefined;
				const MessageFlags ExpectedAcceptedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen;
				var access = await created.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "The UnitTests folder was not opened with the expected access mode.");
				Assert.AreEqual (ExpectedPermanentFlags, created.PermanentFlags, "The PermanentFlags do not match the expected value.");
				Assert.AreEqual (ExpectedAcceptedFlags, created.AcceptedFlags, "The AcceptedFlags do not match the expected value.");

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						var message = MimeMessage.Load (stream);

						var uid = await created.AppendAsync (message, MessageFlags.Seen);
						Assert.IsTrue (uid.HasValue, "Expected a UID to be returned from folder.Append().");
						Assert.AreEqual ((uint) (i + 1), uid.Value.Id, "The UID returned from the APPEND command does not match the expected UID.");
					}
				}

				var query = SearchQuery.ToContains ("nsb").Or (SearchQuery.CcContains ("nsb"));
				var matches = await created.SearchAsync (query);

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId;
				var summaries = await created.FetchAsync (matches, items);

				foreach (var summary in summaries) {
					if (summary.UniqueId.IsValid)
						await created.GetMessageAsync (summary.UniqueId);
					else
						await created.GetMessageAsync (summary.Index);
				}

				await created.SetFlagsAsync (matches, MessageFlags.Seen | MessageFlags.Answered, false);
				await created.RemoveFlagsAsync (matches, MessageFlags.Answered, true);
				await created.AddFlagsAsync (matches, MessageFlags.Deleted, true);

				await created.CloseAsync ();
				Assert.IsFalse (created.IsOpen, "Expected the UnitTests folder to be closed.");

				await created.SubscribeAsync ();
				Assert.IsTrue (created.IsSubscribed, "Expected IsSubscribed to be true after subscribing to the folder.");

				var subscribed = (await personal.GetSubfoldersAsync (true)).ToList ();
				Assert.IsTrue (subscribed.Contains (created), "Expected the list of subscribed folders to contain the UnitTests folder.");

				await created.UnsubscribeAsync ();
				Assert.IsFalse (created.IsSubscribed, "Expected IsSubscribed to be false after unsubscribing from the folder.");

				var dummy = await created.CreateAsync ("Dummy", true);
				bool dummyRenamed = false;
				bool renamed = false;
				bool deleted = false;

				dummy.Renamed += (sender, e) => { dummyRenamed = true; };
				created.Renamed += (sender, e) => { renamed = true; };

				await created.RenameAsync (created.ParentFolder, "RenamedUnitTests");
				Assert.AreEqual ("RenamedUnitTests", created.Name);
				Assert.AreEqual ("RenamedUnitTests", created.FullName);
				Assert.IsTrue (renamed, "Expected the Rename event to be emitted for the UnitTests folder.");

				Assert.AreEqual ("RenamedUnitTests/Dummy", dummy.FullName);
				Assert.IsTrue (dummyRenamed, "Expected the Rename event to be emitted for the UnitTests/Dummy folder.");

				created.Deleted += (sender, e) => { deleted = true; };

				await created.DeleteAsync ();
				Assert.IsTrue (deleted, "Expected the Deleted event to be emitted for the UnitTests folder.");

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public async void TestAccessControlLists ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "acl.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "acl.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETACL INBOX\r\n", "acl.getacl.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LISTRIGHTS INBOX smith\r\n", "acl.listrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 MYRIGHTS INBOX\r\n", "acl.myrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETACL INBOX smith +lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000009 SETACL INBOX smith -lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000010 SETACL INBOX smith lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000011 DELETEACL INBOX smith\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (AclInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (AclAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

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

				// GETACL INBOX
				var acl = await client.Inbox.GetAccessControlListAsync ();
				Assert.AreEqual (2, acl.Count, "The number of access controls does not match.");
				Assert.AreEqual ("Fred", acl[0].Name, "The identifier for the first access control does not match.");
				Assert.AreEqual ("rwipslxetad", acl[0].Rights.ToString (), "The access rights for the first access control does not match.");
				Assert.AreEqual ("Chris", acl[1].Name, "The identifier for the second access control does not match.");
				Assert.AreEqual ("lrswi", acl[1].Rights.ToString (), "The access rights for the second access control does not match.");

				// LISTRIGHTS INBOX smith
				var rights = await client.Inbox.GetAccessRightsAsync ("smith");
				Assert.AreEqual ("lrswipkxtecda0123456789", rights.ToString (), "The access rights do not match for user smith.");

				// MYRIGHTS INBOX
				rights = await client.Inbox.GetMyAccessRightsAsync ();
				Assert.AreEqual ("rwiptsldaex", rights.ToString (), "My access rights do not match.");

				// SETACL INBOX smith +lrswida
				await client.Inbox.AddAccessRightsAsync ("smith", new AccessRights ("lrswida"));

				// SETACL INBOX smith -lrswida
				await client.Inbox.RemoveAccessRightsAsync ("smith", new AccessRights ("lrswida"));

				// SETACL INBOX smith lrswida
				await client.Inbox.SetAccessRightsAsync ("smith", new AccessRights ("lrswida"));

				// DELETEACL INBOX smith
				await client.Inbox.RemoveAccessAsync ("smith");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public async void TestExtractingPrecisePangolinAttachment ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
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

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

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
				var folders = (await personal.GetSubfoldersAsync ()).ToList ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				await client.Inbox.OpenAsync (FolderAccess.ReadOnly);

				var message = await client.Inbox.GetMessageAsync (269);

				using (var jpeg = new MemoryStream ()) {
					var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

					attachment.ContentObject.DecodeTo (jpeg);
					jpeg.Position = 0;

					using (var md5 = new MD5CryptoServiceProvider ()) {
						var md5sum = HexEncode (md5.ComputeHash (jpeg));

						Assert.AreEqual ("167a46aa81e881da2ea8a840727384d3", md5sum, "MD5 checksums do not match.");
					}
				}

				await client.DisconnectAsync (false);
			}
		}
		
		[Test]
		public async void TestMessageCount ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			// INBOX has 1 message present in this test
			commands.Add (new ImapReplayCommand ("A00000005 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.count.examine.txt"));
			// next command simulates one expunge + one new message
			commands.Add (new ImapReplayCommand ("A00000006 NOOP\r\n", "gmail.count.noop.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				
				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

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
			}
		}
	}
}
