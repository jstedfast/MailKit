//
// CompressedStream.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
using System.Threading.Tasks;

using Org.BouncyCastle.Utilities.Zlib;

namespace MailKit {
	/// <summary>
	/// A compressed stream.
	/// </summary>
	class CompressedStream : Stream
	{
		readonly ZStream zIn, zOut;
		bool eos, disposed;

		public CompressedStream (Stream baseStream)
		{
			BaseStream = baseStream;

			zOut = new ZStream ();
			zOut.deflateInit (5, true);
			zOut.next_out = new byte[4096];

			zIn = new ZStream ();
			zIn.inflateInit (true);
			zIn.next_in = new byte[4096];
		}

		/// <summary>
		/// Gets the base stream.
		/// </summary>
		/// <value>The base stream.</value>
		public Stream BaseStream {
			get; private set;
		}

		/// <summary>
		/// Gets whether the stream supports reading.
		/// </summary>
		/// <value><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</value>
		public override bool CanRead {
			get { return BaseStream.CanRead; }
		}

		/// <summary>
		/// Gets whether the stream supports writing.
		/// </summary>
		/// <value><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</value>
		public override bool CanWrite {
			get { return BaseStream.CanWrite; }
		}

		/// <summary>
		/// Gets whether the stream supports seeking.
		/// </summary>
		/// <value><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</value>
		public override bool CanSeek {
			get { return false; }
		}

		/// <summary>
		/// Gets whether the stream supports I/O timeouts.
		/// </summary>
		/// <value><c>true</c> if the stream supports I/O timeouts; otherwise, <c>false</c>.</value>
		public override bool CanTimeout {
			get { return BaseStream.CanTimeout; }
		}

		/// <summary>
		/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
		/// </summary>
		/// <returns>A value, in miliseconds, that determines how long the stream will attempt to read before timing out.</returns>
		/// <value>The read timeout.</value>
		public override int ReadTimeout {
			get { return BaseStream.ReadTimeout; }
			set { BaseStream.ReadTimeout = value; }
		}

		/// <summary>
		/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
		/// </summary>
		/// <returns>A value, in miliseconds, that determines how long the stream will attempt to write before timing out.</returns>
		/// <value>The write timeout.</value>
		public override int WriteTimeout {
			get { return BaseStream.WriteTimeout; }
			set { BaseStream.WriteTimeout = value; }
		}

		/// <summary>
		/// Gets or sets the position within the current stream.
		/// </summary>
		/// <returns>The current position within the stream.</returns>
		/// <value>The position of the stream.</value>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support seeking.
		/// </exception>
		public override long Position {
			get { throw new NotSupportedException (); }
			set { throw new NotSupportedException (); }
		}

		/// <summary>
		/// Gets the length in bytes of the stream.
		/// </summary>
		/// <returns>A long value representing the length of the stream in bytes.</returns>
		/// <value>The length of the stream.</value>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support seeking.
		/// </exception>
		public override long Length {
			get { throw new NotSupportedException (); }
		}

		static void ValidateArguments (byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException (nameof (buffer));

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0 || count > (buffer.Length - offset))
				throw new ArgumentOutOfRangeException (nameof (count));
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException (nameof (CompressedStream));
		}

		async Task<int> ReadAsync (byte[] buffer, int offset, int count, bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (count == 0)
				return 0;

			zIn.next_out = buffer;
			zIn.next_out_index = offset;
			zIn.avail_out = count;

			do {
				if (zIn.avail_in == 0 && !eos) {
					if (doAsync)
						zIn.avail_in = await BaseStream.ReadAsync (zIn.next_in, 0, zIn.next_in.Length, cancellationToken).ConfigureAwait (false);
					else
						zIn.avail_in = BaseStream.Read (zIn.next_in, 0, zIn.next_in.Length);
					eos = zIn.avail_in == 0;
					zIn.next_in_index = 0;
				}

				int retval = zIn.inflate (JZlib.Z_FULL_FLUSH);

				if (retval == JZlib.Z_STREAM_END)
					break;

				if (eos && retval == JZlib.Z_BUF_ERROR)
					return 0;

				if (retval != JZlib.Z_OK)
					throw new IOException ("Error inflating: " + zIn.msg);
			} while (zIn.avail_out == count);

			return count - zIn.avail_out;
		}

		/// <summary>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </summary>
		/// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many
		/// bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The buffer offset.</param>
		/// <param name="count">The number of bytes to read.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than zero or greater than the length of <paramref name="buffer"/>.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="buffer"/> is not large enough to contain <paramref name="count"/> bytes strting
		/// at the specified <paramref name="offset"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override int Read (byte[] buffer, int offset, int count)
		{
			return ReadAsync (buffer, offset, count, false, CancellationToken.None).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </summary>
		/// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many
		/// bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The buffer offset.</param>
		/// <param name="count">The number of bytes to read.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than zero or greater than the length of <paramref name="buffer"/>.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="buffer"/> is not large enough to contain <paramref name="count"/> bytes strting
		/// at the specified <paramref name="offset"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return ReadAsync (buffer, offset, count, true, cancellationToken);
		}

		async Task WriteAsync (byte[] buffer, int offset, int count, bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (count == 0)
				return;

			zOut.next_in = buffer;
			zOut.next_in_index = offset;
			zOut.avail_in = count;

			do {
				zOut.avail_out = zOut.next_out.Length;
				zOut.next_out_index = 0;

				if (zOut.deflate (JZlib.Z_FULL_FLUSH) != JZlib.Z_OK)
					throw new IOException ("Error deflating: " + zOut.msg);

				if (doAsync)
					await BaseStream.WriteAsync (zOut.next_out, 0, zOut.next_out.Length - zOut.avail_out, cancellationToken).ConfigureAwait (false);
				else
					BaseStream.Write (zOut.next_out, 0, zOut.next_out.Length - zOut.avail_out);
			} while (zOut.avail_in > 0 || zOut.avail_out == 0);
		}

		/// <summary>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">The buffer to write.</param>
		/// <param name="offset">The offset of the first byte to write.</param>
		/// <param name="count">The number of bytes to write.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than zero or greater than the length of <paramref name="buffer"/>.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="buffer"/> is not large enough to contain <paramref name="count"/> bytes strting
		/// at the specified <paramref name="offset"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support writing.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override void Write (byte[] buffer, int offset, int count)
		{
			WriteAsync (buffer, offset, count, false, CancellationToken.None).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </summary>
		/// <returns>A task that represents the asynchronous write operation.</returns>
		/// <param name="buffer">The buffer to write.</param>
		/// <param name="offset">The offset of the first byte to write.</param>
		/// <param name="count">The number of bytes to write.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="buffer"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is less than zero or greater than the length of <paramref name="buffer"/>.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="buffer"/> is not large enough to contain <paramref name="count"/> bytes strting
		/// at the specified <paramref name="offset"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support writing.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return WriteAsync (buffer, offset, count, true, cancellationToken);
		}

		/// <summary>
		/// Clears all output buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support writing.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override void Flush ()
		{
			CheckDisposed ();

			BaseStream.Flush ();
		}

		/// <summary>
		/// Clears all output buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <returns>A task that represents the asynchronous flush operation.</returns>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support writing.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override Task FlushAsync (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			return BaseStream.FlushAsync (cancellationToken);
		}

		/// <summary>
		/// Sets the position within the current stream.
		/// </summary>
		/// <returns>The new position within the stream.</returns>
		/// <param name="offset">The offset into the stream relative to the <paramref name="origin"/>.</param>
		/// <param name="origin">The origin to seek from.</param>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support seeking.
		/// </exception>
		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Sets the length of the stream.
		/// </summary>
		/// <param name="value">The desired length of the stream in bytes.</param>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support setting the length.
		/// </exception>
		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="DuplexStream"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				BaseStream.Dispose ();
				disposed = true;
				zOut.free ();
				zIn.free ();
			}

			base.Dispose (disposing);
		}
	}
}
