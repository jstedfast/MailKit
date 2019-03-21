//
// NetworkStream.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2019 Xamarin Inc. (www.xamarin.com)
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
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MailKit.Net
{
	class NetworkStream : Stream
	{
		readonly Socket socket;
		bool ownsSocket;
		bool closed;

		public NetworkStream (Socket socket, bool ownsSocket)
		{
			this.ownsSocket = ownsSocket;
			this.socket = socket;
		}

		~NetworkStream ()
		{
			Dispose (false);
		}

		public bool DataAvailable {
			get { return socket.Available > 0; }
		}

		public override bool CanRead {
			get { return true; }
		}

		public override bool CanWrite {
			get { return true; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanTimeout {
			get { return true; }
		}

		public override long Length {
			get { throw new NotSupportedException (); }
		}

		public override long Position {
			get { throw new NotSupportedException (); }
			set { throw new NotSupportedException (); }
		}

		public override int ReadTimeout {
			get { return socket.ReceiveTimeout; }
			set { socket.ReceiveTimeout = value; }
		}

		public override int WriteTimeout {
			get { return socket.SendTimeout; }
			set { socket.SendTimeout = value; }
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			return socket.Receive (buffer, offset, count, SocketFlags.None);
		}

		public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var tcs = new TaskCompletionSource<bool> ();

			using (var registration = cancellationToken.Register (() => tcs.TrySetCanceled (), false)) {
				var ar = socket.BeginReceive (buffer, offset, count, SocketFlags.None, e => tcs.TrySetResult (true), null);

				try {
					await tcs.Task.ConfigureAwait (false);
					return socket.EndReceive (ar);
				} catch (OperationCanceledException) {
					if (socket.Connected)
						socket.Shutdown (SocketShutdown.Both);

					socket.Dispose ();
					closed = true;
					throw;
				} catch {
					socket.Dispose ();
					closed = true;
					throw;
				}
			}
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			socket.Send (buffer, offset, count, SocketFlags.None);
		}

		public override async Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var tcs = new TaskCompletionSource<bool> ();

			using (var registration = cancellationToken.Register (() => tcs.TrySetCanceled (), false)) {
				var ar = socket.BeginSend (buffer, offset, count, SocketFlags.None, e => tcs.TrySetResult (true), null);

				try {
					await tcs.Task.ConfigureAwait (false);
					socket.EndSend (ar);
				} catch (OperationCanceledException) {
					if (socket.Connected)
						socket.Shutdown (SocketShutdown.Both);

					socket.Dispose ();
					closed = true;
					throw;
				} catch {
					socket.Dispose ();
					closed = true;
					throw;
				}
			}
		}

		public override void Flush ()
		{
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing && ownsSocket && !closed) {
				ownsSocket = false;
				socket.Dispose ();
			}

			closed = true;

			base.Dispose (disposing);
		}
	}
}
