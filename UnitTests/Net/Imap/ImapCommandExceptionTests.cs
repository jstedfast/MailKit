//
//
// ImapCommandExceptionTests.cs
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

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

using NUnit.Framework;

using MailKit.Net.Imap;

namespace UnitTests.Net.Imap
{
	[TestFixture]
	public class ImapCommandExceptionTests
	{
		[Test]
		public void TestImapCommandException ()
		{
			ImapCommandException expected;

			expected = new ImapCommandException (ImapCommandResponse.Ok, "This is the response text.");
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapCommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Response, ex.Response, "Unexpected Response.");
				Assert.AreEqual (expected.ResponseText, ex.ResponseText, "Unexpected ResponseText.");
			}

			expected = new ImapCommandException (ImapCommandResponse.Ok, "This is the response text.", "This is the error message.");
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapCommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Response, ex.Response, "Unexpected Response.");
				Assert.AreEqual (expected.ResponseText, ex.ResponseText, "Unexpected ResponseText.");
			}

			expected = new ImapCommandException (ImapCommandResponse.Ok, "This is the response text.", "This is the error message.", new IOException ("This is the IO error."));
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ImapCommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Response, ex.Response, "Unexpected Response.");
				Assert.AreEqual (expected.ResponseText, ex.ResponseText, "Unexpected ResponseText.");
			}
		}
	}
}
