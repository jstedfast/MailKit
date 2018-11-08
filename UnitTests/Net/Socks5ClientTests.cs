//
// Socks5ClientTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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

using System;
using System.Net;
using System.Net.Sockets;

using NUnit.Framework;

using MailKit.Net;

namespace UnitTests.Net {
	[TestFixture]
	public class Socks5ClientTests
	{
		static readonly string[] Socks5ProxyList = { "98.174.90.36", "198.12.157.31", "72.210.252.134" };
		static readonly int[] Socks5ProxyPorts = { 1080, 46906, 46164 };

		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("user", "password");
			var socks = new Socks5Client ("socks5.proxy.com", 0, credentials);

			Assert.Throws<ArgumentNullException> (() => new Socks4Client (null, 1080));
			Assert.Throws<ArgumentException> (() => new Socks4Client (string.Empty, 1080));
			Assert.Throws<ArgumentOutOfRangeException> (() => new Socks4Client (socks.ProxyHost, -1));
			Assert.Throws<ArgumentNullException> (() => new Socks4Client (socks.ProxyHost, 1080, null));

			Assert.AreEqual (5, socks.SocksVersion);
			Assert.AreEqual (1080, socks.ProxyPort);
			Assert.AreEqual ("socks5.proxy.com", socks.ProxyHost);
			Assert.AreEqual (credentials, socks.ProxyCredentials);

			Assert.Throws<ArgumentNullException> (() => socks.Connect (null, 80));
			Assert.Throws<ArgumentNullException> (() => socks.Connect (null, 80, 100000));
			Assert.Throws<ArgumentNullException> (async () => await socks.ConnectAsync (null, 80));
			Assert.Throws<ArgumentNullException> (async () => await socks.ConnectAsync (null, 80, 100000));

			Assert.Throws<ArgumentException> (() => socks.Connect (string.Empty, 80));
			Assert.Throws<ArgumentException> (() => socks.Connect (string.Empty, 80, 100000));
			Assert.Throws<ArgumentException> (async () => await socks.ConnectAsync (string.Empty, 80));
			Assert.Throws<ArgumentException> (async () => await socks.ConnectAsync (string.Empty, 80, 100000));

			Assert.Throws<ArgumentOutOfRangeException> (() => socks.Connect ("www.google.com", 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => socks.Connect ("www.google.com", 0, 100000));
			Assert.Throws<ArgumentOutOfRangeException> (async () => await socks.ConnectAsync ("www.google.com", 0));
			Assert.Throws<ArgumentOutOfRangeException> (async () => await socks.ConnectAsync ("www.google.com", 0, 100000));

			Assert.Throws<ArgumentOutOfRangeException> (() => socks.Connect ("www.google.com", 80, -100000));
			Assert.Throws<ArgumentOutOfRangeException> (async () => await socks.ConnectAsync ("www.google.com", 80, -100000));
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
		public void TestConnectAnonymous ()
		{
			var socks = new Socks5Client (Socks5ProxyList[0], Socks5ProxyPorts[0]);
			Socket socket = null;

			try {
				socket = socks.Connect ("www.google.com", 80, 10 * 1000);
				socket.Disconnect (false);
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				if (socket != null)
					socket.Dispose ();
			}
		}

		[Test]
		public async void TestConnectAnonymousAsync ()
		{
			var socks = new Socks5Client (Socks5ProxyList[0], Socks5ProxyPorts[0]);
			Socket socket = null;

			try {
				socket = await socks.ConnectAsync ("www.google.com", 80, 10 * 1000);
				socket.Disconnect (false);
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				if (socket != null)
					socket.Dispose ();
			}
		}

		[Test]
		public void TestConnectByIPv4 ()
		{
			var socks = new Socks5Client (Socks5ProxyList[1], Socks5ProxyPorts[1]);
			var host = ResolveIPv4 ("www.google.com");
			Socket socket = null;

			try {
				socket = socks.Connect (host, 80, 10 * 1000);
				socket.Disconnect (false);
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				if (socket != null)
					socket.Dispose ();
			}
		}

		[Test]
		public async void TestConnectByIPv4Async ()
		{
			var socks = new Socks5Client (Socks5ProxyList[1], Socks5ProxyPorts[1]);
			var host = ResolveIPv4 ("www.google.com");
			Socket socket = null;

			try {
				socket = await socks.ConnectAsync (host, 80, 10 * 1000);
				socket.Disconnect (false);
			} catch (Exception ex) {
				Assert.Fail (ex.Message);
			} finally {
				if (socket != null)
					socket.Dispose ();
			}
		}

		[Test]
		public void TestConnectByIPv6 ()
		{
			var socks = new Socks5Client (Socks5ProxyList[2], Socks5ProxyPorts[2]);
			var host = ResolveIPv6 ("www.google.com");

			// This Socks5 server does not support IPv6
			Assert.Throws<ProxyProtocolException> (() => socks.Connect (host, 80, 10 * 1000));
		}

		[Test]
		public void TestConnectByIPv6Async ()
		{
			var socks = new Socks5Client (Socks5ProxyList[2], Socks5ProxyPorts[2]);
			var host = ResolveIPv6 ("www.google.com");

			// This Socks5 server does not support IPv6
			Assert.Throws<ProxyProtocolException> (async () => await socks.ConnectAsync (host, 80, 10 * 1000));
		}
	}
}
