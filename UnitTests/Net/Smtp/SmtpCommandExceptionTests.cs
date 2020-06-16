//
// SmtpCommandExceptionTests.cs
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
using System.Runtime.Serialization.Formatters.Binary;

using NUnit.Framework;

using MimeKit;
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpCommandExceptionTests
	{
		static void TestSerialization (SmtpCommandException expected)
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
			TestSerialization (new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable,
														 new MailboxAddress ("Unit Tests", "example@mimekit.net"), "Message"));
			TestSerialization (new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable,
														 new MailboxAddress ("Unit Tests", "example@mimekit.net"), "Message",
														 new IOException ("There was an IO error.")));
			TestSerialization (new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.InsufficientStorage,
														 "Message"));
			TestSerialization (new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.InsufficientStorage,
														 "Message", new IOException ("There was an IO error.")));
		}
	}
}
