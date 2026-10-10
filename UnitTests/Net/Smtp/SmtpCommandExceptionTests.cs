//
// SmtpCommandExceptionTests.cs
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

using MimeKit;

using MailKit;
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpCommandExceptionTests
	{
		[TestCase (SmtpStatusCode.MailboxBusy, CommandErrorType.InUse)]
		[TestCase (SmtpStatusCode.InsufficientStorage, CommandErrorType.QuotaExceeded)]
		[TestCase (SmtpStatusCode.ExceededStorageAllocation, CommandErrorType.QuotaExceeded)]
		[TestCase (SmtpStatusCode.ServiceNotAvailable, CommandErrorType.TemporaryFailure)]
		[TestCase (SmtpStatusCode.ErrorInProcessing, CommandErrorType.TemporaryFailure)]
		[TestCase (SmtpStatusCode.CommandUnrecognized, CommandErrorType.InvalidCommand)]
		[TestCase (SmtpStatusCode.SyntaxError, CommandErrorType.InvalidCommand)]
		[TestCase (SmtpStatusCode.BadCommandSequence, CommandErrorType.InvalidCommand)]
		[TestCase (SmtpStatusCode.CommandNotImplemented, CommandErrorType.NotSupported)]
		[TestCase (SmtpStatusCode.CommandParameterNotImplemented, CommandErrorType.NotSupported)]
		[TestCase (SmtpStatusCode.AuthenticationRequired, CommandErrorType.PermissionDenied)]
		[TestCase (SmtpStatusCode.AuthenticationMechanismTooWeak, CommandErrorType.PermissionDenied)]
		[TestCase (SmtpStatusCode.AuthenticationInvalidCredentials, CommandErrorType.PermissionDenied)]
		[TestCase (SmtpStatusCode.EncryptionRequiredForAuthenticationMechanism, CommandErrorType.PermissionDenied)]
		[TestCase (SmtpStatusCode.MailboxUnavailable, CommandErrorType.Rejected)]
		[TestCase (SmtpStatusCode.UserNotLocalTryAlternatePath, CommandErrorType.Rejected)]
		[TestCase (SmtpStatusCode.MailboxNameNotAllowed, CommandErrorType.Rejected)]
		[TestCase (SmtpStatusCode.TransactionFailed, CommandErrorType.Rejected)]
		[TestCase (SmtpStatusCode.Ok, CommandErrorType.Unknown)]
		public void TestSmtpCommandExceptionErrorType (SmtpStatusCode statusCode, CommandErrorType expected)
		{
			var ex = new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, statusCode, "message");

			Assert.That (ex.ErrorType, Is.EqualTo (expected), "ErrorType");
			Assert.That (ex.IsTransient, Is.EqualTo (expected == CommandErrorType.TemporaryFailure || expected == CommandErrorType.InUse), "IsTransient");
		}

		[Test]
		public void TestSmtpCommandExceptionParameterNotImplemented ()
		{
			var ex = new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.MailFromOrRcptToParametersNotRecognizedOrNotImplemented, "message");

			Assert.That (ex.ErrorType, Is.EqualTo (CommandErrorType.NotSupported));
		}

		[Test]
		public void TestSmtpCommandExceptionResponseConstructors ()
		{
			var response = new SmtpResponse (SmtpStatusCode.MailboxUnavailable, "5.1.1 <recipient@example.com>: Recipient address rejected");
			var mailbox = new MailboxAddress ("Recipient", "recipient@example.com");
			var inner = new IOException ("inner");
			SmtpCommandException ex;

			ex = new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpCommand.RcptTo, response, mailbox);
			Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.RecipientNotAccepted), "ErrorCode");
			Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.MailboxUnavailable), "StatusCode");
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.RcptTo), "Command");
			Assert.That (ex.ResponseText, Is.EqualTo (response.ResponseText), "ResponseText");
			Assert.That (ex.Message, Is.EqualTo (response.ResponseText), "Message");
			Assert.That (ex.Mailbox, Is.SameAs (mailbox), "Mailbox");

			ex = new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, SmtpCommand.MessageData, response, inner);
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.MessageData), "Command");
			Assert.That (ex.ResponseText, Is.EqualTo (response.ResponseText), "ResponseText");
			Assert.That (ex.InnerException, Is.SameAs (inner), "InnerException");
			Assert.That (ex.Mailbox, Is.Null, "Mailbox");

			ex = new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, SmtpCommand.Noop, response);
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.Noop), "Command");
			Assert.That (ex.ResponseText, Is.EqualTo (response.ResponseText), "ResponseText");

			ex = new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.MailboxUnavailable, "message");
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.Unknown), "Default Command");
			Assert.That (ex.ResponseText, Is.Null, "Default ResponseText");

			Assert.Throws<ArgumentNullException> (() => new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, SmtpCommand.Noop, null));
			Assert.Throws<ArgumentNullException> (() => new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, SmtpCommand.Noop, null, inner));
			Assert.Throws<ArgumentNullException> (() => new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpCommand.RcptTo, null, mailbox));
			Assert.Throws<ArgumentNullException> (() => new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, SmtpCommand.RcptTo, response, (MailboxAddress) null));
		}

		[Test]
		public void TestSmtpServiceNotAuthenticatedException ()
		{
			var response = new SmtpResponse (SmtpStatusCode.AuthenticationRequired, "5.7.0 Authentication required");
			var ex = new SmtpServiceNotAuthenticatedException (SmtpCommand.MailFrom, response);

			Assert.That (ex, Is.InstanceOf<ServiceNotAuthenticatedException> ());
			Assert.That (ex.Command, Is.EqualTo (SmtpCommand.MailFrom), "Command");
			Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.AuthenticationRequired), "StatusCode");
			Assert.That (ex.ResponseText, Is.EqualTo (response.ResponseText), "ResponseText");
			Assert.That (ex.Message, Is.EqualTo (response.ResponseText), "Message");

			Assert.Throws<ArgumentNullException> (() => new SmtpServiceNotAuthenticatedException (SmtpCommand.MailFrom, null));
		}
	}
}
