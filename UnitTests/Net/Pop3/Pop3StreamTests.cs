//
// Pop3StreamTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using System.Text;
using System.Security.Cryptography;

using MailKit;
using MailKit.Net.Pop3;

namespace UnitTests.Net.Pop3 {
	[TestFixture]
	public class Pop3StreamTests
	{
		[Test]
		public void TestCanReadWriteSeek ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.That (stream.CanRead, Is.True);
				Assert.That (stream.CanWrite, Is.True);
				Assert.That (stream.CanSeek, Is.False);
				Assert.That (stream.CanTimeout, Is.True);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				stream.ReadTimeout = 5;
				Assert.That (stream.ReadTimeout, Is.EqualTo (5), "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.That (stream.WriteTimeout, Is.EqualTo (7), "WriteTimeout");
			}
		}

		[Test]
		public void TestRead ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("+OK\r\n");
				var buffer = new byte[16];

				Assert.Throws<ArgumentNullException> (() => stream.Read (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, 0, -1));

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				stream.Mode = Pop3StreamMode.Line;
				Assert.Throws<InvalidOperationException> (() => stream.Read (buffer, 0, buffer.Length));

				stream.Mode = Pop3StreamMode.Data;
				var n = stream.Read (buffer, 0, buffer.Length);
				Assert.That (n, Is.EqualTo (data.Length), "Read");
				Assert.That (Encoding.ASCII.GetString (buffer, 0, n), Is.EqualTo ("+OK\r\n"), "Read");
			}
		}

		[Test]
		public async Task TestReadAsync ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("+OK\r\n");
				var buffer = new byte[16];

				Assert.ThrowsAsync<ArgumentNullException> (async () => await stream.ReadAsync (null, 0, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, -1, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, 0, -1));

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				stream.Mode = Pop3StreamMode.Line;
				Assert.ThrowsAsync<InvalidOperationException> (async () => await stream.ReadAsync (buffer, 0, buffer.Length));

				stream.Mode = Pop3StreamMode.Data;
				var n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.That (n, Is.EqualTo (data.Length), "Read");
				Assert.That (Encoding.ASCII.GetString (buffer, 0, n), Is.EqualTo ("+OK\r\n"), "Read");
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
				Assert.That (stream.Position, Is.EqualTo (0));
				Assert.That (stream.Length, Is.EqualTo (0));
			}
		}

		[Test]
		public void TestSetLength ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.SetLength (500));
			}
		}

		[Test]
		public void TestWrite ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buf1k = RandomNumberGenerator.GetBytes (1024);
				var buf4k = RandomNumberGenerator.GetBytes (4096);
				var buf9k = RandomNumberGenerator.GetBytes (9216);
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				byte[] mem;

				Assert.Throws<ArgumentNullException> (() => stream.Write (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, 0, -1));

				// Test #1: write less than 4K to make sure that Pop3Stream buffers it
				stream.Write (buf1k, 0, buf1k.Length);
				Assert.That (memory.Length, Is.EqualTo (0), "#1");

				// Test #2: make sure that flushing the Pop3Stream flushes the entire buffer out to the network
				stream.Flush ();
				Assert.That (memory.Length, Is.EqualTo (buf1k.Length), "#2");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buf1k[i]), $"#2 byte[{i}]");
				memory.SetLength (0);

				// Test #3: write exactly 4K to make sure it passes through w/o the need to flush
				stream.Write (buf4k, 0, buf4k.Length);
				Assert.That (memory.Length, Is.EqualTo (buf4k.Length), "#3");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf4k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buf4k[i]), $"#3 byte[{i}]");
				memory.SetLength (0);

				// Test #4: write 1k and then write 4k, make sure that only 4k passes thru (last 1k gets buffered)
				stream.Write (buf1k, 0, buf1k.Length);
				stream.Write (buf4k, 0, buf4k.Length);
				Assert.That (memory.Length, Is.EqualTo (4096), "#4");
				stream.Flush ();
				Assert.That (memory.Length, Is.EqualTo (buf1k.Length + buf4k.Length), "#4");
				Array.Copy (buf1k, 0, buffer, 0, buf1k.Length);
				Array.Copy (buf4k, 0, buffer, buf1k.Length, buf4k.Length);
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length + buf4k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buffer[i]), $"#4 byte[{i}]");
				memory.SetLength (0);

				// Test #5: write 9k and make sure only the first 8k goes thru (last 1k gets buffered)
				stream.Write (buf9k, 0, buf9k.Length);
				Assert.That (memory.Length, Is.EqualTo (8192), "#5");
				stream.Flush ();
				Assert.That (memory.Length, Is.EqualTo (buf9k.Length), "#5");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf9k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buf9k[i]), $"#5 byte[{i}]");
				memory.SetLength (0);
			}
		}

		[Test]
		public async Task TestWriteAsync ()
		{
			using (var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buf1k = RandomNumberGenerator.GetBytes (1024);
				var buf4k = RandomNumberGenerator.GetBytes (4096);
				var buf9k = RandomNumberGenerator.GetBytes (9216);
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				byte[] mem;

				// Test #1: write less than 4K to make sure that Pop3Stream buffers it
				await stream.WriteAsync (buf1k, 0, buf1k.Length);
				Assert.That (memory.Length, Is.EqualTo (0), "#1");

				// Test #2: make sure that flushing the Pop3Stream flushes the entire buffer out to the network
				await stream.FlushAsync ();
				Assert.That (memory.Length, Is.EqualTo (buf1k.Length), "#2");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buf1k[i]), $"#2 byte[{i}]");
				memory.SetLength (0);

				// Test #3: write exactly 4K to make sure it passes through w/o the need to flush
				await stream.WriteAsync (buf4k, 0, buf4k.Length);
				Assert.That (memory.Length, Is.EqualTo (buf4k.Length), "#3");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf4k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buf4k[i]), $"#3 byte[{i}]");
				memory.SetLength (0);

				// Test #4: write 1k and then write 4k, make sure that only 4k passes thru (last 1k gets buffered)
				await stream.WriteAsync (buf1k, 0, buf1k.Length);
				await stream.WriteAsync (buf4k, 0, buf4k.Length);
				Assert.That (memory.Length, Is.EqualTo (4096), "#4");
				await stream.FlushAsync ();
				Assert.That (memory.Length, Is.EqualTo (buf1k.Length + buf4k.Length), "#4");
				Array.Copy (buf1k, 0, buffer, 0, buf1k.Length);
				Array.Copy (buf4k, 0, buffer, buf1k.Length, buf4k.Length);
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length + buf4k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buffer[i]), $"#4 byte[{i}]");
				memory.SetLength (0);

				// Test #5: write 9k and make sure only the first 8k goes thru (last 1k gets buffered)
				await stream.WriteAsync (buf9k, 0, buf9k.Length);
				Assert.That (memory.Length, Is.EqualTo (8192), "#5");
				await stream.FlushAsync ();
				Assert.That (memory.Length, Is.EqualTo (buf9k.Length), "#5");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf9k.Length; i++)
					Assert.That (mem[i], Is.EqualTo (buf9k[i]), $"#5 byte[{i}]");
				memory.SetLength (0);
			}
		}

		[Test]
		public void TestQueueReallyLongCommand ()
		{
			using var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;
			var command = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096) + "\r\n";

			stream.QueueCommand (Encoding.UTF8, command, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (command));
		}

		[Test]
		public void TestQueueReallyLongCommandAfterShortCommand ()
		{
			using var stream = new Pop3Stream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var shortCommand = "CAPA\r\n";
			var longCommand = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096) + "\r\n";

			stream.QueueCommand (Encoding.UTF8, shortCommand, default);
			stream.QueueCommand (Encoding.UTF8, longCommand, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (shortCommand + longCommand));
		}
	}
}
