//
// SmtpClientTests.cs
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

using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;

using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;
using MailKit.Net.Proxy;

using UnitTests.Security;
using UnitTests.Net.Proxy;

using AuthenticationException = MailKit.Security.AuthenticationException;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpClientTests
	{
		const CipherAlgorithmType YahooCipherAlgorithm = CipherAlgorithmType.Aes128;
		const int YahooCipherStrength = 128;
#if !MONO
		const HashAlgorithmType YahooHashAlgorithm = HashAlgorithmType.Sha256;
#else
		const HashAlgorithmType YahooHashAlgorithm = HashAlgorithmType.None;
#endif
		const ExchangeAlgorithmType EcdhEphemeral = (ExchangeAlgorithmType) 44550;

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

		static MimeMessage CreateSimpleMessage ()
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

		static MimeMessage CreateBinaryMessage ()
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

		static MimeMessage CreateEightBitMessage ()
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
				var empty = Array.Empty<MailboxAddress> ();
				var options = FormatOptions.Default;

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

				message.Dispose ();
			}
		}

		[Test]
		public void TestGetSafeHostName ()
		{
			string safe;

			safe = SmtpClient.GetSafeHostName (null);
			Assert.That (safe, Is.Null);

			safe = SmtpClient.GetSafeHostName ("domain.com");
			Assert.That (safe, Is.EqualTo ("domain.com"));

			safe = SmtpClient.GetSafeHostName ("underscore_domain.com");
			Assert.That (safe, Is.EqualTo ("underscore-domain.com"));

			safe = SmtpClient.GetSafeHostName ("名がドメイン.com");
			Assert.That (safe, Is.EqualTo ("xn--v8jxj3d1dzdz08w.com"));

			var toolong = new string ('a', 256) + '.' + new string ('b', 256) + '.' + "com";
			safe = SmtpClient.GetSafeHostName (toolong);
			Assert.That (safe, Is.EqualTo (toolong));
		}

		static void AssertDefaultValues (string host, int port, SecureSocketOptions options, Uri expected)
		{
			SmtpClient.ComputeDefaultValues (host, ref port, ref options, out Uri uri, out bool starttls);

			if (expected.PathAndQuery == "/?starttls=when-available") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.StartTlsWhenAvailable), $"{expected}");
				Assert.That (starttls, Is.True, $"{expected}");
			} else if (expected.PathAndQuery == "/?starttls=always") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.StartTls), $"{expected}");
				Assert.That (starttls, Is.True, $"{expected}");
			} else if (expected.Scheme == "smtps") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.SslOnConnect), $"{expected}");
				Assert.That (starttls, Is.False, $"{expected}");
			} else {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.None), $"{expected}");
				Assert.That (starttls, Is.False, $"{expected}");
			}

			Assert.That (uri.ToString (), Is.EqualTo (expected.ToString ()));
			Assert.That (port, Is.EqualTo (expected.Port), $"{expected}");
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
				Socket socket;

				// 1. Test connecting to a non-SSL port fails with an SslHandshakeException.
				Assert.Throws<SslHandshakeException> (() => client.Connect ("www.gmail.com", 80, true));

				socket = Connect ("www.gmail.com", 80);
				Assert.Throws<SslHandshakeException> (() => client.Connect (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				// 2. Test connecting to a server with a bad SSL certificate fails with an SslHandshakeException.
				try {
					client.Connect ("untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}

				try {
					socket = Connect ("untrusted-root.badssl.com", 443);
					client.Connect (socket, "untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestSslHandshakeExceptionsAsync ()
		{
			using (var client = new SmtpClient ()) {
				Socket socket;

				// 1. Test connecting to a non-SSL port fails with an SslHandshakeException.
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync ("www.gmail.com", 80, true));

				socket = Connect ("www.gmail.com", 80);
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				// 2. Test connecting to a server with a bad SSL certificate fails with an SslHandshakeException.
				try {
					await client.ConnectAsync ("untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}

				try {
					socket = Connect ("untrusted-root.badssl.com", 443);
					await client.ConnectAsync (socket, "untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public void TestSyncRoot ()
		{
			using (var client = new SmtpClient ()) {
				Assert.That (client.SyncRoot, Is.EqualTo (client));
			}
		}

		[Test]
		public void TestLocalDomainIPv4 ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "127.0.0.1";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public void TestLocalDomainIPv6 ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO [IPv6:::1]\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public void TestLocalDomainIPv4MappedToIPv6 ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO [129.144.52.38]\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::FFFF:129.144.52.38";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.Disconnect (true);
			}
		}

		static List<SmtpReplayCommand> CreateSendWithoutSenderOrRecipientsCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestSendWithoutSenderOrRecipients ()
		{
			var commands = CreateSendWithoutSenderOrRecipientsCommands ();

			using (var client = new SmtpClient ()) {
					try {
						client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

				using (var message = CreateSimpleMessage ()) {
					message.From.Clear ();
					message.Sender = null;
					Assert.Throws<InvalidOperationException> (() => client.Send (message));

					message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
					message.To.Clear ();
					Assert.Throws<InvalidOperationException> (() => client.Send (message));
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestSendWithoutSenderOrRecipientsAsync ()
		{
			var commands = CreateSendWithoutSenderOrRecipientsCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				using (var message = CreateSimpleMessage ()) {
					message.From.Clear ();
					message.Sender = null;
					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.SendAsync (message));

					message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
					message.To.Clear ();
					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.SendAsync (message));
				}

				await client.DisconnectAsync (true);
			}
		}

		static List<SmtpReplayCommand> CreateInvalidStateExceptionsCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "auth-required.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestInvalidStateExceptions ()
		{
			var commands = CreateInvalidStateExceptionsCommands ();

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;

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
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.Throws<InvalidOperationException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				client.Disconnect (true);

				message.Dispose ();
			}
		}

		[Test]
		public async Task TestInvalidStateExceptionsAsync ()
		{
			var commands = CreateInvalidStateExceptionsCommands ();

			using (var client = new SmtpClient ()) {
				var message = CreateSimpleMessage ();
				var sender = message.From.Mailboxes.FirstOrDefault ();
				var recipients = message.To.Mailboxes.ToList ();
				var options = FormatOptions.Default;

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
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				await client.DisconnectAsync (true);

				message.Dispose ();
			}
		}

		[Test]
		public void TestStartTlsNotSupported ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "ehlo-failed.txt"),
				new SmtpReplayCommand ("HELO unit-tests.mimekit.org\r\n", "helo.txt"),
			};

			using (var client = new SmtpClient () { LocalDomain = "unit-tests.mimekit.org" })
				Assert.Throws<NotSupportedException> (() => client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.StartTls), "STARTTLS");

			using (var client = new SmtpClient () { LocalDomain = "unit-tests.mimekit.org" })
				Assert.ThrowsAsync<NotSupportedException> (() => client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.StartTls), "STARTTLS Async");
		}

		[Test]
		public void TestServiceNotReady ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "greeting-not-ready.txt")
			};

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					Assert.Fail ("Connect is expected to fail.");
				} catch (SmtpCommandException ex) {
					Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.UnexpectedStatusCode), "ErrorCode");
					Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.ServiceClosingTransmissionChannel), "StatusCode");
					Assert.That (ex.Message, Is.EqualTo ("ESMTP server not ready"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception: {ex}");
				}
			}
		}

		[Test]
		public async Task TestServiceNotReadyAsync ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "greeting-not-ready.txt")
			};

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
					Assert.Fail ("Connect is expected to fail.");
				} catch (SmtpCommandException ex) {
					Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.UnexpectedStatusCode), "ErrorCode");
					Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.ServiceClosingTransmissionChannel), "StatusCode");
					Assert.That (ex.Message, Is.EqualTo ("ESMTP server not ready"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception: {ex}");
				}
			}
		}

		[Test]
		public void TestProtocolLoggerExceptions ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
			};

			using (var client = new SmtpClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogConnect)))
				Assert.Throws<NotImplementedException> (() => client.Connect (Stream.Null, "smtp.gmail.com", 587, SecureSocketOptions.None), "LogConnect");

			using (var client = new SmtpClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogConnect)))
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (Stream.Null, "smtp.gmail.com", 587, SecureSocketOptions.None), "LogConnect Async");

			using (var client = new SmtpClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogServer)) { LocalDomain = "unit-tests.mimekit.org" })
				Assert.Throws<NotImplementedException> (() => client.Connect (new SmtpReplayStream (commands, false), "smtp.gmail.com", 587, SecureSocketOptions.None), "LogServer");

			using (var client = new SmtpClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogServer)) { LocalDomain = "unit-tests.mimekit.org" })
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (new SmtpReplayStream (commands, true), "smtp.gmail.com", 587, SecureSocketOptions.None), "LogServer Async");

			using (var client = new SmtpClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogClient)) { LocalDomain = "unit-tests.mimekit.org" })
				Assert.Throws<NotImplementedException> (() => client.Connect (new SmtpReplayStream (commands, false), "smtp.gmail.com", 587, SecureSocketOptions.None), "LogClient");

			using (var client = new SmtpClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogClient)) { LocalDomain = "unit-tests.mimekit.org" })
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (new SmtpReplayStream (commands, true), "smtp.gmail.com", 587, SecureSocketOptions.None), "LogClient Async");
		}

		static void AssertGMailIsConnected (IMailService client)
		{
			Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
			Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
			Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
			Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
			Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
			Assert.That (client.SslCipherAlgorithm == CipherAlgorithmType.Aes128 || client.SslCipherAlgorithm == CipherAlgorithmType.Aes256, Is.True, $"Unexpected SslCipherAlgorithm: {client.SslCipherAlgorithm}");
			Assert.That (client.SslCipherStrength == 128 || client.SslCipherStrength == 256, Is.True, $"Unexpected SslCipherStrength: {client.SslCipherStrength}");
#if !MONO
			Assert.That (client.SslCipherSuite == TlsCipherSuite.TLS_AES_128_GCM_SHA256 || client.SslCipherSuite == TlsCipherSuite.TLS_AES_256_GCM_SHA384, Is.True, $"Unexpected SslCipherSuite: {client.SslCipherSuite}");
			Assert.That (client.SslHashAlgorithm == HashAlgorithmType.Sha256 || client.SslHashAlgorithm == HashAlgorithmType.Sha384, Is.True, $"Unexpected SslHashAlgorithm: {client.SslHashAlgorithm}");
#else
			Assert.That (client.SslHashAlgorithm == HashAlgorithmType.None, Is.True, $"Unexpected SslHashAlgorithm: {client.SslHashAlgorithm}");
#endif
			Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
			Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
			Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
			Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
		}

		static void AssertClientIsDisconnected (IMailService client)
		{
			Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected");
			Assert.That (client.IsSecure, Is.False, "Expected IsSecure to be false after disconnecting");
			Assert.That (client.IsEncrypted, Is.False, "Expected IsEncrypted to be false after disconnecting");
			Assert.That (client.IsSigned, Is.False, "Expected IsSigned to be false after disconnecting");
			Assert.That (client.SslProtocol, Is.EqualTo (SslProtocols.None), "Expected SslProtocol to be None after disconnecting");
			Assert.That (client.SslCipherAlgorithm, Is.Null, "Expected SslCipherAlgorithm to be null after disconnecting");
			Assert.That (client.SslCipherStrength, Is.Null, "Expected SslCipherStrength to be null after disconnecting");
			Assert.That (client.SslCipherSuite, Is.Null, "Expected SslCipherSuite to be null after disconnecting");
			Assert.That (client.SslHashAlgorithm, Is.Null, "Expected SslHashAlgorithm to be null after disconnecting");
			Assert.That (client.SslHashStrength, Is.Null, "Expected SslHashStrength to be null after disconnecting");
			Assert.That (client.SslKeyExchangeAlgorithm, Is.Null, "Expected SslKeyExchangeAlgorithm to be null after disconnecting");
			Assert.That (client.SslKeyExchangeStrength, Is.Null, "Expected SslKeyExchangeStrength to be null after disconnecting");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				client.Connect (host, 0, options);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

				client.Disconnect (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				await client.ConnectAsync ("smtp.gmail.com", 0, SecureSocketOptions.SslOnConnect);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

				await client.DisconnectAsync (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
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
					AssertGMailIsConnected (client);
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

					client.Disconnect (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
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
					AssertGMailIsConnected (client);
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync ("pop.gmail.com", 0, SecureSocketOptions.SslOnConnect));

					await client.DisconnectAsync (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.Throws<ArgumentNullException> (() => client.Connect (socket, null, port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentException> (() => client.Connect (socket, "", port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect (socket, host, -1, SecureSocketOptions.Auto));

				client.Connect (socket, host, port, SecureSocketOptions.Auto);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (socket, host, port, SecureSocketOptions.Auto));

				client.Disconnect (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (socket, null, port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "", port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync (socket, host, -1, SecureSocketOptions.Auto));

				await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto));

				await client.DisconnectAsync (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"smtp://{host}:{port}/?starttls=always");
					client.Connect (uri, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (YahooCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (YahooCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (YahooHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					client.Disconnect (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"smtp://{host}:{port}/?starttls=always");
					await client.ConnectAsync (uri, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (YahooCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (YahooCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (YahooHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					await client.DisconnectAsync (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					client.Connect (socket, host, port, options, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (YahooCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (YahooCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (YahooHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					client.Disconnect (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					await client.ConnectAsync (socket, host, port, options, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (YahooCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (YahooCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (YahooHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					await client.DisconnectAsync (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestSaslInitialResponse ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					client.Authenticate (new SaslMechanismPlain ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslInitialResponseAsync ()
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateAuthenticationFailedCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-failed.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "auth-failed.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-failed.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestAuthenticationFailed ()
		{
			var commands = CreateAuthenticationFailedCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.That (ex.Message, Is.EqualTo ("535: authentication failed"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Authenticate (new SaslMechanismPlain ("username", "password"));
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.That (ex.Message, Is.EqualTo ("535: authentication failed"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestAuthenticationFailedAsync ()
		{
			var commands = CreateAuthenticationFailedCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.That (ex.Message, Is.EqualTo ("535: authentication failed"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password"));
					Assert.Fail ("Authenticate should have failed");
				} catch (AuthenticationException ex) {
					Assert.That (ex.Message, Is.EqualTo ("535: authentication failed"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static void AssertRedacted (MemoryStream stream, string commandPrefix, string nextCommandPrefix)
		{
			stream.Position = 0;

			using (var reader = new StreamReader (stream, Encoding.ASCII, false, 1024, true)) {
				string line, secret;

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith (commandPrefix, StringComparison.Ordinal))
						break;
				}

				Assert.That (line, Is.Not.Null, $"Authentication command not found: {commandPrefix}");

				if (line.Length > commandPrefix.Length) {
					// SASL IR; check next token is redacted.
					secret = line.Substring (commandPrefix.Length);

					Assert.That (secret, Is.EqualTo ("********"), "SASLIR token");
				}

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith (nextCommandPrefix, StringComparison.Ordinal))
						return;

					if (!line.StartsWith ("C: ", StringComparison.Ordinal))
						continue;

					secret = line.Substring (3);

					Assert.That (secret, Is.EqualTo ("********"), "SASL challenge");
				}

				Assert.Fail ("Did not find response.");
			}
		}

		static List<SmtpReplayCommand> CreateRedactAuthenticationCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestRedactAuthentication ()
		{
			var commands = CreateRedactAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new SmtpClient (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					client.LocalDomain = "unit-tests.mimekit.org";

					try {
						client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					client.AuthenticationMechanisms.Remove ("PLAIN");

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: QUIT");
			}
		}

		[Test]
		public async Task TestRedactAuthenticationAsync ()
		{
			var commands = CreateRedactAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new SmtpClient (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					client.LocalDomain = "unit-tests.mimekit.org";

					try {
						await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					client.AuthenticationMechanisms.Remove ("PLAIN");

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: QUIT");
			}
		}

		static List<SmtpReplayCommand> CreateRedactSaslInitialResponseCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestRedactSaslInitialResponse ()
		{
			var commands = CreateRedactSaslInitialResponseCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new SmtpClient (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					client.LocalDomain = "unit-tests.mimekit.org";

					try {
						client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					try {
						client.Authenticate (new SaslMechanismPlain ("username", "password"));
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH PLAIN ", "C: QUIT");
			}
		}

		[Test]
		public async Task TestRedactSaslInitialResponseAsync ()
		{
			var commands = CreateRedactSaslInitialResponseCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new SmtpClient (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					client.LocalDomain = "unit-tests.mimekit.org";

					try {
						await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					try {
						await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password"));
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH PLAIN ", "C: QUIT");
			}
		}

		static List<SmtpReplayCommand> CreateRedactSaslAuthenticationCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestRedactSaslAuthentication ()
		{
			var commands = CreateRedactSaslAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new SmtpClient (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					client.LocalDomain = "unit-tests.mimekit.org";

					try {
						client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					try {
						client.Authenticate (new SaslMechanismLogin ("username", "password"));
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: QUIT");
			}
		}

		[Test]
		public async Task TestRedactSaslAuthenticationAsync ()
		{
			var commands = CreateRedactSaslAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new SmtpClient (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					client.LocalDomain = "unit-tests.mimekit.org";

					try {
						await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					try {
						await client.AuthenticateAsync (new SaslMechanismLogin ("username", "password"));
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: QUIT");
			}
		}

		[TestCase (SmtpResponseMode.Char)]
		[TestCase (SmtpResponseMode.Line)]
		public void TestSmtpResponseModes (SmtpResponseMode mode)
		{
			var commands = new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false, mode), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateHeloFallbackCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO [IPv6:::1]\r\n", "ehlo-failed.txt"),
				new SmtpReplayCommand ("HELO [IPv6:::1]\r\n", "helo.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestHeloFallback ()
		{
			var commands = CreateHeloFallbackCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");
				Assert.That (client.Capabilities, Is.EqualTo (SmtpCapabilities.None), "Capabilities");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestHeloFallbackAsync ()
		{
			var commands = CreateHeloFallbackCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");
				Assert.That (client.Capabilities, Is.EqualTo (SmtpCapabilities.None), "Capabilities");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateHeloErrorHandlingCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO [IPv6:::1]\r\n", "ehlo-failed.txt"),
				new SmtpReplayCommand ("HELO [IPv6:::1]\r\n", "ehlo-failed.txt")
			};
		}

		[Test]
		public void TestHeloErrorHandlingFailed ()
		{
			var commands = CreateHeloErrorHandlingCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					Assert.Fail ("Expected an exception to be thrown in Connect");
				} catch (SmtpCommandException ex) {
					Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.UnexpectedStatusCode), "Unexpected error code");
					Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.CommandParameterNotImplemented), "Unexpected status code");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Client should not be connected.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");
				Assert.That (client.Capabilities, Is.EqualTo (SmtpCapabilities.None), "Capabilities");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestHeloErrorHandlingAsync ()
		{
			var commands = CreateHeloErrorHandlingCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "::1";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (SmtpCommandException ex) {
					Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.UnexpectedStatusCode), "Unexpected error code");
					Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.CommandParameterNotImplemented), "Unexpected status code");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Client should not be connected.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");
				Assert.That (client.Capabilities, Is.EqualTo (SmtpCapabilities.None), "Capabilities");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateBasicFunctionalityCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"),
				new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"),
				new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt"),
				new SmtpReplayCommand ("NOOP\r\n", "comcast-noop.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var commands = CreateBasicFunctionalityCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				MailboxAddress vrfy = null;

				try {
					vrfy = client.Verify ("Smith");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Verify: {ex}");
				}

				Assert.That (vrfy, Is.Not.Null, "VRFY result");
				Assert.That (vrfy.Name, Is.EqualTo ("Fred Smith"), "VRFY name");
				Assert.That (vrfy.Address, Is.EqualTo ("Smith@USC-ISIF.ARPA"), "VRFY address");

				InternetAddressList expn = null;

				try {
					expn = client.Expand ("Example-People");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Expand: {ex}");
				}

				Assert.That (expn, Is.Not.Null, "EXPN result");
				Assert.That (expn, Has.Count.EqualTo (6), "EXPN count");
				Assert.That (expn[0].Name, Is.EqualTo ("Jon Postel"), "expn[0].Name");
				Assert.That (((MailboxAddress) expn[0]).Address, Is.EqualTo ("Postel@USC-ISIF.ARPA"), "expn[0].Address");
				Assert.That (expn[1].Name, Is.EqualTo ("Fred Fonebone"), "expn[1].Name");
				Assert.That (((MailboxAddress) expn[1]).Address, Is.EqualTo ("Fonebone@USC-ISIQ.ARPA"), "expn[1].Address");
				Assert.That (expn[2].Name, Is.EqualTo ("Sam Q. Smith"), "expn[2].Name");
				Assert.That (((MailboxAddress) expn[2]).Address, Is.EqualTo ("SQSmith@USC-ISIQ.ARPA"), "expn[2].Address");
				Assert.That (expn[3].Name, Is.EqualTo ("Quincy Smith"), "expn[3].Name");
				Assert.That (((MailboxAddress) expn[3]).Route[0], Is.EqualTo ("USC-ISIF.ARPA"), "expn[3].Route");
				Assert.That (((MailboxAddress) expn[3]).Address, Is.EqualTo ("Q-Smith@ISI-VAXA.ARPA"), "expn[3].Address");
				Assert.That (expn[4].Name, Is.EqualTo (""), "expn[4].Name");
				Assert.That (((MailboxAddress) expn[4]).Address, Is.EqualTo ("joe@foo-unix.ARPA"), "expn[4].Address");
				Assert.That (expn[5].Name, Is.EqualTo (""), "expn[5].Name");
				Assert.That (((MailboxAddress) expn[5]).Address, Is.EqualTo ("xyz@bar-unix.ARPA"), "expn[5].Address");

				try {
					client.NoOp ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in NoOp: {ex}");
				}

				using (var message = CreateSimpleMessage ()) {
					var options = FormatOptions.Default;
					string response;

					try {
						response = client.Send (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					try {
						response = client.Send (message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					try {
						response = client.Send (options, message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					try {
						response = client.Send (options, message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestBasicFunctionalityAsync ()
		{
			var commands = CreateBasicFunctionalityCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				MailboxAddress vrfy = null;

				try {
					vrfy = await client.VerifyAsync ("Smith");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Verify: {ex}");
				}

				Assert.That (vrfy, Is.Not.Null, "VRFY result");
				Assert.That (vrfy.Name, Is.EqualTo ("Fred Smith"), "VRFY name");
				Assert.That (vrfy.Address, Is.EqualTo ("Smith@USC-ISIF.ARPA"), "VRFY address");

				InternetAddressList expn = null;

				try {
					expn = await client.ExpandAsync ("Example-People");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Expand: {ex}");
				}

				Assert.That (expn, Is.Not.Null, "EXPN result");
				Assert.That (expn, Has.Count.EqualTo (6), "EXPN count");
				Assert.That (expn[0].Name, Is.EqualTo ("Jon Postel"), "expn[0].Name");
				Assert.That (((MailboxAddress) expn[0]).Address, Is.EqualTo ("Postel@USC-ISIF.ARPA"), "expn[0].Address");
				Assert.That (expn[1].Name, Is.EqualTo ("Fred Fonebone"), "expn[1].Name");
				Assert.That (((MailboxAddress) expn[1]).Address, Is.EqualTo ("Fonebone@USC-ISIQ.ARPA"), "expn[1].Address");
				Assert.That (expn[2].Name, Is.EqualTo ("Sam Q. Smith"), "expn[2].Name");
				Assert.That (((MailboxAddress) expn[2]).Address, Is.EqualTo ("SQSmith@USC-ISIQ.ARPA"), "expn[2].Address");
				Assert.That (expn[3].Name, Is.EqualTo ("Quincy Smith"), "expn[3].Name");
				Assert.That (((MailboxAddress) expn[3]).Route[0], Is.EqualTo ("USC-ISIF.ARPA"), "expn[3].Route");
				Assert.That (((MailboxAddress) expn[3]).Address, Is.EqualTo ("Q-Smith@ISI-VAXA.ARPA"), "expn[3].Address");
				Assert.That (expn[4].Name, Is.EqualTo (""), "expn[4].Name");
				Assert.That (((MailboxAddress) expn[4]).Address, Is.EqualTo ("joe@foo-unix.ARPA"), "expn[4].Address");
				Assert.That (expn[5].Name, Is.EqualTo (""), "expn[5].Name");
				Assert.That (((MailboxAddress) expn[5]).Address, Is.EqualTo ("xyz@bar-unix.ARPA"), "expn[5].Address");

				try {
					await client.NoOpAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in NoOp: {ex}");
				}

				using (var message = CreateSimpleMessage ()) {
					var options = FormatOptions.Default;
					string response;

					try {
						response = await client.SendAsync (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					try {
						response = await client.SendAsync (message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					try {
						response = await client.SendAsync (options, message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					try {
						response = await client.SendAsync (options, message, message.From.Mailboxes.FirstOrDefault (), message.To.Mailboxes);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateXEXPSExtensionCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo+x-exps.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestXEXPSExtension ()
		{
			var commands = CreateXEXPSExtensionCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestXEXPSExtensionAsync ()
		{
			var commands = CreateXEXPSExtensionCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateResendCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"),
				new SmtpReplayCommand ("MAIL FROM:<resent-sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<resent-to@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("RCPT TO:<resent-cc@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("RCPT TO:<resent-bcc@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("MAIL FROM:<resent-from@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<resent-to@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("RCPT TO:<resent-cc@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("RCPT TO:<resent-bcc@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestResend ()
		{
			var commands = CreateResendCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				using (var message = CreateSimpleMessage ()) {
					var options = FormatOptions.Default;
					string response;

					message.ResentSender = new MailboxAddress ("Resent Sender", "resent-sender@example.com");
					message.ResentFrom.Add (new MailboxAddress ("Resent From", "resent-from@example.com"));
					message.ResentTo.Add (new MailboxAddress ("Resent To", "resent-to@example.com"));
					message.ResentCc.Add (new MailboxAddress ("Resent Cc", "resent-cc@example.com"));
					message.ResentBcc.Add (new MailboxAddress ("Resent Bcc", "resent-bcc@example.com"));

					try {
						response = client.Send (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					message.ResentSender = null;

					try {
						response = client.Send (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestResendAsync ()
		{
			var commands = CreateResendCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				// disable PLAIN authentication
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				using (var message = CreateSimpleMessage ()) {
					var options = FormatOptions.Default;
					string response;

					message.ResentSender = new MailboxAddress ("Resent Sender", "resent-sender@example.com");
					message.ResentFrom.Add (new MailboxAddress ("Resent From", "resent-from@example.com"));
					message.ResentTo.Add (new MailboxAddress ("Resent To", "resent-to@example.com"));
					message.ResentCc.Add (new MailboxAddress ("Resent Cc", "resent-cc@example.com"));
					message.ResentBcc.Add (new MailboxAddress ("Resent Bcc", "resent-bcc@example.com"));

					try {
						response = await client.SendAsync (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));

					message.ResentSender = null;

					try {
						response = await client.SendAsync (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
						return;
					}

					Assert.That (response, Is.EqualTo ("2.0.0 1Yat1n00V1sBWGw3SYaubg mail accepted for delivery"));
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateNoOpCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("NOOP\r\n", "comcast-noop.txt"),
				new SmtpReplayCommand ("NOOP\r\n", "bad-command-sequence.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestNoOp ()
		{
			var commands = CreateNoOpCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				// The first NOOP should complete successfully.
				client.NoOp ();

				// The second NOOP should get a 503 error.
				try {
					client.NoOp ();
					Assert.Fail ("This NOOP command should have resulted in a 502 error.");
				} catch (SmtpCommandException ex) {
					Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.UnexpectedStatusCode), "Unexpected error code");
					Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.BadCommandSequence), "Unexpected SmtpStatusCode");
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client should still be connected.");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestNoOpAsync ()
		{
			var commands = CreateNoOpCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				// The first NOOP should complete successfully.
				await client.NoOpAsync ();

				// The second NOOP should get a 503 error.
				try {
					await client.NoOpAsync ();
					Assert.Fail ("This NOOP command should have resulted in a 502 error.");
				} catch (SmtpCommandException ex) {
					Assert.That (ex.ErrorCode, Is.EqualTo (SmtpErrorCode.UnexpectedStatusCode), "Unexpected error code");
					Assert.That (ex.StatusCode, Is.EqualTo (SmtpStatusCode.BadCommandSequence), "Unexpected SmtpStatusCode");
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client should still be connected.");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateSaslAuthenticationCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestSaslAuthentication ()
		{
			var commands = CreateSaslAuthenticationCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslAuthenticationAsync ()
		{
			var commands = CreateSaslAuthenticationCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateSaslMechanismTooWeakCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "auth-too-weak.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestSaslMechanismTooWeak ()
		{
			var commands = CreateSaslMechanismTooWeakCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					client.Authenticate (sasl);
					Assert.Fail ("Authenticate should fail");
				} catch (AuthenticationException ax) {
					// This is what we expect
					Assert.That (ax.Message, Is.EqualTo ("authentication mechanism too weak"), "Exception message");
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception in Authenticate: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslMechanismTooWeakAsync ()
		{
			var commands = CreateSaslMechanismTooWeakCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					await client.AuthenticateAsync (sasl);
					Assert.Fail ("AuthenticateAsync should fail");
				} catch (AuthenticationException ax) {
					// This is what we expect
					Assert.That (ax.Message, Is.EqualTo ("authentication mechanism too weak"), "Exception message");
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception in Authenticate: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateSaslMechanismTooWeakFallbackCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "auth-too-weak.txt"),
				new SmtpReplayCommand ("AUTH LOGIN\r\n", "comcast-auth-login-username.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU=\r\n", "comcast-auth-login-password.txt"),
				new SmtpReplayCommand ("cGFzc3dvcmQ=\r\n", "comcast-auth-login.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestSaslMechanismTooWeakFallback ()
		{
			var commands = CreateSaslMechanismTooWeakFallbackCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslMechanismTooWeakFallbackAsync ()
		{
			var commands = CreateSaslMechanismTooWeakFallbackCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateSaslExceptionProperlyResetsCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo+digest-md5.txt"),
				new SmtpReplayCommand ("AUTH DIGEST-MD5\r\n", "comcast-auth-digest-md5.txt"),
				new SmtpReplayCommand ("dXNlcm5hbWU9ImNocmlzIixyZWFsbT0iZWx3b29kLmlubm9zb2Z0LmNvbSIsbm9uY2U9Ik9BNk1HOXRFUUdtMmhoIixjbm9uY2U9Ik9BNk1IWGg2VnFUclJrIixuYz0wMDAwMDAwMSxxb3A9ImF1dGgiLGRpZ2VzdC11cmk9InNtdHAvZWx3b29kLmlubm9zb2Z0LmNvbSIscmVzcG9uc2U9NTJmZjQ0OTA3ZjcyMzE0NDgxYjVjMDk4YzcwOGViZjMsY2hhcnNldD11dGYtOCxhbGdvcml0aG09bWQ1LXNlc3M=\r\n", "comcast-auth-digest-md5-response.txt"),
				new SmtpReplayCommand ("\r\n", "comcast-auth-digest-md5-reset.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestSaslExceptionProperlyResets ()
		{
			var commands = CreateSaslExceptionProperlyResetsCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					client.Connect (new SmtpReplayStream (commands, false), "elwood.innosoft.com", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("DIGEST-MD5"), "Failed to detect the DIGEST-MD5 auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("chris", "secret");
					var sasl = new SaslMechanismDigestMd5 (credentials) {
						cnonce = "OA6MHXh6VqTrRk"
					};

					client.Authenticate (sasl);
					Assert.Fail ("Expected AuthenticationException");
				} catch (AuthenticationException ax) {
					// yay!
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception in Authenticate: {ex}");
				}

				Assert.That (client.IsAuthenticated, Is.False, "IsAuthenticated");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestSaslExceptionProperlyResetsAsync ()
		{
			var commands = CreateSaslExceptionProperlyResetsCommands ();

			using (var client = new SmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "elwood.innosoft.com", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("DIGEST-MD5"), "Failed to detect the DIGEST-MD5 auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					var credentials = new NetworkCredential ("chris", "secret");
					var sasl = new SaslMechanismDigestMd5 (credentials) {
						cnonce = "OA6MHXh6VqTrRk"
					};

					await client.AuthenticateAsync (sasl);
					Assert.Fail ("Expected AuthenticationException");
				} catch (AuthenticationException ax) {
					// yay!
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception in Authenticate: {ex}");
				}

				Assert.That (client.IsAuthenticated, Is.False, "IsAuthenticated");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateEightBitMimeCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestEightBitMime ()
		{
			var commands = CreateEightBitMimeCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateEightBitMessage ())
						client.Send (message);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestEightBitMimeAsync ()
		{
			var commands = CreateEightBitMimeCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateEightBitMessage ())
						await client.SendAsync (message);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateInternationalMailboxesCommands (out MailboxAddress mailbox, out string addrspec)
		{
			mailbox = new MailboxAddress (string.Empty, "úßerñame@example.com");
			addrspec = MailboxAddress.EncodeAddrspec (mailbox.Address);

			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+smtputf8.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ($"MAIL FROM:<{mailbox.Address}> SMTPUTF8 BODY=8BITMIME\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ($"RCPT TO:<{mailbox.Address}>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ($"MAIL FROM:<{addrspec}> BODY=8BITMIME\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ($"RCPT TO:<{addrspec}>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestInternationalMailboxes ()
		{
			var commands = CreateInternationalMailboxesCommands (out var mailbox, out var addrspec);

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.UTF8), Is.True, "Failed to detect SMTPUTF8 extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				using (var message = CreateEightBitMessage ()) {
					try {
						client.Send (message, mailbox, new MailboxAddress[] { mailbox });
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}

					// Disable SMTPUTF8
					client.Capabilities &= ~SmtpCapabilities.UTF8;

					try {
						client.Send (message, mailbox, new MailboxAddress[] { mailbox });
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestInternationalMailboxesAsync ()
		{
			var commands = CreateInternationalMailboxesCommands (out var mailbox, out var addrspec);

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.UTF8), Is.True, "Failed to detect SMTPUTF8 extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				using (var message = CreateEightBitMessage ()) {
					try {
						await client.SendAsync (message, mailbox, new MailboxAddress[] { mailbox });
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}

					// Disable SMTPUTF8
					client.Capabilities &= ~SmtpCapabilities.UTF8;

					try {
						await client.SendAsync (message, mailbox, new MailboxAddress[] { mailbox });
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
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
			using (var message = CreateBinaryMessage ()) {
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

				var commands = new List<SmtpReplayCommand> {
					new SmtpReplayCommand ("", "comcast-greeting.txt"),
					new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+binarymime.txt"),
					new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
					new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=BINARYMIME\r\n", "comcast-mail-from.txt"),
					new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
					new SmtpReplayCommand (bdat, "comcast-data-done.txt"),
					new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
				};

				using (var client = new SmtpClient ()) {
					try {
						client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
					Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime), Is.True, "Failed to detect BINARYMIME extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Chunking), Is.True, "Failed to detect CHUNKING extension");

					try {
						if (showProgress) {
							var progress = new MyProgress ();

							client.Send (message, progress: progress);

							Assert.That (progress.BytesTransferred, Is.EqualTo (size), "BytesTransferred");
						} else {
							client.Send (message);
						}
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}
			}
		}

		[TestCase (false, TestName = "TestBinaryMimeAsyncNoProgress")]
		[TestCase (true, TestName = "TestBinaryMimeAsyncWithProgress")]
		public async Task TestBinaryMimeAsync (bool showProgress)
		{
			using (var message = CreateBinaryMessage ()) {
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

				var commands = new List<SmtpReplayCommand> {
					new SmtpReplayCommand ("", "comcast-greeting.txt"),
					new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+binarymime.txt"),
					new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
					new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=BINARYMIME\r\n", "comcast-mail-from.txt"),
					new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
					new SmtpReplayCommand (bdat, "comcast-data-done.txt"),
					new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
				};

				using (var client = new SmtpClient ()) {
					try {
						await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
					Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.BinaryMime), Is.True, "Failed to detect BINARYMIME extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Chunking), Is.True, "Failed to detect CHUNKING extension");

					try {
						if (showProgress) {
							var progress = new MyProgress ();

							await client.SendAsync (message, progress: progress);

							Assert.That (progress.BytesTransferred, Is.EqualTo (size), "BytesTransferred");
						} else {
							await client.SendAsync (message);
						}
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}
			}
		}

		static List<SmtpReplayCommand> CreatetPipeliningCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+pipelining.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mail-from-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[TestCase (false, TestName = "TestPipeliningNoProgress")]
		[TestCase (true, TestName = "TestPipeliningWithProgress")]
		public void TestPipelining (bool showProgress)
		{
			var commands = CreatetPipeliningCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), Is.True, "Failed to detect PIPELINING extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateEightBitMessage ()) {
						if (showProgress) {
							var progress = new MyProgress ();

							client.Send (message, progress: progress);

							Assert.That (progress.BytesTransferred, Is.EqualTo (Measure (message)), "BytesTransferred");
						} else {
							client.Send (message);
						}
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[TestCase (false, TestName = "TestPipeliningAsyncNoProgress")]
		[TestCase (true, TestName = "TestPipeliningAsyncWithProgress")]
		public async Task TestPipeliningAsync (bool showProgress)
		{
			var commands = CreatetPipeliningCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), Is.True, "Failed to detect PIPELINING extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateEightBitMessage ()) {
						if (showProgress) {
							var progress = new MyProgress ();

							await client.SendAsync (message, progress: progress);

							Assert.That (progress.BytesTransferred, Is.EqualTo (Measure (message)), "BytesTransferred");
						} else {
							await client.SendAsync (message);
						}
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateMailFromMailboxUnavailableCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestMailFromMailboxUnavailable ()
		{
			var commands = CreateMailFromMailboxUnavailableCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.SenderNotAccepted), "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestMailFromMailboxUnavailableAsync ()
		{
			var commands = CreateMailFromMailboxUnavailableCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.SenderNotAccepted), "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateMailFromAuthRequiredRsetDisconnectCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt", SmtpReplayState.UnexpectedDisconnect),
				new SmtpReplayCommand ("RSET\r\n", string.Empty)
			};
		}

		[Test]
		public void TestMailFromAuthRequiredRsetDisconnect ()
		{
			var commands = CreateMailFromAuthRequiredRsetDisconnectCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestMailFromAuthRequiredRsetDisconnectAsync ()
		{
			var commands = CreateMailFromAuthRequiredRsetDisconnectCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateMailFromUnavailableRsetDisconnectCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt", SmtpReplayState.UnexpectedDisconnect),
				new SmtpReplayCommand ("RSET\r\n", string.Empty)
			};
		}

		[Test]
		public void TestMailFromUnavailableRsetDisconnect ()
		{
			var commands = CreateMailFromUnavailableRsetDisconnectCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an SmtpCommandException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.SenderNotAccepted), "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestMailFromUnavailableRsetDisconnectAsync ()
		{
			var commands = CreateMailFromUnavailableRsetDisconnectCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an SmtpCommandException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.SenderNotAccepted), "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateRcptToMailboxUnavailableCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestRcptToMailboxUnavailable ()
		{
			var commands = CreateRcptToMailboxUnavailableCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.RecipientNotAccepted), "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestRcptToMailboxUnavailableAsync ()
		{
			var commands = CreateRcptToMailboxUnavailableCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.RecipientNotAccepted), "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		class NoRecipientsAcceptedSmtpClient : SmtpClient
		{
			public bool NoRecipientsAccepted;
			public int NotAccepted;

			protected override void OnRecipientNotAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
			{
				NotAccepted++;
			}

			protected override void OnNoRecipientsAccepted (MimeMessage message)
			{
				base.OnNoRecipientsAccepted (message);
				NoRecipientsAccepted = true;
			}
		}

		static List<SmtpReplayCommand> CreateNoRecipientsAcceptedCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestNoRecipientsAccepted ()
		{
			var commands = CreateNoRecipientsAcceptedCommands ();

			using (var client = new NoRecipientsAcceptedSmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.MessageNotAccepted), "Unexpected SmtpErrorCode");
					Assert.That (sex.StatusCode, Is.EqualTo (SmtpStatusCode.TransactionFailed), "Unexpected SmtpStatusCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.NotAccepted, Is.EqualTo (1), "NotAccepted");
				Assert.That (client.NoRecipientsAccepted, Is.True, "NoRecipientsAccepted");

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestNoRecipientsAcceptedAsync ()
		{
			var commands = CreateNoRecipientsAcceptedCommands ();

			using (var client = new NoRecipientsAcceptedSmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.MessageNotAccepted), "Unexpected SmtpErrorCode");
					Assert.That (sex.StatusCode, Is.EqualTo (SmtpStatusCode.TransactionFailed), "Unexpected SmtpStatusCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.NotAccepted, Is.EqualTo (1), "NotAccepted");
				Assert.That (client.NoRecipientsAccepted, Is.True, "NoRecipientsAccepted");

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand>  CreateNoRecipientsAcceptedPipelinedCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+pipelining.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mailbox-unavailable.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestNoRecipientsAcceptedPipelined ()
		{
			var commands = CreateNoRecipientsAcceptedPipelinedCommands ();

			using (var client = new NoRecipientsAcceptedSmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.MessageNotAccepted), "Unexpected SmtpErrorCode");
					Assert.That (sex.StatusCode, Is.EqualTo (SmtpStatusCode.TransactionFailed), "Unexpected SmtpStatusCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.NotAccepted, Is.EqualTo (1), "NotAccepted");
				Assert.That (client.NoRecipientsAccepted, Is.True, "NoRecipientsAccepted");

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestNoRecipientsAcceptedPipelinedAsync ()
		{
			var commands = CreateNoRecipientsAcceptedPipelinedCommands ();

			using (var client = new NoRecipientsAcceptedSmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.That (sex.ErrorCode, Is.EqualTo (SmtpErrorCode.MessageNotAccepted), "Unexpected SmtpErrorCode");
					Assert.That (sex.StatusCode, Is.EqualTo (SmtpStatusCode.TransactionFailed), "Unexpected SmtpStatusCode");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.NotAccepted, Is.EqualTo (1), "NotAccepted");
				Assert.That (client.NoRecipientsAccepted, Is.True, "NoRecipientsAccepted");

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateUnauthorizedAccessExceptionCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"),
				new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestUnauthorizedAccessException ()
		{
			var commands = CreateUnauthorizedAccessExceptionCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestUnauthorizedAccessExceptionAsync ()
		{
			var commands = CreateUnauthorizedAccessExceptionCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Expected the client to still be connected");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateResetErrorHandlingCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"),
				new SmtpReplayCommand ("RSET\r\n", "bad-command-sequence.txt")
			};
		}

		[Test]
		public void TestResetErrorHandling ()
		{
			var commands = CreateResetErrorHandlingCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					using (var message = CreateSimpleMessage ())
						client.Send (message);
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected due to RSET error");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestResetErrorHandlingAsync ()
		{
			var commands = CreateResetErrorHandlingCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					using (var message = CreateSimpleMessage ())
						await client.SendAsync (message);
					Assert.Fail ("Expected an ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Send: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected due to RSET error");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		class DsnSmtpClient : SmtpClient
		{
			public DsnSmtpClient ()
			{
			}

			protected override string GetEnvelopeId (MimeMessage message)
			{
				var id = base.GetEnvelopeId (message);

				Assert.That (id, Is.Null);

				return message.MessageId;
			}

			public DeliveryStatusNotification? DeliveryStatusNotifications {
				get; set;
			}

			protected override DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
			{
				var notify = base.GetDeliveryStatusNotifications (message, mailbox);

				Assert.That (notify.HasValue, Is.False);

				return DeliveryStatusNotifications;
			}
		}

		static List<SmtpReplayCommand> CreateDeliveryStatusNotificationCommands (MimeMessage message)
		{
			var mailFrom = string.Format ("MAIL FROM:<sender@example.com> BODY=8BITMIME ENVID={0} RET=HDRS\r\n", message.MessageId);

			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+dsn.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand (mailFrom, "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com> NOTIFY=SUCCESS,FAILURE,DELAY ORCPT=rfc822;recipient@example.com\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestDeliveryStatusNotification ()
		{
			using (var message = CreateEightBitMessage ()) {
				message.MessageId = MimeUtils.GenerateMessageId ();

				var commands = CreateDeliveryStatusNotificationCommands (message);

				using (var client = new DsnSmtpClient ()) {
					try {
						client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), Is.True, "Failed to detect DSN extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), Is.True, "Failed to detect PIPELINING extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
					Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					// disable pipelining
					client.Capabilities &= ~SmtpCapabilities.Pipelining;

					client.DeliveryStatusNotificationType = DeliveryStatusNotificationType.HeadersOnly;
					client.DeliveryStatusNotifications = DeliveryStatusNotification.Delay | DeliveryStatusNotification.Failure | DeliveryStatusNotification.Success;

					try {
						client.Send (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}
			}
		}

		[Test]
		public async Task TestDeliveryStatusNotificationAsync ()
		{
			using (var message = CreateEightBitMessage ()) {
				message.MessageId = MimeUtils.GenerateMessageId ();

				var commands = CreateDeliveryStatusNotificationCommands (message);

				using (var client = new DsnSmtpClient ()) {
					try {
						await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), Is.True, "Failed to detect DSN extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), Is.True, "Failed to detect PIPELINING extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
					Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
					Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					// disable pipelining
					client.Capabilities &= ~SmtpCapabilities.Pipelining;

					client.DeliveryStatusNotificationType = DeliveryStatusNotificationType.HeadersOnly;
					client.DeliveryStatusNotifications = DeliveryStatusNotification.Delay | DeliveryStatusNotification.Failure | DeliveryStatusNotification.Success;

					try {
						await client.SendAsync (message);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Send: {ex}");
					}

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}
			}
		}

		static List<SmtpReplayCommand> CreateDeliveryStatusNotificationWithHexEncodeCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+dsn.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME ENVID=123456789+2B+3Dabc@+E5+90+8D+E3+81+8C+E3+83+89+E3+83+A1+E3+82+A4+E3+83+B3.com RET=FULL\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@xn--v8jxj3d1dzdz08w.com> NOTIFY=NEVER ORCPT=rfc822;recipient@xn--v8jxj3d1dzdz08w.com\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestDeliveryStatusNotificationWithHexEncode ()
		{
			var commands = CreateDeliveryStatusNotificationWithHexEncodeCommands ();

			using (var client = new DsnSmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), Is.True, "Failed to detect DSN extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), Is.True, "Failed to detect PIPELINING extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable pipelining
				client.Capabilities &= ~SmtpCapabilities.Pipelining;

				client.DeliveryStatusNotificationType = DeliveryStatusNotificationType.Full;
				client.DeliveryStatusNotifications = DeliveryStatusNotification.Never;

				try {
					using (var message = CreateEightBitMessage ()) {
						message.MessageId = "123456789+=abc@名がドメイン.com";

						message.To.Clear ();
						message.To.Add (new MailboxAddress ("", "recipient@名がドメイン.com"));

						client.Send (message);
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestDeliveryStatusNotificationWithHexEncodeAsync ()
		{
			var commands = CreateDeliveryStatusNotificationWithHexEncodeCommands ();

			using (var client = new DsnSmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Dsn), Is.True, "Failed to detect DSN extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Pipelining), Is.True, "Failed to detect PIPELINING extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// disable pipelining
				client.Capabilities &= ~SmtpCapabilities.Pipelining;

				client.DeliveryStatusNotificationType = DeliveryStatusNotificationType.Full;
				client.DeliveryStatusNotifications = DeliveryStatusNotification.Never;

				try {
					using (var message = CreateEightBitMessage ()) {
						message.MessageId = "123456789+=abc@名がドメイン.com";

						message.To.Clear ();
						message.To.Add (new MailboxAddress ("", "recipient@名がドメイン.com"));

						await client.SendAsync (message);
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<SmtpReplayCommand> CreateRequireTlsCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ($"EHLO {SmtpClient.DefaultLocalDomain}\r\n", "comcast-ehlo+requiretls.txt"),
				new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME REQUIRETLS\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"),
				new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"),
				new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"),
				new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"),
				new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt")
			};
		}

		[Test]
		public void TestRequireTls ()
		{
			var commands = CreateRequireTlsCommands ();

			using (var client = new SmtpClient ()) {
				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.RequireTLS), Is.True, "Failed to detect REQUIRETLS extension");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.RequireTLS = true;

				try {
					using (var message = CreateEightBitMessage ())
						client.Send (message);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					using (var message = CreateEightBitMessage ()) {
						message.Headers.Add (HeaderId.TLSRequired, "No");
						client.Send (message);
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestRequireTlsAsync ()
		{
			var commands = CreateRequireTlsCommands ();

			using (var client = new SmtpClient ()) {
				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");
				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.RequireTLS), Is.True, "Failed to detect REQUIRETLS extension");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.RequireTLS = true;

				try {
					using (var message = CreateEightBitMessage ())
						await client.SendAsync (message);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					using (var message = CreateEightBitMessage ()) {
						message.Headers.Add (HeaderId.TLSRequired, "No");
						await client.SendAsync (message);
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Send: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
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

		static List<SmtpReplayCommand> CreateCustomCommandCommands ()
		{
			return new List<SmtpReplayCommand> {
				new SmtpReplayCommand ("", "comcast-greeting.txt"),
				new SmtpReplayCommand ("EHLO unit-tests.mimekit.org\r\n", "comcast-ehlo.txt"),
				new SmtpReplayCommand ("VRFY Smith\r\n", "rfc0821-vrfy.txt"),
				new SmtpReplayCommand ("EXPN Example-People\r\n", "rfc0821-expn.txt")
			};
		}

		[Test]
		public void TestCustomCommand ()
		{
			var commands = CreateCustomCommandCommands ();

			using (var client = new CustomSmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				Assert.Throws<ServiceNotConnectedException> (() => client.SendCommand ("COMMAND"));

				try {
					client.Connect (new SmtpReplayStream (commands, false), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				Assert.Throws<ArgumentNullException> (() => client.SendCommand (null));

				SmtpResponse response = null;

				try {
					response = client.SendCommand ("VRFY Smith");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Verify: {ex}");
				}

				Assert.That (response, Is.Not.Null, "VRFY result");
				Assert.That (response.StatusCode, Is.EqualTo (SmtpStatusCode.Ok), "VRFY response code");
				Assert.That (response.Response, Is.EqualTo ("Fred Smith <Smith@USC-ISIF.ARPA>"), "VRFY response");

				try {
					response = client.SendCommand ("EXPN Example-People");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Expand: {ex}");
				}

				Assert.That (response, Is.Not.Null, "EXPN result");
				Assert.That (response.StatusCode, Is.EqualTo (SmtpStatusCode.Ok), "EXPN response code");
				Assert.That (response.Response, Is.EqualTo ("Jon Postel <Postel@USC-ISIF.ARPA>\nFred Fonebone <Fonebone@USC-ISIQ.ARPA>\nSam Q. Smith <SQSmith@USC-ISIQ.ARPA>\nQuincy Smith <@USC-ISIF.ARPA:Q-Smith@ISI-VAXA.ARPA>\n<joe@foo-unix.ARPA>\n<xyz@bar-unix.ARPA>"), "EXPN response");
			}
		}

		[Test]
		public async Task TestCustomCommandAsync ()
		{
			var commands = CreateCustomCommandCommands ();

			using (var client = new CustomSmtpClient ()) {
				client.LocalDomain = "unit-tests.mimekit.org";

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SendCommandAsync ("COMMAND"));

				try {
					await client.ConnectAsync (new SmtpReplayStream (commands, true), "localhost", 25, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), Is.True, "Failed to detect AUTH extension");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), Is.True, "Failed to detect 8BITMIME extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), Is.True, "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.Size), Is.True, "Failed to detect SIZE extension");
				Assert.That (client.MaxSize, Is.EqualTo (36700160), "Failed to parse SIZE correctly");

				Assert.That (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), Is.True, "Failed to detect STARTTLS extension");

				Assert.Throws<ArgumentException> (() => client.Capabilities |= SmtpCapabilities.UTF8);

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SendCommandAsync (null));

				SmtpResponse response = null;

				try {
					response = await client.SendCommandAsync ("VRFY Smith");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Verify: {ex}");
				}

				Assert.That (response, Is.Not.Null, "VRFY result");
				Assert.That (response.StatusCode, Is.EqualTo (SmtpStatusCode.Ok), "VRFY response code");
				Assert.That (response.Response, Is.EqualTo ("Fred Smith <Smith@USC-ISIF.ARPA>"), "VRFY response");

				try {
					response = await client.SendCommandAsync ("EXPN Example-People");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Expand: {ex}");
				}

				Assert.That (response, Is.Not.Null, "EXPN result");
				Assert.That (response.StatusCode, Is.EqualTo (SmtpStatusCode.Ok), "EXPN response code");
				Assert.That (response.Response, Is.EqualTo ("Jon Postel <Postel@USC-ISIF.ARPA>\nFred Fonebone <Fonebone@USC-ISIQ.ARPA>\nSam Q. Smith <SQSmith@USC-ISIQ.ARPA>\nQuincy Smith <@USC-ISIF.ARPA:Q-Smith@ISI-VAXA.ARPA>\n<joe@foo-unix.ARPA>\n<xyz@bar-unix.ARPA>"), "EXPN response");
			}
		}
	}
}
