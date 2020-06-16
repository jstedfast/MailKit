//
// ProxyClient.cs
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
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MailKit.Net.Proxy
{
	/// <summary>
	/// An abstract proxy client base class.
	/// </summary>
	/// <remarks>
	/// A proxy client can be used to connect to a service through a firewall that
	/// would otherwise be blocked.
	/// </remarks>
	public abstract class ProxyClient : IProxyClient
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.ProxyClient"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.ProxyClient"/> class.
		/// </remarks>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		protected ProxyClient (string host, int port)
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0 || host.Length > 255)
				throw new ArgumentException ("The length of the host name must be between 0 and 256 characters.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			ProxyHost = host;
			ProxyPort = port == 0 ? 1080 : port;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.ProxyClient"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Proxy.ProxyClient"/> class.
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
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// <para>-or-</para>
		/// <para>The length of <paramref name="host"/> is greater than 255 characters.</para>
		/// </exception>
		protected ProxyClient (string host, int port, NetworkCredential credentials) : this (host, port)
		{
			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			ProxyCredentials = credentials;
		}

		/// <summary>
		/// Gets the proxy credentials.
		/// </summary>
		/// <remarks>
		/// Gets the credentials to use when authenticating with the proxy server.
		/// </remarks>
		/// <value>The proxy credentials.</value>
		public NetworkCredential ProxyCredentials {
			get; private set;
		}

		/// <summary>
		/// Get the proxy host.
		/// </summary>
		/// <remarks>
		/// Gets the host name of the proxy server.
		/// </remarks>
		/// <value>The host name of the proxy server.</value>
		public string ProxyHost {
			get; private set;
		}

		/// <summary>
		/// Get the proxy port.
		/// </summary>
		/// <remarks>
		/// Gets the port to use when connecting to the proxy server.
		/// </remarks>
		/// <value>The proxy port.</value>
		public int ProxyPort {
			get; private set;
		}

		/// <summary>
		/// Get or set the local IP end point to use when connecting to a remote host.
		/// </summary>
		/// <remarks>
		/// Gets or sets the local IP end point to use when connecting to a remote host.
		/// </remarks>
		/// <value>The local IP end point or <c>null</c> to use the default end point.</value>
		public IPEndPoint LocalEndPoint {
			get; set;
		}

		internal static void ValidateArguments (string host, int port)
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0 || host.Length > 255)
				throw new ArgumentException ("The length of the host name must be between 0 and 256 characters.", nameof (host));

			if (port <= 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));
		}

		static void ValidateArguments (string host, int port, int timeout)
		{
			ValidateArguments (host, port);

			if (timeout < -1)
				throw new ArgumentOutOfRangeException (nameof (timeout));
		}

		static void AsyncOperationCompleted (object sender, SocketAsyncEventArgs args)
		{
			var tcs = (TaskCompletionSource<bool>) args.UserToken;

			if (args.SocketError == SocketError.Success) {
				tcs.TrySetResult (true);
				return;
			}

			tcs.TrySetException (new SocketException ((int) args.SocketError));
		}

		internal static async Task SendAsync (Socket socket, byte[] buffer, int offset, int length, bool doAsync, CancellationToken cancellationToken)
		{
			if (doAsync || cancellationToken.CanBeCanceled) {
				var tcs = new TaskCompletionSource<bool> ();

				using (var registration = cancellationToken.Register (() => tcs.TrySetCanceled (), false)) {
					using (var args = new SocketAsyncEventArgs ()) {
						args.Completed += AsyncOperationCompleted;
						args.SetBuffer (buffer, offset, length);
						args.AcceptSocket = socket;
						args.UserToken = tcs;

						if (!socket.SendAsync (args))
							AsyncOperationCompleted (null, args);

						if (doAsync)
							await tcs.Task.ConfigureAwait (false);
						else
							tcs.Task.GetAwaiter ().GetResult ();

						return;
					}
				}
			}

			SocketUtils.Poll (socket, SelectMode.SelectWrite, cancellationToken);

			socket.Send (buffer, offset, length, SocketFlags.None);
		}

		internal static async Task<int> ReceiveAsync (Socket socket, byte[] buffer, int offset, int length, bool doAsync, CancellationToken cancellationToken)
		{
			if (doAsync || cancellationToken.CanBeCanceled) {
				var tcs = new TaskCompletionSource<bool> ();

				using (var registration = cancellationToken.Register (() => tcs.TrySetCanceled (), false)) {
					using (var args = new SocketAsyncEventArgs ()) {
						args.Completed += AsyncOperationCompleted;
						args.SetBuffer (buffer, offset, length);
						args.AcceptSocket = socket;
						args.UserToken = tcs;

						if (!socket.ReceiveAsync (args))
							AsyncOperationCompleted (null, args);

						if (doAsync)
							await tcs.Task.ConfigureAwait (false);
						else
							tcs.Task.GetAwaiter ().GetResult ();

						return args.BytesTransferred;
					}
				}
			}

			SocketUtils.Poll (socket, SelectMode.SelectRead, cancellationToken);

			return socket.Receive (buffer, offset, length, SocketFlags.None);
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
		/// <paramref name="port"/> is not between <c>1</c> and <c>65535</c>.
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
		public abstract Socket Connect (string host, int port, CancellationToken cancellationToken = default (CancellationToken));

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
		/// <paramref name="port"/> is not between <c>1</c> and <c>65535</c>.
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
		public abstract Task<Socket> ConnectAsync (string host, int port, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Connect to the target host.
		/// </summary>
		/// <remarks>
		/// Connects to the target host and port through the proxy server.
		/// </remarks>
		/// <returns>The connected socket.</returns>
		/// <param name="host">The host name of the proxy server.</param>
		/// <param name="port">The proxy server port.</param>
		/// <param name="timeout">The timeout, in milliseconds.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="port"/> is not between <c>1</c> and <c>65535</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="timeout"/> is less than <c>-1</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.TimeoutException">
		/// The operation timed out.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public virtual Socket Connect (string host, int port, int timeout, CancellationToken cancellationToken = default (CancellationToken))
		{
			ValidateArguments (host, port, timeout);

			using (var ts = new CancellationTokenSource (timeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, ts.Token)) {
					try {
						return Connect (host, port, linked.Token);
 					} catch (OperationCanceledException) {
						if (!cancellationToken.IsCancellationRequested)
							throw new TimeoutException ();
						throw;
					}
				}
			}
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
		/// <param name="timeout">The timeout, in milliseconds.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="port"/> is not between <c>1</c> and <c>65535</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="timeout"/> is less than <c>-1</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.TimeoutException">
		/// The operation timed out.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public async virtual Task<Socket> ConnectAsync (string host, int port, int timeout, CancellationToken cancellationToken = default (CancellationToken))
		{
			ValidateArguments (host, port, timeout);

			using (var ts = new CancellationTokenSource (timeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, ts.Token)) {
					try {
						return await ConnectAsync (host, port, linked.Token).ConfigureAwait (false);
					} catch (OperationCanceledException) {
						if (!cancellationToken.IsCancellationRequested)
							throw new TimeoutException ();
						throw;
					}
				}
			}
		}
	}
}
