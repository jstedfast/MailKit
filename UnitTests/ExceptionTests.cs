//
// ExceptionTests.cs
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
using System.Runtime.Serialization.Formatters.Binary;

using NUnit.Framework;

using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace UnitTests
{
	[TestFixture]
	public class ExceptionTests
	{
		[Test]
		public void TestFolderNotFoundException ()
		{
			var expected = new FolderNotFoundException ("Inbox");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (FolderNotFoundException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.FolderName, ex.FolderName, "Unexpected FolderName.");
			}
		}

		[Test]
		public void TestFolderNotOpenException ()
		{
			var expected = new FolderNotOpenException ("Inbox", FolderAccess.ReadWrite);

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (FolderNotOpenException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.FolderName, ex.FolderName, "Unexpected FolderName.");
				Assert.AreEqual (expected.FolderAccess, ex.FolderAccess, "Unexpected FolderAcess.");
			}
		}

		[Test]
		public void TestImapCommandException ()
		{
			var expected = new ImapCommandException (ImapCommandResponse.Bad, "Bad boys, bad boys. Whatcha gonna do?", "Message", new Exception ("InnerException"));

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapCommandException)formatter.Deserialize (stream);
				Assert.AreEqual (expected.Response, ex.Response, "Unexpected Response.");
				Assert.AreEqual (expected.ResponseText, ex.ResponseText, "Unexpected ResponseText.");
			}

			expected = new ImapCommandException (ImapCommandResponse.Bad, "Bad boys, bad boys. Whatcha gonna do?", "Message");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapCommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Response, ex.Response, "Unexpected Response.");
				Assert.AreEqual (expected.ResponseText, ex.ResponseText, "Unexpected ResponseText.");
			}
		}

		[Test]
		public void TestImapProtocolException ()
		{
			var expected = new ImapProtocolException ("Bad boys, bad boys. Whatcha gonna do?", new Exception ("InnerException"));

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new ImapProtocolException ("Bad boys, bad boys. Whatcha gonna do?");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new ImapProtocolException ();

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}
		}

		[Test]
		public void TestPop3CommandException ()
		{
			var expected = new Pop3CommandException ("Message", "Bad boys, bad boys. Whatcha gonna do?");

			Assert.Throws<ArgumentNullException> (() => new Pop3CommandException ("Message", (string) null));
			Assert.Throws<ArgumentNullException> (() => new Pop3CommandException ("Message", null, new Exception ("inner")));

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3CommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.StatusText, ex.StatusText, "Unexpected StatusText.");
			}
		}

		[Test]
		public void TestPop3ProtocolException ()
		{
			var expected = new Pop3ProtocolException ("Bad boys, bad boys. Whatcha gonna do?", new Exception ("InnerException"));

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3ProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new Pop3ProtocolException ("Bad boys, bad boys. Whatcha gonna do?");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3ProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new Pop3ProtocolException ();

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3ProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}
		}

		static void TestSmtpCommandException (SmtpCommandException expected)
		{
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SmtpCommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.ErrorCode, ex.ErrorCode, "Unexpected ErrorCode.");
				Assert.AreEqual (expected.StatusCode, ex.StatusCode, "Unexpected StatusCode.");

				if (expected.Mailbox != null)
					Assert.IsTrue (expected.Mailbox.Equals (ex.Mailbox), "Unexpected Mailbox.");
				else
					Assert.IsNull (ex.Mailbox, "Expected Mailbox to be null.");
			}
		}

		[Test]
		public void TestSmtpCommandException ()
		{
			TestSmtpCommandException (new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable,
			                                                    new MailboxAddress ("Unit Tests", "example@mimekit.net"), "Message"));
			TestSmtpCommandException (new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.InsufficientStorage,
			                                                    "Message"));
		}

		[Test]
		public void TestSmtpProtocolException ()
		{
			var expected = new SmtpProtocolException ("Bad boys, bad boys. Whatcha gonna do?", new Exception ("InnerException"));

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SmtpProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new SmtpProtocolException ("Bad boys, bad boys. Whatcha gonna do?");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SmtpProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new SmtpProtocolException ();

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SmtpProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}
		}
	}
}
