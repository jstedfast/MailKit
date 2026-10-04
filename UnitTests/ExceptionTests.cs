//
// ExceptionTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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

using MailKit;
using MailKit.Net;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Net.Proxy;

namespace UnitTests {
	[TestFixture]
	public class ExceptionTests
	{
		[Test]
		public void TestFolderNotFoundException ()
		{
			var ex = new FolderNotFoundException ("Inbox");
			Assert.That (ex.FolderName, Is.EqualTo ("Inbox"), "FolderName");

			ex = new FolderNotFoundException ("This is the error message.", "Inbox");
			Assert.That (ex.Message, Is.EqualTo ("This is the error message."), "Message");
			Assert.That (ex.FolderName, Is.EqualTo ("Inbox"), "FolderName");

			var inner = new IOException ("Inner Exception");
			ex = new FolderNotFoundException ("This is the error message.", "Inbox", inner);
			Assert.That (ex.FolderName, Is.EqualTo ("Inbox"), "FolderName");
			Assert.That (ex.InnerException, Is.SameAs (inner), "InnerException");

			Assert.Throws<ArgumentNullException> (() => new FolderNotFoundException (null));
			Assert.Throws<ArgumentNullException> (() => new FolderNotFoundException ("message", null));
			Assert.Throws<ArgumentNullException> (() => new FolderNotFoundException ("message", null, new Exception ("message")));
		}

		[Test]
		public void TestFolderNotOpenException ()
		{
			var ex = new FolderNotOpenException ("Inbox", FolderAccess.ReadWrite);
			Assert.That (ex.FolderName, Is.EqualTo ("Inbox"), "FolderName");
			Assert.That (ex.FolderAccess, Is.EqualTo (FolderAccess.ReadWrite), "FolderAccess");

			ex = new FolderNotOpenException ("Inbox", FolderAccess.ReadOnly, "This is the error message.");
			Assert.That (ex.Message, Is.EqualTo ("This is the error message."), "Message");
			Assert.That (ex.FolderAccess, Is.EqualTo (FolderAccess.ReadOnly), "FolderAccess");

			var inner = new IOException ("Inner Exception");
			ex = new FolderNotOpenException ("Inbox", FolderAccess.ReadWrite, "This is the error message.", inner);
			Assert.That (ex.InnerException, Is.SameAs (inner), "InnerException");

			Assert.Throws<ArgumentNullException> (() => new FolderNotOpenException (null, FolderAccess.ReadOnly));
			Assert.Throws<ArgumentNullException> (() => new FolderNotOpenException (null, FolderAccess.ReadOnly, "message"));
			Assert.Throws<ArgumentNullException> (() => new FolderNotOpenException (null, FolderAccess.ReadOnly, "message", new Exception ("message")));
		}
		[Test]
		public void TestProtocolExceptionDefaultErrorType ()
		{
			Assert.That (new ImapProtocolException ().ErrorType, Is.EqualTo (ProtocolErrorType.Unknown), "ImapProtocolException ()");
			Assert.That (new ImapProtocolException ("message").ErrorType, Is.EqualTo (ProtocolErrorType.Unknown), "ImapProtocolException (string)");
			Assert.That (new Pop3ProtocolException ("message", new IOException ()).ErrorType, Is.EqualTo (ProtocolErrorType.Unknown), "Pop3ProtocolException (string, Exception)");
			Assert.That (new SmtpProtocolException ().ErrorType, Is.EqualTo (ProtocolErrorType.Unknown), "SmtpProtocolException ()");
			Assert.That (new ProxyProtocolException ("message").ErrorType, Is.EqualTo (ProtocolErrorType.Unknown), "ProxyProtocolException (string)");
		}

		[TestCase (ProtocolErrorType.UnexpectedDisconnect)]
		[TestCase (ProtocolErrorType.InvalidResponse)]
		[TestCase (ProtocolErrorType.ServerDisconnected)]
		[TestCase (ProtocolErrorType.ResponseTooLarge)]
		public void TestProtocolExceptionErrorType (ProtocolErrorType errorType)
		{
			var inner = new IOException ("inner");
			ProtocolException ex;

			ex = new ImapProtocolException ("message", errorType);
			Assert.That (ex.ErrorType, Is.EqualTo (errorType), "ImapProtocolException");
			Assert.That (ex.HelpLink, Is.Not.Null, "ImapProtocolException.HelpLink");

			ex = new ImapProtocolException ("message", errorType, inner);
			Assert.That (ex.ErrorType, Is.EqualTo (errorType), "ImapProtocolException (inner)");
			Assert.That (ex.InnerException, Is.SameAs (inner), "ImapProtocolException.InnerException");

			ex = new Pop3ProtocolException ("message", errorType);
			Assert.That (ex.ErrorType, Is.EqualTo (errorType), "Pop3ProtocolException");

			ex = new Pop3ProtocolException ("message", errorType, inner);
			Assert.That (ex.ErrorType, Is.EqualTo (errorType), "Pop3ProtocolException (inner)");
			Assert.That (ex.InnerException, Is.SameAs (inner), "Pop3ProtocolException.InnerException");

			ex = new SmtpProtocolException ("message", errorType);
			Assert.That (ex.ErrorType, Is.EqualTo (errorType), "SmtpProtocolException");

			ex = new SmtpProtocolException ("message", errorType, inner);
			Assert.That (ex.ErrorType, Is.EqualTo (errorType), "SmtpProtocolException (inner)");
			Assert.That (ex.InnerException, Is.SameAs (inner), "SmtpProtocolException.InnerException");
		}

		[TestCase (ProtocolErrorType.Unknown, "protocol_error")]
		[TestCase (ProtocolErrorType.UnexpectedDisconnect, "response_ended")]
		[TestCase (ProtocolErrorType.InvalidResponse, "invalid_response")]
		[TestCase (ProtocolErrorType.ServerDisconnected, "server_disconnected")]
		[TestCase (ProtocolErrorType.ResponseTooLarge, "response_too_large")]
		public void TestMetricsProtocolErrorType (ProtocolErrorType errorType, string expected)
		{
			Assert.That (ClientMetrics.TryGetErrorType (new ImapProtocolException ("message", errorType), out var value), Is.True);
			Assert.That (value, Is.EqualTo (expected));
		}

		[TestCase (null, "command_error")]
		[TestCase ("NONEXISTENT", "not_found")]
		[TestCase ("OVERQUOTA", "quota_exceeded")]
		[TestCase ("INUSE", "in_use")]
		[TestCase ("TEMPFAIL", "temporary_failure")]
		[TestCase ("SERVERBUG", "server_error")]
		[TestCase ("NOPERM", "permission_denied")]
		[TestCase ("ALREADYEXISTS", "already_exists")]
		[TestCase ("LIMIT", "limit_exceeded")]
		[TestCase ("CANNOT", "not_supported")]
		[TestCase ("CLIENTBUG", "invalid_command")]
		[TestCase ("X-UNKNOWN", "rejected")]
		public void TestMetricsCommandErrorType (string responseCode, string expected)
		{
			// Note: an OK response with no response code results in CommandErrorType.Unknown.
			var response = responseCode != null ? ImapCommandResponse.No : ImapCommandResponse.Ok;
			var ex = new ImapCommandException (response, responseCode, "response text", "message");

			Assert.That (ClientMetrics.TryGetErrorType (ex, out var value), Is.True);
			Assert.That (value, Is.EqualTo (expected));
		}

		[Test]
		public void TestMetricsSmtpCommandErrorType ()
		{
			var ex = new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "message");

			Assert.That (ClientMetrics.TryGetErrorType (ex, out var value), Is.True);
			Assert.That (value, Is.EqualTo ("550"));
		}
	}
}
