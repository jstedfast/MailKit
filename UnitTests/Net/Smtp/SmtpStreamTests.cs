//
// SmtpStreamTests.cs
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
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpStreamTests
	{
		[Test]
		public void TestCanReadWriteSeek ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.That (stream.CanRead, Is.True);
				Assert.That (stream.CanWrite, Is.True);
				Assert.That (stream.CanSeek, Is.False);
				Assert.That (stream.CanTimeout, Is.True);
			}
		}

		[Test]
		public void TestGetSetTimeouts ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				stream.ReadTimeout = 5;
				Assert.That (stream.ReadTimeout, Is.EqualTo (5), "ReadTimeout");

				stream.WriteTimeout = 7;
				Assert.That (stream.WriteTimeout, Is.EqualTo (7), "WriteTimeout");
			}
		}

		[Test]
		public void TestRead ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = new byte[16];

				Assert.Throws<NotImplementedException> (() => stream.Read (buffer, 0, buffer.Length));
				Assert.ThrowsAsync<NotImplementedException> (async () => await stream.ReadAsync (buffer, 0, buffer.Length));
			}
		}

		[TestCase ("XXX")]
		[TestCase ("0")]
		[TestCase ("01")]
		[TestCase ("012")]
		[TestCase ("1234")]
		public void TestReadResponseInvalidStatusCode (string statusCode)
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes ($"{statusCode} This is an invalid response.\r\n");
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				Assert.Throws<SmtpProtocolException> (() => stream.ReadResponse (CancellationToken.None));
			}
		}

		static string GenerateCrossBoundaryResponse (int statusCodeUnderflow)
		{
			const string lastLine = "250 ...And this is the final line of the response.\r\n";
			var builder = new StringBuilder ();
			int lineNumber = 1;
			string line;

			do {
				line = $"250-This is line #{lineNumber++} of a really long SMTP response.\r\n";

				if (builder.Length + line.Length + 6 + statusCodeUnderflow > 4096)
					break;

				builder.Append (line);
			} while (true);

			line = "250-" + new string ('a', 4096 - builder.Length - 6 - statusCodeUnderflow) + "\r\n";
			builder.Append (line);
			builder.Append (lastLine);

			// At this point, the last line's status code (and the following <SPACE>) is just barely
			// contained within the first 4096 byte read.

			var input = builder.ToString ();

			var expected = lastLine.Substring (statusCodeUnderflow);
			var buffer2 = input.Substring (4096);

			Assert.That (buffer2, Is.EqualTo (expected));

			return input;
		}

		[TestCase (0)]
		[TestCase (1)]
		[TestCase (2)]
		[TestCase (3)]
		[TestCase (4)]
		public void TestReadResponseStatusCodeUnderflow (int underflow)
		{
			var input = GenerateCrossBoundaryResponse (underflow);
			var expected = input.Replace ("250-", "").Replace ("250 ", "").Replace ("\r\n", "\n").TrimEnd ();

			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes (input);
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				var response = stream.ReadResponse (CancellationToken.None);

				Assert.That ((int) response.StatusCode, Is.EqualTo (250));
				Assert.That (response.Response, Is.EqualTo (expected));
			}
		}

		[Test]
		public void TestReadResponseMismatchedResponseCodes ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes ("250-This is the first line of a response.\r\n340 And this is a mismatched response code.\r\n");
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				Assert.Throws<SmtpProtocolException> (() => stream.ReadResponse (CancellationToken.None));
			}
		}

		[Test]
		public void TestReadResponseLatin1Fallback ()
		{
			const string input = "250-Wikipédia est un projet d'encyclopédie collective en ligne,\r\n250-universelle, multilingue et fonctionnant sur le principe du wiki.\r\n250-Ce projet vise à offrir un contenu librement réutilisable, objectif\r\n250 et vérifiable, que chacun peut modifier et améliorer.\r\n";
			var expected = input.Replace ("250-", "").Replace ("250 ", "").Replace ("\r\n", "\n").TrimEnd ();

			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.GetEncoding (28591).GetBytes (input);
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				var response = stream.ReadResponse (CancellationToken.None);

				Assert.That ((int) response.StatusCode, Is.EqualTo (250));
				Assert.That (response.Response, Is.EqualTo (expected));
			}
		}

		[Test]
		public void TestReadResponseOver4K ()
		{
			var builder = new StringBuilder ();
			var rngData = new byte[72];

			while (builder.Length < 5120) {
				RandomNumberGenerator.Fill (rngData);

				var base64 = Convert.ToBase64String (rngData);
				builder.AppendFormat ("250-{0}\r\n", base64);
			}

			builder.Append ("250 Okay, now we're done.\r\n");

			var input = builder.ToString ();

			var expected = input.Replace ("250-", "").Replace ("250 ", "").Replace ("\r\n", "\n").TrimEnd ();

			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buffer = Encoding.ASCII.GetBytes (input);
				var dummy = (MemoryStream) stream.Stream;

				dummy.Write (buffer, 0, buffer.Length);
				dummy.Position = 0;

				var response = stream.ReadResponse (CancellationToken.None);

				Assert.That ((int) response.StatusCode, Is.EqualTo (250));
				Assert.That (response.Response, Is.EqualTo (expected));
			}
		}

		[Test]
		public void TestSeek ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.Seek (0, SeekOrigin.Begin));
				Assert.Throws<NotSupportedException> (() => stream.Position = 500);
				Assert.That (stream.Position, Is.EqualTo (0));
				Assert.That (stream.Length, Is.EqualTo (0));
			}
		}

		[Test]
		public void TestSetLength ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				Assert.Throws<NotSupportedException> (() => stream.SetLength (500));
			}
		}

		[Test]
		public void TestWrite ()
		{
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buf1k = RandomNumberGenerator.GetBytes (1024);
				var buf4k = RandomNumberGenerator.GetBytes (4096);
				var buf9k = RandomNumberGenerator.GetBytes (9216);
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				byte[] mem;

				Assert.Throws<ArgumentNullException> (() => stream.Write (null, 0, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, -1, buffer.Length));
				Assert.Throws<ArgumentOutOfRangeException> (() => stream.Write (buffer, 0, -1));

				// Test #1: write less than 4K to make sure that SmtpStream buffers it
				stream.Write (buf1k, 0, buf1k.Length);
				Assert.That (memory.Length, Is.EqualTo (0), "#1");

				// Test #2: make sure that flushing the SmtpStream flushes the entire buffer out to the network
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
			using (var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ())) {
				var buf1k = RandomNumberGenerator.GetBytes (1024);
				var buf4k = RandomNumberGenerator.GetBytes (4096);
				var buf9k = RandomNumberGenerator.GetBytes (9216);
				var memory = (MemoryStream) stream.Stream;
				var buffer = new byte[8192];
				byte [] mem;

				// Test #1: write less than 4K to make sure that SmtpStream buffers it
				await stream.WriteAsync (buf1k, 0, buf1k.Length);
				Assert.That (memory.Length, Is.EqualTo (0), "#1");

				// Test #2: make sure that flushing the SmtpStream flushes the entire buffer out to the network
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
			using var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;
			var command = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096) + "\r\n";

			stream.QueueCommand (command, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (command));
		}

		[Test]
		public async Task TestQueueReallyLongCommandAsync ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;
			var command = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096) + "\r\n";

			await stream.QueueCommandAsync (command, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (command));
		}

		[Test]
		public void TestQueueReallyLongCommandAfterShortCommand ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var shortCommand = "EHLO [192.168.1.1]\r\n";
			var longCommand = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096) + "\r\n";

			stream.QueueCommand (shortCommand, default);
			stream.QueueCommand (longCommand, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (shortCommand + longCommand));
		}

		[Test]
		public async Task TestQueueReallyLongCommandAfterShortCommandAsync ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var shortCommand = "EHLO [192.168.1.1]\r\n";
			var longCommand = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096) + "\r\n";

			await stream.QueueCommandAsync (shortCommand, default);
			await stream.QueueCommandAsync (longCommand, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (shortCommand + longCommand));
		}

		[Test]
		public void TestQueueOverflowRemainingOutputBufferCommand ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var shortCommand = "EHLO [192.168.1.1]\r\n";
			var longCommand = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096 - shortCommand.Length - 22) + "\r\n";

			stream.QueueCommand (shortCommand, default);
			stream.QueueCommand (longCommand, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (shortCommand + longCommand));
		}

		[Test]
		public async Task TestQueueOverflowRemainingOutputBufferCommandAsync ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var shortCommand = "EHLO [192.168.1.1]\r\n";
			var longCommand = "AUTH GSSAPI YIIkMgYGK" + new string ('X', 4096 - shortCommand.Length - 22) + "\r\n";

			await stream.QueueCommandAsync (shortCommand, default);
			await stream.QueueCommandAsync (longCommand, default);
			stream.Flush ();

			var actual = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

			Assert.That (actual, Is.EqualTo (shortCommand + longCommand));
		}

		[Test]
		public void TestDisconnectOnWriteException ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (throwOnWrite: true), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var command = new string ('a', 4094) + "\r\n";
			var buffer = Encoding.ASCII.GetBytes (command);

			try {
				stream.Write (buffer, 0, buffer.Length, CancellationToken.None);
				Assert.Fail ("Expected IOException to be thrown.");
			} catch (IOException) {
			}

			Assert.That (stream.IsConnected, Is.False);
		}

		[Test]
		public async Task TestDisconnectOnWriteExceptionAsync ()
		{
			using var stream = new SmtpStream (new DummyNetworkStream (throwOnWrite: true), new NullProtocolLogger ());
			var memory = (MemoryStream) stream.Stream;

			var command = new string ('a', 4094) + "\r\n";
			var buffer = Encoding.ASCII.GetBytes (command);

			try {
				await stream.WriteAsync (buffer, 0, buffer.Length, CancellationToken.None);
				Assert.Fail ("Expected IOException to be thrown.");
			} catch (IOException) {
			}

			Assert.That (stream.IsConnected, Is.False);
		}
	}
}
