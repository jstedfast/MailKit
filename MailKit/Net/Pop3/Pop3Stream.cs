//
// Pop3Stream.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
using System.Net.Sockets;

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// An enumeration of the possible POP3 streaming modes.
	/// </summary>
	/// <remarks>
	/// Normal operation is done in the <see cref="Pop3StreamMode.Line"/> mode,
	/// but when retrieving messages (via RETR) or headers (via TOP), the
	/// <see cref="Pop3StreamMode.Data"/> mode should be used.
	/// </remarks>
	enum Pop3StreamMode {
		/// <summary>
		/// Reads 1 line at a time.
		/// </summary>
		Line,

		/// <summary>
		/// Reads data in chunks, ignoring line state.
		/// </summary>
		Data
	}

	/// <summary>
	/// A stream for communicating with a POP3 server.
	/// </summary>
	/// <remarks>
	/// A stream capable of reading data line-by-line (<see cref="Pop3StreamMode.Line"/>)
	/// or by raw byte streams (<see cref="Pop3StreamMode.Data"/>).
	/// </remarks>
	class Pop3Stream : Stream
	{
		const int ReadAheadSize = 128;
		const int BlockSize = 4096;
		const int PadSize = 4;

		// I/O buffering
		readonly byte[] input = new byte[ReadAheadSize + BlockSize + PadSize];
		const int inputStart = ReadAheadSize;
		readonly IProtocolLogger logger;
		int inputIndex = ReadAheadSize;
		int inputEnd = ReadAheadSize;
		bool disposed;
		bool midline;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Stream"/> class.
		/// </summary>
		/// <param name="source">The underlying network stream.</param>
		/// <param name="protocolLogger">The protocol logger.</param>
		public Pop3Stream (Stream source, IProtocolLogger protocolLogger)
		{
			logger = protocolLogger;
			IsConnected = true;
			Stream = source;
		}

		/// <summary>
		/// Gets or sets the underlying network stream.
		/// </summary>
		/// <value>The underlying network stream.</value>
		public Stream Stream {
			get; set;
		}

		/// <summary>
		/// Gets or sets the mode used for reading.
		/// </summary>
		/// <value>The mode.</value>
		public Pop3StreamMode Mode {
			get; set;
		}

		/// <summary>
		/// Gets whether or not the stream is connected.
		/// </summary>
		/// <value><c>true</c> if the stream is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get; private set;
		}

		/// <summary>
		/// Gets whether or not the end of the raw data has been reached in <see cref="Pop3StreamMode.Data"/> mode.
		/// </summary>
		/// <remarks>
		/// When reading the resonse to a command such as RETR, the end of the data is marked by line matching ".\r\n".
		/// </remarks>
		/// <value><c>true</c> if the end of the data has been reached; otherwise, <c>false</c>.</value>
		public bool IsEndOfData {
			get; private set;
		}

		/// <summary>
		/// Gets whether the stream supports reading.
		/// </summary>
		/// <value><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</value>
		public override bool CanRead {
			get { return Stream.CanRead; }
		}

		/// <summary>
		/// Gets whether the stream supports writing.
		/// </summary>
		/// <value><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</value>
		public override bool CanWrite {
			get { return Stream.CanWrite; }
		}

		/// <summary>
		/// Gets whether the stream supports seeking.
		/// </summary>
		/// <value><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</value>
		public override bool CanSeek {
			get { return Stream.CanSeek; }
		}

		/// <summary>
		/// Gets whether the stream supports I/O timeouts.
		/// </summary>
		/// <value><c>true</c> if the stream supports I/O timeouts; otherwise, <c>false</c>.</value>
		public override bool CanTimeout {
			get { return Stream.CanTimeout; }
		}

		/// <summary>
		/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
		/// </summary>
		/// <returns>A value, in miliseconds, that determines how long the stream will attempt to read before timing out.</returns>
		/// <value>The read timeout.</value>
		public override int ReadTimeout {
			get { return Stream.ReadTimeout; }
			set { Stream.ReadTimeout = value; }
		}

		/// <summary>
		/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out.
		/// </summary>
		/// <returns>A value, in miliseconds, that determines how long the stream will attempt to write before timing out.</returns>
		/// <value>The write timeout.</value>
		public override int WriteTimeout {
			get { return Stream.WriteTimeout; }
			set { Stream.WriteTimeout = value; }
		}

		/// <summary>
		/// Gets or sets the position within the current stream.
		/// </summary>
		/// <returns>The current position within the stream.</returns>
		/// <value>The position of the stream.</value>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support seeking.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		public override long Position {
			get { return Stream.Position; }
			set { Stream.Position = value; }
		}

		/// <summary>
		/// Gets the length in bytes of the stream.
		/// </summary>
		/// <returns>A long value representing the length of the stream in bytes.</returns>
		/// <value>The length of the stream.</value>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support seeking.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		public override long Length {
			get { return Stream.Length; }
		}

		static unsafe void MemMove (byte *buf, int sourceIndex, int destIndex, int length)
		{
			if (sourceIndex + length > destIndex) {
				byte* src = buf + sourceIndex + length - 1;
				byte *dest = buf + destIndex + length - 1;
				byte *start = buf + sourceIndex;

				while (src >= start)
					*dest-- = *src--;
			} else {
				byte* src = buf + sourceIndex;
				byte* dest = buf + destIndex;
				byte* end = src + length;

				while (src < end)
					*dest++ = *src++;
			}
		}

		unsafe int ReadAhead (byte* inbuf)
		{
			int left = inputEnd - inputIndex;

			var network = Stream as NetworkStream;
			if (network != null) {
				if (!network.DataAvailable)
					return left;
			} else if (Stream.CanSeek) {
				// running the unit tests
				if (Stream.Position == Stream.Length)
					return left;
			}

			int index = inputIndex;
			int start = inputStart;
			int end = inputEnd;
			int nread;

			// attempt to align the end of the remaining input with ReadAheadSize
			if (index >= start) {
				start -= Math.Min (ReadAheadSize, left);
				MemMove (inbuf, index, start, left);
				index = start;
				start += left;
			} else if (index > 0) {
				int shift = Math.Min (index, end - start);
				MemMove (inbuf, index, index - shift, left);
				index -= shift;
				start = index + left;
			} else {
				// we can't shift...
				start = end;
			}

			inputIndex = index;
			inputEnd = start;

			end = input.Length - PadSize;

			try {
				if ((nread = Stream.Read (input, start, end - start)) > 0) {
					logger.LogServer (input, start, nread);
					inputEnd += nread;
				}
			} catch (IOException) {
				IsConnected = false;
				throw;
			}

			return inputEnd - inputIndex;
		}

		static void ValidateArguments (byte[] buffer, int offset, int count)
		{
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0 || offset + count > buffer.Length)
				throw new ArgumentOutOfRangeException ("count");
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("Pop3Stream");
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
		/// <exception cref="System.InvalidOperationException">
		/// The stream is in line mode (see <see cref="Pop3StreamMode.Line"/>).
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override int Read (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (Mode != Pop3StreamMode.Data)
				throw new InvalidOperationException ();

			if (IsEndOfData)
				return 0;

			unsafe {
				fixed (byte* inbuf = input, outbuf = buffer) {
					byte* outptr = outbuf + offset;
					byte* outend = outptr + count;
					bool allowReadAhead = true;
					byte* inptr, inend;

					do {
						// we need at least 3 bytes: ".\r\n"
						if ((inputEnd - inputIndex) < 3) {
							if (!allowReadAhead)
								break;

							allowReadAhead = false;
							ReadAhead (inbuf);
						}

						inptr = inbuf + inputIndex;
						inend = inbuf + inputEnd;
						*inend = (byte) '\n';

						while (inptr < inend) {
							if (midline) {
								// read until end-of-line
								while (outptr < outend && *inptr != (byte) '\n')
									*outptr++ = *inptr++;

								if (inptr == inend || outptr == outend)
									break;

								*outptr++ = *inptr++;
								midline = false;
							}

							if (inptr == inend)
								break;

							if (*inptr == (byte) '.') {
								if ((inend - inptr) >= 3 && *(inptr + 1) == (byte) '\r' && *(inptr + 2) == (byte) '\n') {
									IsEndOfData = true;
									midline = false;
									inptr += 3;
									break;
								}

								if ((inend - inptr) >= 2 && *(inptr + 1) == (byte) '\n') {
									IsEndOfData = true;
									midline = false;
									inptr += 2;
									break;
								}

								if (*(inptr + 1) == (byte) '.')
									inptr++;
							}

							midline = true;
						}

						inputIndex = (int) (inptr - inbuf);
					} while (outptr < outend && !IsEndOfData);

					return (int) (outptr - outbuf) - offset;
				}
			}
		}

		/// <summary>
		/// Reads a single line of input from the stream.
		/// </summary>
		/// <remarks>
		/// This method should be called in a loop until it returns <c>true</c>.
		/// </remarks>
		/// <returns><c>true</c>, if reading the line is complete, <c>false</c> otherwise.</returns>
		/// <param name="buffer">The buffer containing the line data.</param>
		/// <param name="offset">The offset into the buffer containing bytes read.</param>
		/// <param name="count">The number of bytes read.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		internal bool ReadLine (out byte[] buffer, out int offset, out int count)
		{
			CheckDisposed ();

			unsafe {
				fixed (byte* inbuf = input) {
					byte* start, inptr, inend;

					if (inputIndex == inputEnd)
						ReadAhead (inbuf);

					offset = inputIndex;
					buffer = input;

					start = inbuf + inputIndex;
					inend = inbuf + inputEnd;
					*inend = (byte) '\n';
					inptr = start;

					// FIXME: use SIMD to optimize this
					while (*inptr != (byte) '\n')
						inptr++;

					inputIndex = (int) (inptr - inbuf);
					count = (int) (inptr - start);

					if (inptr == inend) {
						midline = true;
						return false;
					}

					// consume the '\n'
					midline = false;
					inputIndex++;
					count++;

					return true;
				}
			}
		}

		/// <summary>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </summary>
		/// <param name='buffer'>The buffer to write.</param>
		/// <param name='offset'>The offset of the first byte to write.</param>
		/// <param name='count'>The number of bytes to write.</param>
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
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			try {
				Stream.Write (buffer, offset, count);
				logger.LogClient (buffer, offset, count);
			} catch (IOException) {
				IsConnected = false;
				throw;
			}

			IsEndOfData = false;
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written
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

			try {
				Stream.Flush ();
			} catch (IOException) {
				IsConnected = false;
				throw;
			}
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
		/// Dispose the specified disposing.
		/// </summary>
		/// <param name="disposing">If set to <c>true</c> disposing.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				IsConnected = false;
				Stream.Dispose ();
				disposed = true;
			}

			base.Dispose (disposing);
		}
	}
}
