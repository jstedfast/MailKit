//
// SocketUtils.cs
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
using System.Threading;
using System.Net.Sockets;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MailKit.Net
{
	static class SocketUtils
	{
		class SocketConnectState
		{
			readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool> ();
#if NET6_0_OR_GREATER
			readonly long connectStartTicks = Stopwatch.GetTimestamp ();
#endif
			readonly Socket socket;
			readonly IPAddress ip;
			readonly string host;
			readonly int port;

			public SocketConnectState (Socket socket, IPAddress ip, string host, int port)
			{
				this.socket = socket;
				this.ip = ip;
				this.host = host;
				this.port = port;
			}

			public Task Task { get { return tcs.Task; } }

			public void OnCanceled ()
			{
				tcs.TrySetCanceled ();
			}

			public void OnEndConnect (IAsyncResult ar)
			{
				try {
					socket.EndConnect (ar);
				} catch (Exception ex) {
					// The connection failed. Try setting an exception in case the connection hasn't also been cancelled.
#if NET6_0_OR_GREATER
					bool cancelled = !tcs.TrySetException (ex);

					Telemetry.Socket.Metrics?.ReportConnectFailed (connectStartTicks, ip, host, port, cancelled, ex);
#else
					tcs.TrySetException (ex);
#endif
					socket.Dispose ();
					return;
				}

				// The connection was successful.
				if (tcs.TrySetResult (true)) {
#if NET6_0_OR_GREATER
					Telemetry.Socket.Metrics?.ReportConnected (connectStartTicks, ip, host, port);
#endif
					return;
				}

				// Note: If we get this far, then it means that the connection has been cancelled.
#if NET6_0_OR_GREATER
				Telemetry.Socket.Metrics?.ReportConnectFailed (connectStartTicks, ip, host, port, true);
#endif

				try {
					socket.Disconnect (false);
					socket.Dispose ();
				} catch {
					return;
				}
			}
		}

		static void OnEndConnect (IAsyncResult ar)
		{
			var state = (SocketConnectState) ar.AsyncState;

			state.OnEndConnect (ar);
		}

		public static Socket Connect (string host, int port, IPEndPoint localEndPoint, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var ipAddresses = Dns.GetHostAddresses (host);

			for (int i = 0; i < ipAddresses.Length; i++) {
				cancellationToken.ThrowIfCancellationRequested ();

				var socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					if (localEndPoint != null)
						socket.Bind (localEndPoint);
				} catch {
					socket.Dispose ();

					if (i + 1 == ipAddresses.Length)
						throw;

					continue;
				}

#if NET6_0_OR_GREATER
				long connectStartTicks = Stopwatch.GetTimestamp ();
#endif

				try {
					if (cancellationToken.CanBeCanceled) {
						var state = new SocketConnectState (socket, ipAddresses[i], host, port);

						using (var registration = cancellationToken.Register (state.OnCanceled, false)) {
							var ar = socket.BeginConnect (ipAddresses[i], port, OnEndConnect, state);
							state.Task.GetAwaiter ().GetResult ();
						}
					} else {
						socket.Connect (ipAddresses[i], port);

#if NET6_0_OR_GREATER
						Telemetry.Socket.Metrics?.ReportConnected (connectStartTicks, ipAddresses[i], host, port);
#endif
					}

					return socket;
				} catch (OperationCanceledException) {
					throw;
				} catch (Exception ex) {
					if (!cancellationToken.CanBeCanceled) {
#if NET6_0_OR_GREATER
						Telemetry.Socket.Metrics?.ReportConnectFailed (connectStartTicks, ipAddresses[i], host, port, false, ex);
#endif

						socket.Dispose ();
					}

					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			throw new IOException (string.Format ("Failed to resolve host: {0}", host));
		}

		public static async Task<Socket> ConnectAsync (string host, int port, IPEndPoint localEndPoint, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

#if NET6_0_OR_GREATER
			var ipAddresses = await Dns.GetHostAddressesAsync (host, cancellationToken).ConfigureAwait (false);
#else
			var ipAddresses = await Dns.GetHostAddressesAsync (host).ConfigureAwait (false);
#endif

			for (int i = 0; i < ipAddresses.Length; i++) {
				cancellationToken.ThrowIfCancellationRequested ();

				var socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					if (localEndPoint != null)
						socket.Bind (localEndPoint);
				} catch {
					socket.Dispose ();

					if (i + 1 == ipAddresses.Length)
						throw;

					continue;
				}

				try {
					var state = new SocketConnectState (socket, ipAddresses[i], host, port);

					using (var registration = cancellationToken.Register (state.OnCanceled, false)) {
						var ar = socket.BeginConnect (ipAddresses[i], port, OnEndConnect, state);
						await state.Task.ConfigureAwait (false);
					}

					return socket;
				} catch (OperationCanceledException) {
					throw;
				} catch {
					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			throw new IOException (string.Format ("Failed to resolve host: {0}", host));
		}

		public static Socket Connect (string host, int port, IPEndPoint localEndPoint, int timeout, CancellationToken cancellationToken)
		{
			using (var ts = new CancellationTokenSource (timeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, ts.Token)) {
					try {
						return Connect (host, port, localEndPoint, linked.Token);
					} catch (OperationCanceledException) {
						if (!cancellationToken.IsCancellationRequested)
							throw new TimeoutException ();
						throw;
					}
				}
			}
		}

		public static async Task<Socket> ConnectAsync (string host, int port, IPEndPoint localEndPoint, int timeout, CancellationToken cancellationToken)
		{
			using (var ts = new CancellationTokenSource (timeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, ts.Token)) {
					try {
						return await ConnectAsync (host, port, localEndPoint, linked.Token).ConfigureAwait (false);
					} catch (OperationCanceledException) {
						if (!cancellationToken.IsCancellationRequested)
							throw new TimeoutException ();
						throw;
					}
				}
			}
		}

		public static void Poll (Socket socket, SelectMode mode, CancellationToken cancellationToken)
		{
			do {
				cancellationToken.ThrowIfCancellationRequested ();
				// wait 1/4 second and then re-check for cancellation
			} while (!socket.Poll (250000, mode));
		}
	}
}
