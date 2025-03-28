﻿//
// WebProxyClientTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

namespace UnitTests.Net.Proxy
{
	[TestFixture]
	public class WebProxyClientTests
	{
		const int ConnectTimeout = 5 * 1000; // 5 seconds

		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("user", "password");
			var proxy = ProxyClient.SystemProxy;

			Assert.Throws<ArgumentNullException> (() => new WebProxyClient (null));

			Assert.That (proxy.ProxyPort, Is.EqualTo (1080));
			Assert.That (proxy.ProxyHost, Is.EqualTo ("System"));
			Assert.That (proxy.ProxyCredentials, Is.Null);

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

		[TestCase ("http://proxy:8080", null, null, typeof (HttpProxyClient))]
		[TestCase ("http://proxy:8080", "user", "password", typeof (HttpProxyClient))]
		[TestCase ("https://proxy:8080", null, null, typeof (HttpsProxyClient))]
		[TestCase ("https://proxy:8080", "user", "password", typeof (HttpsProxyClient))]
		[TestCase ("socks4://proxy:1080", null, null, typeof (Socks4Client))]
		[TestCase ("socks4://proxy:1080", "user", "password", typeof (Socks4Client))]
		[TestCase ("socks4a://proxy:1080", null, null, typeof (Socks4aClient))]
		[TestCase ("socks4a://proxy:1080", "user", "password", typeof (Socks4aClient))]
		[TestCase ("socks5://proxy:1080", null, null, typeof (Socks5Client))]
		[TestCase ("socks5://proxy:1080", "user", "password", typeof (Socks5Client))]
		[TestCase ("unsupported://proxy:1080", null, null, null)]
		public void TestGetProxyClient (string proxyUri, string user, string password, Type expectedType)
		{
			var credentials = user != null ? new NetworkCredential (user, password) : null;

			if (expectedType != null) {
				var proxy = WebProxyClient.GetProxyClient (new Uri (proxyUri), credentials);
				Assert.That (proxy, Is.InstanceOf (expectedType));
			} else {
				Assert.Throws<NotSupportedException> (() => WebProxyClient.GetProxyClient (new Uri (proxyUri), credentials));
			}
		}

		[Test]
		public void TestConnect ()
		{
			var proxy = ProxyClient.SystemProxy;
			Stream stream = null;

			try {
				stream = proxy.Connect ("www.google.com", 80);
			} catch (TimeoutException) {
				Assert.Inconclusive ("Timed out.");
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				stream?.Dispose ();
			}
		}

		[Test]
		public async Task TestConnectAsync ()
		{
			var proxy = ProxyClient.SystemProxy;
			Stream stream = null;

			try {
				stream = await proxy.ConnectAsync ("www.google.com", 80);
			} catch (TimeoutException) {
				Assert.Inconclusive ("Timed out.");
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				stream?.Dispose ();
			}
		}

		[Test]
		public void TestConnectViaWebProxy ()
		{
			using (var server = new HttpProxyListener ()) {
				server.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var webProxy = new WebProxy (new Uri ($"http://{server.IPAddress}:{server.Port}"), true, null, credentials);

				var proxy = new WebProxyClient (webProxy);
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
		public async Task TestConnectViaWebProxyAsync ()
		{
			using (var server = new HttpProxyListener ()) {
				server.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var webProxy = new WebProxy (new Uri ($"http://{server.IPAddress}:{server.Port}"), true, null, credentials);

				var proxy = new WebProxyClient (webProxy);
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
