//
// HttpProxyClientTests.cs
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

using MailKit.Net.Proxy;

namespace UnitTests.Net.Proxy {
	[TestFixture]
	public class HttpProxyClientTests
	{
		const int ConnectTimeout = 5 * 1000; // 5 seconds

		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("user", "password");
			var proxy = new HttpProxyClient ("http.proxy.com", 0, credentials);

			Assert.Throws<ArgumentNullException> (() => new HttpProxyClient (null, 1080));
			Assert.Throws<ArgumentException> (() => new HttpProxyClient (string.Empty, 1080));
			Assert.Throws<ArgumentOutOfRangeException> (() => new HttpProxyClient (proxy.ProxyHost, -1));
			Assert.Throws<ArgumentNullException> (() => new HttpProxyClient (proxy.ProxyHost, 1080, null));

			Assert.That (proxy.ProxyPort, Is.EqualTo (1080));
			Assert.That (proxy.ProxyHost, Is.EqualTo ("http.proxy.com"));
			Assert.That (proxy.ProxyCredentials, Is.EqualTo (credentials));

			Assert.Throws<ArgumentNullException> (() => proxy.Connect (null, 80));
			Assert.Throws<ArgumentNullException> (() => proxy.Connect (null, 80, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentNullException> (async () => await proxy.ConnectAsync (null, 80));
			Assert.ThrowsAsync<ArgumentNullException> (async () => await proxy.ConnectAsync (null, 80, ConnectTimeout));

			Assert.Throws<ArgumentException> (() => proxy.Connect (string.Empty, 80));
			Assert.Throws<ArgumentException> (() => proxy.Connect (string.Empty, 80, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentException> (async () => await proxy.ConnectAsync (string.Empty, 80));
			Assert.ThrowsAsync<ArgumentException> (async () => await proxy.ConnectAsync (string.Empty, 80, ConnectTimeout));

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
			var proxy = new HttpProxyClient ("www.google.com", 80);
			Stream stream = null;

			try {
				stream = proxy.Connect ("www.google.com", 80);
				Assert.Fail ("www.google.com is not an HTTP proxy, so CONNECT should have failed.");
			} catch (ProxyProtocolException ex) {
				// This is expected since this is not an HTTP proxy
				var response = ex.Message.Substring (0, ex.Message.IndexOf ("\r\n"));
				Assert.That (response, Is.EqualTo ("Failed to connect to www.google.com:80: HTTP/1.1 405 Method Not Allowed"));
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
			var proxy = new HttpProxyClient ("www.google.com", 80);
			Stream stream = null;

			try {
				stream = await proxy.ConnectAsync ("www.google.com", 80);
				Assert.Fail ("www.google.com is not an HTTP proxy, so CONNECT should have failed.");
			} catch (ProxyProtocolException ex) {
				// This is expected since this is not an HTTP proxy
				var response = ex.Message.Substring (0, ex.Message.IndexOf ("\r\n"));
				Assert.That (response, Is.EqualTo ("Failed to connect to www.google.com:80: HTTP/1.1 405 Method Not Allowed"));
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
			using (var server = new HttpProxyListener ()) {
				server.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var proxy = new HttpProxyClient (server.IPAddress.ToString (), server.Port, credentials);
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
			using (var server = new HttpProxyListener ()) {
				server.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var proxy = new HttpProxyClient (server.IPAddress.ToString (), server.Port, credentials);
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
