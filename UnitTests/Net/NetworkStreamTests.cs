//
// NetworkStreamTests.cs
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
using System.Threading;
using System.Net.Sockets;

using NUnit.Framework;

using NetworkStream = MailKit.Net.NetworkStream;

namespace UnitTests.Net {
	[TestFixture]
	public class NetworkStreamTests : IDisposable
	{
		readonly Socket socket;

		public NetworkStreamTests ()
		{
			socket = new Socket (SocketType.Stream, ProtocolType.Tcp);
			socket.Connect ("www.google.com", 80);
		}

		public void Dispose ()
		{
			socket.Dispose ();
		}

		[Test]
		public void TestCanReadWriteSeekTimeout ()
		{
			using (var stream = new NetworkStream (socket, false)) {
				Assert.IsTrue (stream.CanRead, "CanRead");
				Assert.IsTrue (stream.CanWrite, "CanWrite");
				Assert.IsFalse (stream.CanSeek, "CanSeek");
				Assert.IsTrue (stream.CanTimeout, "CanTimeout");
			}
		}

		[Test]
		public void TestNotSupportedExceptions ()
		{
			using (var stream = new NetworkStream (socket, false)) {
				Assert.Throws<NotSupportedException> (() => { var x = stream.Length; });
				Assert.Throws<NotSupportedException> (() => stream.SetLength (512));
				Assert.Throws<NotSupportedException> (() => { var x = stream.Position; });
				Assert.Throws<NotSupportedException> (() => { stream.Position = 512; });
				Assert.Throws<NotSupportedException> (() => stream.Seek (512, SeekOrigin.Begin));
			}
		}

		[Test]
		public void TestTimeouts ()
		{
			using (var stream = new NetworkStream (socket, false)) {
				Assert.AreEqual (Timeout.Infinite, stream.ReadTimeout, "ReadTimeout #1");
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.ReadTimeout = 0);
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.ReadTimeout = -2);
				stream.ReadTimeout = 500;
				Assert.AreEqual (500, stream.ReadTimeout, "ReadTimeout #2");
				stream.ReadTimeout = Timeout.Infinite;
				Assert.AreEqual (Timeout.Infinite, stream.ReadTimeout, "ReadTimeout #3");

				Assert.AreEqual (Timeout.Infinite, stream.WriteTimeout, "WriteTimeout #1");
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.WriteTimeout = 0);
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.WriteTimeout = -2);
				stream.WriteTimeout = 500;
				Assert.AreEqual (500, stream.WriteTimeout, "WriteTimeout #2");
				stream.WriteTimeout = Timeout.Infinite;
				Assert.AreEqual (Timeout.Infinite, stream.WriteTimeout, "WriteTimeout #3");
			}
		}
	}
}
