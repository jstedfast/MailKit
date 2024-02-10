//
// Socks5Client.cs
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

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Globalization;
using System.Threading.Tasks;

using MailKit.Security;

using NetworkStream = MailKit.Net.NetworkStream;

namespace MailKit.Net.Proxy
{
	/// <summary>
	/// A SOCKS5 proxy client.
	/// </summary>
	/// <remarks>
	/// A SOCKS5 proxy client.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ProxyExamples.cs" region="ProxyClient" />
	/// </example>
	public class Socks5Client : SocksClient
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks5Client"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks5Client"/> class.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ProxyExamples.cs" region="ProxyClient" />
		/// </example>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>1</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		public Socks5Client (string host, int port) : base (5, host, port)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks5Client"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.Socks5Client"/> class.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ProxyExamples.cs" region="ProxyClient" />
		/// </example>
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
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		public Socks5Client (string host, int port, NetworkCredential credentials) : base (5, host, port, credentials)
		{
		}

		internal enum Socks5AddressType : byte
		{
			None   = 0x00,
			IPv4   = 0x01,
			Domain = 0x03,
			IPv6   = 0x04
		}

		enum Socks5AuthMethod : byte
		{
			Anonymous    = 0x00,
			GSSAPI       = 0x01,
			UserPassword = 0x02,
			NotSupported = 0xff
		}

		enum Socks5Command : byte
		{
			Connect      = 0x01,
			Bind         = 0x02,
			UdpAssociate = 0x03,
		}

		internal enum Socks5Reply : byte
		{
			Success                 = 0x00,
			GeneralServerFailure    = 0x01,
			ConnectionNotAllowed    = 0x02,
			NetworkUnreachable      = 0x03,
			HostUnreachable         = 0x04,
			ConnectionRefused       = 0x05,
			TTLExpired              = 0x06,
			CommandNotSupported     = 0x07,
			AddressTypeNotSupported = 0x08
		}

		internal static string GetFailureReason (byte reply)
		{
			switch ((Socks5Reply) reply) {
			case Socks5Reply.GeneralServerFailure:    return "General server failure.";
			case Socks5Reply.ConnectionNotAllowed:    return "Connection not allowed.";
			case Socks5Reply.NetworkUnreachable:      return "Network unreachable.";
			case Socks5Reply.HostUnreachable:         return "Host unreachable.";
			case Socks5Reply.ConnectionRefused:       return "Connection refused.";
			case Socks5Reply.TTLExpired:              return "TTL expired.";
			case Socks5Reply.CommandNotSupported:     return "Command not supported.";
			case Socks5Reply.AddressTypeNotSupported: return "Address type not supported.";
			default:                                  return string.Format (CultureInfo.InvariantCulture, "Unknown error ({0}).", (int) reply);
			}
		}

		internal static Socks5AddressType GetAddressType (string host, out IPAddress ip)
		{
			if (!IPAddress.TryParse (host, out ip))
				return Socks5AddressType.Domain;

			switch (ip.AddressFamily) {
			case AddressFamily.InterNetworkV6: return Socks5AddressType.IPv6;
			case AddressFamily.InterNetwork: return Socks5AddressType.IPv4;
			default: throw new ArgumentException ("The host address must be an IPv4 or IPv6 address.", nameof (host));
			}
		}

		void VerifySocksVersion (byte version)
		{
			if (version != (byte) SocksVersion)
				throw new ProxyProtocolException (string.Format (CultureInfo.InvariantCulture, "Proxy server responded with unknown SOCKS version: {0}", (int) version));
		}

		byte[] GetNegotiateAuthMethodCommand (Socks5AuthMethod[] methods)
		{
			// +-----+----------+----------+
			// | VER | NMETHODS | METHODS  |
			// +-----+----------+----------+
			// |  1  |    1     | 1 to 255 |
			// +-----+----------+----------+
			var buffer = new byte[2 + methods.Length];
			int n = 0;

			buffer[n++] = (byte) SocksVersion;
			buffer[n++] = (byte) methods.Length;
			for (int i = 0; i < methods.Length; i++)
				buffer[n++] = (byte) methods[i];

			return buffer;
		}

		Socks5AuthMethod NegotiateAuthMethod (Socket socket, CancellationToken cancellationToken, params Socks5AuthMethod[] methods)
		{
			var buffer = GetNegotiateAuthMethodCommand (methods);

			Send (socket, buffer, 0, buffer.Length, cancellationToken);

			// +-----+--------+
			// | VER | METHOD |
			// +-----+--------+
			// |  1  |   1    |
			// +-----+--------+
			int nread, n = 0;
			do {
				if ((nread = Receive (socket, buffer, 0 + n, 2 - n, cancellationToken)) > 0)
					n += nread;
			} while (n < 2);

			VerifySocksVersion (buffer[0]);

			return (Socks5AuthMethod) buffer[1];
		}

		async Task<Socks5AuthMethod> NegotiateAuthMethodAsync (Socket socket, CancellationToken cancellationToken, params Socks5AuthMethod[] methods)
		{
			var buffer = GetNegotiateAuthMethodCommand (methods);

			await SendAsync (socket, buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false);

			// +-----+--------+
			// | VER | METHOD |
			// +-----+--------+
			// |  1  |   1    |
			// +-----+--------+
			int nread, n = 0;
			do {
				if ((nread = await ReceiveAsync (socket, buffer, 0 + n, 2 - n, cancellationToken).ConfigureAwait (false)) > 0)
					n += nread;
			} while (n < 2);

			VerifySocksVersion (buffer[0]);

			return (Socks5AuthMethod) buffer[1];
		}

		byte[] GetAuthenticateCommand ()
		{
			var user = Encoding.UTF8.GetBytes (ProxyCredentials.UserName);

			if (user.Length > 255)
				throw new AuthenticationException ("User name too long.");

			var passwd = Encoding.UTF8.GetBytes (ProxyCredentials.Password);

			if (passwd.Length > 255) {
				Array.Clear (passwd, 0, passwd.Length);
				throw new AuthenticationException ("Password too long.");
			}

			var buffer = new byte[user.Length + passwd.Length + 3];
			int n = 0;

			buffer[n++] = 1;
			buffer[n++] = (byte) user.Length;
			Buffer.BlockCopy (user, 0, buffer, n, user.Length);
			n += user.Length;
			buffer[n++] = (byte) passwd.Length;
			Buffer.BlockCopy (passwd, 0, buffer, n, passwd.Length);

			Array.Clear (passwd, 0, passwd.Length);

			return buffer;
		}

		void Authenticate (Socket socket, CancellationToken cancellationToken)
		{
			var buffer = GetAuthenticateCommand ();

			Send (socket, buffer, 0, buffer.Length, cancellationToken);

			int nread, n = 0;

			do {
				if ((nread = Receive (socket, buffer, 0 + n, 2 - n, cancellationToken)) > 0)
					n += nread;
			} while (n < 2);

			if (buffer[1] != (byte) Socks5Reply.Success)
				throw new AuthenticationException ("Failed to authenticate with SOCKS5 proxy server.");
		}

		async Task AuthenticateAsync (Socket socket, CancellationToken cancellationToken)
		{
			var buffer = GetAuthenticateCommand ();

			await SendAsync (socket, buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false);

			int nread, n = 0;

			do {
				if ((nread = await ReceiveAsync (socket, buffer, 0 + n, 2 - n, cancellationToken).ConfigureAwait (false)) > 0)
					n += nread;
			} while (n < 2);

			if (buffer[1] != (byte) Socks5Reply.Success)
				throw new AuthenticationException ("Failed to authenticate with SOCKS5 proxy server.");
		}

		byte[] GetConnectCommand (Socks5AddressType addrType, byte[] domain, IPAddress ip, int port, out int n)
		{
			// +----+-----+-------+------+----------+----------+
			// |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
			// +----+-----+-------+------+----------+----------+
			// | 1  |  1  | X'00' |  1   | Variable |    2     |
			// +----+-----+-------+------+----------+----------+
			var buffer = new byte[4 + 257 + 2];
			byte[] addr;

			n = 0;

			buffer[n++] = (byte) SocksVersion;
			buffer[n++] = (byte) Socks5Command.Connect;
			buffer[n++] = 0x00;
			buffer[n++] = (byte) addrType;
			switch (addrType) {
			case Socks5AddressType.Domain:
				buffer[n++] = (byte) domain.Length;
				Buffer.BlockCopy (domain, 0, buffer, n, domain.Length);
				n += domain.Length;
				break;
			case Socks5AddressType.IPv6:
				addr = ip.GetAddressBytes ();
				Buffer.BlockCopy (addr, 0, buffer, n, addr.Length);
				n += 16;
				break;
			case Socks5AddressType.IPv4:
				addr = ip.GetAddressBytes ();
				Buffer.BlockCopy (addr, 0, buffer, n, addr.Length);
				n += 4;
				break;
			}
			buffer[n++] = (byte) (port >> 8);
			buffer[n++] = (byte) port;

			return buffer;
		}

		int ProcessPartialConnectResponse (string host, int port, byte[] buffer)
		{
			VerifySocksVersion (buffer[0]);

			if (buffer[1] != (byte) Socks5Reply.Success)
				throw new ProxyProtocolException (string.Format (CultureInfo.InvariantCulture, "Failed to connect to {0}:{1}: {2}", host, port, GetFailureReason (buffer[1])));

			// +-----+-----+-------+------+----------+----------+
			// | VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
			// +-----+-----+-------+------+----------+----------+
			// |  1  |  1  | X'00' |  1   | Variable |    2     |
			// +-----+-----+-------+------+----------+----------+
			var addrType = (Socks5AddressType) buffer[3];

			switch (addrType) {
			case Socks5AddressType.Domain: return 4 + buffer[4] + 2;
			case Socks5AddressType.IPv6: return 4 + 16 + 2;
			case Socks5AddressType.IPv4: return 4 + 4 + 2;
			default: throw new ProxyProtocolException ("Proxy server returned unknown address type.");
			}
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
		public override Stream Connect (string host, int port, CancellationToken cancellationToken = default)
		{
			ValidateArguments (host, port);

			cancellationToken.ThrowIfCancellationRequested ();

			var socket = SocketUtils.Connect (ProxyHost, ProxyPort, LocalEndPoint, cancellationToken);
			var addrType = GetAddressType (host, out var ip);
			byte[] domain = null;

			if (addrType == Socks5AddressType.Domain)
				domain = Encoding.UTF8.GetBytes (host);

			try {
				Socks5AuthMethod method;

				if (ProxyCredentials != null)
					method = NegotiateAuthMethod (socket, cancellationToken, Socks5AuthMethod.UserPassword, Socks5AuthMethod.Anonymous);
				else
					method = NegotiateAuthMethod (socket, cancellationToken, Socks5AuthMethod.Anonymous);

				switch (method) {
				case Socks5AuthMethod.UserPassword:
					Authenticate (socket, cancellationToken);
					break;
				case Socks5AuthMethod.Anonymous:
					break;
				default:
					throw new ProxyProtocolException ("Failed to negotiate authentication method with the proxy server.");
				}

				var buffer = GetConnectCommand (addrType, domain, ip, port, out int n);

				Send (socket, buffer, 0, n, cancellationToken);

				// +-----+-----+-------+------+----------+----------+
				// | VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
				// +-----+-----+-------+------+----------+----------+
				// |  1  |  1  | X'00' |  1   | Variable |    2     |
				// +-----+-----+-------+------+----------+----------+

				// Note: We know we'll need at least 4 bytes of header + a minimum of 1 byte
				// to determine the length of the BND.ADDR field if ATYP is a domain.
				int nread, need = 5;
				n = 0;

				do {
					if ((nread = Receive (socket, buffer, 0 + n, need - n, cancellationToken)) > 0)
						n += nread;
				} while (n < need);

				need = ProcessPartialConnectResponse (host, port, buffer);

				do {
					if ((nread = Receive (socket, buffer, 0 + n, need - n, cancellationToken)) > 0)
						n += nread;
				} while (n < need);

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
		public override async Task<Stream> ConnectAsync (string host, int port, CancellationToken cancellationToken = default)
		{
			ValidateArguments (host, port);

			cancellationToken.ThrowIfCancellationRequested ();

			var socket = await SocketUtils.ConnectAsync (ProxyHost, ProxyPort, LocalEndPoint, cancellationToken).ConfigureAwait (false);
			var addrType = GetAddressType (host, out var ip);
			byte[] domain = null;

			if (addrType == Socks5AddressType.Domain)
				domain = Encoding.UTF8.GetBytes (host);

			try {
				Socks5AuthMethod method;

				if (ProxyCredentials != null)
					method = await NegotiateAuthMethodAsync (socket, cancellationToken, Socks5AuthMethod.UserPassword, Socks5AuthMethod.Anonymous).ConfigureAwait (false);
				else
					method = await NegotiateAuthMethodAsync (socket, cancellationToken, Socks5AuthMethod.Anonymous).ConfigureAwait (false);

				switch (method) {
				case Socks5AuthMethod.UserPassword:
					await AuthenticateAsync (socket, cancellationToken).ConfigureAwait (false);
					break;
				case Socks5AuthMethod.Anonymous:
					break;
				default:
					throw new ProxyProtocolException ("Failed to negotiate authentication method with the proxy server.");
				}

				var buffer = GetConnectCommand (addrType, domain, ip, port, out int n);

				await SendAsync (socket, buffer, 0, n, cancellationToken).ConfigureAwait (false);

				// +-----+-----+-------+------+----------+----------+
				// | VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
				// +-----+-----+-------+------+----------+----------+
				// |  1  |  1  | X'00' |  1   | Variable |    2     |
				// +-----+-----+-------+------+----------+----------+

				// Note: We know we'll need at least 4 bytes of header + a minimum of 1 byte
				// to determine the length of the BND.ADDR field if ATYP is a domain.
				int nread, need = 5;
				n = 0;

				do {
					if ((nread = await ReceiveAsync (socket, buffer, 0 + n, need - n, cancellationToken).ConfigureAwait (false)) > 0)
						n += nread;
				} while (n < need);

				need = ProcessPartialConnectResponse (host, port, buffer);

				do {
					if ((nread = await ReceiveAsync (socket, buffer, 0 + n, need - n, cancellationToken).ConfigureAwait (false)) > 0)
						n += nread;
				} while (n < need);

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
