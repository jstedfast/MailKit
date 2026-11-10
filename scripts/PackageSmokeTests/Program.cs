//
// Program.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using MimeKit;

using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Pop3;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace PackageSmokeTests {
	static class Program
	{
		static readonly string BodyText = "Package smoke test." + Environment.NewLine + ".A dot-stuffed line." + Environment.NewLine;

		public static void Require (bool condition, string message)
		{
			if (!condition)
				throw new InvalidOperationException (message);
		}

		static void CheckMessage (MimeMessage message)
		{
			Require (message.Subject == "MailKit package smoke test", "Subject did not round-trip.");
			Require (((MailboxAddress) message.From[0]).Address == "sender@example.com", "Sender did not round-trip.");
			Require (((MailboxAddress) message.To[0]).Address == "recipient@example.com", "Recipient did not round-trip.");
			Require (message.TextBody == BodyText, "Body did not round-trip.");
		}

		static async Task ExpectAsync (StreamReader reader, string expected)
		{
			var actual = await reader.ReadLineAsync ().ConfigureAwait (false);
			Require (actual == expected, $"Expected '{expected}', got '{actual}'.");
		}

		static async Task ReceiveAsync (TcpListener listener, CancellationToken cancellationToken)
		{
			using var connection = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
			using var registration = cancellationToken.Register (() => connection.Dispose ());
			using var stream = connection.GetStream ();
			using var reader = new StreamReader (stream, Encoding.UTF8, false, 1024, true);
			using var writer = new StreamWriter (stream, new UTF8Encoding (false), 1024, true) {
				NewLine = "\r\n",
				AutoFlush = true
			};

			await writer.WriteLineAsync ("220 localhost package smoke test").ConfigureAwait (false);
			await ExpectAsync (reader, "EHLO localhost").ConfigureAwait (false);
			await writer.WriteLineAsync ("250 localhost").ConfigureAwait (false);
			await ExpectAsync (reader, "MAIL FROM:<sender@example.com>").ConfigureAwait (false);
			await writer.WriteLineAsync ("250 OK").ConfigureAwait (false);
			await ExpectAsync (reader, "RCPT TO:<recipient@example.com>").ConfigureAwait (false);
			await writer.WriteLineAsync ("250 OK").ConfigureAwait (false);
			await ExpectAsync (reader, "DATA").ConfigureAwait (false);
			await writer.WriteLineAsync ("354 Send message").ConfigureAwait (false);

			var data = new StringBuilder ();
			while (true) {
				var line = await reader.ReadLineAsync ().ConfigureAwait (false);
				Require (line != null, "Connection closed before the end of DATA.");
				if (line == ".")
					break;
				if (line.StartsWith ("..", StringComparison.Ordinal))
					line = line.Substring (1);
				data.Append (line).Append ("\r\n");
				Require (data.Length <= 1024 * 1024, "SMTP DATA exceeded the smoke test's size limit.");
			}

			using (var content = new MemoryStream (Encoding.UTF8.GetBytes (data.ToString ())))
			using (var received = MimeMessage.Load (content))
				CheckMessage (received);

			await writer.WriteLineAsync ("250 Message accepted").ConfigureAwait (false);
			await ExpectAsync (reader, "QUIT").ConfigureAwait (false);
			await writer.WriteLineAsync ("221 Goodbye").ConfigureAwait (false);
		}

		static async Task SendAsync (int port, MimeMessage message, CancellationToken cancellationToken)
		{
			using var client = new SmtpClient { LocalDomain = "localhost", Timeout = 30000 };
			await client.ConnectAsync ("127.0.0.1", port, SecureSocketOptions.None, cancellationToken).ConfigureAwait (false);
			await client.SendAsync (new SendRequest (message), cancellationToken).ConfigureAwait (false);
			await client.DisconnectAsync (true, cancellationToken).ConfigureAwait (false);
		}

		static async Task Main ()
		{
			using var imap = new ImapClient ();
			using var pop3 = new Pop3Client ();
			using var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient", "recipient@example.com"));
			message.Subject = "MailKit package smoke test";
			message.Body = new TextPart ("plain") { Text = BodyText };

			using (var stream = new MemoryStream ()) {
				message.WriteTo (stream);
				stream.Position = 0;
				using var parsed = MimeMessage.Load (stream);
				CheckMessage (parsed);
			}

			var listener = new TcpListener (IPAddress.Loopback, 0);
			using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (30));
			using var registration = timeout.Token.Register (() => listener.Stop ());
			listener.Start ();
			try {
				var port = ((IPEndPoint) listener.LocalEndpoint).Port;
				await Task.WhenAll (ReceiveAsync (listener, timeout.Token), SendAsync (port, message, timeout.Token)).ConfigureAwait (false);
			} finally {
				listener.Stop ();
			}

#if WITH_CRYPTOGRAPHY
			CryptographySmokeTest.Run ();
#endif
			Console.WriteLine ("MIME round-trip and loopback SMTP passed.");
		}
	}
}
