//
// CompressedStreamTests.cs
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
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using MailKit;

using UnitTests.Net;

namespace UnitTests {
	[TestFixture]
	public class CompressedStreamTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				var buffer = new byte[16];

				Assert.Throws<ArgumentNullException> (() => stream.Read (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, 0, -1));
				Assert.AreEqual (0, stream.Read (buffer, 0, 0));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await stream.ReadAsync (null, 0, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, -1, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, 0, -1));

				Assert.Throws<ArgumentNullException> (() => stream.Write (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, 0, -1));
				stream.Write (buffer, 0, 0);

				Assert.ThrowsAsync<ArgumentNullException> (async () => await stream.WriteAsync (null, 0, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.WriteAsync (buffer, -1, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.WriteAsync (buffer, 0, -1));
			}
		}

		[Test]
		public void TestCanReadWriteSeek ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				Assert.IsTrue (stream.CanRead);
				Assert.IsTrue (stream.CanWrite);
				Assert.IsFalse (stream.CanSeek);
				Assert.IsTrue (stream.CanTimeout);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				stream.ReadTimeout = 5;
				Assert.AreEqual (5, stream.ReadTimeout, "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.AreEqual (7, stream.WriteTimeout, "WriteTimeout");
			}
		}

		[Test]
		public void TestReadWrite ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				string command = "A00000001 APPEND INBOX (\\Seen \\Draft) {4096+}\r\nFrom: Sample Sender <sender@sample.com>\r\nTo: Sample Recipient <recipient@sample.com>\r\nSubject: This is a test message...\r\nDate: Mon, 22 Oct 2018 18:22:56 EDT\r\nMessage-Id: <msgid@localhost.com>\r\n\r\nTesting... 1. 2. 3.\r\nTesting.\r\nOver and out.\r\n";
				var output = Encoding.ASCII.GetBytes (command);
				const int compressedLength = 221;
				var buffer = new byte[1024];
				int n;

				stream.Write (output, 0, output.Length);
				stream.Flush ();

				Assert.AreEqual (compressedLength, stream.InnerStream.Position, "Compressed output length");

				stream.InnerStream.Position = 0;

				n = stream.Read (buffer, 0, buffer.Length);
				Assert.AreEqual (output.Length, n, "Decompressed input length");

				var text = Encoding.ASCII.GetString (buffer, 0, n);
				Assert.AreEqual (command, text);
			}
		}

		[Test]
		public async Task TestReadWriteAsync ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				string command = "A00000001 APPEND INBOX (\\Seen \\Draft) {4096+}\r\nFrom: Sample Sender <sender@sample.com>\r\nTo: Sample Recipient <recipient@sample.com>\r\nSubject: This is a test message...\r\nDate: Mon, 22 Oct 2018 18:22:56 EDT\r\nMessage-Id: <msgid@localhost.com>\r\n\r\nTesting... 1. 2. 3.\r\nTesting.\r\nOver and out.\r\n";
				var output = Encoding.ASCII.GetBytes (command);
				const int compressedLength = 221;
				var buffer = new byte[1024];
				int n;

				await stream.WriteAsync (output, 0, output.Length);
				await stream.FlushAsync ();

				Assert.AreEqual (compressedLength, stream.InnerStream.Position, "Compressed output length");

				stream.InnerStream.Position = 0;

				n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.AreEqual (output.Length, n, "Decompressed input length");

				var text = Encoding.ASCII.GetString (buffer, 0, n);
				Assert.AreEqual (command, text);
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => { var x = stream.Position; });
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
			}
		}

		[Test]
		public void TestSetLength ()
		{
			using (var stream = new CompressedStream (new DummyNetworkStream ())) {
				Assert.Throws<NotSupportedException> (() => { var x = stream.Length; });
				Assert.Throws<NotSupportedException> (() => stream.SetLength (500));
			}
		}
	}
}
