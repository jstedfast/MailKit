//
// NetworkStream.cs
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
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MailKit.Net
{
	class NetworkStream : Stream
	{
		SocketAsyncEventArgs send;
		SocketAsyncEventArgs recv;
		bool ownsSocket;
		bool connected;

		public NetworkStream (Socket socket, bool ownsSocket)
		{
			send = new SocketAsyncEventArgs ();
			send.Completed += AsyncOperationCompleted;
			send.AcceptSocket = socket;

			recv = new SocketAsyncEventArgs ();
			recv.Completed += AsyncOperationCompleted;
			recv.AcceptSocket = socket;

			this.ownsSocket = ownsSocket;
			connected = socket.Connected;
			Socket = socket;
		}

		public Socket Socket {
			get; private set;
		}

		public bool DataAvailable {
			get { return connected && Socket.Available > 0; }
		}

		public override bool CanRead {
			get { return connected; }
		}

		public override bool CanWrite {
			get { return connected; }
		}

		public override bool CanSeek {
			get { return false; }
		}

		public override bool CanTimeout {
			get { return connected; }
		}

		public override long Length {
			get { throw new NotSupportedException (); }
		}

		public override long Position {
			get { throw new NotSupportedException (); }
			set { throw new NotSupportedException (); }
		}

		public override int ReadTimeout {
			get {
				int timeout = Socket.ReceiveTimeout;

				return timeout == 0 ? Timeout.Infinite : timeout;
			}
			set {
				if (value <= 0 && value != Timeout.Infinite)
					throw new ArgumentOutOfRangeException (nameof (value));

				Socket.ReceiveTimeout = value;
			}
		}

		public override int WriteTimeout {
			get {
				int timeout = Socket.SendTimeout;

				return timeout == 0 ? Timeout.Infinite : timeout;
			}
			set {
				if (value <= 0 && value != Timeout.Infinite)
					throw new ArgumentOutOfRangeException (nameof (value));

				Socket.SendTimeout = value;
			}
		}

		void AsyncOperationCompleted (object sender, SocketAsyncEventArgs args)
		{
			var tcs = (TaskCompletionSource<bool>) args.UserToken;

			if (args.SocketError == SocketError.Success) {
				tcs.TrySetResult (true);
				return;
			}

			tcs.TrySetException (new SocketException ((int) args.SocketError));
		}

		void Disconnect ()
		{
			try {
#if !NETSTANDARD1_3 && !NETSTANDARD1_6
				Socket.Disconnect (false);
#endif
				Socket.Dispose ();
			} catch {
				return;
			} finally {
				connected = false;
				send.Dispose ();
				send = null;
				recv.Dispose ();
				recv = null;
			}
		}

		public override int Read (byte[] buffer, int offset, int count)
		{
			try {
				return Socket.Receive (buffer, offset, count, SocketFlags.None);
			} catch (SocketException ex) {
				throw new IOException (ex.Message, ex);
			}
		}

		public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var tcs = new TaskCompletionSource<bool> ();

			using (var timeout = new CancellationTokenSource (ReadTimeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeout.Token)) {
					using (var registration = linked.Token.Register (() => tcs.TrySetCanceled (), false)) {
						recv.SetBuffer (buffer, offset, count);
						recv.UserToken = tcs;

						if (!Socket.ReceiveAsync (recv))
							AsyncOperationCompleted (null, recv);

						try {
							await tcs.Task.ConfigureAwait (false);
							return recv.BytesTransferred;
						} catch (OperationCanceledException) {
							Disconnect ();
							throw;
						} catch (Exception ex) {
							Disconnect ();
							if (ex is SocketException)
								throw new IOException (ex.Message, ex);
							throw;
						}
					}
				}
			}
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			try {
				Socket.Send (buffer, offset, count, SocketFlags.None);
			} catch (SocketException ex) {
				throw new IOException (ex.Message, ex);
			}
		}

		public override async Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			var tcs = new TaskCompletionSource<bool> ();

			using (var timeout = new CancellationTokenSource (WriteTimeout)) {
				using (var linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeout.Token)) {
					using (var registration = linked.Token.Register (() => tcs.TrySetCanceled (), false)) {
						send.SetBuffer (buffer, offset, count);
						send.UserToken = tcs;

						if (!Socket.SendAsync (send))
							AsyncOperationCompleted (null, send);

						try {
							await tcs.Task.ConfigureAwait (false);
						} catch (OperationCanceledException) {
							Disconnect ();
							throw;
						} catch (Exception ex) {
							Disconnect ();
							if (ex is SocketException)
								throw new IOException (ex.Message, ex);
							throw;
						}
					}
				}
			}
		}

		public override void Flush ()
		{
		}

		public override Task FlushAsync (CancellationToken cancellationToken)
		{
			return Task.FromResult (true);
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		public static NetworkStream Get (Stream stream)
		{
			if (stream is CompressedStream compressed)
				stream = compressed.InnerStream;

			if (stream is SslStream ssl)
				stream = ssl.InnerStream;

			return stream as NetworkStream;
		}

		public void Poll (SelectMode mode, CancellationToken cancellationToken)
		{
			if (!cancellationToken.CanBeCanceled)
				return;

			do {
				cancellationToken.ThrowIfCancellationRequested ();
				// wait 1/4 second and then re-check for cancellation
			} while (!Socket.Poll (250000, mode));

			cancellationToken.ThrowIfCancellationRequested ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				if (ownsSocket && connected) {
					ownsSocket = false;
					Disconnect ();
				} else {
					send?.Dispose ();
					send = null;

					recv?.Dispose ();
					recv = null;
				}
			}

			base.Dispose (disposing);
		}
	}
}
