//
// SslHandshakeExceptionTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2019 Xamarin Inc. (www.xamarin.com)
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

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SslHandshakeExceptionTests
	{
		const string HelpLink = "https://github.com/jstedfast/MailKit/blob/master/FAQ.md#SslHandshakeException";

		[Test]
		public void TestSerialization ()
		{
			var expected = new SslHandshakeException ("Bad boys, bad boys. Whatcha gonna do?", new IOException ("I/O Error."));

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new SslHandshakeException ("Bad boys, bad boys. Whatcha gonna do?");

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = new SslHandshakeException ();

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (new AggregateException ("Aggregate errors.", new IOException (), new IOException ()), false);

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (new AggregateException ("Aggregate errors.", new IOException (), new IOException ()), true);

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (new AggregateException ("Aggregate errors.", new IOException ()), false);

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (new AggregateException ("Aggregate errors.", new IOException ()), true);

			Assert.AreEqual (HelpLink, expected.HelpLink, "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.AreEqual (expected.Message, ex.Message, "Unexpected Message.");
				Assert.AreEqual (expected.HelpLink, ex.HelpLink, "Unexpected HelpLink.");
			}
		}
	}
}
