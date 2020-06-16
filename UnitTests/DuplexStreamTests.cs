//
// DuplexStreamTests.cs
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
using System.Threading.Tasks;

using NUnit.Framework;

using MailKit;

using UnitTests.Net;

namespace UnitTests {
	[TestFixture]
	public class DuplexStreamTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new DuplexStream (null, Stream.Null));
			Assert.Throws<ArgumentNullException> (() => new DuplexStream (Stream.Null, null));

			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				var buffer = new byte[16];

				Assert.Throws<ArgumentNullException> (() => stream.Read (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, 0, -1));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await stream.ReadAsync (null, 0, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, -1, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, 0, -1));

				Assert.Throws<ArgumentNullException> (() => stream.Write (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, 0, -1));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await stream.WriteAsync (null, 0, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.WriteAsync (buffer, -1, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.WriteAsync (buffer, 0, -1));
			}
		}

		[Test]
		public void TestCanReadWriteSeek ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				Assert.IsTrue (stream.CanRead);
				Assert.IsTrue (stream.CanWrite);
				Assert.IsFalse (stream.CanSeek);
				Assert.IsTrue (stream.CanTimeout);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				stream.ReadTimeout = 5;
				Assert.AreEqual (5, stream.ReadTimeout, "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.AreEqual (7, stream.WriteTimeout, "WriteTimeout");
			}
		}

		[Test]
		public void TestRead ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				var buffer = new byte[1024];
				int n;

				stream.InputStream.Write (buffer, 0, buffer.Length);
				stream.InputStream.Position = 0;

				n = stream.Read (buffer, 0, buffer.Length);
				Assert.AreEqual (buffer.Length, n);
			}
		}

		[Test]
		public async Task TestReadAsync ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				var buffer = new byte[1024];
				int n;

				stream.InputStream.Write (buffer, 0, buffer.Length);
				stream.InputStream.Position = 0;

				n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.AreEqual (buffer.Length, n);
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => { var x = stream.Position; });
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
			}
		}

		[Test]
		public void TestSetLength ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				Assert.Throws<NotSupportedException> (() => { var x = stream.Length; });
				Assert.Throws<NotSupportedException> (() => stream.SetLength (500));
			}
		}

		[Test]
		public void TestWrite ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				var buffer = new byte[1024];

				stream.Write (buffer, 0, buffer.Length);
				stream.Flush ();
				Assert.AreEqual (buffer.Length, stream.OutputStream.Position);
			}
		}

		[Test]
		public async Task TestWriteAsync ()
		{
			using (var stream = new DuplexStream (new DummyNetworkStream (), new DummyNetworkStream ())) {
				var buffer = new byte[1024];

				await stream.WriteAsync (buffer, 0, buffer.Length);
				await stream.FlushAsync ();
				Assert.AreEqual (buffer.Length, stream.OutputStream.Position);
			}
		}
	}
}
