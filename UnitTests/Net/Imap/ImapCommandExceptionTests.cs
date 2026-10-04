//
// ImapCommandExceptionTests.cs
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
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapCommandExceptionTests
	{
		[TestCase ("CANNOT", CommandErrorType.NotSupported)]
		[TestCase ("UNKNOWN-CTE", CommandErrorType.NotSupported)]
		[TestCase ("BADCHARSET", CommandErrorType.NotSupported)]
		[TestCase ("BADCOMPARATOR", CommandErrorType.NotSupported)]
		[TestCase ("BADEVENT", CommandErrorType.NotSupported)]
		[TestCase ("USEATTR", CommandErrorType.NotSupported)]
		[TestCase ("CLIENTBUG", CommandErrorType.InvalidCommand)]
		[TestCase ("NOPERM", CommandErrorType.PermissionDenied)]
		[TestCase ("PRIVACYREQUIRED", CommandErrorType.PermissionDenied)]
		[TestCase ("AUTHENTICATIONFAILED", CommandErrorType.PermissionDenied)]
		[TestCase ("AUTHORIZATIONFAILED", CommandErrorType.PermissionDenied)]
		[TestCase ("EXPIRED", CommandErrorType.PermissionDenied)]
		[TestCase ("CONTACTADMIN", CommandErrorType.PermissionDenied)]
		[TestCase ("NONEXISTENT", CommandErrorType.NotFound)]
		[TestCase ("TRYCREATE", CommandErrorType.NotFound)]
		[TestCase ("UNDEFINED-FILTER", CommandErrorType.NotFound)]
		[TestCase ("BADURL", CommandErrorType.NotFound)]
		[TestCase ("ALREADYEXISTS", CommandErrorType.AlreadyExists)]
		[TestCase ("OVERQUOTA", CommandErrorType.QuotaExceeded)]
		[TestCase ("LIMIT", CommandErrorType.LimitExceeded)]
		[TestCase ("TOOBIG", CommandErrorType.LimitExceeded)]
		[TestCase ("MAXCONVERTMESSAGES", CommandErrorType.LimitExceeded)]
		[TestCase ("MAXCONVERTPARTS", CommandErrorType.LimitExceeded)]
		[TestCase ("INUSE", CommandErrorType.InUse)]
		[TestCase ("UNAVAILABLE", CommandErrorType.TemporaryFailure)]
		[TestCase ("TEMPFAIL", CommandErrorType.TemporaryFailure)]
		[TestCase ("SERVERBUG", CommandErrorType.ServerError)]
		[TestCase ("CORRUPTION", CommandErrorType.ServerError)]
		[TestCase ("X-UNKNOWN-CODE", CommandErrorType.Rejected)]
		public void TestImapCommandExceptionErrorType (string responseCode, CommandErrorType expected)
		{
			var ex = new ImapCommandException (ImapCommandResponse.No, responseCode, "response text", "message");

			Assert.That (ex.ResponseCode, Is.EqualTo (responseCode), "ResponseCode");
			Assert.That (ex.ErrorType, Is.EqualTo (expected), "ErrorType");
			Assert.That (ex.IsTransient, Is.EqualTo (expected == CommandErrorType.TemporaryFailure || expected == CommandErrorType.InUse), "IsTransient");

			ex = new ImapCommandException (ImapCommandResponse.No, responseCode, "response text", "message", new IOException ());
			Assert.That (ex.ResponseCode, Is.EqualTo (responseCode), "ResponseCode (inner)");
			Assert.That (ex.ErrorType, Is.EqualTo (expected), "ErrorType (inner)");
		}

		[TestCase (ImapCommandResponse.No, CommandErrorType.Rejected)]
		[TestCase (ImapCommandResponse.Bad, CommandErrorType.InvalidCommand)]
		[TestCase (ImapCommandResponse.Ok, CommandErrorType.Unknown)]
		[TestCase (ImapCommandResponse.None, CommandErrorType.Unknown)]
		public void TestImapCommandExceptionErrorTypeWithoutResponseCode (ImapCommandResponse response, CommandErrorType expected)
		{
			var ex = new ImapCommandException (response, "response text", "message");

			Assert.That (ex.ResponseCode, Is.Null, "ResponseCode");
			Assert.That (ex.ErrorType, Is.EqualTo (expected), "ErrorType");

			ex = new ImapCommandException (response, "response text");
			Assert.That (ex.ResponseCode, Is.Null, "ResponseCode (2 args)");
			Assert.That (ex.ErrorType, Is.EqualTo (expected), "ErrorType (2 args)");
		}
	}
}
