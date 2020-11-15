//
// ImapStreamTests.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

using NUnit.Framework;

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
				Assert.IsTrue (stream.CanRead);
				Assert.IsTrue (stream.CanWrite);
				Assert.IsFalse (stream.CanSeek);
				Assert.IsTrue (stream.CanTimeout);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				stream.ReadTimeout = 5;
				Assert.AreEqual (5, stream.ReadTimeout, "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.AreEqual (7, stream.WriteTimeout, "WriteTimeout");
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
				Assert.AreEqual (0, n, "ImapStreamMode.Token");

				stream.Mode = ImapStreamMode.Literal;
				n = stream.Read (buffer, 0, buffer.Length);
				Assert.AreEqual (data.Length, n, "ImapStreamMode.Literal");
				Assert.AreEqual ("This is some random text...\r\n", Encoding.ASCII.GetString (buffer, 0, n), "Read");
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
				Assert.AreEqual (0, n, "ImapStreamMode.Token");

				stream.Mode = ImapStreamMode.Literal;
				n = await stream.ReadAsync (buffer, 0, buffer.Length);
				Assert.AreEqual (data.Length, n, "Read");
				Assert.AreEqual ("This is some random text...\r\n", Encoding.ASCII.GetString (buffer, 0, n), "Read");
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

				using (var memory = new MemoryStream ()) {
					while (!stream.ReadLine (memory, CancellationToken.None))
						;

					var buffer = memory.GetBuffer ();
					int n = (int) memory.Length;

					var text = Encoding.ASCII.GetString (buffer, 0, n);
					Assert.AreEqual (line1, text, "Line1");

					memory.SetLength (0);
					memory.Position = 0;

					while (!stream.ReadLine (memory, CancellationToken.None))
						;

					buffer = memory.GetBuffer ();
					n = (int) memory.Length;

					text = Encoding.ASCII.GetString (buffer, 0, n);
					Assert.AreEqual (line2, text, "Line2");
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

				using (var memory = new MemoryStream ()) {
					while (!await stream.ReadLineAsync (memory, CancellationToken.None))
						;

					var buffer = memory.GetBuffer ();
					int n = (int) memory.Length;

					var text = Encoding.ASCII.GetString (buffer, 0, n);
					Assert.AreEqual (line1, text, "Line1");

					memory.SetLength (0);
					memory.Position = 0;

					while (!await stream.ReadLineAsync (memory, CancellationToken.None))
						;

					buffer = memory.GetBuffer ();
					n = (int) memory.Length;

					text = Encoding.ASCII.GetString (buffer, 0, n);
					Assert.AreEqual (line2, text, "Line2");
				}
			}
		}

		[Test]
		public void TestReadToken ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("* atom (\\flag \"qstring\" NIL) [] \r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				Assert.Throws<ArgumentNullException> (() => stream.UngetToken (null));

				var token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Asterisk, token.Type);
				Assert.AreEqual ("'*'", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Atom, token.Type);
				Assert.AreEqual ("atom", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.OpenParen, token.Type);
				Assert.AreEqual ("'('", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Flag, token.Type);
				Assert.AreEqual ("\\flag", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.QString, token.Type);
				Assert.AreEqual ("\"qstring\"", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Nil, token.Type);
				Assert.AreEqual ("NIL", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.CloseParen, token.Type);
				Assert.AreEqual ("')'", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.OpenBracket, token.Type);
				Assert.AreEqual ("'['", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.CloseBracket, token.Type);
				Assert.AreEqual ("']'", token.ToString ());

				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Eoln, token.Type);
				Assert.AreEqual ("'\\n'", token.ToString ());

				stream.UngetToken (token);
				token = stream.ReadToken (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Eoln, token.Type);
				Assert.AreEqual ("'\\n'", token.ToString ());
			}
		}

		[Test]
		public async Task TestReadTokenAsync ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var data = Encoding.ASCII.GetBytes ("* atom (\\flag \"qstring\" NIL) [] \r\n");

				stream.Stream.Write (data, 0, data.Length);
				stream.Stream.Position = 0;

				Assert.Throws<ArgumentNullException> (() => stream.UngetToken (null));

				var token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Asterisk, token.Type);
				Assert.AreEqual ("'*'", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Atom, token.Type);
				Assert.AreEqual ("atom", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.OpenParen, token.Type);
				Assert.AreEqual ("'('", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Flag, token.Type);
				Assert.AreEqual ("\\flag", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.QString, token.Type);
				Assert.AreEqual ("\"qstring\"", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Nil, token.Type);
				Assert.AreEqual ("NIL", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.CloseParen, token.Type);
				Assert.AreEqual ("')'", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.OpenBracket, token.Type);
				Assert.AreEqual ("'['", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.CloseBracket, token.Type);
				Assert.AreEqual ("']'", token.ToString ());

				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Eoln, token.Type);
				Assert.AreEqual ("'\\n'", token.ToString ());

				stream.UngetToken (token);
				token = await stream.ReadTokenAsync (CancellationToken.None);
				Assert.AreEqual (ImapTokenType.Eoln, token.Type);
				Assert.AreEqual ("'\\n'", token.ToString ());
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
				Assert.AreEqual (ImapTokenType.Literal, token.Type);
				Assert.AreEqual ("{4096}", token.ToString ());
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
				Assert.AreEqual (ImapTokenType.Literal, token.Type);
				Assert.AreEqual ("{4096}", token.ToString ());
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
				Assert.AreEqual (0, stream.Position);
				Assert.AreEqual (0, stream.Length);
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
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				var buf1k = new byte[1024];
				var buf4k = new byte[4096];
				var buf9k = new byte[9216];
				byte[] mem;

				using (var rng = new RNGCryptoServiceProvider ()) {
					rng.GetBytes (buf1k);
					rng.GetBytes (buf4k);
					rng.GetBytes (buf9k);
				}

				Assert.Throws<ArgumentNullException> (() => stream.Write (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, 0, -1));

				// Test #1: write less than 4K to make sure that ImapStream buffers it
				stream.Write (buf1k, 0, buf1k.Length);
				Assert.AreEqual (0, memory.Length, "#1");

				// Test #2: make sure that flushing the ImapStream flushes the entire buffer out to the network
				stream.Flush ();
				Assert.AreEqual (buf1k.Length, memory.Length, "#2");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length; i++)
					Assert.AreEqual (buf1k[i], mem[i], "#2 byte[{0}]", i);
				memory.SetLength (0);

				// Test #3: write exactly 4K to make sure it passes through w/o the need to flush
				stream.Write (buf4k, 0, buf4k.Length);
				Assert.AreEqual (buf4k.Length, memory.Length, "#3");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf4k.Length; i++)
					Assert.AreEqual (buf4k[i], mem[i], "#3 byte[{0}]", i);
				memory.SetLength (0);

				// Test #4: write 1k and then write 4k, make sure that only 4k passes thru (last 1k gets buffered)
				stream.Write (buf1k, 0, buf1k.Length);
				stream.Write (buf4k, 0, buf4k.Length);
				Assert.AreEqual (4096, memory.Length, "#4");
				stream.Flush ();
				Assert.AreEqual (buf1k.Length + buf4k.Length, memory.Length, "#4");
				Array.Copy (buf1k, 0, buffer, 0, buf1k.Length);
				Array.Copy (buf4k, 0, buffer, buf1k.Length, buf4k.Length);
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length + buf4k.Length; i++)
					Assert.AreEqual (buffer[i], mem[i], "#4 byte[{0}]", i);
				memory.SetLength (0);

				// Test #5: write 9k and make sure only the first 8k goes thru (last 1k gets buffered)
				stream.Write (buf9k, 0, buf9k.Length);
				Assert.AreEqual (8192, memory.Length, "#5");
				stream.Flush ();
				Assert.AreEqual (buf9k.Length, memory.Length, "#5");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf9k.Length; i++)
					Assert.AreEqual (buf9k[i], mem[i], "#5 byte[{0}]", i);
				memory.SetLength (0);
			}
		}

		[Test]
		public async Task TestWriteAsync ()
		{
			using (var stream = new ImapStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				var buf1k = new byte[1024];
				var buf4k = new byte[4096];
				var buf9k = new byte[9216];
				byte[] mem;

				using (var rng = new RNGCryptoServiceProvider ()) {
					rng.GetBytes (buf1k);
					rng.GetBytes (buf4k);
					rng.GetBytes (buf9k);
				}

				// Test #1: write less than 4K to make sure that ImapStream buffers it
				await stream.WriteAsync (buf1k, 0, buf1k.Length);
				Assert.AreEqual (0, memory.Length, "#1");

				// Test #2: make sure that flushing the ImapStream flushes the entire buffer out to the network
				await stream.FlushAsync ();
				Assert.AreEqual (buf1k.Length, memory.Length, "#2");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length; i++)
					Assert.AreEqual (buf1k[i], mem[i], "#2 byte[{0}]", i);
				memory.SetLength (0);

				// Test #3: write exactly 4K to make sure it passes through w/o the need to flush
				await stream.WriteAsync (buf4k, 0, buf4k.Length);
				Assert.AreEqual (buf4k.Length, memory.Length, "#3");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf4k.Length; i++)
					Assert.AreEqual (buf4k[i], mem[i], "#3 byte[{0}]", i);
				memory.SetLength (0);

				// Test #4: write 1k and then write 4k, make sure that only 4k passes thru (last 1k gets buffered)
				await stream.WriteAsync (buf1k, 0, buf1k.Length);
				await stream.WriteAsync (buf4k, 0, buf4k.Length);
				Assert.AreEqual (4096, memory.Length, "#4");
				await stream.FlushAsync ();
				Assert.AreEqual (buf1k.Length + buf4k.Length, memory.Length, "#4");
				Array.Copy (buf1k, 0, buffer, 0, buf1k.Length);
				Array.Copy (buf4k, 0, buffer, buf1k.Length, buf4k.Length);
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf1k.Length + buf4k.Length; i++)
					Assert.AreEqual (buffer[i], mem[i], "#4 byte[{0}]", i);
				memory.SetLength (0);

				// Test #5: write 9k and make sure only the first 8k goes thru (last 1k gets buffered)
				await stream.WriteAsync (buf9k, 0, buf9k.Length);
				Assert.AreEqual (8192, memory.Length, "#5");
				await stream.FlushAsync ();
				Assert.AreEqual (buf9k.Length, memory.Length, "#5");
				mem = memory.GetBuffer ();
				for (int i = 0; i < buf9k.Length; i++)
					Assert.AreEqual (buf9k[i], mem[i], "#5 byte[{0}]", i);
				memory.SetLength (0);
			}
		}
	}
}
