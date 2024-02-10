//
// ImapFolderAnnotationsTests.cs
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
using System.Globalization;

using MimeKit;

using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapFolderAnnotationsTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);

		static Stream GetResourceStream (string name)
		{
			return typeof (ImapFolderAnnotationsTests).Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt")
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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				var annotations = new List<Annotation> (new[] {
					new Annotation (AnnotationEntry.AltSubject)
				});
				annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "value");

				// Store
				Assert.Throws<ArgumentException> (() => inbox.Store (-1, annotations));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.StoreAsync (-1, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (0, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (0, (IList<Annotation>) null));

				Assert.Throws<ArgumentException> (() => inbox.Store (UniqueId.Invalid, annotations));
				Assert.ThrowsAsync<ArgumentException> (() => inbox.StoreAsync (UniqueId.Invalid, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueId.MinValue, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueId.MinValue, (IList<Annotation>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<int>) null, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<int>) null, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (new int[] { 0 }, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (new int[] { 0 }, (IList<Annotation>) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<int>) null, 1, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<int>) null, 1, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (new int[] { 0 }, 1, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (new int[] { 0 }, 1, (IList<Annotation>) null));

				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<UniqueId>) null, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<UniqueId>) null, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueIdRange.All, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueIdRange.All, (IList<Annotation>) null));
				Assert.Throws<ArgumentNullException> (() => inbox.Store ((IList<UniqueId>) null, 1, annotations));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync ((IList<UniqueId>) null, 1, annotations));
				Assert.Throws<ArgumentNullException> (() => inbox.Store (UniqueIdRange.All, 1, (IList<Annotation>) null));
				Assert.ThrowsAsync<ArgumentNullException> (() => inbox.StoreAsync (UniqueIdRange.All, 1, (IList<Annotation>) null));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestNotSupportedExceptions ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (ANNOTATE)\r\n", "common.select-inbox-annotate-no-modseq.txt")
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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.None), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.None), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				var annotations = new List<Annotation> (new[] {
					new Annotation (AnnotationEntry.AltSubject)
				});
				annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "value");

				// verify NotSupportedException for storing annotations
				Assert.Throws<NotSupportedException> (() => inbox.Store (0, annotations));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.StoreAsync (0, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (UniqueId.MinValue, annotations));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.StoreAsync (UniqueId.MinValue, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (new int[] { 0 }, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.StoreAsync (new int[] { 0 }, 1, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (UniqueIdRange.All, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.StoreAsync (UniqueIdRange.All, 1, annotations));

				// disable CONDSTORE and verify that we get NotSupportedException when we send modseq
				client.Capabilities &= ~ImapCapabilities.CondStore;
				inbox.Open (FolderAccess.ReadWrite);

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				Assert.Throws<NotSupportedException> (() => inbox.Store (new int[] { 0 }, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.StoreAsync (new int[] { 0 }, 1, annotations));

				Assert.Throws<NotSupportedException> (() => inbox.Store (UniqueIdRange.All, 1, annotations));
				Assert.ThrowsAsync<NotSupportedException> (() => inbox.StoreAsync (UniqueIdRange.All, 1, annotations));

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestChangingAnnotationsOnEmptyListOfMessages ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt")
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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				var annotations = new List<Annotation> (new[] {
					new Annotation (AnnotationEntry.AltSubject)
				});
				annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "value");

				ulong modseq = 409601020304;
				var uids = Array.Empty<UniqueId> ();
				var indexes = Array.Empty<int> ();
				IList<UniqueId> unmodifiedUids;
				IList<int> unmodifiedIndexes;

				unmodifiedIndexes = inbox.Store (indexes, modseq, annotations);
				Assert.That (unmodifiedIndexes, Is.Empty);

				unmodifiedUids = inbox.Store (uids, modseq, annotations);
				Assert.That (unmodifiedUids, Is.Empty);

				client.Disconnect (false);
			}
		}

		static IList<ImapReplayCommand> CreateAppendWithAnnotationsCommands (bool withInternalDates, out List<MimeMessage> messages, out List<MessageFlags> flags, out List<DateTimeOffset> internalDates, out List<Annotation> annotations)
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt")
			};

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

				command.Append ('{').Append (length.ToString (CultureInfo.InvariantCulture)).Append ("+}\r\n").Append (latin1).Append ("\r\n");
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

			var commands = CreateAppendWithAnnotationsCommands (withInternalDates, out var messages, out var flags, out var internalDates, out var annotations);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = client.Inbox.Append (messages[i], flags[i], internalDates[i], new [] { annotations[i] });
					else
						uid = client.Inbox.Append (messages[i], flags[i], null, new [] { annotations[i] });

					Assert.That (uid.HasValue, Is.True, "Expected a UIDAPPEND resp-code");
					Assert.That (uid.Value.Id, Is.EqualTo (i + 1), "Unexpected UID");

					messages[i].Dispose ();
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

			var commands = CreateAppendWithAnnotationsCommands (withInternalDates, out var messages, out var flags, out var internalDates, out var annotations);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				for (int i = 0; i < messages.Count; i++) {
					UniqueId? uid;

					if (withInternalDates)
						uid = await client.Inbox.AppendAsync (messages[i], flags[i], internalDates[i], new[] { annotations[i] });
					else
						uid = await client.Inbox.AppendAsync (messages[i], flags[i], null, new[] { annotations[i] });

					Assert.That (uid.HasValue, Is.True, "Expected a UIDAPPEND resp-code");
					Assert.That (uid.Value.Id, Is.EqualTo (i + 1), "Unexpected UID");

					messages[i].Dispose ();
				}

				await client.DisconnectAsync (true);
			}
		}

		static IList<ImapReplayCommand> CreateMultiAppendWithAnnotationsCommands (bool withInternalDates, out List<IAppendRequest> requests)
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt")
			};

			var command = new StringBuilder ("A00000004 APPEND INBOX");
			var options = FormatOptions.Default.Clone ();
			options.NewLineFormat = NewLineFormat.Dos;
			options.EnsureNewLine = true;
			int id = 5;

			requests = new List<IAppendRequest> ();

			for (int i = 0; i < 8; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				var request = new AppendRequest (message, MessageFlags.Seen);
				requests.Add (request);

				if (withInternalDates)
					request.InternalDate = message.Date;

				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties[AnnotationAttribute.PrivateValue] = string.Format ("Alternate subject {0}", i);
				request.Annotations = new Annotation[] { annotation };

				using (var stream = new MemoryStream ()) {
					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				command.Append (" (\\Seen) ");
				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (message.Date));
				command.AppendFormat ("ANNOTATION (/altsubject (value.priv \"Alternate subject {0}\")) ", i);
				command.Append ('{');
				command.AppendFormat ("{0}+", length);
				command.Append ("}\r\n");
				command.Append (latin1);
			}

			command.Append ("\r\n");
			commands.Add (new ImapReplayCommand (command.ToString (), "dovecot.multiappend.txt"));

			for (int i = 0; i < requests.Count; i++) {
				string latin1;
				long length;

				command.Clear ();
				command.AppendFormat ("A{0:D8} APPEND INBOX", id++);

				using (var stream = new MemoryStream ()) {
					requests[i].Message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				command.Append (" (\\Seen) ");
				if (withInternalDates)
					command.AppendFormat ("\"{0}\" ", ImapUtils.FormatInternalDate (requests[i].InternalDate.Value));
				command.AppendFormat ("ANNOTATION (/altsubject (value.priv \"Alternate subject {0}\")) ", i);
				command.Append ('{');
				command.AppendFormat ("{0}+", length);
				command.Append ("}\r\n");
				command.Append (latin1);
				command.Append ("\r\n");
				commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
			}

			commands.Add (new ImapReplayCommand (string.Format ("A{0:D8} LOGOUT\r\n", id), "gmail.logout.txt"));

			return commands;
		}

		[TestCase (false, TestName = "TestMultiAppendWithAnnotations")]
		[TestCase (true, TestName = "TestMultiAppendWithAnnotationsAndInternalDates")]
		public void TestMultiAppendWithAnnotations (bool withInternalDates)
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			IList<UniqueId> uids;

			var commands = CreateMultiAppendWithAnnotationsCommands (withInternalDates, out var requests);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// Use MULTIAPPEND to append some test messages
				uids = client.Inbox.Append (requests);
				Assert.That (uids, Has.Count.EqualTo (8), "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), "Unexpected UID");

				// Disable the MULTIAPPEND extension and do it again
				client.Capabilities &= ~ImapCapabilities.MultiAppend;
				uids = client.Inbox.Append (requests);

				Assert.That (uids, Has.Count.EqualTo (8), "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), "Unexpected UID");

				client.Disconnect (true);

				foreach (var request in requests)
					request.Message.Dispose ();
			}
		}

		[TestCase (false, TestName = "TestMultiAppendWithAnnotationsAsync")]
		[TestCase (true, TestName = "TestMultiAppendWithAnnotationsAndInternalDatesAsync")]
		public async Task TestMultiAppendWithAnnotationsAsync (bool withInternalDates)
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			IList<UniqueId> uids;

			var commands = CreateMultiAppendWithAnnotationsCommands (withInternalDates, out var requests);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// Use MULTIAPPEND to append some test messages
				uids = await client.Inbox.AppendAsync (requests);
				Assert.That (uids, Has.Count.EqualTo (8), "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), "Unexpected UID");

				// Disable the MULTIAPPEND extension and do it again
				client.Capabilities &= ~ImapCapabilities.MultiAppend;
				uids = await client.Inbox.AppendAsync (requests);

				Assert.That (uids, Has.Count.EqualTo (8), "Unexpected number of messages appended");

				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), "Unexpected UID");

				await client.DisconnectAsync (true);

				foreach (var request in requests)
					request.Message.Dispose ();
			}
		}

		static IList<ImapReplayCommand> CreateReplaceWithAnnotationsCommands (bool byUid, out List<ReplaceRequest> requests)
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate+replace.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt")
			};
			var command = new StringBuilder ();
			int id = 5;

			requests = new List<ReplaceRequest> ();

			for (int i = 0; i < 8; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties[AnnotationAttribute.PrivateValue] = string.Format ("Alternate subject {0}", i);
				requests.Add (new ReplaceRequest (message, MessageFlags.Seen) {
					Annotations = new Annotation[] { annotation }
				});

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

				command.AppendFormat ("{0} {1} {2} INBOX (\\Seen) ", tag, byUid ? "UID REPLACE" : "REPLACE", i + 1);
				command.AppendFormat ("ANNOTATION (/altsubject (value.priv \"Alternate subject {0}\")) ", i);

				command.Append ('{').Append (length.ToString (CultureInfo.InvariantCulture)).Append ("+}\r\n").Append (latin1).Append ("\r\n");
				commands.Add (new ImapReplayCommand (command.ToString (), string.Format ("dovecot.append.{0}.txt", i + 1)));
			}

			commands.Add (new ImapReplayCommand (string.Format ("A{0:D8} LOGOUT\r\n", id), "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestReplaceWithAnnotations ()
		{
			var commands = CreateReplaceWithAnnotationsCommands (false, out var requests);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.Inbox.Open (FolderAccess.ReadWrite);

				for (int i = 0; i < requests.Count; i++) {
					var uid = client.Inbox.Replace (i, requests[i]);

					Assert.That (uid.HasValue, Is.True, "Expected a UIDAPPEND resp-code");
					Assert.That (uid.Value.Id, Is.EqualTo (i + 1), "Unexpected UID");

					requests[i].Message.Dispose ();
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestReplaceWithAnnotationsAsync ()
		{
			var commands = CreateReplaceWithAnnotationsCommands (false, out var requests);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				await client.Inbox.OpenAsync (FolderAccess.ReadWrite);

				for (int i = 0; i < requests.Count; i++) {
					var uid = await client.Inbox.ReplaceAsync (i, requests[i]);

					Assert.That (uid.HasValue, Is.True, "Expected a UIDAPPEND resp-code");
					Assert.That (uid.Value.Id, Is.EqualTo (i + 1), "Unexpected UID");

					requests[i].Message.Dispose ();
				}

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestReplaceByUidWithAnnotations ()
		{
			var commands = CreateReplaceWithAnnotationsCommands (true, out var requests);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.Inbox.Open (FolderAccess.ReadWrite);

				for (int i = 0; i < requests.Count; i++) {
					var uid = client.Inbox.Replace (new UniqueId ((uint) i + 1), requests[i]);

					Assert.That (uid.HasValue, Is.True, "Expected a UIDAPPEND resp-code");
					Assert.That (uid.Value.Id, Is.EqualTo (i + 1), "Unexpected UID");

					requests[i].Message.Dispose ();
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestReplaceByUidWithAnnotationsAsync ()
		{
			var commands = CreateReplaceWithAnnotationsCommands (true, out var requests);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				await client.Inbox.OpenAsync (FolderAccess.ReadWrite);

				for (int i = 0; i < requests.Count; i++) {
					var uid = await client.Inbox.ReplaceAsync (new UniqueId ((uint) i + 1), requests[i]);

					Assert.That (uid.HasValue, Is.True, "Expected a UIDAPPEND resp-code");
					Assert.That (uid.Value.Id, Is.EqualTo (i + 1), "Unexpected UID");

					requests[i].Message.Dispose ();
				}

				await client.DisconnectAsync (true);
			}
		}

		static IList<ImapReplayCommand> CreateSelectAnnotateNoneCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-none.txt")
			};
		}

		[Test]
		public void TestSelectAnnotateNone ()
		{
			var commands = CreateSelectAnnotateNoneCommands ();

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.None), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.None), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSelectAnnotateNoneAsync ()
		{
			var commands = CreateSelectAnnotateNoneCommands ();

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.None), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.None), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSearchAnnotationsCommands ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-readonly.txt"),
				new ImapReplayCommand ("A00000005 UID SEARCH RETURN (ALL) ANNOTATION /comment value \"a comment\"\r\n", "dovecot.search-uids.txt")
			};

			return commands;
		}

		[Test]
		public void TestSearchAnnotations ()
		{
			var commands = CreateSearchAnnotationsCommands ();

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadOnly), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Both), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				var query = SearchQuery.AnnotationsContain (AnnotationEntry.Comment, AnnotationAttribute.Value, "a comment");
				var uids = inbox.Search (query);

				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadOnly), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Both), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				var query = SearchQuery.AnnotationsContain (AnnotationEntry.Comment, AnnotationAttribute.Value, "a comment");
				var uids = await inbox.SearchAsync (query);

				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");

				// disable ANNOTATE-EXPERIMENT-1 and try again
				client.Capabilities &= ~ImapCapabilities.Annotate;

				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SearchAsync (query));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSortAnnotationsCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate-readonly.txt"),
				new ImapReplayCommand ("A00000005 UID SORT RETURN (ALL) (ANNOTATION /altsubject value.shared) US-ASCII ALL\r\n", "dovecot.sort-by-strings.txt"),
				new ImapReplayCommand ("A00000006 UID SORT RETURN (ALL) (REVERSE ANNOTATION /altsubject value.shared) US-ASCII ALL\r\n", "dovecot.sort-by-strings.txt")
			};
		}

		[Test]
		public void TestSortAnnotations ()
		{
			var commands = CreateSortAnnotationsCommands ();

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadOnly), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Both), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				var orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Ascending);
				var uids = inbox.Sort (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");

				orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Descending);
				uids = inbox.Sort (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadOnly), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Both), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (0), "MaxAnnotationSize");

				var orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Ascending);
				var uids = await inbox.SortAsync (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");

				orderBy = new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Descending);
				uids = await inbox.SortAsync (SearchQuery.All, new OrderBy[] { orderBy });

				Assert.That (uids, Has.Count.EqualTo (14), "Unexpected number of UIDs");

				// disable ANNOTATE-EXPERIMENT-1 and try again
				client.Capabilities &= ~ImapCapabilities.Annotate;

				Assert.ThrowsAsync<NotSupportedException> (() => inbox.SortAsync (SearchQuery.All, new OrderBy[] { orderBy }));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateStoreCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+annotate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 SELECT INBOX (CONDSTORE ANNOTATE)\r\n", "common.select-inbox-annotate.txt"),
				new ImapReplayCommand ("A00000005 STORE 1 ANNOTATION (/altsubject (value.shared \"This is an alternate subject.\"))\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000006 UID STORE 1 ANNOTATION (/altsubject (value.shared \"This is an alternate subject.\"))\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000007 STORE 1 ANNOTATION (/altsubject (value.shared NIL))\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000008 UID STORE 1 ANNOTATION (/altsubject (value.shared NIL))\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000009 STORE 1 (UNCHANGEDSINCE 42) ANNOTATION (/altsubject (value.shared NIL))\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000010 UID STORE 1 (UNCHANGEDSINCE 42) ANNOTATION (/altsubject (value.shared NIL))\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000011 STORE 1 ANNOTATION (/altsubject (value.shared \"This alternate subject will cause an error.\"))\r\n", Encoding.ASCII.GetBytes ("A00000011 NO [ANNOTATE TOOBIG] Annotate failed.\r\n")),
				new ImapReplayCommand ("A00000012 UID STORE 1 ANNOTATION (/altsubject (value.shared \"This alternate subject will cause an error.\"))\r\n", Encoding.ASCII.GetBytes ("A00000012 NO [ANNOTATE TOOMANY] Annotate failed.\r\n")),
			};
		}

		[Test]
		public void TestStore ()
		{
			var commands = CreateStoreCommands ();

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties.Add (AnnotationAttribute.SharedValue, "This is an alternate subject.");

				var annotations = new [] { annotation };

				inbox.Store (0, annotations);
				inbox.Store (new UniqueId (1), annotations);

				annotation.Properties[AnnotationAttribute.SharedValue] = null;

				inbox.Store (0, annotations);
				inbox.Store (new UniqueId (1), annotations);

				inbox.Store (new[] { 0 }, 42, annotations);
				inbox.Store (new[] { new UniqueId (1) }, 42, annotations);

				annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties.Add (AnnotationAttribute.SharedValue, "This alternate subject will cause an error.");

				annotations = new[] { annotation };

				Assert.Throws<ImapCommandException> (() => inbox.Store (0, annotations));
				Assert.Throws<ImapCommandException> (() => inbox.Store (new UniqueId (1), annotations));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestStoreAsync ()
		{
			var commands = CreateStoreCommands ();

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

				Assert.That (inbox.AnnotationAccess, Is.EqualTo (AnnotationAccess.ReadWrite), "AnnotationAccess");
				Assert.That (inbox.AnnotationScopes, Is.EqualTo (AnnotationScope.Shared), "AnnotationScopes");
				Assert.That (inbox.MaxAnnotationSize, Is.EqualTo (20480), "MaxAnnotationSize");

				var annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties.Add (AnnotationAttribute.SharedValue, "This is an alternate subject.");

				var annotations = new[] { annotation };

				await inbox.StoreAsync (0, annotations);
				await inbox.StoreAsync (new UniqueId (1), annotations);

				annotation.Properties[AnnotationAttribute.SharedValue] = null;

				await inbox.StoreAsync (0, annotations);
				await inbox.StoreAsync (new UniqueId (1), annotations);

				await inbox.StoreAsync (new[] { 0 }, 42, annotations);
				await inbox.StoreAsync (new[] { new UniqueId (1) }, 42, annotations);

				annotation = new Annotation (AnnotationEntry.AltSubject);
				annotation.Properties.Add (AnnotationAttribute.SharedValue, "This alternate subject will cause an error.");

				annotations = new[] { annotation };

				Assert.ThrowsAsync<ImapCommandException> (() => inbox.StoreAsync (0, annotations));
				Assert.ThrowsAsync<ImapCommandException> (() => inbox.StoreAsync (new UniqueId (1), annotations));

				await client.DisconnectAsync (false);
			}
		}
	}
}
