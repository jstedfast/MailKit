//
// ProxyProtocolExceptionTests.cs
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

using MailKit.Net.Proxy;

namespace UnitTests.Net.Proxy {
	[TestFixture]
	public class ProxyProtocolExceptionTests
	{
		[Test]
		public void TestSerialization ()
		{
			var expected = new ProxyProtocolException ("Bad boys, bad boys. Whatcha gonna do?", new Exception ("InnerException"));

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ProxyProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
			}

			expected = new ProxyProtocolException ("Bad boys, bad boys. Whatcha gonna do?");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ProxyProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
			}

			expected = new ProxyProtocolException ();

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (ProxyProtocolException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
			}
		}
	}
}
