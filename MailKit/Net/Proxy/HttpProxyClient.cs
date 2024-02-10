//
// HttpProxyClient.cs
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
using System.Buffers;
using System.Threading;
using System.Net.Sockets;
using System.Globalization;
using System.Threading.Tasks;

using NetworkStream = MailKit.Net.NetworkStream;

namespace MailKit.Net.Proxy
{
	/// <summary>
	/// An HTTP proxy client.
	/// </summary>
	/// <remarks>
	/// An HTTP proxy client.
	/// </remarks>
	public class HttpProxyClient : ProxyClient
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.HttpProxyClient"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.HttpProxyClient"/> class.
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
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		public HttpProxyClient (string host, int port) : base (host, port)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.HttpProxyClient"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.HttpProxyClient"/> class.
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
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		public HttpProxyClient (string host, int port, NetworkCredential credentials) : base (host, port, credentials)
		{
		}

		internal static byte[] GetConnectCommand (string host, int port, NetworkCredential proxyCredentials)
		{
			var builder = new StringBuilder ();

			builder.AppendFormat (CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.1\r\n", host, port);
			builder.AppendFormat (CultureInfo.InvariantCulture, "Host: {0}:{1}\r\n", host, port);
			if (proxyCredentials != null) {
				var token = Encoding.UTF8.GetBytes (string.Format (CultureInfo.InvariantCulture, "{0}:{1}", proxyCredentials.UserName, proxyCredentials.Password));
				var base64 = Convert.ToBase64String (token);
				builder.AppendFormat (CultureInfo.InvariantCulture, "Proxy-Authorization: Basic {0}\r\n", base64);
			}
			builder.Append ("\r\n");

			return Encoding.UTF8.GetBytes (builder.ToString ());
		}

		internal static bool TryConsumeHeaders (ByteArrayBuilder builder, byte c, ref bool newLine)
		{
			var endOfHeaders = false;

			switch ((char) c) {
			case '\r':
				break;
			case '\n':
				endOfHeaders = newLine;
				newLine = true;
				break;
			default:
				newLine = false;
				break;
			}

			builder.Append (c);

			return endOfHeaders;
		}

		internal static void ValidateHttpResponse (string response, string host, int port)
		{
			// Verify that the response starts with something like "HTTP/1.1 200 ..."
			if (response.Length >= 15 && response.StartsWith ("HTTP/1.", StringComparison.OrdinalIgnoreCase) &&
				(response[7] == '1' || response[7] == '0') && response[8] == ' ' &&
				response[9] == '2' && response[10] == '0' && response[11] == '0' &&
				response[12] == ' ') {
				return;
			}

			throw new ProxyProtocolException (string.Format (CultureInfo.InvariantCulture, "Failed to connect to {0}:{1}: {2}", host, port, response));
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

			var command = GetConnectCommand (host, port, ProxyCredentials);
			var socket = SocketUtils.Connect (ProxyHost, ProxyPort, LocalEndPoint, cancellationToken);

			try {
				Send (socket, command, 0, command.Length, cancellationToken);

				var builder = new ByteArrayBuilder (256);
				var buffer = new byte[1];
				var newline = false;
				string response;

				try {
					// read until we consume the end of the headers
					do {
						int nread = Receive (socket, buffer, 0, 1, cancellationToken);

						if (nread < 1 || TryConsumeHeaders (builder, buffer[0], ref newline))
							break;
					} while (true);

					response = builder.ToString ();
				} finally {
					builder.Dispose ();
				}

				ValidateHttpResponse (response, host, port);
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
			var command = GetConnectCommand (host, port, ProxyCredentials);

			try {
				await SendAsync (socket, command, 0, command.Length, cancellationToken).ConfigureAwait (false);

				var builder = new ByteArrayBuilder (256);
				var buffer = new byte[1];
				var newline = false;
				string response;

				try {
					// read until we consume the end of the headers
					do {
						int nread = await ReceiveAsync (socket, buffer, 0, 1, cancellationToken).ConfigureAwait (false);

						if (nread < 1 || TryConsumeHeaders (builder, buffer[0], ref newline))
							break;
					} while (true);

					response = builder.ToString ();
				} finally {
					builder.Dispose ();
				}

				ValidateHttpResponse (response, host, port);
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
