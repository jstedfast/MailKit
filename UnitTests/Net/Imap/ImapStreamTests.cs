//
// ImapStreamTests.cs
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
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapStreamTests
	{
		[Test]
		public void TestCanReadWriteSeek ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.That (stream.CanRead, Is.True);
				Assert.That (stream.CanWrite, Is.True);
				Assert.That (stream.CanSeek, Is.False);
				Assert.That (stream.CanTimeout, Is.True);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				stream.ReadTimeout = 5;
				Assert.That (stream.ReadTimeout, Is.EqualTo (5), "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.That (stream.WriteTimeout, Is.EqualTo (7), "WriteTimeout");
			}
		}

		[Test]
		public void TestRead ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("This is some random text...\r\n");
				var buffer = new byte[32];
				int n;

				Assert.Throws<ArgumentNullException> (() => stream.Read (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Read (buffer, 0, -1));

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				stream.LiteralLength = data.Length;

				stream.Mode = ImapStreamMode.Token;
				n = stream.Read (buffer, 0, buffer.Length);
				Assert.That (n, Is.EqualTo (0), "ImapStreamMode.Token");

				stream.Mode = ImapStreamMode.Literal;
				n = stream.Read (buffer, 0, buffer.Length);
				Assert.That (n, Is.EqualTo (data.Length), "ImapStreamMode.Literal");
				Assert.That (Encoding.ASCII.GetString (buffer, 0, n), Is.EqualTo ("This is some random text...\r\n"), "Read");
			}
		}

		[Test]
		public async Task TestReadAsync ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("This is some random text...\r\n");
				var buffer = new byte[32];
				int n;

				Assert.ThrowsAsync<ArgumentNullException> (async () => await stream.ReadAsync (null, 0, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, -1, buffer.Length));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await stream.ReadAsync (buffer, 0, -1));

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				stream.LiteralLength = data.Length;

				stream.Mode = ImapStreamMode.Token;
				n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.That (n, Is.EqualTo (0), "ImapStreamMode.Token");

				stream.Mode = ImapStreamMode.Literal;
				n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.That (n, Is.EqualTo (data.Length), "Read");
				Assert.That (Encoding.ASCII.GetString (buffer, 0, n), Is.EqualTo ("This is some random text...\r\n"), "Read");
			}
		}

		[Test]
		public void TestReadLine ()
		{
			var line1 = "This is a really long line..." + new string ('.', 4096) + "\r\n";
			var line2 = "And this is another line...\r\n";

			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes (line1 + line2);

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				using (var builder = new ByteArrayBuilder (64)) {
					while (!stream.ReadLine (builder, CancellationToken.None))
						;

					var text = builder.ToString ();

					Assert.That (text, Is.EqualTo (line1), "Line1");
				}

				using (var builder = new ByteArrayBuilder (64)) {
					while (!stream.ReadLine (builder, CancellationToken.None))
						;

					var text = builder.ToString ();

					Assert.That (text, Is.EqualTo (line2), "Line2");
				}
			}
		}

		[Test]
		public async Task TestReadLineAsync ()
		{
			var line1 = "This is a really long line..." + new string ('.', 4096) + "\r\n";
			var line2 = "And this is another line...\r\n";

			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes (line1 + line2);

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				using (var builder = new ByteArrayBuilder (64)) {
					while (!await stream.ReadLineAsync (builder, CancellationToken.None))
						;

					var text = builder.ToString ();

					Assert.That (text, Is.EqualTo (line1), "Line1");
				}

				using (var builder = new ByteArrayBuilder (64)) {
					while (!await stream.ReadLineAsync (builder, CancellationToken.None))
						;

					var text = builder.ToString ();

					Assert.That (text, Is.EqualTo (line2), "Line2");
				}
			}
		}

		[Test]
		public void TestReadToken ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("* atom (\\flag \"qstring\" NIL Nil nil) [] \r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				Assert.Throws<ArgumentNullException> (() => stream.UngetToken (null));

				var token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Asterisk));
				Assert.That (token.ToString (), Is.EqualTo ("'*'"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Atom));
				Assert.That (token.ToString (), Is.EqualTo ("atom"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.OpenParen));
				Assert.That (token.ToString (), Is.EqualTo ("'('"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Flag));
				Assert.That (token.ToString (), Is.EqualTo ("\\flag"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.QString));
				Assert.That (token.ToString (), Is.EqualTo ("\"qstring\""));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Nil));
				Assert.That (token.ToString (), Is.EqualTo ("NIL"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Nil));
				Assert.That (token.ToString (), Is.EqualTo ("Nil"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Nil));
				Assert.That (token.ToString (), Is.EqualTo ("nil"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.CloseParen));
				Assert.That (token.ToString (), Is.EqualTo ("')'"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.OpenBracket));
				Assert.That (token.ToString (), Is.EqualTo ("'['"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.CloseBracket));
				Assert.That (token.ToString (), Is.EqualTo ("']'"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln));
				Assert.That (token.ToString (), Is.EqualTo ("'\\n'"));

				stream.UngetToken (token);
				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln));
				Assert.That (token.ToString (), Is.EqualTo ("'\\n'"));
			}
		}

		[Test]
		public async Task TestReadTokenAsync ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("* atom (\\flag \"qstring\" NIL Nil nil) [] \r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				Assert.Throws<ArgumentNullException> (() => stream.UngetToken (null));

				var token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Asterisk));
				Assert.That (token.ToString (), Is.EqualTo ("'*'"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Atom));
				Assert.That (token.ToString (), Is.EqualTo ("atom"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.OpenParen));
				Assert.That (token.ToString (), Is.EqualTo ("'('"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Flag));
				Assert.That (token.ToString (), Is.EqualTo ("\\flag"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.QString));
				Assert.That (token.ToString (), Is.EqualTo ("\"qstring\""));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Nil));
				Assert.That (token.ToString (), Is.EqualTo ("NIL"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Nil));
				Assert.That (token.ToString (), Is.EqualTo ("Nil"));

				token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Nil));
				Assert.That (token.ToString (), Is.EqualTo ("nil"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.CloseParen));
				Assert.That (token.ToString (), Is.EqualTo ("')'"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.OpenBracket));
				Assert.That (token.ToString (), Is.EqualTo ("'['"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.CloseBracket));
				Assert.That (token.ToString (), Is.EqualTo ("']'"));

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln));
				Assert.That (token.ToString (), Is.EqualTo ("'\\n'"));

				stream.UngetToken (token);
				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln));
				Assert.That (token.ToString (), Is.EqualTo ("'\\n'"));
			}
		}

		[Test]
		public void TestReadContinuationToken ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("+ Please continue...\r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				var token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Plus));
				Assert.That (token.ToString (), Is.EqualTo ("'+'"));
			}
		}

		[Test]
		public async Task TestReadContinuationTokenAsync ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("+ Please continue...\r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				var token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Plus));
				Assert.That (token.ToString (), Is.EqualTo ("'+'"));
			}
		}

		[Test]
		public void TestReadBrokenLiteralToken ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("{4096+" + new string (' ', 4096) + "}" + new string (' ', 4096) + "\r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				var token = stream.ReadToken (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Literal));
				Assert.That (token.ToString (), Is.EqualTo ("{4096}"));
			}
		}

		[Test]
		public async Task TestReadBrokenLiteralTokenAsync ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("{4096+" + new string (' ', 4096) + "}" + new string (' ', 4096) + "\r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				var token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.That (token.Type, Is.EqualTo (ImapTokenType.Literal));
				Assert.That (token.ToString (), Is.EqualTo ("{4096}"));
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
				Assert.That (stream.Position, Is.EqualTo (0));
				Assert.That (stream.Length, Is.EqualTo (0));
			}
		}

		[Test]
		public void TestSetLength ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.SetLength (500));
			}
		}

		[Test]
		public void TestWrite ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buf1k = RandomNumberGenerator.GetBytes (1024);
				var buf4k = RandomNumberGenerator.GetBytes (4096);
				var buf9k = RandomNumberGenerator.GetBytes (9216);
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				byte[] mem;

				Assert.Throws<ArgumentNullException> (() => stream.Write (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, 0, -1));

				// Test #1: write less than 4K to make sure that ImapStream buffers it
				stream.Write (buf1k, 0, buf1k.Length);
				Assert.That (memory.Length, Is.EqualTo (0), "#1");

				// Test #2: make sure that flushing the ImapStream flushes the entire buffer out to the network
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
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buf1k = RandomNumberGenerator.GetBytes (1024);
				var buf4k = RandomNumberGenerator.GetBytes (4096);
				var buf9k = RandomNumberGenerator.GetBytes (9216);
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				byte[] mem;

				// Test #1: write less than 4K to make sure that ImapStream buffers it
				await stream.WriteAsync (buf1k, 0, buf1k.Length);
				Assert.That (memory.Length, Is.EqualTo (0), "#1");

				// Test #2: make sure that flushing the ImapStream flushes the entire buffer out to the network
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
	}
}
