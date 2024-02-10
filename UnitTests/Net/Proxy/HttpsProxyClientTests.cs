//
// HttpsProxyClientTests.cs
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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using MailKit.Net.Proxy;

namespace UnitTests.Net.Proxy {
	[TestFixture]
	public class HttpsProxyClientTests
	{
		const int ConnectTimeout = 5 * 1000; // 5 seconds
		readonly X509Certificate2 certificate;

		public HttpsProxyClientTests ()
		{
			using (var rsa = RSA.Create (4096)) {
				var req = new CertificateRequest ("cn=MailKit Proxy", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
				req.CertificateExtensions.Add (new X509KeyUsageExtension (X509KeyUsageFlags.DigitalSignature, true));

				var cert = req.CreateSelfSigned (DateTimeOffset.Now, DateTimeOffset.Now.AddYears (5));

				certificate = new X509Certificate2 (cert.Export (X509ContentType.Pfx, "password"), "password", X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
			}
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("user", "password");
			var proxy = new HttpsProxyClient ("http.proxy.com", 0, credentials);

			Assert.Throws<ArgumentNullException> (() => new HttpsProxyClient (null, 1080));
			Assert.Throws<ArgumentException> (() => new HttpsProxyClient (string.Empty, 1080));
			Assert.Throws<ArgumentOutOfRangeException> (() => new HttpsProxyClient (proxy.ProxyHost, -1));
			Assert.Throws<ArgumentNullException> (() => new HttpsProxyClient (proxy.ProxyHost, 1080, null));

			Assert.That (proxy.ProxyPort, Is.EqualTo (1080));
			Assert.That (proxy.ProxyHost, Is.EqualTo ("http.proxy.com"));
			Assert.That (proxy.ProxyCredentials, Is.EqualTo (credentials));

			Assert.Throws<ArgumentNullException> (() => proxy.Connect (null, 443));
			Assert.Throws<ArgumentNullException> (() => proxy.Connect (null, 443, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentNullException> (async () => await proxy.ConnectAsync (null, 443));
			Assert.ThrowsAsync<ArgumentNullException> (async () => await proxy.ConnectAsync (null, 443, ConnectTimeout));

			Assert.Throws<ArgumentException> (() => proxy.Connect (string.Empty, 443));
			Assert.Throws<ArgumentException> (() => proxy.Connect (string.Empty, 443, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentException> (async () => await proxy.ConnectAsync (string.Empty, 443));
			Assert.ThrowsAsync<ArgumentException> (async () => await proxy.ConnectAsync (string.Empty, 443, ConnectTimeout));

			Assert.Throws<ArgumentOutOfRangeException> (() => proxy.Connect ("www.google.com", 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => proxy.Connect ("www.google.com", 0, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await proxy.ConnectAsync ("www.google.com", 0));
			Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await proxy.ConnectAsync ("www.google.com", 0, ConnectTimeout));

			Assert.Throws<ArgumentOutOfRangeException> (() => proxy.Connect ("www.google.com", 80, -ConnectTimeout));
			Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await proxy.ConnectAsync ("www.google.com", 80, -ConnectTimeout));
		}

		[Test]
		public void TestMethodNotAllowed ()
		{
			var proxy = new HttpsProxyClient ("www.google.com", 443);
			Stream stream = null;

			try {
				stream = proxy.Connect ("www.google.com", 443);
				Assert.Fail ("www.google.com is not an HTTP proxy, so CONNECT should have failed.");
			} catch (ProxyProtocolException ex) {
				// This is expected since this is not an HTTP proxy
				var response = ex.Message.Substring (0, ex.Message.IndexOf ("\r\n"));
				Assert.That (response, Is.EqualTo ("Failed to connect to www.google.com:443: HTTP/1.1 405 Method Not Allowed"));
			} catch (TimeoutException) {
				Assert.Inconclusive ("Timed out.");
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				stream?.Dispose ();
			}
		}

		[Test]
		public async Task TestMethodNotAllowedAsync ()
		{
			var proxy = new HttpsProxyClient ("www.google.com", 443);
			Stream stream = null;

			try {
				stream = await proxy.ConnectAsync ("www.google.com", 443);
				Assert.Fail ("www.google.com is not an HTTP proxy, so CONNECT should have failed.");
			} catch (ProxyProtocolException ex) {
				// This is expected since this is not an HTTP proxy
				var response = ex.Message.Substring (0, ex.Message.IndexOf ("\r\n"));
				Assert.That (response, Is.EqualTo ("Failed to connect to www.google.com:443: HTTP/1.1 405 Method Not Allowed"));
			} catch (TimeoutException) {
				Assert.Inconclusive ("Timed out.");
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				stream?.Dispose ();
			}
		}

		[Test]
		public void TestConnectWithCredentials ()
		{
			using (var server = new HttpProxyListener (certificate)) {
				server.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var proxy = new HttpsProxyClient (server.IPAddress.ToString (), server.Port, credentials) {
					ServerCertificateValidationCallback = (s, c, ch, e) => true,
					CheckCertificateRevocation = false
				};
				Stream stream = null;

				try {
					stream = proxy.Connect ("www.google.com", 80, ConnectTimeout);
				} catch (TimeoutException) {
					Assert.Inconclusive ("Timed out.");
				} catch (Exception ex) {
					Assert.Fail (ex.Message);
				} finally {
					stream?.Dispose ();
				}
			}
		}

		[Test]
		public async Task TestConnectWithCredentialsAsync ()
		{
			using (var server = new HttpProxyListener (certificate)) {
				server.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var proxy = new HttpsProxyClient (server.IPAddress.ToString (), server.Port, credentials) {
					ServerCertificateValidationCallback = (s, c, ch, e) => true,
					CheckCertificateRevocation = false
				};
				Stream stream = null;

				try {
					stream = await proxy.ConnectAsync ("www.google.com", 80, ConnectTimeout);
				} catch (TimeoutException) {
					Assert.Inconclusive ("Timed out.");
				} catch (Exception ex) {
					Assert.Fail (ex.Message);
				} finally {
					stream?.Dispose ();
				}
			}
		}
	}
}
