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
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;

using NUnit.Framework;

using MailKit.Net.Imap;
using MailKit.Search;
using MailKit;
using MimeKit;

namespace UnitTests.Net.Imap {

	public class ImapClientTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);

		static readonly ImapCapabilities GreetingCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Namespace | ImapCapabilities.Unselect;
		static readonly ImapCapabilities GMailInitialCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Unselect | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Quota |
			ImapCapabilities.XList | ImapCapabilities.Children | ImapCapabilities.GMailExt1 | ImapCapabilities.SaslIR |
			ImapCapabilities.Id;
		static readonly ImapCapabilities GMailAuthenticatedCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Unselect | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Quota |
			ImapCapabilities.XList | ImapCapabilities.Children | ImapCapabilities.GMailExt1 | ImapCapabilities.UidPlus |
			ImapCapabilities.Compress | ImapCapabilities.Enable | ImapCapabilities.Move | ImapCapabilities.CondStore |
			ImapCapabilities.ESearch | ImapCapabilities.Id;

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
		public void TestImapClientGreetingCapabilities ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false), CancellationToken.None);
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
		public void TestImapClientGMail ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 CREATE UnitTests\r\n", "gmail.create-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "gmail.list-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SELECT UnitTests (CONDSTORE)\r\n", "gmail.select-unittests.txt"));

			for (int i = 0; i < 50; i++) {
				using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
					var message = MimeMessage.Load (stream);
					long length = stream.Length;
					string latin1;

					stream.Position = 0;
					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();

					var command = string.Format ("A{0:D8} APPEND UnitTests (\\Seen) ", i + 9);
					command += "{" + length + "}\r\n";

					commands.Add (new ImapReplayCommand (command, "gmail.go-ahead.txt"));
					commands.Add (new ImapReplayCommand (latin1 + "\r\n", string.Format ("gmail.append.{0}.txt", i + 1)));
				}
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
			commands.Add (new ImapReplayCommand ("A00000083 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -FLAGS.SILENT (\\Answered)\r\n", "gmail.remove-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000084 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", "gmail.add-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000085 UNSELECT\r\n", "gmail.unselect-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000086 DELETE UnitTests\r\n", "gmail.delete-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000087 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false), CancellationToken.None);
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

				try {
					var credentials = new NetworkCredential ("username", "password");

					// Note: Do not try XOAUTH2
					client.AuthenticationMechanisms.Remove ("XOAUTH2");

					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

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

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders (false, CancellationToken.None).ToList ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = personal.Create ("UnitTests", true, CancellationToken.None);

				Assert.IsNotNull (created.ParentFolder, "The ParentFolder property should not be null.");

				created.Open (FolderAccess.ReadWrite, CancellationToken.None);

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						var message = MimeMessage.Load (stream);

						created.Append (message, MessageFlags.Seen, CancellationToken.None);
					}
				}

				var query = SearchQuery.ToContains ("nsb").Or (SearchQuery.CcContains ("nsb"));
				var matches = created.Search (query, CancellationToken.None);

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId;
				var summaries = created.Fetch (matches, items, CancellationToken.None);

				foreach (var summary in summaries) {
					if (summary.UniqueId.HasValue)
						created.GetMessage (summary.UniqueId.Value, CancellationToken.None);
					else
						created.GetMessage (summary.Index, CancellationToken.None);
				}

				created.SetFlags (matches, MessageFlags.Seen | MessageFlags.Answered, false, CancellationToken.None);
				created.RemoveFlags (matches, MessageFlags.Answered, true, CancellationToken.None);
				created.AddFlags (matches, MessageFlags.Deleted, true, CancellationToken.None);

				created.Close (false, CancellationToken.None);
				created.Delete (CancellationToken.None);

				client.Disconnect (true, CancellationToken.None);
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
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 EXAMINE INBOX (CONDSTORE)\r\n", "gmail.examine-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 FETCH 270 (BODY.PEEK[])\r\n", "gmail.precise-pangolin-message.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false), CancellationToken.None);
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

				try {
					var credentials = new NetworkCredential ("username", "password");

					// Note: Do not try XOAUTH2
					client.AuthenticationMechanisms.Remove ("XOAUTH2");

					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.HasNoChildren, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

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

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders (false, CancellationToken.None).ToList ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				client.Inbox.Open (FolderAccess.ReadOnly, CancellationToken.None);

				var message = client.Inbox.GetMessage (269, CancellationToken.None);

				using (var jpeg = new MemoryStream ()) {
					var attachment = message.Attachments.FirstOrDefault ();

					attachment.ContentObject.DecodeTo (jpeg);
					jpeg.Position = 0;

					using (var md5 = new MD5CryptoServiceProvider ()) {
						var md5sum = HexEncode (md5.ComputeHash (jpeg));

						Assert.AreEqual ("167a46aa81e881da2ea8a840727384d3", md5sum, "MD5 checksums do not match.");
					}
				}

				client.Disconnect (false, CancellationToken.None);
			}
		}
		
		[Test]
		public void TestMessageCount ()
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
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				
				try {
					var credentials = new NetworkCredential ("username", "password");

					// Note: Do not try XOAUTH2
					client.AuthenticationMechanisms.Remove ("XOAUTH2");

					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}
				
				var count = -1;
				
				client.Inbox.Open (FolderAccess.ReadOnly);
				
				client.Inbox.CountChanged += delegate {
					count = client.Inbox.Count;
				};
				
				client.NoOp();
				
				Assert.AreEqual(1, count, "Count is not correct");
			}
		}
	}
}
