//
// SmtpClientTests.cs
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
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;

using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;
using MailKit.Net.Proxy;

using UnitTests.Net.Proxy;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpClientTests
	{
		class MyProgress : ITransferProgress
		{
			public long BytesTransferred;
			public long TotalSize;

			public void Report (long bytesTransferred, long totalSize)
			{
				BytesTransferred = bytesTransferred;
				TotalSize = totalSize;
			}

			public void Report (long bytesTransferred)
			{
				BytesTransferred = bytesTransferred;
			}
		}

		MimeMessage CreateSimpleMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body."
			};

			return message;
		}

		MimeMessage CreateBinaryMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body with some unicode unicode: ☮ ☯",
				ContentTransferEncoding = ContentEncoding.Binary
			};

			return message;
		}

		MimeMessage CreateEightBitMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body with some unicode unicode: ☮ ☯"
			};

			return message;
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			using (var client = new SmtpClient ()) {
				var credentials = new NetworkCredential ("username", "password");
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;
				var empty = new MailboxAddress[0];

				// ReplayConnect
				Assert.Throws<ArgumentNullException> (() => client.ReplayConnect (null, Stream.Null));
				Assert.Throws<ArgumentNullException> (() => client.ReplayConnect ("host", null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ReplayConnectAsync (null, Stream.Null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ReplayConnectAsync ("host", null));

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentException> (() => client.Connect (new Uri ("path", UriKind.Relative)));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (new Uri ("path", UriKind.Relative)));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 25, false));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (null, 25, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 25, false));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 25, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 25, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (null, 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 25, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				Assert.Throws<ArgumentNullException> (() => client.Connect ((Socket) null, "host", 25, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Socket) null, "host", 25, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Stream) null, "host", 25, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Stream) null, "host", 25, SecureSocketOptions.None));

				using (var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
					Assert.Throws<ArgumentException> (() => client.Connect (socket, "host", 25, SecureSocketOptions.None));
					Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "host", 25, SecureSocketOptions.None));
				}

				// Authenticate
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((SaslMechanism) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync ((SaslMechanism) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((ICredentials) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync ((ICredentials) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "password"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ("username", null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync ("username", null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, credentials));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (null, credentials));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "username", "password"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null, "password"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, "username", null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, "username", null));

				// Send
				Assert.Throws<ArgumentNullException> (() => client.Send (null));

				Assert.Throws<ArgumentNullException> (() => client.Send (null, message));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, null));

				Assert.Throws<ArgumentNullException> (() => client.Send (message, null, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (message, sender, null));
				Assert.Throws<InvalidOperationException> (() => client.Send (message, sender, empty));

				Assert.Throws<ArgumentNullException> (() => client.Send (null, message, sender, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, null, sender, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, message, null, recipients));
				Assert.Throws<ArgumentNullException> (() => client.Send (options, message, sender, null));
				Assert.Throws<InvalidOperationException> (() => client.Send (options, message, sender, empty));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (null));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (null, message));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (options, null));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (message, null, recipients));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (message, sender, null));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.SendAsync (message, sender, empty));

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (null, message, sender, recipients));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (options, null, sender, recipients));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (options, message, null, recipients));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendAsync (options, message, sender, null));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.SendAsync (options, message, sender, empty));

				// Expand
				Assert.Throws<ArgumentNullException> (() => client.Expand (null));
				Assert.Throws<ArgumentException> (() => client.Expand (string.Empty));
				Assert.Throws<ArgumentException> (() => client.Expand ("line1\r\nline2"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ExpandAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ExpandAsync (string.Empty));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ExpandAsync ("line1\r\nline2"));

				// Verify
				Assert.Throws<ArgumentNullException> (() => client.Verify (null));
				Assert.Throws<ArgumentException> (() => client.Verify (string.Empty));
				Assert.Throws<ArgumentException> (() => client.Verify ("line1\r\nline2"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.VerifyAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.VerifyAsync (string.Empty));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.VerifyAsync ("line1\r\nline2"));
			}
		}

		static void AssertDefaultValues (string host, int port, SecureSocketOptions options, Uri expected)
		{
			SmtpClient.ComputeDefaultValues (host, ref port, ref options, out Uri uri, out bool starttls);

			if (expected.PathAndQuery == "/?starttls=when-available") {
				Assert.AreEqual (SecureSocketOptions.StartTlsWhenAvailable, options, "{0}", expected);
				Assert.IsTrue (starttls, "{0}", expected);
			} else if (expected.PathAndQuery == "/?starttls=always") {
				Assert.AreEqual (SecureSocketOptions.StartTls, options, "{0}", expected);
				Assert.IsTrue (starttls, "{0}", expected);
			} else if (expected.Scheme == "smtps") {
				Assert.AreEqual (SecureSocketOptions.SslOnConnect, options, "{0}", expected);
				Assert.IsFalse (starttls, "{0}", expected);
			} else {
				Assert.AreEqual (SecureSocketOptions.None, options, "{0}", expected);
				Assert.IsFalse (starttls, "{0}", expected);
			}

			Assert.AreEqual (expected.ToString (), uri.ToString ());
			Assert.AreEqual (expected.Port, port, "{0}", expected);
		}

		[Test]
		public void TestComputeDefaultValues ()
		{
			const string host = "smtp.skyfall.net";

			AssertDefaultValues (host, 0, SecureSocketOptions.None, new Uri ($"smtp://{host}:25"));
			AssertDefaultValues (host, 25, SecureSocketOptions.None, new Uri ($"smtp://{host}:25"));
			AssertDefaultValues (host, 465, SecureSocketOptions.None, new Uri ($"smtp://{host}:465"));

			AssertDefaultValues (host, 0, SecureSocketOptions.SslOnConnect, new Uri ($"smtps://{host}:465"));
			AssertDefaultValues (host, 25, SecureSocketOptions.SslOnConnect, new Uri ($"smtps://{host}:25"));
			AssertDefaultValues (host, 465, SecureSocketOptions.SslOnConnect, new Uri ($"smtps://{host}:465"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTls, new Uri ($"smtp://{host}:25/?starttls=always"));
			AssertDefaultValues (host, 25, SecureSocketOptions.StartTls, new Uri ($"smtp://{host}:25/?starttls=always"));
			AssertDefaultValues (host, 465, SecureSocketOptions.StartTls, new Uri ($"smtp://{host}:465/?starttls=always"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 25, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 465, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"smtp://{host}:465/?starttls=when-available"));

			AssertDefaultValues (host, 0, SecureSocketOptions.Auto, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 25, SecureSocketOptions.Auto, new Uri ($"smtp://{host}:25/?starttls=when-available"));
			AssertDefaultValues (host, 465, SecureSocketOptions.Auto, new Uri ($"smtps://{host}:465"));
		}

		static Socket Connect (string host, int port)
		{
			var ipAddresses = Dns.GetHostAddresses (host);
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					socket.Connect (ipAddresses[i], port);
					break;
				} catch {
					socket.Dispose ();
					socket = null;
				}
			}

			return socket;
		}

		[Test]
		public void TestSslHandshakeExceptions ()
		{
			using (var client = new SmtpClient ()) {
				Assert.Throws<SslHandshakeException> (() => client.Connect ("www.gmail.com", 80, true));
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync ("www.gmail.com", 80, true));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.Throws<SslHandshakeException> (() => client.Connect (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));
			}
		}

		[Test]
		public void TestSyncRoot ()
		{
			using (var client = new SmtpClient ()) {
				Assert.AreEqual (client, client.SyncRoot);
			}
		}

		[Test]
		public void TestSendWithoutSenderOrRecipients ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();

				client.LocalDomain = "127.0.0.1";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				message.From.Clear ();
				message.Sender = null;
				Assert.Throws<InvalidOperationException> (() => client.Send (message));

				message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
				message.To.Clear ();
				Assert.Throws<InvalidOperationException> (() => client.Send (message));

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestSendWithoutSenderOrRecipientsAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();

				client.LocalDomain = "127.0.0.1";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				message.From.Clear ();
				message.Sender = null;
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.SendAsync (message));

				message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
				message.To.Clear ();
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.SendAsync (message));

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestInvalidStateExceptions ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;

				client.LocalDomain = "127.0.0.1";

				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				Assert.Throws<ServiceNotConnectedException> (() => client.NoOp ());

				Assert.Throws<ServiceNotConnectedException> (() => client.Send (options, message, sender, recipients));
				Assert.Throws<ServiceNotConnectedException> (() => client.Send (message, sender, recipients));
				Assert.Throws<ServiceNotConnectedException> (() => client.Send (options, message));
				Assert.Throws<ServiceNotConnectedException> (() => client.Send (message));

				Assert.Throws<ServiceNotConnectedException> (() => client.Expand ("user@example.com"));
				Assert.Throws<ServiceNotConnectedException> (() => client.Verify ("user@example.com"));

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.Throws<InvalidOperationException> (() => client.Connect ("host", 465, SecureSocketOptions.SslOnConnect));
				Assert.Throws<InvalidOperationException> (() => client.Connect ("host", 465, true));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.Throws<InvalidOperationException> (() => client.Connect (socket, "host", 465, SecureSocketOptions.SslOnConnect));

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (options, message, sender, recipients));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (message, sender, recipients));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (options, message));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.Send (message));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.Throws<InvalidOperationException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestInvalidStateExceptionsAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;

				client.LocalDomain = "127.0.0.1";

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.NoOpAsync ());

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SendAsync (options, message, sender, recipients));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SendAsync (message, sender, recipients));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SendAsync (options, message));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SendAsync (message));

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.ExpandAsync ("user@example.com"));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.VerifyAsync ("user@example.com"));

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync ("host", 465, SecureSocketOptions.SslOnConnect));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync ("host", 465, true));

				using (var socket = Connect ("www.gmail.com", 80))
					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (socket, "host", 465, SecureSocketOptions.SslOnConnect));

				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.SendAsync (options, message, sender, recipients));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.SendAsync (message, sender, recipients));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.SendAsync (options, message));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.SendAsync (message));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestConnectGMail ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "smtp.gmail.com";
			int port = 465;

			using (var client = new SmtpClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				client.Connect (host, 0, options);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public async Task TestConnectGMailAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "smtp.gmail.com";
			int port = 465;

			using (var client = new SmtpClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				await client.ConnectAsync ("smtp.gmail.com", 0, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public void TestConnectGMailViaProxy ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "smtp.gmail.com";
			int port = 465;

			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				using (var client = new SmtpClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					client.ProxyClient = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
					client.ServerCertificateValidationCallback = (s, c, h, e) => true;
					client.ClientCertificates = null;
					client.LocalEndPoint = null;
					client.Timeout = 20000;

					try {
						client.Connect (host, 0, options);
					} catch (TimeoutException) {
						Assert.Inconclusive ("Timed out.");
						return;
					} catch (Exception ex) {
						Assert.Fail (ex.Message);
					}
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public async Task TestConnectGMailViaProxyAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "smtp.gmail.com";
			int port = 465;

			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				using (var client = new SmtpClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					client.ProxyClient = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
					client.ServerCertificateValidationCallback = (s, c, h, e) => true;
					client.ClientCertificates = null;
					client.LocalEndPoint = null;
					client.Timeout = 20000;

					try {
						await client.ConnectAsync ("smtp.gmail.com", 0, SecureSocketOptions.SslOnConnect);
					} catch (TimeoutException) {
						Assert.Inconclusive ("Timed out.");
						return;
					} catch (Exception ex) {
						Assert.Fail (ex.Message);
					}
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync ("pop.gmail.com", 0, SecureSocketOptions.SslOnConnect));

					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestConnectGMailSocket ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "smtp.gmail.com";
			int port = 465;

			using (var client = new SmtpClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.Throws<ArgumentNullException> (() => client.Connect (socket, null, port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentException> (() => client.Connect (socket, "", port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect (socket, host, -1, SecureSocketOptions.Auto));

				client.Connect (socket, host, port, SecureSocketOptions.Auto);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (socket, host, port, SecureSocketOptions.Auto));

				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public async Task TestConnectGMailSocketAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "smtp.gmail.com";
			int port = 465;

			using (var client = new SmtpClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (socket, null, port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "", port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync (socket, host, -1, SecureSocketOptions.Auto));

				await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto));

				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public void TestConnectYahoo ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "smtp.mail.yahoo.com";
			var port = 587;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"smtp://{host}:{port}/?starttls=always");
					client.Connect (uri, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public async Task TestConnectYahooAsync ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "smtp.mail.yahoo.com";
			var port = 587;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"smtp://{host}:{port}/?starttls=always");
					await client.ConnectAsync (uri, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestConnectYahooSocket ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "smtp.mail.yahoo.com";
			var port = 587;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					client.Connect (socket, host, port, options, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public async Task TestConnectYahooSocketAsync ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "smtp.mail.yahoo.com";
			var port = 587;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new SmtpClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					await client.ConnectAsync (socket, host, port, options, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestSaslInitialResponse ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					client.Authenticate (new SaslMechanismPlain ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslInitialResponseAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestAuthenticationFailed ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-failed.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "auth-failed.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-failed.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.AreEqual ("535: authentication failed", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Authenticate (new SaslMechanismPlain ("username", "password"));
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.AreEqual ("535: authentication failed", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestAuthenticationFailedAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-failed.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "auth-failed.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-failed.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.AreEqual ("535: authentication failed", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password"));
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.AreEqual ("535: authentication failed", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestHeloFallback ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [IPv6:::1]\r\n", "ehlo-failed.txt"));
			commands.Add (new SmtpReplayCommand ("HELO [IPv6:::1]\r\n", "helo.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");
				Assert.AreEqual (SmtpCapabilities.None, client.Capabilities, "Capabilities");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestHeloFallbackAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [IPv6:::1]\r\n", "ehlo-failed.txt"));
			commands.Add (new SmtpReplayCommand ("HELO [IPv6:::1]\r\n", "helo.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");
				Assert.AreEqual (SmtpCapabilities.None, client.Capabilities, "Capabilities");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"));
			commands.Add (new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"));
			commands.Add (new SmtpReplayCommand ("NOOP\r\n", "comcast-noop.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				MailboxAddress vrfy = null;

				try {
					vrfy = client.Verify ("Smith");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Verify: {0}", ex);
				}

				Assert.NotNull (vrfy, "VRFY result");
				Assert.AreEqual ("Fred Smith", vrfy.Name, "VRFY name");
				Assert.AreEqual ("Smith@USC-ISIF.ARPA", vrfy.Address, "VRFY address");

				InternetAddressList expn = null;

				try {
					expn = client.Expand ("Example-People");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Expand: {0}", ex);
				}

				Assert.NotNull (expn, "EXPN result");
				Assert.AreEqual (6, expn.Count, "EXPN count");
				Assert.AreEqual ("Jon Postel", expn[0].Name, "expn[0].Name");
				Assert.AreEqual ("Postel@USC-ISIF.ARPA", ((MailboxAddress) expn[0]).Address, "expn[0].Address");
				Assert.AreEqual ("Fred Fonebone", expn[1].Name, "expn[1].Name");
				Assert.AreEqual ("Fonebone@USC-ISIQ.ARPA", ((MailboxAddress) expn[1]).Address, "expn[1].Address");
				Assert.AreEqual ("Sam Q. Smith", expn[2].Name, "expn[2].Name");
				Assert.AreEqual ("SQSmith@USC-ISIQ.ARPA", ((MailboxAddress) expn[2]).Address, "expn[2].Address");
				Assert.AreEqual ("Quincy Smith", expn[3].Name, "expn[3].Name");
				Assert.AreEqual ("USC-ISIF.ARPA", ((MailboxAddress) expn[3]).Route[0], "expn[3].Route");
				Assert.AreEqual ("Q-Smith@ISI-VAXA.ARPA", ((MailboxAddress) expn[3]).Address, "expn[3].Address");
				Assert.AreEqual ("", expn[4].Name, "expn[4].Name");
				Assert.AreEqual ("joe@foo-unix.ARPA", ((MailboxAddress) expn[4]).Address, "expn[4].Address");
				Assert.AreEqual ("", expn[5].Name, "expn[5].Name");
				Assert.AreEqual ("xyz@bar-unix.ARPA", ((MailboxAddress) expn[5]).Address, "expn[5].Address");

				try {
					client.NoOp ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in NoOp: {0}", ex);
				}

				var message = CreateSimpleMessage ();
				var options = FormatOptions.Default;

				try {
					client.Send (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Send (message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Send (options, message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Send (options, message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestBasicFunctionalityAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"));
			commands.Add (new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"));
			commands.Add (new SmtpReplayCommand ("NOOP\r\n", "comcast-noop.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				MailboxAddress vrfy = null;

				try {
					vrfy = await client.VerifyAsync ("Smith");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Verify: {0}", ex);
				}

				Assert.NotNull (vrfy, "VRFY result");
				Assert.AreEqual ("Fred Smith", vrfy.Name, "VRFY name");
				Assert.AreEqual ("Smith@USC-ISIF.ARPA", vrfy.Address, "VRFY address");

				InternetAddressList expn = null;

				try {
					expn = await client.ExpandAsync ("Example-People");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Expand: {0}", ex);
				}

				Assert.NotNull (expn, "EXPN result");
				Assert.AreEqual (6, expn.Count, "EXPN count");
				Assert.AreEqual ("Jon Postel", expn[0].Name, "expn[0].Name");
				Assert.AreEqual ("Postel@USC-ISIF.ARPA", ((MailboxAddress) expn[0]).Address, "expn[0].Address");
				Assert.AreEqual ("Fred Fonebone", expn[1].Name, "expn[1].Name");
				Assert.AreEqual ("Fonebone@USC-ISIQ.ARPA", ((MailboxAddress) expn[1]).Address, "expn[1].Address");
				Assert.AreEqual ("Sam Q. Smith", expn[2].Name, "expn[2].Name");
				Assert.AreEqual ("SQSmith@USC-ISIQ.ARPA", ((MailboxAddress) expn[2]).Address, "expn[2].Address");
				Assert.AreEqual ("Quincy Smith", expn[3].Name, "expn[3].Name");
				Assert.AreEqual ("USC-ISIF.ARPA", ((MailboxAddress) expn[3]).Route[0], "expn[3].Route");
				Assert.AreEqual ("Q-Smith@ISI-VAXA.ARPA", ((MailboxAddress) expn[3]).Address, "expn[3].Address");
				Assert.AreEqual ("", expn[4].Name, "expn[4].Name");
				Assert.AreEqual ("joe@foo-unix.ARPA", ((MailboxAddress) expn[4]).Address, "expn[4].Address");
				Assert.AreEqual ("", expn[5].Name, "expn[5].Name");
				Assert.AreEqual ("xyz@bar-unix.ARPA", ((MailboxAddress) expn[5]).Address, "expn[5].Address");

				try {
					await client.NoOpAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in NoOp: {0}", ex);
				}

				var message = CreateSimpleMessage ();
				var options = FormatOptions.Default;

				try {
					await client.SendAsync (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.SendAsync (message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.SendAsync (options, message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.SendAsync (options, message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestSaslAuthentication ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (new Uri ("smtp://localhost"), credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslAuthenticationAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"));
			commands.Add (new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"));
			commands.Add (new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (new Uri ("smtp://localhost"), credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestEightBitMime ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateEightBitMessage ());
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestEightBitMimeAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (CreateEightBitMessage ());
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestInternationalMailboxes ()
		{
			var mailbox = new MailboxAddress (string.Empty, "úßerñame@example.com");
			var addrspec = MailboxAddress.EncodeAddrspec (mailbox.Address);

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+smtputf8.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ($"MAIL FROM:<{mailbox.Address}> SMTPUTF8 BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ($"RCPT TO:<{mailbox.Address}>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ($"MAIL FROM:<{addrspec}> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ($"RCPT TO:<{addrspec}>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.UTF8), "Failed to detect SMTPUTF8 extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				var message = CreateEightBitMessage ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (message, mailbox, new MailboxAddress[] { mailbox });
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				// Disable SMTPUTF8
				client.Capabilities &= ~SmtpCapabilities.UTF8;

				try {
					client.Send (message, mailbox, new MailboxAddress[] { mailbox });
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestInternationalMailboxesAsync ()
		{
			var mailbox = new MailboxAddress (string.Empty, "úßerñame@example.com");
			var addrspec = MailboxAddress.EncodeAddrspec (mailbox.Address);

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+smtputf8.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ($"MAIL FROM:<{mailbox.Address}> SMTPUTF8 BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ($"RCPT TO:<{mailbox.Address}>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ($"MAIL FROM:<{addrspec}> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ($"RCPT TO:<{addrspec}>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.UTF8), "Failed to detect SMTPUTF8 extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				var message = CreateEightBitMessage ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (message, mailbox, new MailboxAddress[] { mailbox });
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				// Disable SMTPUTF8
				client.Capabilities &= ~SmtpCapabilities.UTF8;

				try {
					await client.SendAsync (message, mailbox, new MailboxAddress[] { mailbox });
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		static long Measure (MimeMessage message)
		{
			var options = FormatOptions.Default.Clone ();

			options.NewLineFormat = NewLineFormat.Dos;
			options.EnsureNewLine = true;

			using (var measure = new MeasuringStream ()) {
				message.WriteTo (options, measure);
				return measure.Length;
			}
		}

		[TestCase (false, TestName = "TestBinaryMimeNoProgress")]
		[TestCase (true, TestName = "TestBinaryMimeWithProgress")]
		public void TestBinaryMime (bool showProgress)
		{
			var message = CreateBinaryMessage ();
			var size = Measure (message);
			string bdat;

			using (var memory = new MemoryStream ()) {
				var options = FormatOptions.Default.Clone ();

				options.NewLineFormat = NewLineFormat.Dos;
				options.EnsureNewLine = true;

				var bytes = Encoding.ASCII.GetBytes (string.Format ("BDAT {0} LAST\r\n", size));
				memory.Write (bytes, 0, bytes.Length);
				message.WriteTo (options, memory);

				bytes = memory.GetBuffer ();

				bdat = Encoding.UTF8.GetString (bytes, 0, (int) memory.Length);
			}

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+binarymime.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=BINARYMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand (bdat, "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime), "Failed to detect BINARYMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Chunking), "Failed to detect CHUNKING extension");

				try {
					if (showProgress) {
						var progress = new MyProgress ();

						client.Send (message, progress: progress);

						Assert.AreEqual (size, progress.BytesTransferred, "BytesTransferred");
					} else {
						client.Send (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestBinaryMimeAsyncNoProgress")]
		[TestCase (true, TestName = "TestBinaryMimeAsyncWithProgress")]
		public async Task TestBinaryMimeAsync (bool showProgress)
		{
			var message = CreateBinaryMessage ();
			var size = Measure (message);
			string bdat;

			using (var memory = new MemoryStream ()) {
				var options = FormatOptions.Default.Clone ();

				options.NewLineFormat = NewLineFormat.Dos;
				options.EnsureNewLine = true;

				var bytes = Encoding.ASCII.GetBytes (string.Format ("BDAT {0} LAST\r\n", size));
				memory.Write (bytes, 0, bytes.Length);
				message.WriteTo (options, memory);

				bytes = memory.GetBuffer ();

				bdat = Encoding.UTF8.GetString (bytes, 0, (int) memory.Length);
			}

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+binarymime.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=BINARYMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand (bdat, "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime), "Failed to detect BINARYMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Chunking), "Failed to detect CHUNKING extension");

				try {
					if (showProgress) {
						var progress = new MyProgress ();

						await client.SendAsync (message, progress: progress);

						Assert.AreEqual (size, progress.BytesTransferred, "BytesTransferred");
					} else {
						await client.SendAsync (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestPipeliningNoProgress")]
		[TestCase (true, TestName = "TestPipeliningWithProgress")]
		public void TestPipelining (bool showProgress)
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+pipelining.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mail-from-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					var message = CreateEightBitMessage ();

					if (showProgress) {
						var progress = new MyProgress ();

						client.Send (message, progress: progress);

						Assert.AreEqual (Measure (message), progress.BytesTransferred, "BytesTransferred");
					} else {
						client.Send (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestPipeliningAsyncNoProgress")]
		[TestCase (true, TestName = "TestPipeliningAsyncWithProgress")]
		public async Task TestPipeliningAsync (bool showProgress)
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+pipelining.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mail-from-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					var message = CreateEightBitMessage ();

					if (showProgress) {
						var progress = new MyProgress ();

						await client.SendAsync (message, progress: progress);

						Assert.AreEqual (Measure (message), progress.BytesTransferred, "BytesTransferred");
					} else {
						await client.SendAsync (message);
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestMailFromMailboxUnavailable ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.SenderNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestMailFromMailboxUnavailableAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.SenderNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestRcptToMailboxUnavailable ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.RecipientNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestRcptToMailboxUnavailableAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.SendAsync (CreateSimpleMessage ());
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.RecipientNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestUnauthorizedAccessException ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Send (CreateSimpleMessage ());
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestUnauthorizedAccessExceptionAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.SendAsync (CreateSimpleMessage ());
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		class DsnSmtpClient : SmtpClient
		{
			public DsnSmtpClient ()
			{
				DeliveryStatusNotificationType = DeliveryStatusNotificationType.HeadersOnly;
			}

			protected override string GetEnvelopeId (MimeMessage message)
			{
				var id = base.GetEnvelopeId (message);

				Assert.IsNull (id);

				return message.MessageId;
			}

			protected override DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
			{
				var notify = base.GetDeliveryStatusNotifications (message, mailbox);

				Assert.IsFalse (notify.HasValue);

				return DeliveryStatusNotification.Delay | DeliveryStatusNotification.Failure | DeliveryStatusNotification.Success;
			}
		}

		[Test]
		public void TestDeliveryStatusNotification ()
		{
			var message = CreateEightBitMessage ();
			message.MessageId = MimeUtils.GenerateMessageId ();

			var mailFrom = string.Format ("MAIL FROM:<sender@example.com> BODY=8BITMIME ENVID={0} RET=HDRS\r\n", message.MessageId);

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+dsn.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand (mailFrom, "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com> NOTIFY=SUCCESS,FAILURE,DELAY\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new DsnSmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), "Failed to detect DSN extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable pipelining
				client.Capabilities &= ~SmtpCapabilities.Pipelining;

				try {
					client.Send (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestDeliveryStatusNotificationAsync ()
		{
			var message = CreateEightBitMessage ();
			message.MessageId = MimeUtils.GenerateMessageId ();

			var mailFrom = string.Format ("MAIL FROM:<sender@example.com> BODY=8BITMIME ENVID={0} RET=HDRS\r\n", message.MessageId);

			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+dsn.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand (mailFrom, "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com> NOTIFY=SUCCESS,FAILURE,DELAY\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new DsnSmtpClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), "Failed to detect DSN extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), "Failed to detect PIPELINING extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");
				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// disable pipelining
				client.Capabilities &= ~SmtpCapabilities.Pipelining;

				try {
					await client.SendAsync (message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		class CustomSmtpClient : SmtpClient
		{
			public SmtpResponse SendCommand (string command)
			{
				return SendCommand (command, CancellationToken.None);
			}

			public Task<SmtpResponse> SendCommandAsync (string command)
			{
				return SendCommandAsync (command, CancellationToken.None);
			}
		}

		[Test]
		public void TestCustomCommand ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"));
			commands.Add (new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"));

			using (var client = new CustomSmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				Assert.Throws<ServiceNotConnectedException> (() => client.SendCommand ("COMMAND"));

				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				Assert.Throws<ArgumentNullException> (() => client.SendCommand (null));

				SmtpResponse response = null;

				try {
					response = client.SendCommand ("VRFY Smith");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Verify: {0}", ex);
				}

				Assert.NotNull (response, "VRFY result");
				Assert.AreEqual (SmtpStatusCode.Ok, response.StatusCode, "VRFY response code");
				Assert.AreEqual ("Fred Smith <Smith@USC-ISIF.ARPA>", response.Response, "VRFY response");

				try {
					response = client.SendCommand ("EXPN Example-People");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Expand: {0}", ex);
				}

				Assert.NotNull (response, "EXPN result");
				Assert.AreEqual (SmtpStatusCode.Ok, response.StatusCode, "EXPN response code");
				Assert.AreEqual ("Jon Postel <Postel@USC-ISIF.ARPA>\nFred Fonebone <Fonebone@USC-ISIQ.ARPA>\nSam Q. Smith <SQSmith@USC-ISIQ.ARPA>\nQuincy Smith <@USC-ISIF.ARPA:Q-Smith@ISI-VAXA.ARPA>\n<joe@foo-unix.ARPA>\n<xyz@bar-unix.ARPA>", response.Response, "EXPN response");
			}
		}

		[Test]
		public async Task TestCustomCommandAsync ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"));
			commands.Add (new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"));

			using (var client = new CustomSmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SendCommandAsync ("COMMAND"));

				try {
					await client.ReplayConnectAsync ("localhost", new SmtpReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendCommandAsync (null));

				SmtpResponse response = null;

				try {
					response = await client.SendCommandAsync ("VRFY Smith");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Verify: {0}", ex);
				}

				Assert.NotNull (response, "VRFY result");
				Assert.AreEqual (SmtpStatusCode.Ok, response.StatusCode, "VRFY response code");
				Assert.AreEqual ("Fred Smith <Smith@USC-ISIF.ARPA>", response.Response, "VRFY response");

				try {
					response = await client.SendCommandAsync ("EXPN Example-People");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Expand: {0}", ex);
				}

				Assert.NotNull (response, "EXPN result");
				Assert.AreEqual (SmtpStatusCode.Ok, response.StatusCode, "EXPN response code");
				Assert.AreEqual ("Jon Postel <Postel@USC-ISIF.ARPA>\nFred Fonebone <Fonebone@USC-ISIQ.ARPA>\nSam Q. Smith <SQSmith@USC-ISIQ.ARPA>\nQuincy Smith <@USC-ISIF.ARPA:Q-Smith@ISI-VAXA.ARPA>\n<joe@foo-unix.ARPA>\n<xyz@bar-unix.ARPA>", response.Response, "EXPN response");
			}
		}
	}
}
