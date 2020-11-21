//
// HttpProxyClient.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Buffers;
using System.Threading;
using System.Net.Sockets;
using System.Globalization;
using System.Threading.Tasks;

namespace MailKit.Net.Proxy
{
	/// <summary>
	/// An HTTP proxy client.
	/// </summary>
	/// <remarkas>
	/// An HTTP proxy client.
	/// </remarkas>
	public class HttpProxyClient : ProxyClient
	{
		const int BufferSize = 4096;

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

		async Task<Socket> ConnectAsync (string host, int port, bool doAsync, CancellationToken cancellationToken)
		{
			ValidateArguments (host, port);

			cancellationToken.ThrowIfCancellationRequested ();

			var socket = await SocketUtils.ConnectAsync (ProxyHost, ProxyPort, LocalEndPoint, doAsync, cancellationToken).ConfigureAwait (false);
			var builder = new StringBuilder ();

			builder.AppendFormat (CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.1\r\n", host, port);
			builder.AppendFormat (CultureInfo.InvariantCulture, "Host: {0}:{1}\r\n", host, port);
			if (ProxyCredentials != null) {
				var token = Encoding.UTF8.GetBytes (string.Format (CultureInfo.InvariantCulture, "{0}:{1}", ProxyCredentials.UserName, ProxyCredentials.Password));
				var base64 = Convert.ToBase64String (token);
				builder.AppendFormat (CultureInfo.InvariantCulture, "Proxy-Authorization: Basic {0}\r\n", base64);
			}
			builder.Append ("\r\n");

			var command = Encoding.UTF8.GetBytes (builder.ToString ());

			try {
				await SendAsync (socket, command, 0, command.Length, doAsync, cancellationToken).ConfigureAwait (false);

				var buffer = ArrayPool<byte>.Shared.Rent (BufferSize);

				try {
					var endOfHeaders = false;
					var newline = false;

					builder.Clear ();

					// read until we consume the end of the headers (it's ok if we read some of the content)
					do {
						int nread = await ReceiveAsync (socket, buffer, 0, BufferSize, doAsync, cancellationToken).ConfigureAwait (false);

						if (nread > 0) {
							int n = nread;

							for (int i = 0; i < nread && !endOfHeaders; i++) {
								switch ((char) buffer[i]) {
								case '\r':
									break;
								case '\n':
									endOfHeaders = newline;
									newline = true;

									if (endOfHeaders)
										n = i + 1;
									break;
								default:
									newline = false;
									break;
								}
							}

							builder.Append (Encoding.UTF8.GetString (buffer, 0, n));
						}
					} while (!endOfHeaders);
				} finally {
					ArrayPool<byte>.Shared.Return (buffer);
				}

				int index = 0;

				while (builder[index] != '\n')
					index++;

				if (index > 0 && builder[index - 1] == '\r')
					index--;

				// trim everything beyond the "HTTP/1.1 200 ..." part of the response
				builder.Length = index;

				var response = builder.ToString ();

				if (response.Length >= 15 && response.StartsWith ("HTTP/1.", StringComparison.OrdinalIgnoreCase) &&
					(response[7] == '1' || response[7] == '0') && response[8] == ' ' &&
					response[9] == '2' && response[10] == '0' && response[11] == '0' &&
					response[12] == ' ') {
					return socket;
				}

				throw new ProxyProtocolException (string.Format (CultureInfo.InvariantCulture, "Failed to connect to {0}:{1}: {2}", host, port, response));
			} catch {
#if !NETSTANDARD1_3 && !NETSTANDARD1_6
				if (socket.Connected)
					socket.Disconnect (false);
#endif
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
