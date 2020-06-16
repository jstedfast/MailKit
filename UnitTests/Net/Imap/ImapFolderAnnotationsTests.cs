//
// ImapFolderAnnotationsTests.cs
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
	public class ImapFolderAnnotationsTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);

		Stream GetResourceStream (string name)
		{
			return GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt"));

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

				Assert.AreEqual (AnnotationAccess.ReadWrite, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var annotations = new List<Annotation> (new[] {
					new Annotation (AnnotationEntry.AltSubject)
				});
				annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "value");

				// Store
				Assert.Throws<ArgumentException> (() => inbox.Store (-1, annotations));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.StoreAsync (-1, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (0, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync (0, (IList<Annotation>) null));

				Assert.Throws<ArgumentException> (() => inbox.Store (UniqueId.Invalid, annotations));
				Assert.ThrowsAsync<ArgumentException> (async () => await inbox.StoreAsync (UniqueId.Invalid, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueId.MinValue, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync (UniqueId.MinValue, (IList<Annotation>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<int>) null, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync ((IList<int>) null, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (new int[] { 0 }, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync (new int[] { 0 }, (IList<Annotation>) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<int>) null, 1, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync ((IList<int>) null, 1, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (new int[] { 0 }, 1, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync (new int[] { 0 }, 1, (IList<Annotation>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<UniqueId>) null, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync ((IList<UniqueId>) null, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueIdRange.All, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync (UniqueIdRange.All, (IList<Annotation>) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<UniqueId>) null, 1, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync ((IList<UniqueId>) null, 1, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueIdRange.All, 1, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await inbox.StoreAsync (UniqueIdRange.All, 1, (IList<Annotation>) null));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestNotSupportedExceptions ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (ANNOTATE)\r\n", "common.select-inbox-annotate-no-modseq.txt"));

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

				Assert.AreEqual (AnnotationAccess.None, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.None, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (0, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var annotations = new List<Annotation> (new[] {
					new Annotation (AnnotationEntry.AltSubject)
				});
				annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "value");

				// verify NotSupportedException for storing annotations
				Assert.Throws<NotSupportedException> (() => inbox.Store (0, annotations));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StoreAsync (0, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (UniqueId.MinValue, annotations));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StoreAsync (UniqueId.MinValue, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (new int[] { 0 }, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StoreAsync (new int[] { 0 }, 1, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (UniqueIdRange.All, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StoreAsync (UniqueIdRange.All, 1, annotations));

				// disable CONDSTORE and verify that we get NotSupportedException when we send modseq
				client.Capabilities &= ~ImapCapabilities.CondStore;
				inbox.Open (FolderAccess.ReadWrite);

				Assert.AreEqual (AnnotationAccess.ReadWrite, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				Assert.Throws<NotSupportedException> (() => inbox.Store (new int[] { 0 }, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StoreAsync (new int[] { 0 }, 1, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (UniqueIdRange.All, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.StoreAsync (UniqueIdRange.All, 1, annotations));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestChangingAnnotationsOnEmptyListOfMessages ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt"));

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

				Assert.AreEqual (AnnotationAccess.ReadWrite, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var annotations = new List<Annotation> (new[] {
					new Annotation (AnnotationEntry.AltSubject)
				});
				annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "value");

				ulong modseq = 409601020304;
				var uids = new UniqueId[0];
				var indexes = new int[0];
				IList<UniqueId> unmodifiedUids;
				IList<int> unmodifiedIndexes;

				unmodifiedIndexes = inbox.Store (indexes, modseq, annotations);
				Assert.AreEqual (0, unmodifiedIndexes.Count);

				unmodifiedUids = inbox.Store (uids, modseq, annotations);
				Assert.AreEqual (0, unmodifiedUids.Count);

				client.Disconnect (false);
			}
		}

		IList<ImapReplayCommand> CreateAppendWithAnnotationsCommands (bool withInternalDates, out List<MimeMessage> messages, out List<MessageFlags> flags, out List<DateTimeOffset> internalDates, out List<Annotation> annotations)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));

			internalDates = withInternalDates ? new List<DateTimeOffset> () : null;
			annotations = new List<Annotation> ();
			messages = new List<MimeMessage> ();
			flags = new List<MessageFlags> ();
			var command = new StringBuilder ();
			int id = 4;

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
				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties[AnnotationAttribute.PrivateValue] = string.Format ("Alternate subject {0}", i);
				annotations.Add (annotation);

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

				command.AppendFormat ("ANNOTATION (/altsubject (value.priv \"Alternate subject {0}\")) ", i);

				command.Append ('{').Append (length.ToString ()).Append ("+}\r\n").Append (latin1).Append ("\r\n");
				commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
			}

			commands.Add (new ImapReplayCommand (string.Format ("A{0:D8} LOGOUT\r\n", id), "gmail.logout.txt"));

			return commands;
		}

		[TestCase (false, TestName = "TestAppendWithAnnotations")]
		[TestCase (true, TestName = "TestAppendWithAnnotationsAndInternalDates")]
		public void TestAppendWithAnnotations (bool withInternalDates)
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			List<DateTimeOffset> internalDates;
			List<Annotation> annotations;
			List<MimeMessage> messages;
			List<MessageFlags> flags;

			var commands = CreateAppendWithAnnotationsCommands (withInternalDates, out messages, out flags, out internalDates, out annotations);

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = client.Inbox.Append (messages[i], flags[i], internalDates[i], new [] { annotations[i] });
					else
						uid = client.Inbox.Append (messages[i], flags[i], null, new [] { annotations[i] });

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				client.Disconnect (true);
			}
		}

		[TestCase (false, TestName = "TestAppendWithAnnotationsAsync")]
		[TestCase (true, TestName = "TestAppendWithAnnotationsAndInternalDatesAsync")]
		public async Task TestAppendWithAnnotationsAsync (bool withInternalDates)
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			List<DateTimeOffset> internalDates;
			List<Annotation> annotations;
			List<MimeMessage> messages;
			List<MessageFlags> flags;

			var commands = CreateAppendWithAnnotationsCommands (withInternalDates, out messages, out flags, out internalDates, out annotations);

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = await client.Inbox.AppendAsync (messages[i], flags[i], internalDates[i], new[] { annotations[i] });
					else
						uid = await client.Inbox.AppendAsync (messages[i], flags[i], null, new[] { annotations[i] });

					Assert.IsTrue (uid.HasValue, "Expected a UIDAPPEND resp-code");
					Assert.AreEqual (i + 1, uid.Value.Id, "Unexpected UID");
				}

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestSelectAnnotateNone ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-none.txt"));

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

				Assert.AreEqual (AnnotationAccess.None, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.None, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (0, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				client.Disconnect (false);
			}
		}

		List<ImapReplayCommand> CreateSearchAnnotationsCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-readonly.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 UID SEARCH RETURN () ANNOTATION /comment value \"a comment\"\r\n", "dovecot.search-uids.txt"));

			return commands;
		}

		[Test]
		public void TestSearchAnnotations ()
		{
			var commands = CreateSearchAnnotationsCommands ();

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

				Assert.AreEqual (AnnotationAccess.ReadOnly, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Both, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (0, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var query = SearchQuery.AnnotationsContain (AnnotationEntry.Comment, AnnotationAttribute.Value, "a comment");
				var uids = inbox.Search (query);

				Assert.AreEqual (14, uids.Count, "Unexpected number of UIDs");

				// disable ANNOTATE-EXPERIMENT-1 and try again
				client.Capabilities &= ~ImapCapabilities.Annotate;

				Assert.Throws<NotSupportedException> (() => inbox.Search (query));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSearchAnnotationsAsync ()
		{
			var commands = CreateSearchAnnotationsCommands ();

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.AreEqual (AnnotationAccess.ReadOnly, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Both, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (0, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var query = SearchQuery.AnnotationsContain (AnnotationEntry.Comment, AnnotationAttribute.Value, "a comment");
				var uids = await inbox.SearchAsync (query);

				Assert.AreEqual (14, uids.Count, "Unexpected number of UIDs");

				// disable ANNOTATE-EXPERIMENT-1 and try again
				client.Capabilities &= ~ImapCapabilities.Annotate;

				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SearchAsync (query));

				await client.DisconnectAsync (false);
			}
		}

		List<ImapReplayCommand> CreateSortAnnotationsCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-readonly.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 UID SORT RETURN () (ANNOTATION /altsubject value.shared) US-ASCII ALL\r\n", "dovecot.sort-by-strings.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 UID SORT RETURN () (REVERSE ANNOTATION /altsubject value.shared) US-ASCII ALL\r\n", "dovecot.sort-by-strings.txt"));

			return commands;
		}

		[Test]
		public void TestSortAnnotations ()
		{
			var commands = CreateSortAnnotationsCommands ();

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

				Assert.AreEqual (AnnotationAccess.ReadOnly, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Both, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (0, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Ascending);
				var uids = inbox.Sort (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.AreEqual (14, uids.Count, "Unexpected number of UIDs");

				orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Descending);
				uids = inbox.Sort (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.AreEqual (14, uids.Count, "Unexpected number of UIDs");

				// disable ANNOTATE-EXPERIMENT-1 and try again
				client.Capabilities &= ~ImapCapabilities.Annotate;

				Assert.Throws<NotSupportedException> (() => inbox.Sort (SearchQuery.All, new OrderBy[] { orderBy }));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSortAnnotationsAsync ()
		{
			var commands = CreateSortAnnotationsCommands ();

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.AreEqual (AnnotationAccess.ReadOnly, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Both, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (0, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Ascending);
				var uids = await inbox.SortAsync (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.AreEqual (14, uids.Count, "Unexpected number of UIDs");

				orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Descending);
				uids = await inbox.SortAsync (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.AreEqual (14, uids.Count, "Unexpected number of UIDs");

				// disable ANNOTATE-EXPERIMENT-1 and try again
				client.Capabilities &= ~ImapCapabilities.Annotate;

				Assert.ThrowsAsync<NotSupportedException> (async () => await inbox.SortAsync (SearchQuery.All, new OrderBy[] { orderBy }));

				await client.DisconnectAsync (false);
			}
		}

		List<ImapReplayCommand> CreateStoreCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 STORE 1 ANNOTATION (/altsubject (value.shared \"This is an alternate subject.\"))\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000006 UID STORE 1 ANNOTATION (/altsubject (value.shared \"This is an alternate subject.\"))\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000007 STORE 1 ANNOTATION (/altsubject (value.shared NIL))\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000008 UID STORE 1 ANNOTATION (/altsubject (value.shared NIL))\r\n", ImapReplayCommandResponse.OK));

			return commands;
		}

		[Test]
		public void TestStore ()
		{
			var commands = CreateStoreCommands ();

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

				Assert.AreEqual (AnnotationAccess.ReadWrite, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties.Add (AnnotationAttribute.SharedValue, "This is an alternate subject.");

				var annotations = new [] { annotation };

				inbox.Store (0, annotations);
				inbox.Store (new UniqueId (1), annotations);

				annotation.Properties[AnnotationAttribute.SharedValue] = null;

				inbox.Store (0, annotations);
				inbox.Store (new UniqueId (1), annotations);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestStoreAsync ()
		{
			var commands = CreateStoreCommands ();

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsInstanceOf<ImapEngine> (client.Inbox.SyncRoot, "SyncRoot");

				var inbox = (ImapFolder) client.Inbox;
				await inbox.OpenAsync (FolderAccess.ReadWrite);

				Assert.AreEqual (AnnotationAccess.ReadWrite, inbox.AnnotationAccess, "AnnotationAccess");
				Assert.AreEqual (AnnotationScope.Shared, inbox.AnnotationScopes, "AnnotationScopes");
				Assert.AreEqual (20480, inbox.MaxAnnotationSize, "MaxAnnotationSize");

				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties.Add (AnnotationAttribute.SharedValue, "This is an alternate subject.");

				var annotations = new[] { annotation };

				await inbox.StoreAsync (0, annotations);
				await inbox.StoreAsync (new UniqueId (1), annotations);

				annotation.Properties[AnnotationAttribute.SharedValue] = null;

				await inbox.StoreAsync (0, annotations);
				await inbox.StoreAsync (new UniqueId (1), annotations);

				await client.DisconnectAsync (false);
			}
		}
	}
}
