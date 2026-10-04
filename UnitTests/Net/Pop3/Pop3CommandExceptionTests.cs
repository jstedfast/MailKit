//
// Pop3CommandExceptionTests.cs
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
using MailKit.Net.Pop3;

namespace UnitTests.Net.Pop3 {
	[TestFixture]
	public class Pop3CommandExceptionTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new Pop3CommandException ("Message", (string) null));
			Assert.Throws<ArgumentNullException> (() => new Pop3CommandException ("Message", null, new Exception ("inner")));
		}
		[TestCase ("[IN-USE] Mailbox is locked by another session.", "IN-USE", CommandErrorType.InUse)]
		[TestCase ("[LOGIN-DELAY] Please wait before logging in again.", "LOGIN-DELAY", CommandErrorType.TemporaryFailure)]
		[TestCase ("[SYS/TEMP] Temporary system failure.", "SYS/TEMP", CommandErrorType.TemporaryFailure)]
		[TestCase ("[sys/temp/disk] Temporary disk failure.", "SYS/TEMP/DISK", CommandErrorType.TemporaryFailure)]
		[TestCase ("[SYS/PERM] Permanent system failure.", "SYS/PERM", CommandErrorType.ServerError)]
		[TestCase ("[AUTH] Authentication failed.", "AUTH", CommandErrorType.PermissionDenied)]
		[TestCase ("[X-CUSTOM] Something else.", "X-CUSTOM", CommandErrorType.Rejected)]
		[TestCase ("No such message.", null, CommandErrorType.Rejected)]
		[TestCase ("[] Empty response code.", null, CommandErrorType.Rejected)]
		[TestCase ("[UNTERMINATED response code.", null, CommandErrorType.Rejected)]
		[TestCase ("", null, CommandErrorType.Rejected)]
		public void TestPop3CommandExceptionErrorType (string statusText, string responseCode, CommandErrorType expected)
		{
			var ex = new Pop3CommandException ("message", statusText);

			Assert.That (ex.ResponseCode, Is.EqualTo (responseCode), "ResponseCode");
			Assert.That (ex.ErrorType, Is.EqualTo (expected), "ErrorType");
		}
	}
}
