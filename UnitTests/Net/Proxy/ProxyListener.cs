//
// ProxyListener.cs
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
using System.Collections;

using NetworkStream = MailKit.Net.NetworkStream;

namespace UnitTests.Net.Proxy {
	abstract class ProxyListener : IDisposable
	{
		CancellationTokenSource cancellationTokenSource;
		TcpListener listener;
		Task runningTask;
		bool disposed;

		protected ProxyListener ()
		{
		}

		~ProxyListener ()
		{
			Dispose (false);
		}

		public bool IsRunning {
			get; private set;
		}

		public IPAddress IPAddress {
			get; private set;
		}

		public int Port {
			get; private set;
		}

		public void Start (IPAddress addr, int port)
		{
			listener = new TcpListener (addr, port);
			listener.Start (1);

			IPAddress = ((IPEndPoint) listener.LocalEndpoint).Address;
			Port = ((IPEndPoint) listener.LocalEndpoint).Port;

			cancellationTokenSource = new CancellationTokenSource ();
			var token = cancellationTokenSource.Token;

			runningTask = Task.Run (() => AcceptProxyConnections (token), token);
			IsRunning = true;
		}

		public void Stop ()
		{
			if (listener != null && IsRunning) {
				try {
					cancellationTokenSource.Cancel ();
				} catch {
				}

				try {
					listener.Stop ();
				} catch {
				}

				try {
					runningTask.GetAwaiter ().GetResult ();
				} catch {
				}

				cancellationTokenSource.Dispose ();
				cancellationTokenSource = null;
				IsRunning = false;
			}
		}

		protected abstract Task<Socket> ClientCommandReceived (NetworkStream client, byte[] buffer, int length, CancellationToken cancellationToken);

		async Task AcceptProxyConnection (Socket socket, CancellationToken cancellationToken)
		{
			using (var client = new NetworkStream (socket, true)) {
				var buffer = new byte[4096];
				Socket remote = null;
				int nread;

				while ((nread = await client.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0) {
					try {
						remote = await ClientCommandReceived (client, buffer, nread, cancellationToken).ConfigureAwait (false);
						if (remote != null)
							break;
					} catch {
						return;
					}
				}

				if (remote == null)
					return;

				try {
					using (var server = new NetworkStream (remote, true)) {
						do {
							while (client.DataAvailable || server.DataAvailable) {
								if (client.DataAvailable) {
									if ((nread = await client.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0)
										await server.WriteAsync (buffer, 0, nread, cancellationToken).ConfigureAwait (false);
								}

								if (server.DataAvailable) {
									if ((nread = await server.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0)
										await client.WriteAsync (buffer, 0, nread, cancellationToken).ConfigureAwait (false);
								}
							}

							var checkRead = new ArrayList ();
							checkRead.Add (socket);
							checkRead.Add (remote);

							var checkError = new ArrayList ();
							checkError.Add (socket);
							checkError.Add (remote);

							Socket.Select (checkRead, null, checkError, 250000);

							cancellationToken.ThrowIfCancellationRequested ();
						} while (socket.Connected && remote.Connected);
					}
				} catch (OperationCanceledException) {
					return;
				} catch (SocketException) {
					return;
				}
			}
		}

		async Task AcceptProxyConnections (CancellationToken cancellationToken)
		{
			while (!cancellationToken.IsCancellationRequested) {
				try {
					using (var socket = await listener.AcceptSocketAsync ().ConfigureAwait (false))
						await AcceptProxyConnection (socket, cancellationToken).ConfigureAwait (false);
				} catch (ObjectDisposedException) {
				}
			}
		}

		protected virtual void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				Stop ();
				disposed = true;
			}
		}

		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}
	}
}
