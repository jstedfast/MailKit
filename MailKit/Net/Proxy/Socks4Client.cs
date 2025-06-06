﻿//
// Socks4Client.cs
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

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Globalization;
using System.Threading.Tasks;

using NetworkStream = MailKit.Net.NetworkStream;

namespace MailKit.Net.Proxy
{
	/// <summary>
	/// A SOCKS4 proxy client.
	/// </summary>
	/// <remarks>
	/// A SOCKS4 proxy client.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ProxyExamples.cs" region="ProxyClient" />
	/// </example>
	public class Socks4Client : SocksClient
	{
		static readonly byte[] InvalidIPAddress = { 0, 0, 0, 1 };

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks4Client"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks4Client"/> class.
		/// </remarks>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>1</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		public Socks4Client (string host, int port) : base (4, host, port)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks4Client"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks4Client"/> class.
		/// </remarks>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="credentials">The credentials to use to authenticate with the proxy server.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="host"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/>is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>1</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		public Socks4Client (string host, int port, NetworkCredential credentials) : base (4, host, port, credentials)
		{
		}

		/// <summary>
		/// Get or set whether this <see cref="T:MailKit.Net.Proxy.Socks4Client"/> is a Socks4a client.
		/// </summary>
		/// <remarks>
		/// Gets or sets whether this <see cref="T:MailKit.Net.Proxy.Socks4Client"/> is a Socks4a client.
		/// </remarks>
		/// <value><see langword="true" /> if is is a Socks4a client; otherwise, <see langword="false" />.</value>
		protected bool IsSocks4a {
			get; set;
		}

		enum Socks4Command : byte
		{
			Connect = 0x01,
			Bind    = 0x02,
		}

		enum Socks4Reply : byte
		{
			RequestGranted        = 0x5a,
			RequestRejected       = 0x5b,
			RequestFailedNoIdentd = 0x5c,
			RequestFailedWrongId  = 0x5d
		}

		static string GetFailureReason (byte reply)
		{
			switch ((Socks4Reply) reply) {
			case Socks4Reply.RequestRejected:       return "Request rejected or failed.";
			case Socks4Reply.RequestFailedNoIdentd: return "Request failed; unable to contact client machine's identd service.";
			case Socks4Reply.RequestFailedWrongId:  return "Request failed; client ID does not match specified username.";
			default:                                return "Unknown error.";
			}
		}

		static IPAddress Resolve (string host, IPAddress[] ipAddresses)
		{
			for (int i = 0; i < ipAddresses.Length; i++) {
				if (ipAddresses[i].AddressFamily == AddressFamily.InterNetwork)
					return ipAddresses[i];
			}

			throw new ArgumentException ($"Could not resolve a suitable IPv4 address for '{host}'.", nameof (host));
		}

		static IPAddress Resolve (string host, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var ipAddresses = Dns.GetHostAddresses (host);

			return Resolve (host, ipAddresses);
		}

		static async Task<IPAddress> ResolveAsync (string host, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

#if NET6_0_OR_GREATER
			var ipAddresses = await Dns.GetHostAddressesAsync (host, cancellationToken).ConfigureAwait (false);
#else
			var ipAddresses = await Dns.GetHostAddressesAsync (host).ConfigureAwait (false);
#endif

			return Resolve (host, ipAddresses);
		}

		byte[] GetConnectCommand (byte[] domain, byte[] addr, int port)
		{
			// +----+-----+----------+----------+----------+-------+--------------+-------+
			// |VER | CMD | DST.PORT | DST.ADDR |  USERID  | NULL  |  DST.DOMAIN  | NULL  |
			// +----+-----+----------+----------+----------+-------+--------------+-------+
			// | 1  |  1  |    2     |    4     | VARIABLE | X'00' |   VARIABLE   | X'00' |
			// +----+-----+----------+----------+----------+-------+--------------+-------+
			var user = ProxyCredentials != null ? Encoding.UTF8.GetBytes (ProxyCredentials.UserName) : Array.Empty<byte> ();
			int bufferSize = 9 + user.Length + (domain != null ? domain.Length + 1 : 0);
			var buffer = new byte[bufferSize];
			int n = 0;

			buffer[n++] = (byte) SocksVersion;
			buffer[n++] = (byte) Socks4Command.Connect;
			buffer[n++] = (byte) (port >> 8);
			buffer[n++] = (byte) port;
			Buffer.BlockCopy (addr, 0, buffer, n, 4);
			n += 4;
			Buffer.BlockCopy (user, 0, buffer, n, user.Length);
			n += user.Length;
			buffer[n++] = 0x00;

			if (domain != null) {
				Buffer.BlockCopy (domain, 0, buffer, n, domain.Length);
				n += domain.Length;
				buffer[n++] = 0x00;
			}

			return buffer;
		}

		/// <summary>
		/// Connect to the target host.
		/// </summary>
		/// <remarks>
		/// Connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected network stream.</returns>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override Stream Connect (string host, int port, CancellationToken cancellationToken = default)
		{
			byte[] addr, domain = null;

			ValidateArguments (host, port);

			if (!IPAddress.TryParse (host, out var ip)) {
				if (IsSocks4a) {
					domain = Encoding.UTF8.GetBytes (host);
					addr = InvalidIPAddress;
				} else {
					ip = Resolve (host, cancellationToken);
					addr = ip.GetAddressBytes ();
				}
			} else {
				if (ip.AddressFamily != AddressFamily.InterNetwork)
					throw new ArgumentException ("The specified host address must be IPv4.", nameof (host));

				addr = ip.GetAddressBytes ();
			}

			cancellationToken.ThrowIfCancellationRequested ();

			var socket = SocketUtils.Connect (ProxyHost, ProxyPort, LocalEndPoint, cancellationToken);

			try {
				var buffer = GetConnectCommand (domain, addr, port);

				Send (socket, buffer, 0, buffer.Length, cancellationToken);

				// +-----+-----+----------+----------+
				// | VER | REP | BND.PORT | BND.ADDR |
				// +-----+-----+----------+----------+
				// |  1  |  1  |    2     |    4     |
				// +-----+-----+----------+----------+
				int nread, n = 0;

				do {
					if ((nread = Receive (socket, buffer, 0 + n, 8 - n, cancellationToken)) > 0)
						n += nread;
				} while (n < 8);

				if (buffer[1] != (byte) Socks4Reply.RequestGranted)
					throw new ProxyProtocolException (string.Format (CultureInfo.InvariantCulture, "Failed to connect to {0}:{1}: {2}", host, port, GetFailureReason (buffer[1])));

				// TODO: do we care about BND.ADDR and BND.PORT?

				return new NetworkStream (socket, true);
			} catch {
				if (socket.Connected)
					socket.Disconnect (false);

				socket.Dispose ();
				throw;
			}
		}

		/// <summary>
		/// Asynchronously connect to the target host.
		/// </summary>
		/// <remarks>
		/// Asynchronously connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected network stream.</returns>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override async Task<Stream> ConnectAsync (string host, int port, CancellationToken cancellationToken = default)
		{
			byte[] addr, domain = null;

			ValidateArguments (host, port);

			if (!IPAddress.TryParse (host, out var ip)) {
				if (IsSocks4a) {
					domain = Encoding.UTF8.GetBytes (host);
					addr = InvalidIPAddress;
				} else {
					ip = await ResolveAsync (host, cancellationToken).ConfigureAwait (false);
					addr = ip.GetAddressBytes ();
				}
			} else {
				if (ip.AddressFamily != AddressFamily.InterNetwork)
					throw new ArgumentException ("The specified host address must be IPv4.", nameof (host));

				addr = ip.GetAddressBytes ();
			}

			cancellationToken.ThrowIfCancellationRequested ();

			var socket = await SocketUtils.ConnectAsync (ProxyHost, ProxyPort, LocalEndPoint, cancellationToken).ConfigureAwait (false);

			try {
				var buffer = GetConnectCommand (domain, addr, port);

				await SendAsync (socket, buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false);

				// +-----+-----+----------+----------+
				// | VER | REP | BND.PORT | BND.ADDR |
				// +-----+-----+----------+----------+
				// |  1  |  1  |    2     |    4     |
				// +-----+-----+----------+----------+
				int nread, n = 0;

				do {
					if ((nread = await ReceiveAsync (socket, buffer, 0 + n, 8 - n, cancellationToken).ConfigureAwait (false)) > 0)
						n += nread;
				} while (n < 8);

				if (buffer[1] != (byte) Socks4Reply.RequestGranted)
					throw new ProxyProtocolException (string.Format (CultureInfo.InvariantCulture, "Failed to connect to {0}:{1}: {2}", host, port, GetFailureReason (buffer[1])));

				// TODO: do we care about BND.ADDR and BND.PORT?

				return new NetworkStream (socket, true);
			} catch {
				if (socket.Connected)
					socket.Disconnect (false);

				socket.Dispose ();
				throw;
			}
		}
	}
}
