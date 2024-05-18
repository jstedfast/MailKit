//
// SslHandshakeExceptionTests.cs
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
using System.Runtime.Serialization.Formatters.Binary;

using MailKit;
using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SslHandshakeExceptionTests
	{
		const string HelpLink = "https://github.com/jstedfast/MailKit/blob/master/FAQ.md#ssl-handshake-exception";

#if NET6_0

		[Test]
		public void TestSerialization ()
		{
			var expected = new SslHandshakeException ("Bad boys, bad boys. Whatcha gonna do?", new IOException ("I/O Error."));
			SslCertificateValidationInfo info = null;

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}

			expected = new SslHandshakeException ("Bad boys, bad boys. Whatcha gonna do?");

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}

			expected = new SslHandshakeException ();

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (ref info, new AggregateException ("Aggregate errors.", new IOException (), new IOException ()), false, "IMAP", "localhost", 993, 993, 143);

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (ref info, new AggregateException ("Aggregate errors.", new IOException (), new IOException ()), true, "IMAP", "localhost", 143, 993, 143);

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (ref info, new AggregateException ("Aggregate errors.", new IOException ()), false, "IMAP", "localhost", 993, 993, 143);

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}

			expected = SslHandshakeException.Create (ref info, new AggregateException ("Aggregate errors.", new IOException ()), true, "IMAP", "localhost", 143, 993, 143);

			Assert.That (expected.HelpLink, Is.EqualTo (HelpLink), "Unexpected HelpLink.");

			using (var stream = new MemoryStream ()) {
				var formatter = new BinaryFormatter ();
				formatter.Serialize (stream, expected);
				stream.Position = 0;

				var ex = (SslHandshakeException) formatter.Deserialize (stream);
				Assert.That (ex.Message, Is.EqualTo (expected.Message), "Unexpected Message.");
				Assert.That (ex.HelpLink, Is.EqualTo (expected.HelpLink), "Unexpected HelpLink.");
			}
		}

#endif // NET6_0

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
					sslValidationInfo = new SslCertificateValidationInfo (sender, certificate, chain, sslPolicyErrors);
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

		static void AssertBadSslExpiredServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("*.badssl.com"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=COMODO RSA Domain Validation Secure Server CA, O=COMODO CA Limited, L=Salford, S=Greater Manchester, C=GB"), "Issuer");
			//Assert.That (certificate.SerialNumber, Is.EqualTo ("008040A36688A3B1F2"), "SerialNumber");
			//Assert.That (certificate.Thumbprint, Is.EqualTo ("209BADBBC9E63BBFFC301B3E30C5B51216FCE81D"), "Thumbprint");
		}

		static void AssertBadSslExpiredCACertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("COMODO RSA Certification Authority"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=COMODO RSA Certification Authority, O=COMODO CA Limited, L=Salford, S=Greater Manchester, C=GB"), "Issuer");
			Assert.That (certificate.SerialNumber, Is.EqualTo ("4CAAF9CADB636FE01FF74ED85B03869D"), "SerialNumber");
			Assert.That (certificate.Thumbprint, Is.EqualTo ("AFE5D244A8D1194230FF479FE2F897BBCD7A8CB4"), "Thumbprint");
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
						AssertBadSslExpiredCACertificate (root);
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
						AssertBadSslExpiredCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		static void AssertBadSslWrongHostServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("*.badssl.com"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=R3, O=Let's Encrypt, C=US"), "Issuer");
			//Assert.That (certificate.SerialNumber, Is.EqualTo ("008040A36688A3B1F2"), "SerialNumber");
			//Assert.That (certificate.Thumbprint, Is.EqualTo ("209BADBBC9E63BBFFC301B3E30C5B51216FCE81D"), "Thumbprint");
		}

		static void AssertBadSslWrongHostCACertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("ISRG Root X1"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=ISRG Root X1, O=Internet Security Research Group, C=US"), "Issuer");
			Assert.That (certificate.SerialNumber, Is.EqualTo ("008210CFB0D240E3594463E0BB63828B00"), "SerialNumber");
			Assert.That (certificate.Thumbprint, Is.EqualTo ("CABD2A79A1076A31F21D253635CB039D4329A5E8"), "Thumbprint");
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
						AssertBadSslWrongHostCACertificate (root);
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
						AssertBadSslWrongHostCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		static void AssertBadSslSelfSignedServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("*.badssl.com"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=*.badssl.com, O=BadSSL, L=San Francisco, S=California, C=US"), "Issuer");
			//Assert.That (certificate.SerialNumber, Is.EqualTo ("008040A36688A3B1F2"), "SerialNumber");
			//Assert.That (certificate.Thumbprint, Is.EqualTo ("209BADBBC9E63BBFFC301B3E30C5B51216FCE81D"), "Thumbprint");
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

		public static void AssertBadSslUntrustedRootServerCertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("*.badssl.com"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=BadSSL Untrusted Root Certificate Authority, O=BadSSL, L=San Francisco, S=California, C=US"), "Issuer");
			//Assert.That (certificate.SerialNumber, Is.EqualTo ("008040A36688A3B1F2"), "SerialNumber");
			//Assert.That (certificate.Thumbprint, Is.EqualTo ("209BADBBC9E63BBFFC301B3E30C5B51216FCE81D"), "Thumbprint");
		}

		public static void AssertBadSslUntrustedRootCACertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("BadSSL Untrusted Root Certificate Authority"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=BadSSL Untrusted Root Certificate Authority, O=BadSSL, L=San Francisco, S=California, C=US"), "Issuer");
			Assert.That (certificate.SerialNumber, Is.EqualTo ("0097A0FCFAD7E528FD"), "SerialNumber");
			Assert.That (certificate.Thumbprint, Is.EqualTo ("7890C8934D5869B25D2F8D0D646F9A5D7385BA85"), "Thumbprint");
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
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("revoked.badssl.com"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=R3, O=Let's Encrypt, C=US"), "Issuer");
			//Assert.That (certificate.SerialNumber, Is.EqualTo ("008040A36688A3B1F2"), "SerialNumber");
			//Assert.That (certificate.Thumbprint, Is.EqualTo ("209BADBBC9E63BBFFC301B3E30C5B51216FCE81D"), "Thumbprint");
		}

		static void AssertBadSslRevokedCACertificate (X509Certificate2 certificate)
		{
			Assert.That (certificate.GetNameInfo (X509NameType.SimpleName, false), Is.EqualTo ("ISRG Root X1"), "CommonName");
			Assert.That (certificate.Issuer, Is.EqualTo ("CN=ISRG Root X1, O=Internet Security Research Group, C=US"), "Issuer");
			Assert.That (certificate.SerialNumber, Is.EqualTo ("008210CFB0D240E3594463E0BB63828B00"), "SerialNumber");
			Assert.That (certificate.Thumbprint, Is.EqualTo ("CABD2A79A1076A31F21D253635CB039D4329A5E8"), "Thumbprint");
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
						AssertBadSslRevokedCACertificate (root);
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
						AssertBadSslRevokedCACertificate (root);
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
