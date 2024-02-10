//
// Pop3Stream.cs
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
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;

using MimeKit.IO;

using Buffer = System.Buffer;
using SslStream = MailKit.Net.SslStream;
using NetworkStream = MailKit.Net.NetworkStream;

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
	class Pop3Stream : Stream, ICancellableStream
	{
		const int ReadAheadSize = 128;
		const int BlockSize = 4096;
		const int PadSize = 4;

		// I/O buffering
		readonly byte[] input = new byte[ReadAheadSize + BlockSize + PadSize];
		const int inputStart = ReadAheadSize;

		readonly byte[] output = new byte[BlockSize];
		int outputIndex;

		readonly IProtocolLogger logger;
		int inputIndex = ReadAheadSize;
		int inputEnd = ReadAheadSize;
		Pop3StreamMode mode;
		bool disposed;
		bool midline;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Stream"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Pop3Stream"/>.
		/// </remarks>
		/// <param name="source">The underlying network stream.</param>
		/// <param name="protocolLogger">The protocol logger.</param>
		public Pop3Stream (Stream source, IProtocolLogger protocolLogger)
		{
			logger = protocolLogger;
			IsConnected = true;
			Stream = source;
		}

		/// <summary>
		/// Get or sets the underlying network stream.
		/// </summary>
		/// <remarks>
		/// Gets or sets the underlying network stream.
		/// </remarks>
		/// <value>The underlying network stream.</value>
		public Stream Stream {
			get; internal set;
		}

		/// <summary>
		/// Gets or sets the mode used for reading.
		/// </summary>
		/// <value>The mode.</value>
		public Pop3StreamMode Mode {
			get { return mode; }
			set {
				IsEndOfData = false;
				mode = value;
			}
		}

		/// <summary>
		/// Get whether or not the stream is connected.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the stream is connected.
		/// </remarks>
		/// <value><c>true</c> if the stream is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get; private set;
		}

		/// <summary>
		/// Get whether or not the end of the raw data has been reached in <see cref="Pop3StreamMode.Data"/> mode.
		/// </summary>
		/// <remarks>
		/// When reading the resonse to a command such as RETR, the end of the data is marked by line matching ".\r\n".
		/// </remarks>
		/// <value><c>true</c> if the end of the data has been reached; otherwise, <c>false</c>.</value>
		public bool IsEndOfData {
			get; private set;
		}

		/// <summary>
		/// Get whether the stream supports reading.
		/// </summary>
		/// <remarks>
		/// Gets whether the stream supports reading.
		/// </remarks>
		/// <value><c>true</c> if the stream supports reading; otherwise, <c>false</c>.</value>
		public override bool CanRead {
			get { return Stream.CanRead; }
		}

		/// <summary>
		/// Get whether the stream supports writing.
		/// </summary>
		/// <remarks>
		/// Gets whether the stream supports writing.
		/// </remarks>
		/// <value><c>true</c> if the stream supports writing; otherwise, <c>false</c>.</value>
		public override bool CanWrite {
			get { return Stream.CanWrite; }
		}

		/// <summary>
		/// Get whether the stream supports seeking.
		/// </summary>
		/// <remarks>
		/// Gets whether the stream supports seeking.
		/// </remarks>
		/// <value><c>true</c> if the stream supports seeking; otherwise, <c>false</c>.</value>
		public override bool CanSeek {
			get { return false; }
		}

		/// <summary>
		/// Get whether the stream supports I/O timeouts.
		/// </summary>
		/// <remarks>
		/// Gets whether the stream supports I/O timeouts.
		/// </remarks>
		/// <value><c>true</c> if the stream supports I/O timeouts; otherwise, <c>false</c>.</value>
		public override bool CanTimeout {
			get { return Stream.CanTimeout; }
		}

		/// <summary>
		/// Get or set a value, in milliseconds, that determines how long the stream will attempt to read before timing out.
		/// </summary>
		/// <remarks>
		/// Gets or sets a value, in milliseconds, that determines how long the stream will attempt to read before timing out.
		/// </remarks>
		/// <returns>A value, in milliseconds, that determines how long the stream will attempt to read before timing out.</returns>
		/// <value>The read timeout.</value>
		public override int ReadTimeout {
			get { return Stream.ReadTimeout; }
			set { Stream.ReadTimeout = value; }
		}

		/// <summary>
		/// Get or set a value, in milliseconds, that determines how long the stream will attempt to write before timing out.
		/// </summary>
		/// <remarks>
		/// Gets or sets a value, in milliseconds, that determines how long the stream will attempt to write before timing out.
		/// </remarks>
		/// <returns>A value, in milliseconds, that determines how long the stream will attempt to write before timing out.</returns>
		/// <value>The write timeout.</value>
		public override int WriteTimeout {
			get { return Stream.WriteTimeout; }
			set { Stream.WriteTimeout = value; }
		}

		/// <summary>
		/// Get or set the position within the current stream.
		/// </summary>
		/// <remarks>
		/// Gets or sets the position within the current stream.
		/// </remarks>
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
			set { throw new NotSupportedException (); }
		}

		/// <summary>
		/// Get the length of the stream, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the length of the stream, in bytes.
		/// </remarks>
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

		void AlignReadAheadBuffer (out int start, out int end)
		{
			int left = inputEnd - inputIndex;

			start = inputStart;
			end = inputEnd;

			if (left > 0) {
				int index = inputIndex;

				// attempt to align the end of the remaining input with ReadAheadSize
				if (index >= start) {
					start -= Math.Min (ReadAheadSize, left);
					Buffer.BlockCopy (input, index, input, start, left);
					index = start;
					start += left;
				} else if (index > 0) {
					int shift = Math.Min (index, end - start);
					Buffer.BlockCopy (input, index, input, index - shift, left);
					index -= shift;
					start = index + left;
				} else {
					// we can't shift...
					start = end;
				}

				inputIndex = index;
				inputEnd = start;
			} else {
				inputIndex = start;
				inputEnd = start;
			}

			end = input.Length - PadSize;
		}

		void OnReadAhead (int start, int nread)
		{
			if (nread > 0) {
				logger.LogServer (input, start, nread);
				inputEnd += nread;
			} else {
				throw new Pop3ProtocolException ("The POP3 server has unexpectedly disconnected.");
			}
		}

		int ReadAhead (CancellationToken cancellationToken)
		{
			AlignReadAheadBuffer (out int start, out int end);

			try {
				var network = Stream as NetworkStream;
				int nread;

				cancellationToken.ThrowIfCancellationRequested ();

				network?.Poll (SelectMode.SelectRead, cancellationToken);
				nread = Stream.Read (input, start, end - start);

				OnReadAhead (start, nread);
			} catch {
				IsConnected = false;
				throw;
			}

			return inputEnd - inputIndex;
		}

		async Task<int> ReadAheadAsync (CancellationToken cancellationToken)
		{
			AlignReadAheadBuffer (out int start, out int end);

			try {
				var network = Stream as NetworkStream;
				int nread;

				cancellationToken.ThrowIfCancellationRequested ();

				nread = await Stream.ReadAsync (input, start, end - start, cancellationToken).ConfigureAwait (false);

				OnReadAhead (start, nread);
			} catch {
				IsConnected = false;
				throw;
			}

			return inputEnd - inputIndex;
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
				throw new ObjectDisposedException (nameof (Pop3Stream));
		}

		bool NeedInput (int index, int inputLeft)
		{
			if (inputLeft == 2 && input[index] == (byte) '.' && input[index + 1] == '\n')
				return false;

			return true;
		}

		void Read (byte[] buffer, ref int index, int endIndex)
		{
			// terminate the input buffer with a '\n' to remove bounds checking in our inner loop
			input[inputEnd] = (byte) '\n';

			while (inputIndex < inputEnd) {
				if (midline) {
					// read until end-of-line
					while (index < endIndex && input[inputIndex] != (byte) '\n')
						buffer[index++] = input[inputIndex++];

					if (inputIndex == inputEnd || index == endIndex)
						break;

					// consume the '\n' character
					buffer[index++] = input[inputIndex++];
					midline = false;
				}

				if (inputIndex == inputEnd)
					break;

				if (input[inputIndex] == (byte) '.') {
					int inputLeft = inputEnd - inputIndex;

					// check for ".\r\n" which signifies the end of the data stream
					if (inputLeft >= 3 && input[inputIndex + 1] == (byte) '\r' && input[inputIndex + 2] == (byte) '\n') {
						IsEndOfData = true;
						midline = false;
						inputIndex += 3;
						break;
					}

					// check for ".\n" which is used by some broken UNIX servers in place of ".\r\n"
					if (inputLeft >= 2 && input[inputIndex + 1] == (byte) '\n') {
						IsEndOfData = true;
						midline = false;
						inputIndex += 2;
						break;
					}

					// check for "." or ".\r" which might be an incomplete termination sequence
					if (inputLeft == 1 || (inputLeft == 2 && input[inputIndex + 1] == (byte) '\r')) {
						// not enough data...
						break;
					}

					// check for lines beginning with ".." which should be transformed into "."
					if (input[inputIndex + 1] == (byte) '.')
						inputIndex++;
				}

				midline = true;
			}
		}

		/// <summary>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </summary>
		/// <remarks>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </remarks>
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
		/// <exception cref="System.InvalidOperationException">
		/// The stream is in line mode (see <see cref="Pop3StreamMode.Line"/>).
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public int Read (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (Mode != Pop3StreamMode.Data)
				throw new InvalidOperationException ();

			if (IsEndOfData || count == 0)
				return 0;

			int endIndex = offset + count;
			int index = offset;
			int inputLeft;

			do {
				inputLeft = inputEnd - inputIndex;

				// we need at least 3 bytes: ".\r\n"
				if (inputLeft < 3 && (midline || NeedInput (inputIndex, inputLeft))) {
					if (index > offset)
						break;

					ReadAhead (cancellationToken);
				}

				Read (buffer, ref index, endIndex);
			} while (index < endIndex && !IsEndOfData);

			return index - offset;
		}

		/// <summary>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </summary>
		/// <remarks>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </remarks>
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
			return Read (buffer, offset, count, CancellationToken.None);
		}

		/// <summary>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </summary>
		/// <remarks>
		/// Reads a sequence of bytes from the stream and advances the position
		/// within the stream by the number of bytes read.
		/// </remarks>
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
		/// <exception cref="System.InvalidOperationException">
		/// The stream is in line mode (see <see cref="Pop3StreamMode.Line"/>).
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (Mode != Pop3StreamMode.Data)
				throw new InvalidOperationException ();

			if (IsEndOfData || count == 0)
				return 0;

			int endIndex = offset + count;
			int index = offset;
			int inputLeft;

			do {
				inputLeft = inputEnd - inputIndex;

				// we need at least 3 bytes: ".\r\n"
				if (inputLeft < 3 && (midline || NeedInput (inputIndex, inputLeft))) {
					if (index > offset)
						break;

					await ReadAheadAsync (cancellationToken).ConfigureAwait (false);
				}

				Read (buffer, ref index, endIndex);
			} while (index < endIndex && !IsEndOfData);

			return index - offset;
		}

		bool TryReadLine (ByteArrayBuilder builder)
		{
			unsafe {
				fixed (byte* inbuf = input) {
					byte* start, inptr, inend;
					int offset = inputIndex;
					int count;

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
						builder.Append (input, offset, count);
						midline = true;
						return false;
					}

					// consume the '\n'
					midline = false;
					inputIndex++;
					count++;

					builder.Append (input, offset, count);

					return true;
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
		/// <param name="builder">The output buffer to write the line data into.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		internal bool ReadLine (ByteArrayBuilder builder, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (inputIndex == inputEnd)
				ReadAhead (cancellationToken);

			return TryReadLine (builder);
		}

		/// <summary>
		/// Asynchronously reads a single line of input from the stream.
		/// </summary>
		/// <remarks>
		/// This method should be called in a loop until it returns <c>true</c>.
		/// </remarks>
		/// <returns><c>true</c>, if reading the line is complete, <c>false</c> otherwise.</returns>
		/// <param name="builder">The output buffer to write the line data into.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		internal async Task<bool> ReadLineAsync (ByteArrayBuilder builder, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (inputIndex == inputEnd)
				await ReadAheadAsync (cancellationToken).ConfigureAwait (false);

			return TryReadLine (builder);
		}

		unsafe bool TryQueueCommand (Encoder encoder, string command, ref int index)
		{
			fixed (char* cmd = command) {
				int outputLeft = output.Length - outputIndex;
				int charCount = command.Length - index;
				char* chars = cmd + index;

				var needed = encoder.GetByteCount (chars, charCount, true);

				if (needed > output.Length) {
					// If the command we are trying to queue is larger than the output buffer and we
					// already have some commands queued in the output buffer, then flush the queue
					// before queuing this command.
					if (outputIndex > 0)
						return false;
				} else if (needed > outputLeft && index == 0) {
					// If we are trying to queue a new command (index == 0) and we need more space than
					// what remains in the output buffer, then flush the output buffer before queueing
					// the new command. Some servers do not handle receiving partial commands well.
					return false;
				}

				fixed (byte* outbuf = output) {
					byte* outptr = outbuf + outputIndex;

					encoder.Convert (chars, charCount, outptr, outputLeft, true, out int charsUsed, out int bytesUsed, out bool completed);
					outputIndex += bytesUsed;
					index += charsUsed;

					return completed;
				}
			}
		}

		/// <summary>
		/// Queue a command to the POP3 server.
		/// </summary>
		/// <remarks>
		/// Queues a command to the POP3 server.
		/// </remarks>
		/// <param name="encoding">The character encoding.</param>
		/// <param name="command">The command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public void QueueCommand (Encoding encoding, string command, CancellationToken cancellationToken)
		{
			var encoder = encoding.GetEncoder ();
			int index = 0;

			while (!TryQueueCommand (encoder, command, ref index))
				Flush (cancellationToken);
		}

		/// <summary>
		/// Asynchronously queue a command to the POP3 server.
		/// </summary>
		/// <remarks>
		/// Asynchronously queues a command to the POP3 server.
		/// </remarks>
		/// <param name="encoding">The character encoding.</param>
		/// <param name="command">The command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public async Task QueueCommandAsync (Encoding encoding, string command, CancellationToken cancellationToken)
		{
			var encoder = encoding.GetEncoder ();
			int index = 0;

			while (!TryQueueCommand (encoder, command, ref index))
				await FlushAsync (cancellationToken).ConfigureAwait (false);
		}

		void OnWriteException (Exception ex, CancellationToken cancellationToken)
		{
			IsConnected = false;
			if (ex is not OperationCanceledException)
				cancellationToken.ThrowIfCancellationRequested ();
		}

		/// <summary>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </summary>
		/// <remarks>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </remarks>
		/// <param name='buffer'>The buffer to write.</param>
		/// <param name='offset'>The offset of the first byte to write.</param>
		/// <param name='count'>The number of bytes to write.</param>
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public void Write (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			try {
				var network = NetworkStream.Get (Stream);
				int index = offset;
				int left = count;

				while (left > 0) {
					int n = Math.Min (BlockSize - outputIndex, left);

					if (outputIndex > 0 || n < BlockSize) {
						// append the data to the output buffer
						Buffer.BlockCopy (buffer, index, output, outputIndex, n);
						outputIndex += n;
						index += n;
						left -= n;
					}

					if (outputIndex == BlockSize) {
						// flush the output buffer
						network?.Poll (SelectMode.SelectWrite, cancellationToken);
						Stream.Write (output, 0, BlockSize);
						logger.LogClient (output, 0, BlockSize);
						outputIndex = 0;
					}

					if (outputIndex == 0) {
						// write blocks of data to the stream without buffering
						while (left >= BlockSize) {
							network?.Poll (SelectMode.SelectWrite, cancellationToken);
							Stream.Write (buffer, index, BlockSize);
							logger.LogClient (buffer, index, BlockSize);
							index += BlockSize;
							left -= BlockSize;
						}
					}
				}
			} catch (Exception ex) {
				OnWriteException (ex, cancellationToken);
				throw;
			}

			IsEndOfData = false;
		}

		/// <summary>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </summary>
		/// <remarks>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </remarks>
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
			Write (buffer, offset, count, CancellationToken.None);
		}

		/// <summary>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </summary>
		/// <remarks>
		/// Writes a sequence of bytes to the stream and advances the current
		/// position within this stream by the number of bytes written.
		/// </remarks>
		/// <returns>A task that represents the asynchronous write operation.</returns>
		/// <param name='buffer'>The buffer to write.</param>
		/// <param name='offset'>The offset of the first byte to write.</param>
		/// <param name='count'>The number of bytes to write.</param>
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override async Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			try {
				int index = offset;
				int left = count;

				while (left > 0) {
					int n = Math.Min (BlockSize - outputIndex, left);

					if (outputIndex > 0 || n < BlockSize) {
						// append the data to the output buffer
						Buffer.BlockCopy (buffer, index, output, outputIndex, n);
						outputIndex += n;
						index += n;
						left -= n;
					}

					if (outputIndex == BlockSize) {
						// flush the output buffer
						await Stream.WriteAsync (output, 0, BlockSize, cancellationToken).ConfigureAwait (false);
						logger.LogClient (output, 0, BlockSize);
						outputIndex = 0;
					}

					if (outputIndex == 0) {
						// write blocks of data to the stream without buffering
						while (left >= BlockSize) {
							await Stream.WriteAsync (buffer, index, BlockSize, cancellationToken).ConfigureAwait (false);
							logger.LogClient (buffer, index, BlockSize);
							index += BlockSize;
							left -= BlockSize;
						}
					}
				}
			} catch (Exception ex) {
				OnWriteException (ex, cancellationToken);
				throw;
			}

			IsEndOfData = false;
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <remarks>
		/// Clears all buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support writing.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public void Flush (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (outputIndex == 0)
				return;

			try {
				var network = NetworkStream.Get (Stream);

				network?.Poll (SelectMode.SelectWrite, cancellationToken);
				Stream.Write (output, 0, outputIndex);
				Stream.Flush ();

				logger.LogClient (output, 0, outputIndex);
				outputIndex = 0;
			} catch (Exception ex) {
				OnWriteException (ex, cancellationToken);
				throw;
			}
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <remarks>
		/// Clears all buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </remarks>
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
			Flush (CancellationToken.None);
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <remarks>
		/// Clears all buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </remarks>
		/// <returns>A task that represents the asynchronous flush operation.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support writing.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override async Task FlushAsync (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (outputIndex == 0)
				return;

			try {
				await Stream.WriteAsync (output, 0, outputIndex, cancellationToken).ConfigureAwait (false);
				await Stream.FlushAsync (cancellationToken).ConfigureAwait (false);

				logger.LogClient (output, 0, outputIndex);
				outputIndex = 0;
			} catch (Exception ex) {
				OnWriteException (ex, cancellationToken);
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
		/// Releases the unmanaged resources used by the <see cref="Pop3Stream"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				IsConnected = false;
				Stream.Dispose ();
			}

			disposed = true;

			base.Dispose (disposing);
		}
	}
}
