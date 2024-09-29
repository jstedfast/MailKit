//
// WebProxyClient.cs
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

#if NET6_0_OR_GREATER

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MailKit.Net.Proxy
{
	/// <summary>
	/// A proxy client that makes use of a <see cref="IWebProxy"/>.
	/// </summary>
	/// <remarks>
	/// A proxy client that makes use of a <see cref="IWebProxy"/>.
	/// </remarks>
	internal class WebProxyClient : ProxyClient
	{
		readonly IWebProxy proxy;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.WebProxyClient"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.ProxyClient"/> class.
		/// </remarks>
		/// <param name="proxy">The web proxy.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="proxy"/> is <c>null</c>.
		/// </exception>
		public WebProxyClient (IWebProxy proxy) : base ("System", 0)
		{
			if (proxy is null)
				throw new ArgumentNullException (nameof (proxy));

			this.proxy = proxy;
		}

		static Uri GetTargetUri (string host, int port)
		{
			string scheme;

			switch (port) {
			case 25: case 465: case 587: scheme = "smtp"; break;
			case 110: case 995: scheme = "pop"; break;
			case 143: case 993: scheme = "imap"; break;
			default: scheme = "http"; break;
			}

			return new Uri ($"{scheme}://{host}:{port}");
		}

		static NetworkCredential GetNetworkCredential (ICredentials credentials, Uri uri)
		{
			if (credentials is NetworkCredential network)
				return network;

			return credentials.GetCredential (uri, "Basic");
		}

		static ProxyClient GetProxyClient (Uri proxyUri, ICredentials credentials)
		{
			var credential = GetNetworkCredential (credentials, proxyUri);

			if (proxyUri.Scheme.Equals ("https", StringComparison.OrdinalIgnoreCase))
				return new HttpsProxyClient (proxyUri.Host, proxyUri.Port, credential);

			if (proxyUri.Scheme.Equals ("http", StringComparison.OrdinalIgnoreCase))
				return new HttpProxyClient (proxyUri.Host, proxyUri.Port, credential);

			throw new NotImplementedException ($"The default system proxy does not support {proxyUri.Scheme}.");
		}

		/// <summary>
		/// Connect to the target host.
		/// </summary>
		/// <remarks>
		/// Connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected network stream.</returns>
		/// <param name="host">The host name of the target server.</param>
		/// <param name="port">The target server port.</param>
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

			var targetUri = GetTargetUri (host, port);
			var proxyUri = proxy.GetProxy (targetUri);

			if (proxyUri is null) {
				// Note: if the proxy URI is null, then it means that the proxy should be bypassed.
				var socket = SocketUtils.Connect (host, port, LocalEndPoint, cancellationToken);
				return new NetworkStream (socket, true);
			}

			var proxyClient = GetProxyClient (proxyUri, proxy.Credentials);
			
			return proxyClient.Connect (host, port, cancellationToken);
		}

		/// <summary>
		/// Asynchronously connect to the target host.
		/// </summary>
		/// <remarks>
		/// Asynchronously connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected network stream.</returns>
		/// <param name="host">The host name of the target server.</param>
		/// <param name="port">The target server port.</param>
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

			var targetUri = GetTargetUri (host, port);
			var proxyUri = proxy.GetProxy (targetUri);

			if (proxyUri is null) {
				// Note: if the proxy URI is null, then it means that the proxy should be bypassed.
				var socket = await SocketUtils.ConnectAsync (host, port, LocalEndPoint, cancellationToken).ConfigureAwait (false);
				return new NetworkStream (socket, true);
			}

			var proxyClient = GetProxyClient (proxyUri, proxy.Credentials);

			return await proxyClient.ConnectAsync (host, port, cancellationToken);
		}
	}
}

#endif
