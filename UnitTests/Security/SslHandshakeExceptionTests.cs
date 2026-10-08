//
// SslHandshakeExceptionTests.cs
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

using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MailKit;
using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SslHandshakeExceptionTests
	{
		const string HelpLink = "https://github.com/jstedfast/MailKit/blob/master/FAQ.md#ssl-handshake-exception";

		[Test]
		public void TestHelpLink ()
		{
			SslCertificateValidationInfo info = null;

			Assert.That (new SslHandshakeException ("message", new IOException ("I/O Error.")).HelpLink, Is.EqualTo (HelpLink), "(string, Exception)");
			Assert.That (new SslHandshakeException ("message").HelpLink, Is.EqualTo (HelpLink), "(string)");
			Assert.That (new SslHandshakeException ().HelpLink, Is.EqualTo (HelpLink), "()");

			var ex = SslHandshakeException.Create (ref info, new AggregateException ("Aggregate errors.", new IOException (), new IOException ()), false, "IMAP", "localhost", 993, 993, 143);
			Assert.That (ex.HelpLink, Is.EqualTo (HelpLink), "Create");
		}

		class FakeClient : MailService
		{
			SslCertificateValidationInfo sslValidationInfo;
			int timeout = 2 * 60 * 1000;
			string hostName;

			public FakeClient (IProtocolLogger logger) : base (logger)
			{
			}

			public override object SyncRoot => throw new NotImplementedException ();

			public override HashSet<string> AuthenticationMechanisms => throw new NotImplementedException ();

			public override bool IsConnected => throw new NotImplementedException ();

			public override bool IsSecure => throw new NotImplementedException ();

			public override bool IsEncrypted => throw new NotImplementedException ();

			public override bool IsSigned => throw new NotImplementedException ();

			public override SslProtocols SslProtocol => throw new NotImplementedException ();

			public override CipherAlgorithmType? SslCipherAlgorithm => throw new NotImplementedException ();

			public override int? SslCipherStrength => throw new NotImplementedException ();

#if NET5_0_OR_GREATER
			public override TlsCipherSuite? SslCipherSuite => throw new NotImplementedException ();
#endif

			public override HashAlgorithmType? SslHashAlgorithm => throw new NotImplementedException ();

			public override int? SslHashStrength => throw new NotImplementedException ();

			public override ExchangeAlgorithmType? SslKeyExchangeAlgorithm => throw new NotImplementedException ();

			public override int? SslKeyExchangeStrength => throw new NotImplementedException ();

			public override bool IsAuthenticated => throw new NotImplementedException ();

			public override int Timeout {
				get { return timeout; }
				set { timeout = value; }
			}

			protected override string Protocol => throw new NotImplementedException ();

			public override void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override Task AuthenticateAsync (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override Task AuthenticateAsync (SaslMechanism mechanism, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
			{
				bool valid;

				sslValidationInfo?.Dispose ();
				sslValidationInfo = null;

				if (ServerCertificateValidationCallback != null) {
					valid = ServerCertificateValidationCallback (hostName, certificate, chain, sslPolicyErrors);
				} else if (ServicePointManager.ServerCertificateValidationCallback != null) {
					valid = ServicePointManager.ServerCertificateValidationCallback (hostName, certificate, chain, sslPolicyErrors);
				} else {
					valid = DefaultServerCertificateValidationCallback (hostName, certificate, chain, sslPolicyErrors);
				}

				if (!valid) {
					// Note: The SslHandshakeException.Create() method will nullify this once it's done using it.
					sslValidationInfo = new SslCertificateValidationInfo (hostName, certificate, chain, sslPolicyErrors);
				}

				return valid;
			}

			public override void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
			{
				using (var stream = ConnectNetwork (host, port, cancellationToken)) {
					hostName = host;

					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "HTTP", host, port, 443, 80);
					}
				}
			}

			public override void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override void Connect (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override async Task ConnectAsync (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
			{
				using (var stream = await ConnectNetworkAsync (host, port, cancellationToken).ConfigureAwait (false)) {
					hostName = host;

					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						await ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).ConfigureAwait (false);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "HTTP", host, port, 443, 80);
					}
				}
			}

			public override Task ConnectAsync (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override Task ConnectAsync (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override void Disconnect (bool quit, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override Task DisconnectAsync (bool quit, CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override void NoOp (CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}

			public override Task NoOpAsync (CancellationToken cancellationToken = default)
			{
				throw new NotImplementedException ();
			}
		}

		// Note: The badssl.com certificates get renewed periodically (often with a different issuing CA, intermediate
		// and/or root), so these helpers only assert properties that are inherent to each badssl.com test endpoint
		// rather than pinning issuers, serial numbers or thumbprints.
		static string GetCommonName (X509Certificate2 certificate)
		{
			return certificate.GetNameInfo (X509NameType.SimpleName, false);
		}

		static void AssertSelfSigned (X509Certificate2 certificate, string message)
		{
			Assert.That (certificate.SubjectName.RawData, Is.EqualTo (certificate.IssuerName.RawData), message);
		}

		static void AssertBadSslRootCertificateAuthority (X509Certificate2 root, X509Certificate2 server)
		{
			Assert.That (root.Thumbprint, Is.Not.EqualTo (server.Thumbprint), "RootCertificateAuthority should not be the server certificate");
			AssertSelfSigned (root, "RootCertificateAuthority should be self-signed");
		}

		static void AssertBadSslExpiredServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (GetCommonName (certificate), Is.EqualTo ("*.badssl.com"), "CommonName");
			Assert.That (certificate.NotAfter, Is.LessThan (DateTime.Now), "NotAfter");
		}

		[Test]
		public void TestExpiredCertificateValidationFailure ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					client.Connect ("expired.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with expired.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslExpiredServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslRootCertificateAuthority (root, (X509Certificate2) ex.ServerCertificate);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestExpiredCertificateValidationFailureAsync ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					await client.ConnectAsync ("expired.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with expired.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslExpiredServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslRootCertificateAuthority (root, (X509Certificate2) ex.ServerCertificate);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		static void AssertBadSslWrongHostServerCertificate (X509Certificate2 certificate)
		{
			// Note: wrong.host.badssl.com serves the *.badssl.com certificate which does not match the 2-level subdomain.
			Assert.That (GetCommonName (certificate), Is.EqualTo ("*.badssl.com"), "CommonName");
		}

		[Test]
		public void TestWrongHostCertificateValidationFailure ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					client.Connect ("wrong.host.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with wrong.host.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslWrongHostServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslRootCertificateAuthority (root, (X509Certificate2) ex.ServerCertificate);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestWrongHostCertificateValidationFailureAsync ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					await client.ConnectAsync ("wrong.host.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with wrong.host.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslWrongHostServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslRootCertificateAuthority (root, (X509Certificate2) ex.ServerCertificate);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		static void AssertBadSslSelfSignedServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (GetCommonName (certificate), Is.EqualTo ("*.badssl.com"), "CommonName");
			AssertSelfSigned (certificate, "ServerCertificate should be self-signed");
		}

		[Test]
		public void TestSelfSignedCertificateValidationFailure ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					client.Connect ("self-signed.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with self-signed.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslSelfSignedServerCertificate ((X509Certificate2) ex.ServerCertificate);

					Assert.That (ex.RootCertificateAuthority, Is.Null, "RootCertificateAuthority");
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestSelfSignedCertificateValidationFailureAsync ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					await client.ConnectAsync ("self-signed.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with self-signed.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslSelfSignedServerCertificate ((X509Certificate2) ex.ServerCertificate);

					Assert.That (ex.RootCertificateAuthority, Is.Null, "RootCertificateAuthority");
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		const string BadSslUntrustedRootCommonName = "BadSSL Untrusted Root Certificate Authority";

		public static void AssertBadSslUntrustedRootServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (GetCommonName (certificate), Is.EqualTo ("*.badssl.com"), "CommonName");
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, true), Is.EqualTo (BadSslUntrustedRootCommonName), "Issuer");
		}

		public static void AssertBadSslUntrustedRootCACertificate (X509Certificate2 certificate)
		{
			Assert.That (GetCommonName (certificate), Is.EqualTo (BadSslUntrustedRootCommonName), "CommonName");
			AssertSelfSigned (certificate, "RootCertificateAuthority should be self-signed");
		}

		[Test]
		public void TestUntrustedRootCertificateValidationFailure ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					client.Connect ("untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestUntrustedRootCertificateValidationFailureAsync ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					await client.ConnectAsync ("untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		static void AssertBadSslRevokedServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (GetCommonName (certificate), Is.EqualTo ("revoked.badssl.com"), "CommonName");
		}

		[Test]
		public void TestRevokedCertificateValidationFailure ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					client.Connect ("revoked.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with revoked.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslRevokedServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslRootCertificateAuthority (root, (X509Certificate2) ex.ServerCertificate);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestRevokedCertificateValidationFailureAsync ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					await client.ConnectAsync ("revoked.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with revoked.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					AssertBadSslRevokedServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						AssertBadSslRootCertificateAuthority (root, (X509Certificate2) ex.ServerCertificate);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public void TestSslConnectOnPlainTextPortFailure ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					client.Connect ("www.google.com", 80, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with www.google.com:80.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Null, "ServerCertificate");
					Assert.That (ex.RootCertificateAuthority, Is.Null, "RootCertificateAuthority");
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestSslConnectOnPlainTextPortFailureAsync ()
		{
			Assert.Throws<ArgumentNullException> (() => new FakeClient (null));

			using (var client = new FakeClient (new NullProtocolLogger ())) {
				try {
					await client.ConnectAsync ("www.google.com", 80, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with www.google.com:80.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Null, "ServerCertificate");
					Assert.That (ex.RootCertificateAuthority, Is.Null, "RootCertificateAuthority");
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}
	}
}
