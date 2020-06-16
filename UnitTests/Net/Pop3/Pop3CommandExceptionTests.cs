//
// Pop3CommandExceptionTests.cs
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

using MailKit.Net.Pop3;

namespace UnitTests.Net.Pop3 {
	[TestFixture]
	public class Pop3CommandExceptionTests
	{
		[Test]
		public void TestPop3CommandException ()
		{
			Pop3CommandException expected;

			expected = new Pop3CommandException ();
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3CommandException)formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.StatusText, ex.StatusText, "Unexpected StatusText.");
			}

			expected = new Pop3CommandException ("This is the error message.");
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3CommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.StatusText, ex.StatusText, "Unexpected StatusText.");
			}

			expected = new Pop3CommandException ("This is the error message.", new IOException ("There was an IO error."));
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3CommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.StatusText, ex.StatusText, "Unexpected StatusText.");
			}

			expected = new Pop3CommandException ("This is the error message.", "This is the status text");
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3CommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.StatusText, ex.StatusText, "Unexpected StatusText.");
			}

			expected = new Pop3CommandException ("This is the error message.", "This is the status text", new IOException ("There was an IO error."));
			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (Pop3CommandException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.StatusText, ex.StatusText, "Unexpected StatusText.");
			}
		}
	}
}
