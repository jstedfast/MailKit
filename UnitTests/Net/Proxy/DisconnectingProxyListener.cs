//
// DisconnectingProxyListener.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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

using System.Net;
using System.Net.Sockets;

namespace UnitTests.Net.Proxy {
	/// <summary>
	/// A proxy listener that accepts a single connection and, for each scripted reply, reads a
	/// request from the client and then sends the reply (which may be empty). Once the replies
	/// have been exhausted, the connection is closed. This makes it possible to simulate a proxy
	/// server that disconnects in the middle of a reply.
	/// </summary>
	sealed class DisconnectingProxyListener : IDisposable
	{
		readonly TcpListener listener;
		readonly Task task;

		public DisconnectingProxyListener (params byte[][] replies)
		{
			listener = new TcpListener (IPAddress.Loopback, 0);
			listener.Start ();

			task = Task.Run (async () => {
				using var client = await listener.AcceptTcpClientAsync ().ConfigureAwait (false);
				using var stream = client.GetStream ();
				var buffer = new byte[1024];

				foreach (var reply in replies) {
					if (await stream.ReadAsync (buffer, 0, buffer.Length).ConfigureAwait (false) == 0)
						return;

					if (reply.Length > 0) {
						await stream.WriteAsync (reply, 0, reply.Length).ConfigureAwait (false);
						await stream.FlushAsync ().ConfigureAwait (false);
					}
				}
			});
		}

		public string Host => IPAddress.Loopback.ToString ();

		public int Port => ((IPEndPoint) listener.LocalEndpoint).Port;

		public void Dispose ()
		{
			listener.Stop ();

			try {
				task.Wait (TimeSpan.FromSeconds (5));
			} catch {
			}
		}
	}
}
