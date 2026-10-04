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
	}
}
