//
// Pop3ClientTests.cs
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
using System.Collections;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;

using MailKit;
using MailKit.Security;
using MailKit.Net.Pop3;
using MailKit.Net.Proxy;

using UnitTests.Security;
using UnitTests.Net.Proxy;

using AuthenticationException = MailKit.Security.AuthenticationException;

namespace UnitTests.Net.Pop3 {
	[TestFixture]
	public class Pop3ClientTests
	{
		static readonly Pop3Capabilities ComcastCapa1 = Pop3Capabilities.Expire | Pop3Capabilities.StartTLS |
			Pop3Capabilities.Top | Pop3Capabilities.UIDL | Pop3Capabilities.User;
		static readonly Pop3Capabilities ComcastCapa2 = Pop3Capabilities.Expire | Pop3Capabilities.StartTLS |
			Pop3Capabilities.Sasl | Pop3Capabilities.Top | Pop3Capabilities.UIDL | Pop3Capabilities.User;
		static readonly Pop3Capabilities ExchangeCapa = Pop3Capabilities.Sasl | Pop3Capabilities.Top |
			Pop3Capabilities.UIDL | Pop3Capabilities.User;
		static readonly Pop3Capabilities GMailCapa1 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
			Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay | Pop3Capabilities.Top |
			Pop3Capabilities.UIDL | Pop3Capabilities.Sasl;
		static readonly Pop3Capabilities GMailCapa2 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
			Pop3Capabilities.Pipelining | Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay |
			Pop3Capabilities.Top | Pop3Capabilities.UIDL;
		static readonly Pop3Capabilities LangCapa1 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
		    Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay | Pop3Capabilities.Top |
		    Pop3Capabilities.UIDL | Pop3Capabilities.Sasl | Pop3Capabilities.UTF8 |
		    Pop3Capabilities.UTF8User | Pop3Capabilities.Lang | Pop3Capabilities.Apop;
		static readonly Pop3Capabilities LangCapa2 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
		    Pop3Capabilities.Pipelining | Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay |
		    Pop3Capabilities.Top | Pop3Capabilities.UIDL | Pop3Capabilities.Lang | Pop3Capabilities.Apop;
		const CipherAlgorithmType GmxDeCipherAlgorithm = CipherAlgorithmType.Aes256;
		const int GmxDeCipherStrength = 256;
#if !MONO
		const HashAlgorithmType GmxDeHashAlgorithm = HashAlgorithmType.Sha384;
#else
		const HashAlgorithmType GmxDeHashAlgorithm = HashAlgorithmType.None;
#endif
		const ExchangeAlgorithmType EcdhEphemeral = (ExchangeAlgorithmType) 44550;

		static string HexEncode (byte[] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			using (var client = new Pop3Client ()) {
				var credentials = new NetworkCredential ("username", "password");

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentException> (() => client.Connect (new Uri ("path", UriKind.Relative)));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (new Uri ("path", UriKind.Relative)));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 110, false));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (null, 110, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 110, false));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 110, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 110, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (null, 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 110, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				Assert.Throws<ArgumentNullException> (() => client.Connect ((Socket) null, "host", 110, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Socket) null, "host", 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Stream) null, "host", 110, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Stream) null, "host", 110, SecureSocketOptions.None));

				using (var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
					Assert.Throws<ArgumentException> (() => client.Connect (socket, "host", 110, SecureSocketOptions.None));
					Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "host", 110, SecureSocketOptions.None));
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
			}
		}

		static void AssertDefaultValues (string host, int port, SecureSocketOptions options, Uri expected)
		{
			Pop3Client.ComputeDefaultValues (host, ref port, ref options, out Uri uri, out bool starttls);

			if (expected.PathAndQuery == "/?starttls=when-available") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.StartTlsWhenAvailable), $"{expected}");
				Assert.That (starttls, Is.True, $"{expected}");
			} else if (expected.PathAndQuery == "/?starttls=always") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.StartTls), $"{expected}");
				Assert.That (starttls, Is.True, $"{expected}");
			} else if (expected.Scheme == "pops") {
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
			const string host = "pop.skyfall.net";

			AssertDefaultValues (host, 0, SecureSocketOptions.None, new Uri ($"pop://{host}:110"));
			AssertDefaultValues (host, 110, SecureSocketOptions.None, new Uri ($"pop://{host}:110"));
			AssertDefaultValues (host, 995, SecureSocketOptions.None, new Uri ($"pop://{host}:995"));

			AssertDefaultValues (host, 0, SecureSocketOptions.SslOnConnect, new Uri ($"pops://{host}:995"));
			AssertDefaultValues (host, 110, SecureSocketOptions.SslOnConnect, new Uri ($"pops://{host}:110"));
			AssertDefaultValues (host, 995, SecureSocketOptions.SslOnConnect, new Uri ($"pops://{host}:995"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTls, new Uri ($"pop://{host}:110/?starttls=always"));
			AssertDefaultValues (host, 110, SecureSocketOptions.StartTls, new Uri ($"pop://{host}:110/?starttls=always"));
			AssertDefaultValues (host, 995, SecureSocketOptions.StartTls, new Uri ($"pop://{host}:995/?starttls=always"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"pop://{host}:110/?starttls=when-available"));
			AssertDefaultValues (host, 110, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"pop://{host}:110/?starttls=when-available"));
			AssertDefaultValues (host, 995, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"pop://{host}:995/?starttls=when-available"));

			AssertDefaultValues (host, 0, SecureSocketOptions.Auto, new Uri ($"pop://{host}:110/?starttls=when-available"));
			AssertDefaultValues (host, 110, SecureSocketOptions.Auto, new Uri ($"pop://{host}:110/?starttls=when-available"));
			AssertDefaultValues (host, 995, SecureSocketOptions.Auto, new Uri ($"pops://{host}:995"));
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
			using (var client = new Pop3Client ()) {
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
			using (var client = new Pop3Client ()) {
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
			using (var client = new Pop3Client ()) {
				Assert.That (client.SyncRoot, Is.InstanceOf<Pop3Engine> ());
			}
		}

		[Test]
		public void TestInvalidStateExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<ServiceNotConnectedException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				Assert.Throws<ServiceNotConnectedException> (() => client.EnableUTF8 ());
				Assert.Throws<ServiceNotConnectedException> (() => client.GetLanguages ());
				Assert.Throws<ServiceNotConnectedException> (() => client.SetLanguage ("en"));

				Assert.Throws<ServiceNotConnectedException> (() => client.NoOp ());

				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageSizes ());
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageSize (0));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageUids ());
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageUid (0));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessage (0));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessages (0, 1));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessages (new int[] { 0 }));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageHeaders (0));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageHeaders (0, 1));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetMessageHeaders (new int[] { 0 }));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetStream (0));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetStreams (0, 1));
				Assert.Throws<ServiceNotConnectedException> (() => client.GetStreams (new int[] { 0 }));
				Assert.Throws<ServiceNotConnectedException> (() => client.DeleteMessage (0));
				Assert.Throws<ServiceNotConnectedException> (() => client.DeleteMessages (0, 1));
				Assert.Throws<ServiceNotConnectedException> (() => client.DeleteMessages (new int [] { 0 }));

				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));
				Assert.That (client.LoginDelay, Is.EqualTo (0));

				Assert.Throws<ArgumentException> (() => client.Capabilities |= Pop3Capabilities.Apop);
				Assert.DoesNotThrow (() => client.Capabilities &= ~Pop3Capabilities.UIDL);

				Assert.Throws<ArgumentNullException> (() => client.SetLanguage (null));
				Assert.Throws<ArgumentException> (() => client.SetLanguage (string.Empty));

				Assert.Throws<AuthenticationException> (() => client.Authenticate ("username", "password"));
				Assert.That (client.IsConnected, Is.True, "AuthenticationException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageSizes ());
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageSize (0));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageUids ());
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageUid (0));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessage (0));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessages (0, 1));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessages (new int[] { 0 }));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageHeaders (0));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageHeaders (0, 1));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageHeaders (new int[] { 0 }));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetStream (0));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetStreams (0, 1));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetStreams (new int[] { 0 }));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.DeleteMessage (0));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.DeleteMessages (0, 1));
				Assert.Throws<ServiceNotAuthenticatedException> (() => client.DeleteMessages (new int[] { 0 }));
				Assert.That (client.IsConnected, Is.True, "ServiceNotAuthenticatedException should not cause a disconnect.");

				client.Authenticate (Encoding.UTF8, "username", "password");
				Assert.That (client.IsAuthenticated, Is.True, "IsAuthenticated");

				Assert.Throws<InvalidOperationException> (() => client.Authenticate ("username", "password"));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new NetworkCredential ("username", "password")));
				Assert.Throws<InvalidOperationException> (() => client.Authenticate (new SaslMechanismPlain ("username", "password")));

				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessageSize (-1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessageUid (-1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessage (-1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessages (-1, 1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessages (0, -1));
				Assert.Throws<ArgumentNullException> (() => client.GetMessages (null));
				Assert.Throws<ArgumentException> (() => client.GetMessages (new int[] { -1 }));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessageHeaders (-1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessageHeaders (-1, 1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetMessageHeaders (0, -1));
				Assert.Throws<ArgumentNullException> (() => client.GetMessageHeaders (null));
				Assert.Throws<ArgumentException> (() => client.GetMessageHeaders (new int[] { -1 }));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetStream (-1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetStreams (-1, 1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.GetStreams (0, -1));
				Assert.Throws<ArgumentNullException> (() => client.GetStreams (null));
				Assert.Throws<ArgumentException> (() => client.GetStreams (new int[] { -1 }));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.DeleteMessage (-1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.DeleteMessages (-1, 1));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.DeleteMessages (0, -1));
				Assert.Throws<ArgumentNullException> (() => client.DeleteMessages (null));
				Assert.Throws<ArgumentException> (() => client.DeleteMessages (new int[] { -1 }));

				Assert.That (client.GetStreams (0, 0), Is.Empty);
				Assert.That (client.GetStreams (Array.Empty<int> ()), Is.Empty);
				Assert.That (client.GetMessages (0, 0), Is.Empty);
				Assert.That (client.GetMessages (Array.Empty<int> ()), Is.Empty);
				Assert.That (client.GetMessageHeaders (0, 0), Is.Empty);
				Assert.That (client.GetMessageHeaders (Array.Empty<int> ()), Is.Empty);

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestInvalidStateExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.EnableUTF8Async ());
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetLanguagesAsync ());
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.SetLanguageAsync ("en"));

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.NoOpAsync ());

				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageSizesAsync ());
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageUidsAsync ());
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageUidAsync (0));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageAsync (0));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessagesAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessagesAsync (new int[] { 0 }));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync (new int[] { 0 }));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetStreamAsync (0));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.DeleteMessageAsync (0));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.DeleteMessagesAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotConnectedException> (async () => await client.DeleteMessagesAsync (new int[] { 0 }));

				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));
				Assert.That (client.LoginDelay, Is.EqualTo (0));

				Assert.Throws<ArgumentException> (() => client.Capabilities |= Pop3Capabilities.Apop);
				Assert.DoesNotThrow (() => client.Capabilities &= ~Pop3Capabilities.UIDL);

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.SetLanguageAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.SetLanguageAsync (string.Empty));

				Assert.ThrowsAsync<AuthenticationException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.That (client.IsConnected, Is.True, "AuthenticationException should not cause a disconnect.");

				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageSizesAsync ());
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageUidsAsync ());
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageUidAsync (0));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageAsync (0));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessagesAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessagesAsync (new int[] { 0 }));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageHeadersAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetMessageHeadersAsync (new int[] { 0 }));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetStreamAsync (0));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.DeleteMessageAsync (0));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.DeleteMessagesAsync (0, 1));
				Assert.ThrowsAsync<ServiceNotAuthenticatedException> (async () => await client.DeleteMessagesAsync (new int[] { 0 }));
				Assert.That (client.IsConnected, Is.True, "ServiceNotAuthenticatedException should not cause a disconnect.");

				await client.AuthenticateAsync (Encoding.UTF8, "username", "password");
				Assert.That (client.IsAuthenticated, Is.True, "IsAuthenticated");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.AuthenticateAsync (new SaslMechanismPlain ("username", "password")));

				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessageSizeAsync (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessageUidAsync (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessageAsync (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessagesAsync (-1, 1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessagesAsync (0, -1));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetMessagesAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.GetMessagesAsync (new int[] { -1 }));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessageHeadersAsync (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessageHeadersAsync (-1, 1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetMessageHeadersAsync (0, -1));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetMessageHeadersAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.GetMessageHeadersAsync (new int[] { -1 }));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetStreamAsync (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetStreamsAsync (-1, 1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.GetStreamsAsync (0, -1));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetStreamsAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.GetStreamsAsync (new int[] { -1 }));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.DeleteMessageAsync (-1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.DeleteMessagesAsync (-1, 1));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.DeleteMessagesAsync (0, -1));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.DeleteMessagesAsync (null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.DeleteMessagesAsync (new int[] { -1 }));

				Assert.That ((await client.GetStreamsAsync (0, 0)), Is.Empty);
				Assert.That ((await client.GetStreamsAsync (Array.Empty<int> ())), Is.Empty);
				Assert.That ((await client.GetMessagesAsync (0, 0)), Is.Empty);
				Assert.That ((await client.GetMessagesAsync (Array.Empty<int> ())), Is.Empty);
				Assert.That ((await client.GetMessageHeadersAsync (0, 0)), Is.Empty);
				Assert.That ((await client.GetMessageHeadersAsync (Array.Empty<int> ())), Is.Empty);

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestStartTlsNotSupported ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt")
			};

			using (var client = new Pop3Client ())
				Assert.Throws<NotSupportedException> (() => client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.StartTls), "STLS");

			using (var client = new Pop3Client ())
				Assert.ThrowsAsync<NotSupportedException> (() => client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.StartTls), "STLS Async");
		}

		[Test]
		public void TestProtocolLoggerExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt")
			};

			using (var client = new Pop3Client (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogConnect)))
				Assert.Throws<NotImplementedException> (() => client.Connect (Stream.Null, "pop.gmail.com", 110, SecureSocketOptions.None), "LogConnect");

			using (var client = new Pop3Client (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogConnect)))
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (Stream.Null, "pop.gmail.com", 110, SecureSocketOptions.None), "LogConnect Async");

			using (var client = new Pop3Client (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogServer)))
				Assert.Throws<NotImplementedException> (() => client.Connect (new Pop3ReplayStream (commands, false), "pop.gmail.com", 110, SecureSocketOptions.None), "LogServer");

			using (var client = new Pop3Client (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogServer)))
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (new Pop3ReplayStream (commands, true), "pop.gmail.com", 110, SecureSocketOptions.None), "LogServer Async");

			using (var client = new Pop3Client (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogClient)))
				Assert.Throws<NotImplementedException> (() => client.Connect (new Pop3ReplayStream (commands, false), "pop.gmail.com", 110, SecureSocketOptions.None), "LogClient");

			using (var client = new Pop3Client (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogClient)))
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (new Pop3ReplayStream (commands, true), "pop.gmail.com", 110, SecureSocketOptions.None), "LogClient Async");
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
			var host = "pop.gmail.com";
			int port = 995;

			using (var client = new Pop3Client ()) {
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
			var host = "pop.gmail.com";
			int port = 995;

			using (var client = new Pop3Client ()) {
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

				await client.ConnectAsync (host, 0, options);
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
			var host = "pop.gmail.com";
			int port = 995;

			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				using (var client = new Pop3Client ()) {
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
			var host = "pop.gmail.com";
			int port = 995;

			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				using (var client = new Pop3Client ()) {
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
						await client.ConnectAsync (host, 0, options);
					} catch (TimeoutException) {
						Assert.Inconclusive ("Timed out.");
						return;
					} catch (Exception ex) {
						Assert.Fail (ex.Message);
					}
					AssertGMailIsConnected (client);
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

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
			var host = "pop.gmail.com";
			int port = 995;

			using (var client = new Pop3Client ()) {
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

				Assert.Throws<InvalidOperationException> (() => client.Connect (socket, "pop.gmail.com", 995, SecureSocketOptions.Auto));

				client.Disconnect (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
			}
		}

		[Test]
		public async Task TestConnectGMailSocketAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "pop.gmail.com";
			int port = 995;

			using (var client = new Pop3Client ()) {
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
		public void TestConnectGmxDe ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "pop.gmx.de";
			var port = 110;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new Pop3Client ()) {
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

					var uri = new Uri ($"pop://{host}/?starttls=always");
					client.Connect (uri, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
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
		public async Task TestConnectGmxDeAsync ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "pop.gmx.de";
			var port = 110;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new Pop3Client ()) {
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

					var uri = new Uri ($"pop://{host}/?starttls=always");
					await client.ConnectAsync (uri, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
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
		public void TestConnectGmxDeSocket ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "pop.gmx.de";
			var port = 110;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new Pop3Client ()) {
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
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
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
		public async Task TestConnectGmxDeSocketAsync ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "pop.gmx.de";
			var port = 110;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new Pop3Client ()) {
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
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
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
		public void TestGreetingOk ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "common.ok-greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31), "ExpirePolicy");
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
		public async Task TestGreetingOkAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "common.ok-greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31), "ExpirePolicy");
				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestGreetingErr ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "common.err-greeting.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					Assert.Fail ("Expected Connect to fail.");
				} catch (Pop3ProtocolException) {
					Assert.Pass ();
				} catch (Exception ex) {
					Assert.Fail ($"Expected Pop3ProtocolException from Connect: {ex}");
				}
			}
		}

		[Test]
		public async Task TestGreetingErrAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "common.err-greeting.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					Assert.Fail ("Expected Connect to fail.");
				} catch (Pop3ProtocolException) {
					Assert.Pass ();
				} catch (Exception ex) {
					Assert.Fail ($"Expected Pop3ProtocolException from Connect: {ex}");
				}
			}
		}

		static IList<Pop3ReplayCommand> CreateBasicPop3ClientCommands ()
		{
			return new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "comcast.retr1.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};
		}

		[Test]
		public void TestBasicPop3Client ()
		{
			var commands = CreateBasicPop3ClientCommands ();

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31), "ExpirePolicy");
				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				try {
					var sizes = client.GetMessageSizes ();
					Assert.That (sizes, Has.Count.EqualTo (7), "Expected 7 message sizes");
					for (int i = 0; i < sizes.Count; i++)
						Assert.That (sizes[i], Is.EqualTo ((i + 1) * 1024), $"Unexpected size for message #{i}");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					var size = client.GetMessageSize (0);
					Assert.That (size, Is.EqualTo (1024), "Unexpected size for 1st message");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					using (var message = client.GetMessage (0)) {
						// TODO: assert that the message is byte-identical to what we expect
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					var count = client.GetMessageCount ();
					Assert.That (count, Is.EqualTo (7), "Expected 7 messages again");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageCount: {ex}");
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
		public async Task TestBasicPop3ClientAsync ()
		{
			var commands = CreateBasicPop3ClientCommands ();

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31), "ExpirePolicy");
				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				try {
					var sizes = await client.GetMessageSizesAsync ();
					Assert.That (sizes, Has.Count.EqualTo (7), "Expected 7 message sizes");
					for (int i = 0; i < sizes.Count; i++)
						Assert.That (sizes[i], Is.EqualTo ((i + 1) * 1024), $"Unexpected size for message #{i}");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					var size = await client.GetMessageSizeAsync (0);
					Assert.That (size, Is.EqualTo (1024), "Unexpected size for 1st message");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					using (var message = await client.GetMessageAsync (0)) {
						// TODO: assert that the message is byte-identical to what we expect
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					var count = await client.GetMessageCountAsync ();
					Assert.That (count, Is.EqualTo (7), "Expected 7 messages again");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageCount: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestBasicPop3ClientUnixLineEndings ()
		{
			var commands = CreateBasicPop3ClientCommands ();

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				try {
					var sizes = client.GetMessageSizes ();
					Assert.That (sizes, Has.Count.EqualTo (7), "Expected 7 message sizes");
					for (int i = 0; i < sizes.Count; i++)
						Assert.That (sizes[i], Is.EqualTo ((i + 1) * 1024), $"Unexpected size for message #{i}");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					var size = client.GetMessageSize (0);
					Assert.That (size, Is.EqualTo (1024), "Unexpected size for 1st message");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					using (var message = client.GetMessage (0)) {
						// TODO: assert that the message is byte-identical to what we expect
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					var count = client.GetMessageCount ();
					Assert.That (count, Is.EqualTo (7), "Expected 7 messages again");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageCount: {ex}");
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
		public async Task TestBasicPop3ClientUnixLineEndingsAsync ()
		{
			var commands = CreateBasicPop3ClientCommands ();

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				try {
					var sizes = await client.GetMessageSizesAsync ();
					Assert.That (sizes, Has.Count.EqualTo (7), "Expected 7 message sizes");
					for (int i = 0; i < sizes.Count; i++)
						Assert.That (sizes[i], Is.EqualTo ((i + 1) * 1024), $"Unexpected size for message #{i}");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					var size = await client.GetMessageSizeAsync (0);
					Assert.That (size, Is.EqualTo (1024), "Unexpected size for 1st message");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageSizes: {ex}");
				}

				try {
					using (var message = await client.GetMessageAsync (0)) {
						// TODO: assert that the message is byte-identical to what we expect
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					var count = await client.GetMessageCountAsync ();
					Assert.That (count, Is.EqualTo (7), "Expected 7 messages again");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageCount: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestProbedUidlSupport ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User | Pop3Capabilities.UIDL));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				client.GetMessageUids ();

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestProbedUidlSupportAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User | Pop3Capabilities.UIDL));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				await client.GetMessageUidsAsync ();

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestProbedUidlSupportError ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.Throws<NotSupportedException> (() => client.GetMessageUids ());
				Assert.Throws<NotSupportedException> (() => client.GetMessageUid (0));

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestProbedUidlSupportErrorAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "comcast.err.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (Pop3Capabilities.User));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (0));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.ThrowsAsync<NotSupportedException> (() => client.GetMessageUidsAsync ());
				Assert.ThrowsAsync<NotSupportedException> (() => client.GetMessageUidAsync (0));

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestEnableUTF8 ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "lang.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client ()) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						client.EnableUTF8 ();
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in EnableUTF8: {ex}");
					}

					// Try to enable UTF8 again even though we've done it. This should just no-op and not send another command.
					try {
						client.EnableUTF8 ();
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception from second call to EnableUTF8: {ex}");
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
		public async Task TestEnableUTF8Async ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "lang.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client ()) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						await client.EnableUTF8Async ();
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in EnableUTF8: {ex}");
					}

					// Try to enable UTF8 again even though we've done it. This should just no-op and not send another command.
					try {
						await client.EnableUTF8Async ();
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception from second call to EnableUTF8: {ex}");
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

		[Test]
		public void TestEnableUTF8AfterAuthenticate ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "lang.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client ()) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
					Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

					try {
						client.EnableUTF8 ();
						Assert.Fail ("EnableUTF8() should throw InvalidOperationException.");
					} catch (InvalidOperationException) {
						Assert.Pass ();
					} catch (Exception ex) {
						Assert.Fail ($"Unexpected exception thrown by EnableUTF8: {ex}");
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
		public async Task TestEnableUTF8AfterAuthenticateAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "lang.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client ()) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
					Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

					try {
						await client.EnableUTF8Async ();
						Assert.Fail ("EnableUTF8Async() should throw InvalidOperationException.");
					} catch (InvalidOperationException) {
						Assert.Pass ();
					} catch (Exception ex) {
						Assert.Fail ($"Unexpected exception thrown by EnableUTF8Async: {ex}");
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

		[Test]
		public void TestEnableUTF8NotSupported ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client ()) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						client.EnableUTF8 ();
						Assert.Fail ("EnableUTF8() should throw NotSupportedException.");
					} catch (NotSupportedException) {
						Assert.Pass ();
					} catch (Exception ex) {
						Assert.Fail ($"Unexpected exception thrown by EnableUTF8: {ex}");
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
		public async Task TestEnableUTF8NotSupportedAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client ()) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						await client.EnableUTF8Async ();
						Assert.Fail ("EnableUTF8Async() should throw NotSupportedException.");
					} catch (NotSupportedException) {
						Assert.Pass ();
					} catch (Exception ex) {
						Assert.Fail ($"Unexpected exception thrown by EnableUTF8Async: {ex}");
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

		[Test]
		public void TestGetMessageCountParseExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat-error1.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat-error2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat-error3.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageCount ());
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageCount ());
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageCount ());
				Assert.That (client.IsConnected, Is.True);

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestGetMessageCountParseExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat-error1.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat-error2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat-error3.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageCountAsync ());
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageCountAsync ());
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageCountAsync ());
				Assert.That (client.IsConnected, Is.True);

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestGetMessageSizeParseExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1-error1.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1-error2.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1-error3.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageSize (0));
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageSize (0));
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageSize (0));
				Assert.That (client.IsConnected, Is.True);

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestGetMessageSizeParseExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1-error1.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1-error2.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "comcast.list1-error3.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageSizeAsync (0));
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageSizeAsync (0));
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageSizeAsync (0));
				Assert.That (client.IsConnected, Is.True);

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestGetMessageSizesParseExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list-error1.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list-error2.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list-error3.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageSizes ());
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageSizes ());
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageSizes ());
				Assert.That (client.IsConnected, Is.True);

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestGetMessageSizesParseExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list-error1.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list-error2.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "comcast.list-error3.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client.ExpirePolicy, Is.EqualTo (31));

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
				Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageSizesAsync ());
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageSizesAsync ());
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageSizesAsync ());
				Assert.That (client.IsConnected, Is.True);

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestGetMessageUidParseExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1-error1.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1-error2.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);

				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageUid (0));
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageUid (0));
				Assert.That (client.IsConnected, Is.True);

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestGetMessageUidParseExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1-error1.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1-error2.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);

				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageUidAsync (0));
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageUidAsync (0));
				Assert.That (client.IsConnected, Is.True);

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestGetMessageUidsParseExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl-error1.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl-error2.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);

				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageUids ());
				Assert.That (client.IsConnected, Is.True);

				Assert.Throws<Pop3ProtocolException> (() => client.GetMessageUids ());
				Assert.That (client.IsConnected, Is.True);

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestGetMessageUidsParseExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl-error1.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl-error2.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);

				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageUidsAsync ());
				Assert.That (client.IsConnected, Is.True);

				Assert.ThrowsAsync<Pop3ProtocolException> (async () => await client.GetMessageUidsAsync ());
				Assert.That (client.IsConnected, Is.True);

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestSaslAuthentication ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

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
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

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
				string secrets;
				string line;

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith (commandPrefix, StringComparison.Ordinal))
						break;
				}

				Assert.That (line, Is.Not.Null, $"Authentication command not found: {commandPrefix}");

				if (line.Length > commandPrefix.Length) {
					secrets = line.Substring (commandPrefix.Length);

					var tokens = secrets.Split (' ');
					var expectedTokens = new string[tokens.Length];
					for (int i = 0; i < tokens.Length; i++)
						expectedTokens[i] = "********";

					var expected = string.Join (" ", expectedTokens);

					Assert.That (secrets, Is.EqualTo (expected), commandPrefix);
				}

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith (nextCommandPrefix, StringComparison.Ordinal))
						return;

					if (!line.StartsWith ("C: ", StringComparison.Ordinal))
						continue;

					secrets = line.Substring (3);

					Assert.That (secrets, Is.EqualTo ("********"), "SASL challenge");
				}

				Assert.Fail ("Did not find response.");
			}
		}

		[Test]
		public void TestRedactApop ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"),
				new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						client.EnableUTF8 ();
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in EnableUTF8: {ex}");
					}

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa2));
					Assert.That (client.AuthenticationMechanisms, Is.Empty);

					Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
					Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: APOP ", "C: CAPA");
			}
		}

		[Test]
		public async Task TestRedactApopAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"),
				new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

					try {
						await client.EnableUTF8Async ();
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in EnableUTF8: {ex}");
					}

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (LangCapa2));
					Assert.That (client.AuthenticationMechanisms, Is.Empty);

					Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
					Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: APOP ", "C: CAPA");
			}
		}

		[Test]
		public void TestRedactAuthentication ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					client.AuthenticationMechanisms.Remove ("GSSAPI");
					client.AuthenticationMechanisms.Remove ("NTLM");
					client.AuthenticationMechanisms.Remove ("PLAIN");

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
					Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: CAPA");
			}
		}

		[Test]
		public async Task TestRedactAuthenticationAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					client.AuthenticationMechanisms.Remove ("GSSAPI");
					client.AuthenticationMechanisms.Remove ("NTLM");
					client.AuthenticationMechanisms.Remove ("PLAIN");

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
					Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: CAPA");
			}
		}

		[Test]
		public void TestRedactUserPass ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
					Assert.That (client.AuthenticationMechanisms, Is.Empty);
					Assert.That (client.ExpirePolicy, Is.EqualTo (31));

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
					Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
					Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

					Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
					Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: USER ", "C: PASS");
				AssertRedacted (stream, "C: PASS ", "C: CAPA");
			}
		}

		[Test]
		public async Task TestRedactUserPassAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "comcast.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"),
				new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "comcast.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa1));
					Assert.That (client.AuthenticationMechanisms, Is.Empty);
					Assert.That (client.ExpirePolicy, Is.EqualTo (31));

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (ComcastCapa2));
					Assert.That (client.Implementation, Is.EqualTo ("ZimbraInc"));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
					Assert.That (client.ExpirePolicy, Is.EqualTo (-1));

					Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
					Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: USER ", "C: PASS");
				AssertRedacted (stream, "C: PASS ", "C: CAPA");
			}
		}

		[Test]
		public void TestRedactSaslAuthentication ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					try {
						var credentials = new NetworkCredential ("username", "password");
						var sasl = new SaslMechanismLogin (credentials);

						client.Authenticate (sasl);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
					Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

					try {
						client.Disconnect (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: CAPA");
			}
		}

		[Test]
		public async Task TestRedactSaslAuthenticationAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var stream = new MemoryStream ()) {
				using (var client = new Pop3Client (new ProtocolLogger (stream, true) { RedactSecrets = true })) {
					try {
						await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					try {
						var credentials = new NetworkCredential ("username", "password");
						var sasl = new SaslMechanismLogin (credentials);

						await client.AuthenticateAsync (sasl);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
					Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

					try {
						await client.DisconnectAsync (true);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
					}

					Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
				}

				AssertRedacted (stream, "C: AUTH LOGIN", "C: CAPA");
			}
		}

		[Test]
		public void TestExchangePop3Client ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "exchange.uidl.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "exchange.retr1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				// Note: remove these auth mechanisms to force LOGIN auth
				client.AuthenticationMechanisms.Remove ("GSSAPI");
				client.AuthenticationMechanisms.Remove ("NTLM");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				try {
					var uids = client.GetMessageUids ();
					Assert.That (uids, Has.Count.EqualTo (7), "Expected 7 uids");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageUids: {ex}");
				}

				try {
					using (var message = client.GetMessage (0)) {
						// TODO: assert that the message is byte-identical to what we expect
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
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
		public async Task TestExchangePop3ClientAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "exchange.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"),
				new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "exchange.uidl.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "exchange.retr1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				// Note: remove these auth mechanisms to force LOGIN auth
				client.AuthenticationMechanisms.Remove ("GSSAPI");
				client.AuthenticationMechanisms.Remove ("NTLM");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ExchangeCapa));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client, Has.Count.EqualTo (7), "Expected 7 messages");
				Assert.That (client.Size, Is.EqualTo (1800662), "Expected 1800662 octets");

				try {
					var uids = await client.GetMessageUidsAsync ();
					Assert.That (uids, Has.Count.EqualTo (7), "Expected 7 uids");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageUids: {ex}");
				}

				try {
					using (var message = await client.GetMessageAsync (0)) {
						// TODO: assert that the message is byte-identical to what we expect
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		static List<Pop3ReplayCommand> CreateGMailCommands ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1.txt"),
				new Pop3ReplayCommand ("UIDL 2\r\n", "gmail.uidl2.txt"),
				new Pop3ReplayCommand ("UIDL 3\r\n", "gmail.uidl3.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "gmail.list.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "gmail.list1.txt"),
				new Pop3ReplayCommand ("LIST 2\r\n", "gmail.list2.txt"),
				new Pop3ReplayCommand ("LIST 3\r\n", "gmail.list3.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"),
				new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\nTOP 2 0\r\nTOP 3 0\r\n", "gmail.top123.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\nTOP 2 0\r\nTOP 3 0\r\n", "gmail.top123.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"),
				new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"),
				new Pop3ReplayCommand ("NOOP\r\n", "gmail.noop.txt"),
				new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			return commands;
		}

		static void TestGMailPop3Client (List<Pop3ReplayCommand> commands, bool disablePipelining)
		{
			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				if (disablePipelining)
					client.Capabilities &= ~Pop3Capabilities.Pipelining;

				var uids = client.GetMessageUids ();
				Assert.That (uids, Has.Count.EqualTo (3));
				Assert.That (uids[0], Is.EqualTo ("101"));
				Assert.That (uids[1], Is.EqualTo ("102"));
				Assert.That (uids[2], Is.EqualTo ("103"));

				for (int i = 0; i < 3; i++) {
					var uid = client.GetMessageUid (i);

					Assert.That (uid, Is.EqualTo (uids[i]));
				}

				var sizes = client.GetMessageSizes ();
				Assert.That (sizes, Has.Count.EqualTo (3));
				Assert.That (sizes[0], Is.EqualTo (1024));
				Assert.That (sizes[1], Is.EqualTo (1025));
				Assert.That (sizes[2], Is.EqualTo (1026));

				for (int i = 0; i < 3; i++) {
					var size = client.GetMessageSize (i);

					Assert.That (size, Is.EqualTo (sizes[i]));
				}

				try {
					using (var message = client.GetMessage (0)) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
							jpeg.Position = 0;

							using (var md5 = MD5.Create ()) {
								var md5sum = HexEncode (md5.ComputeHash (jpeg));

								Assert.That (md5sum, Is.EqualTo ("5b1b8b2c9300c9cd01099f44e1155e2b"), "MD5 checksums do not match.");
							}
						}
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					var messages = client.GetMessages (0, 3);

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
							jpeg.Position = 0;

							using (var md5 = MD5.Create ()) {
								var md5sum = HexEncode (md5.ComputeHash (jpeg));

								Assert.That (md5sum, Is.EqualTo ("5b1b8b2c9300c9cd01099f44e1155e2b"), "MD5 checksums do not match.");
							}
						}

						message.Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessages: {ex}");
				}

				try {
					var messages = client.GetMessages (new [] { 0, 1, 2 });

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
							jpeg.Position = 0;

							using (var md5 = MD5.Create ()) {
								var md5sum = HexEncode (md5.ComputeHash (jpeg));

								Assert.That (md5sum, Is.EqualTo ("5b1b8b2c9300c9cd01099f44e1155e2b"), "MD5 checksums do not match.");
							}
						}

						message.Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessages: {ex}");
				}

				try {
					var header = client.GetMessageHeaders (0);

					Assert.That (header[HeaderId.Subject], Is.EqualTo ("Test inline image"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageHeaders: {ex}");
				}

				try {
					var headers = client.GetMessageHeaders (0, 3);

					Assert.That (headers, Has.Count.EqualTo (3));
					for (int i = 0; i < headers.Count; i++)
						Assert.That (headers[i][HeaderId.Subject], Is.EqualTo ("Test inline image"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageHeaders: {ex}");
				}

				try {
					var headers = client.GetMessageHeaders (new [] { 0, 1, 2 });

					Assert.That (headers, Has.Count.EqualTo (3));
					for (int i = 0; i < headers.Count; i++)
						Assert.That (headers[i][HeaderId.Subject], Is.EqualTo ("Test inline image"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageHeaders: {ex}");
				}

				try {
					using (var stream = client.GetStream (0)) {
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetStream: {ex}");
				}

				try {
					var streams = client.GetStreams (0, 3);

					Assert.That (streams, Has.Count.EqualTo (3));
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetStreams: {ex}");
				}

				try {
					var streams = client.GetStreams (new int[] { 0, 1, 2 });

					Assert.That (streams, Has.Count.EqualTo (3));
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetStreams: {ex}");
				}

				try {
					client.NoOp ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in NoOp: {ex}");
				}

				try {
					client.DeleteMessage (0);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteMessage: {ex}");
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					client.DeleteMessages (new [] { 0, 1, 2 });
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteMessages: {ex}");
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					client.DeleteMessages (0, 3);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteMessages: {ex}");
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					client.DeleteAllMessages ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteAllMessages: {ex}");
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
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
		public void TestGMailPop3Client ()
		{
			TestGMailPop3Client (CreateGMailCommands (), false);
		}

		static async Task TestGMailPop3ClientAsync (List<Pop3ReplayCommand> commands, bool disablePipelining)
		{
			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				if (disablePipelining)
					client.Capabilities &= ~Pop3Capabilities.Pipelining;

				var uids = await client.GetMessageUidsAsync ();
				Assert.That (uids, Has.Count.EqualTo (3));
				Assert.That (uids[0], Is.EqualTo ("101"));
				Assert.That (uids[1], Is.EqualTo ("102"));
				Assert.That (uids[2], Is.EqualTo ("103"));

				for (int i = 0; i < 3; i++) {
					var uid = await client.GetMessageUidAsync (i);

					Assert.That (uid, Is.EqualTo (uids[i]));
				}

				var sizes = await client.GetMessageSizesAsync ();
				Assert.That (sizes, Has.Count.EqualTo (3));
				Assert.That (sizes[0], Is.EqualTo (1024));
				Assert.That (sizes[1], Is.EqualTo (1025));
				Assert.That (sizes[2], Is.EqualTo (1026));

				for (int i = 0; i < 3; i++) {
					var size = await client.GetMessageSizeAsync (i);

					Assert.That (size, Is.EqualTo (sizes[i]));
				}

				try {
					var message = await client.GetMessageAsync (0);

					using (var jpeg = new MemoryStream ()) {
						var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

						attachment.Content.DecodeTo (jpeg);
						jpeg.Position = 0;

						using (var md5 = MD5.Create ()) {
							var md5sum = HexEncode (md5.ComputeHash (jpeg));

							Assert.That (md5sum, Is.EqualTo ("5b1b8b2c9300c9cd01099f44e1155e2b"), "MD5 checksums do not match.");
						}
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessage: {ex}");
				}

				try {
					var messages = await client.GetMessagesAsync (0, 3);

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
							jpeg.Position = 0;

							using (var md5 = MD5.Create ()) {
								var md5sum = HexEncode (md5.ComputeHash (jpeg));

								Assert.That (md5sum, Is.EqualTo ("5b1b8b2c9300c9cd01099f44e1155e2b"), "MD5 checksums do not match.");
							}
						}

						message.Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessages: {ex}");
				}

				try {
					var messages = await client.GetMessagesAsync (new [] { 0, 1, 2 });

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
							jpeg.Position = 0;

							using (var md5 = MD5.Create ()) {
								var md5sum = HexEncode (md5.ComputeHash (jpeg));

								Assert.That (md5sum, Is.EqualTo ("5b1b8b2c9300c9cd01099f44e1155e2b"), "MD5 checksums do not match.");
							}
						}

						message.Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessages: {ex}");
				}

				try {
					var header = await client.GetMessageHeadersAsync (0);

					Assert.That (header[HeaderId.Subject], Is.EqualTo ("Test inline image"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageHeaders: {ex}");
				}

				try {
					var headers = await client.GetMessageHeadersAsync (0, 3);

					Assert.That (headers, Has.Count.EqualTo (3));
					for (int i = 0; i < headers.Count; i++)
						Assert.That (headers[i][HeaderId.Subject], Is.EqualTo ("Test inline image"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageHeaders: {ex}");
				}

				try {
					var headers = await client.GetMessageHeadersAsync (new [] { 0, 1, 2 });

					Assert.That (headers, Has.Count.EqualTo (3));
					for (int i = 0; i < headers.Count; i++)
						Assert.That (headers[i][HeaderId.Subject], Is.EqualTo ("Test inline image"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetMessageHeaders: {ex}");
				}

				try {
					using (var stream = await client.GetStreamAsync (0)) {
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetStream: {ex}");
				}

				try {
					var streams = await client.GetStreamsAsync (0, 3);

					Assert.That (streams, Has.Count.EqualTo (3));
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetStreams: {ex}");
				}

				try {
					var streams = await client.GetStreamsAsync (new int[] { 0, 1, 2 });

					Assert.That (streams, Has.Count.EqualTo (3));
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in GetStreams: {ex}");
				}

				try {
					await client.NoOpAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in NoOp: {ex}");
				}

				try {
					await client.DeleteMessageAsync (0);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteMessage: {ex}");
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					await client.DeleteMessagesAsync (new [] { 0, 1, 2 });
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteMessages: {ex}");
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					await client.DeleteMessagesAsync (0, 3);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteMessages: {ex}");
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					await client.DeleteAllMessagesAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in DeleteAllMessages: {ex}");
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Reset: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public Task TestGMailPop3ClientAsync ()
		{
			return TestGMailPop3ClientAsync (CreateGMailCommands (), false);
		}

		static List<Pop3ReplayCommand> CreateGMailCommandsNoPipelining ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl.txt"),
				new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1.txt"),
				new Pop3ReplayCommand ("UIDL 2\r\n", "gmail.uidl2.txt"),
				new Pop3ReplayCommand ("UIDL 3\r\n", "gmail.uidl3.txt"),
				new Pop3ReplayCommand ("LIST\r\n", "gmail.list.txt"),
				new Pop3ReplayCommand ("LIST 1\r\n", "gmail.list1.txt"),
				new Pop3ReplayCommand ("LIST 2\r\n", "gmail.list2.txt"),
				new Pop3ReplayCommand ("LIST 3\r\n", "gmail.list3.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 2\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 3\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 2\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 3\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 2 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 3 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 2 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("TOP 3 0\r\n", "gmail.top.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 2\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 3\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 2\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 3\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("NOOP\r\n", "gmail.noop.txt"),
				new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("DELE 2\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("DELE 3\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("DELE 2\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("DELE 3\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("DELE 2\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("DELE 3\r\n", "gmail.dele.txt"),
				new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			return commands;
		}

		[Test]
		public void TestGMailPop3ClientNoPipelining ()
		{
			TestGMailPop3Client (CreateGMailCommandsNoPipelining (), true);
		}

		[Test]
		public Task TestGMailPop3ClientNoPipeliningAsync ()
		{
			return TestGMailPop3ClientAsync (CreateGMailCommandsNoPipelining (), true);
		}

		[Test]
		public void TestGetEnumerator ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 2\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 3\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 2\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("RETR 3\r\n", "gmail.retr1.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				int count = 0;
				foreach (var message in client)
					count++;
				Assert.That (count, Is.EqualTo (3));

				count = 0;
				foreach (var message in (IEnumerable) client)
					count++;
				Assert.That (count, Is.EqualTo (3));

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestLangExtension ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"),
				new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"),
				new Pop3ReplayCommand ("LANG\r\n", "lang.getlang.txt"),
				new Pop3ReplayCommand ("LANG en\r\n", "lang.setlang.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.EnableUTF8 ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in EnableUTF8: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (LangCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);

				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				var languages = client.GetLanguages ();
				Assert.That (languages, Has.Count.EqualTo (6));
				Assert.That (languages[0].Language, Is.EqualTo ("en"));
				Assert.That (languages[0].Description, Is.EqualTo ("English"));
				Assert.That (languages[1].Language, Is.EqualTo ("en-boont"));
				Assert.That (languages[1].Description, Is.EqualTo ("English Boontling dialect"));
				Assert.That (languages[2].Language, Is.EqualTo ("de"));
				Assert.That (languages[2].Description, Is.EqualTo ("Deutsch"));
				Assert.That (languages[3].Language, Is.EqualTo ("it"));
				Assert.That (languages[3].Description, Is.EqualTo ("Italiano"));
				Assert.That (languages[4].Language, Is.EqualTo ("es"));
				Assert.That (languages[4].Description, Is.EqualTo ("Espanol"));
				Assert.That (languages[5].Language, Is.EqualTo ("sv"));
				Assert.That (languages[5].Description, Is.EqualTo ("Svenska"));

				client.SetLanguage ("en");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public async Task TestLangExtensionAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "lang.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"),
				new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"),
				new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "lang.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"),
				new Pop3ReplayCommand ("LANG\r\n", "lang.getlang.txt"),
				new Pop3ReplayCommand ("LANG en\r\n", "lang.setlang.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (LangCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					await client.EnableUTF8Async ();
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in EnableUTF8: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (LangCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);

				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				var languages = await client.GetLanguagesAsync ();
				Assert.That (languages, Has.Count.EqualTo (6));
				Assert.That (languages[0].Language, Is.EqualTo ("en"));
				Assert.That (languages[0].Description, Is.EqualTo ("English"));
				Assert.That (languages[1].Language, Is.EqualTo ("en-boont"));
				Assert.That (languages[1].Description, Is.EqualTo ("English Boontling dialect"));
				Assert.That (languages[2].Language, Is.EqualTo ("de"));
				Assert.That (languages[2].Description, Is.EqualTo ("Deutsch"));
				Assert.That (languages[3].Language, Is.EqualTo ("it"));
				Assert.That (languages[3].Description, Is.EqualTo ("Italiano"));
				Assert.That (languages[4].Language, Is.EqualTo ("es"));
				Assert.That (languages[4].Description, Is.EqualTo ("Espanol"));
				Assert.That (languages[5].Language, Is.EqualTo ("sv"));
				Assert.That (languages[5].Description, Is.EqualTo ("Svenska"));

				await client.SetLanguageAsync ("en");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestLangNotSupported ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				Assert.Throws<NotSupportedException> (() => client.GetLanguages ());
				Assert.Throws<NotSupportedException> (() => client.SetLanguage ("en"));

				Assert.ThrowsAsync<NotSupportedException> (() => client.GetLanguagesAsync ());
				Assert.ThrowsAsync<NotSupportedException> (() => client.SetLanguageAsync ("en"));

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Disconnect: {ex}");
				}

				Assert.That (client.IsConnected, Is.False, "Failed to disconnect");
			}
		}

		[Test]
		public void TestParseExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1-parse-error.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.retr1-parse-error.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					client.Connect (new Pop3ReplayStream (commands, false), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				try {
					client.GetMessage (0);
					Assert.Fail ("Expected GetMessage() to throw Pop3ProtocolException");
				} catch (Pop3ProtocolException pex) {
					Assert.That (pex.InnerException, Is.InstanceOf<FormatException> ());
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception thrown by GetMessage: {ex}");
				}

				try {
					client.GetMessageHeaders (0);
					Assert.Fail ("Expected GetMessageHeaders() to throw Pop3ProtocolException");
				} catch (Pop3ProtocolException pex) {
					Assert.That (pex.InnerException, Is.InstanceOf<FormatException> ());
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception thrown by GetMessageHeaders: {ex}");
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
		public async Task TestParseExceptionsAsync ()
		{
			var commands = new List<Pop3ReplayCommand> {
				new Pop3ReplayCommand ("", "gmail.greeting.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"),
				new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"),
				new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"),
				new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"),
				new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"),
				new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1-parse-error.txt"),
				new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.retr1-parse-error.txt"),
				new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt")
			};

			using (var client = new Pop3Client ()) {
				try {
					await client.ConnectAsync (new Pop3ReplayStream (commands, true), "localhost", 110, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa1));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailCapa2));
				Assert.That (client.AuthenticationMechanisms, Is.Empty);
				Assert.That (client, Has.Count.EqualTo (3), "Expected 3 messages");
				Assert.That (client.Size, Is.EqualTo (221409), "Expected 221409 octets");

				try {
					await client.GetMessageAsync (0);
					Assert.Fail ("Expected GetMessageAsync() to throw Pop3ProtocolException");
				} catch (Pop3ProtocolException pex) {
					Assert.That (pex.InnerException, Is.InstanceOf<FormatException> ());
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception thrown by GetMessageAsync: {ex}");
				}

				try {
					await client.GetMessageHeadersAsync (0);
					Assert.Fail ("Expected GetMessageHeadersAsync() to throw Pop3ProtocolException");
				} catch (Pop3ProtocolException pex) {
					Assert.That (pex.InnerException, Is.InstanceOf<FormatException> ());
				} catch (Exception ex) {
					Assert.Fail ($"Unexpected exception thrown by GetMessageHeadersAsync: {ex}");
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
}
