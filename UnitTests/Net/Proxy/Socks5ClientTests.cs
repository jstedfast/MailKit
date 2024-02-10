//
// Socks5ClientTests.cs
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

// Note: Find Socks5 proxy list here: http://www.gatherproxy.com/sockslist/country/?c=United%20States

using System.Net;
using System.Net.Sockets;

using MailKit.Security;
using MailKit.Net.Proxy;

namespace UnitTests.Net.Proxy {
	[TestFixture]
	public class Socks5ClientTests
	{
		public static readonly string[] Socks5ProxyList = { "98.174.90.36", "198.12.157.31", "72.210.252.134" };
		public static readonly int[] Socks5ProxyPorts = { 1080, 46906, 46164 };
		const int ConnectTimeout = 5 * 1000; // 5 seconds

		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("user", "password");
			var socks = new Socks5Client ("socks5.proxy.com", 0, credentials);

			Assert.Throws<ArgumentNullException> (() => new Socks4Client (null, 1080));
			Assert.Throws<ArgumentException> (() => new Socks4Client (string.Empty, 1080));
			Assert.Throws<ArgumentOutOfRangeException> (() => new Socks4Client (socks.ProxyHost, -1));
			Assert.Throws<ArgumentNullException> (() => new Socks4Client (socks.ProxyHost, 1080, null));

			Assert.That (socks.SocksVersion, Is.EqualTo (5));
			Assert.That (socks.ProxyPort, Is.EqualTo (1080));
			Assert.That (socks.ProxyHost, Is.EqualTo ("socks5.proxy.com"));
			Assert.That (socks.ProxyCredentials, Is.EqualTo (credentials));

			Assert.Throws<ArgumentNullException> (() => socks.Connect (null, 80));
			Assert.Throws<ArgumentNullException> (() => socks.Connect (null, 80, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentNullException> (async () => await socks.ConnectAsync (null, 80));
			Assert.ThrowsAsync<ArgumentNullException> (async () => await socks.ConnectAsync (null, 80, ConnectTimeout));

			Assert.Throws<ArgumentException> (() => socks.Connect (string.Empty, 80));
			Assert.Throws<ArgumentException> (() => socks.Connect (string.Empty, 80, 100000));
			Assert.ThrowsAsync<ArgumentException> (async () => await socks.ConnectAsync (string.Empty, 80));
			Assert.ThrowsAsync<ArgumentException> (async () => await socks.ConnectAsync (string.Empty, 80, ConnectTimeout));

			Assert.Throws<ArgumentOutOfRangeException> (() => socks.Connect ("www.google.com", 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => socks.Connect ("www.google.com", 0, ConnectTimeout));
			Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await socks.ConnectAsync ("www.google.com", 0));
			Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await socks.ConnectAsync ("www.google.com", 0, ConnectTimeout));

			Assert.Throws<ArgumentOutOfRangeException> (() => socks.Connect ("www.google.com", 80, -ConnectTimeout));
			Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await socks.ConnectAsync ("www.google.com", 80, -ConnectTimeout));
		}

		static string ResolveIPv4 (string host)
		{
			var ipAddresses = Dns.GetHostAddresses (host);

			for (int i = 0; i < ipAddresses.Length; i++) {
				if (ipAddresses[i].AddressFamily == AddressFamily.InterNetwork)
					return ipAddresses[i].ToString ();
			}

			return null;
		}

		static string ResolveIPv6 (string host)
		{
			var ipAddresses = Dns.GetHostAddresses (host);

			for (int i = 0; i < ipAddresses.Length; i++) {
				if (ipAddresses[i].AddressFamily == AddressFamily.InterNetworkV6)
					return ipAddresses[i].ToString ();
			}

			return null;
		}

		[Test]
		public void TestGetAddressType ()
		{
			const string host = "www.google.com";
			const string ipv6 = "2607:f8b0:400e:c03::69";
			const string ipv4 = "74.125.197.99";

			Assert.That (Socks5Client.GetAddressType (host, out _), Is.EqualTo (Socks5Client.Socks5AddressType.Domain));
			Assert.That (Socks5Client.GetAddressType (ipv4, out _), Is.EqualTo (Socks5Client.Socks5AddressType.IPv4));
			Assert.That (Socks5Client.GetAddressType (ipv6, out _), Is.EqualTo (Socks5Client.Socks5AddressType.IPv6));
		}

		[Test]
		public void TestGetFailureReason ()
		{
			for (byte i = 1; i < 10; i++) {
				var reason = Socks5Client.GetFailureReason (i);

				if (i > 8)
					Assert.That (reason.StartsWith ("Unknown error", StringComparison.Ordinal), Is.True);
			}
		}

		[Test]
		public void TestConnectAnonymous ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				Stream stream = null;

				try {
					stream = socks.Connect ("www.google.com", 80, ConnectTimeout);
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
		public async Task TestConnectAnonymousAsync ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				Stream stream = null;

				try {
					stream = await socks.ConnectAsync ("www.google.com", 80, ConnectTimeout);
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
		public void TestConnectWithCredentials ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port, credentials);
				Stream stream = null;

				try {
					stream = socks.Connect ("www.google.com", 80, ConnectTimeout);
				} catch (AuthenticationException) {
					// this is what we expect to get
					Assert.Pass ("Got an AuthenticationException just as expected");
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
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "password");
				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port, credentials);
				Stream stream = null;

				try {
					stream = await socks.ConnectAsync ("www.google.com", 80, ConnectTimeout);
				} catch (AuthenticationException) {
					// this is what we expect to get
					Assert.Pass ("Got an AuthenticationException just as expected");
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
		public void TestConnectWithBadCredentials ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "bad");
				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port, credentials);
				Stream stream = null;

				try {
					stream = socks.Connect ("www.google.com", 80, ConnectTimeout);
					Assert.Fail ("Expected AuthenticationException");
				} catch (AuthenticationException) {
					// this is what we expect to get
					Assert.Pass ("Got an AuthenticationException just as expected");
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
		public async Task TestConnectWithBadCredentialsAsync ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var credentials = new NetworkCredential ("username", "bad");
				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port, credentials);
				Stream stream = null;

				try {
					stream = await socks.ConnectAsync ("www.google.com", 80, ConnectTimeout);
					Assert.Fail ("Expected AuthenticationException");
				} catch (AuthenticationException) {
					// this is what we expect to get
					Assert.Pass ("Got an AuthenticationException just as expected");
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
		public void TestConnectByIPv4 ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				var host = "74.125.197.99"; // ResolveIPv4 ("www.google.com");
				Stream stream = null;

				if (host == null)
					return;

				try {
					stream = socks.Connect (host, 80, ConnectTimeout);
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
		public async Task TestConnectByIPv4Async ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				var host = "74.125.197.99"; // ResolveIPv4 ("www.google.com");
				Stream stream = null;

				if (host == null)
					return;

				try {
					stream = await socks.ConnectAsync (host, 80, ConnectTimeout);
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
		public void TestConnectByIPv6 ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				var host = "2607:f8b0:400e:c03::69"; // ResolveIPv6 ("www.google.com");
				Stream stream = null;

				if (host == null)
					return;

				try {
					stream = socks.Connect (host, 80, ConnectTimeout);
				} catch (ProxyProtocolException ex) {
					Assert.That (ex.Message.StartsWith ($"Failed to connect to {host}:80: ", StringComparison.Ordinal), Is.True, ex.Message);
					Assert.Inconclusive (ex.Message);
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
		public async Task TestConnectByIPv6Async ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				var host = "2607:f8b0:400e:c03::69"; // ResolveIPv6 ("www.google.com");
				Stream stream = null;

				if (host == null)
					return;

				try {
					stream = await socks.ConnectAsync (host, 80, ConnectTimeout);
				} catch (ProxyProtocolException ex) {
					Assert.That (ex.Message.StartsWith ($"Failed to connect to {host}:80: ", StringComparison.Ordinal), Is.True, ex.Message);
					Assert.Inconclusive (ex.Message);
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
		public async Task TestTimeoutException ()
		{
			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				var socks = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
				Stream stream = null;

				try {
					stream = await socks.ConnectAsync ("example.com", 25, 1000);
				} catch (TimeoutException) {
					Assert.Pass ();
				} catch (Exception ex) {
					Assert.Fail (ex.Message);
				} finally {
					stream?.Dispose ();
				}
			}
		}
	}
}
