//
// Socks4Client.cs
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

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MailKit.Net
{
	/// <summary>
	/// A SOCKS4 proxy client.
	/// </summary>
	/// <remarkas>
	/// A SOCKS4 proxy client.
	/// </remarkas>
	public class Socks4Client : SocksClient
	{
		static readonly byte[] InvalidIPAddress = { 0, 0, 0, 1 };

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Socks4Client"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Socks4Client"/> class.
		/// </remarks>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>1</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		public Socks4Client (string host, int port) : base (4, host, port)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Socks4Client"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Socks4Client"/> class.
		/// </remarks>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="credentials">The credentials to use to authenticate with the proxy server.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="host"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/>is <c>null</c>.</para>
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

		async Task<Socket> ConnectAsync (string host, int port, bool doAsync, CancellationToken cancellationToken)
		{
			byte[] addr, domain = null;
			IPAddress ip;

			ValidateArguments (host, port);

			if (!IPAddress.TryParse (host, out ip)) {
				domain = Encoding.UTF8.GetBytes (host);
				addr = InvalidIPAddress;
			} else {
				if (ip.AddressFamily != AddressFamily.InterNetwork)
					throw new ArgumentException (nameof (host));

				addr = ip.GetAddressBytes ();
			}

			cancellationToken.ThrowIfCancellationRequested ();

			var socket = await SocketUtils.ConnectAsync (ProxyHost, ProxyPort, null, doAsync, cancellationToken).ConfigureAwait (false);
			var user = ProxyCredentials != null ? Encoding.UTF8.GetBytes (ProxyCredentials.UserName) : new byte[0];

			try {
				// +----+-----+----------+----------+----------+-------+--------------+-------+
				// |VER | CMD | DST.PORT | DST.ADDR |  USERID  | NULL  |  DST.DOMAIN  | NULL  |
				// +----+-----+----------+----------+----------+-------+--------------+-------+
				// | 1  |  1  |    2     |    4     | VARIABLE | X'00' |   VARIABLE   | X'00' |
				// +----+-----+----------+----------+----------+-------+--------------+-------+
				int bufferSize = 9 + user.Length + (domain != null ? domain.Length + 1 : 0);
				var buffer = new byte[bufferSize];
				int nread, n = 0;

				buffer[n++] = (byte) SocksVersion;
				buffer[n++] = (byte) Socks4Command.Connect;
				buffer[n++] = (byte)(port >> 8);
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

				SocketUtils.Poll (socket, SelectMode.SelectWrite, cancellationToken);
				socket.Send (buffer, 0, n, SocketFlags.None);

				// +-----+-----+----------+----------+
				// | VER | REP | BND.PORT | BND.ADDR |
				// +-----+-----+----------+----------+
				// |  1  |  1  |    2     |    4     |
				// +-----+-----+----------+----------+
				n = 0;

				do {
					SocketUtils.Poll (socket, SelectMode.SelectRead, cancellationToken);
					if ((nread = socket.Receive (buffer, 0 + n, 8 - n, SocketFlags.None)) > 0)
						n += nread;
				} while (n < 8);

				if (buffer[1] != (byte) Socks4Reply.RequestGranted)
					throw new ProxyProtocolException (string.Format ("Failed to connect to {0}:{1}: {2}", host, port, GetFailureReason (buffer[1])));

				// TODO: do we care about BND.ADDR and BND.PORT?

				return socket;
			} catch {
				if (socket.Connected)
					socket.Disconnect (false);
				socket.Dispose ();
				throw;
			}
		}

		/// <summary>
		/// Connect to the target host.
		/// </summary>
		/// <remarks>
		/// Connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected socket.</returns>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
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
		public override Socket Connect (string host, int port, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ConnectAsync (host, port, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously connect to the target host.
		/// </summary>
		/// <remarks>
		/// Asynchronously connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected socket.</returns>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
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
		public override Task<Socket> ConnectAsync (string host, int port, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ConnectAsync (host, port, true, cancellationToken);
		}
	}
}
