//
// SmtpStream.cs
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
using System.Net.Security;
using System.Threading.Tasks;

using MimeKit.IO;

using Buffer = System.Buffer;
using NetworkStream = MailKit.Net.NetworkStream;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// A stream for communicating with an SMTP server.
	/// </summary>
	/// <remarks>
	/// A stream capable of reading SMTP server responses.
	/// </remarks>
	class SmtpStream : Stream, ICancellableStream
	{
		const int BlockSize = 4096;
		const int PadSize = 4;

		// I/O buffering
		readonly byte[] input = new byte[BlockSize + PadSize];
		readonly byte[] output = new byte[BlockSize];
		int outputIndex;

		readonly IProtocolLogger logger;
		int inputIndex, inputEnd;
		string lastResponse;
		bool disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpStream"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpStream"/>.
		/// </remarks>
		/// <param name="source">The underlying network stream.</param>
		/// <param name="protocolLogger">The protocol logger.</param>
		public SmtpStream (Stream source, IProtocolLogger protocolLogger)
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

		void AlignReadAheadBuffer (out int offset, out int count)
		{
			int left = inputEnd - inputIndex;

			if (left > 0) {
				if (inputIndex > 0) {
					// move all of the remaining input to the beginning of the buffer
					Buffer.BlockCopy (input, inputIndex, input, 0, left);
					inputEnd = left;
					inputIndex = 0;
				}
			} else {
				inputIndex = 0;
				inputEnd = 0;
			}

			count = BlockSize - inputEnd;
			offset = inputEnd;
		}

		int ReadAhead (CancellationToken cancellationToken)
		{
			AlignReadAheadBuffer (out int offset, out int count);

			try {
				var network = Stream as NetworkStream;

				cancellationToken.ThrowIfCancellationRequested ();

				network?.Poll (SelectMode.SelectRead, cancellationToken);
				int nread = Stream.Read (input, offset, count);

				if (nread > 0) {
					logger.LogServer (input, offset, nread);
					inputEnd += nread;

					// Optimization hack used by ReadResponse
					input[inputEnd] = (byte) '\n';
				} else if (lastResponse is not null) {
					throw new SmtpProtocolException ($"The SMTP server has unexpectedly disconnected: {lastResponse}");
				} else {
					throw new SmtpProtocolException ("The SMTP server has unexpectedly disconnected.");
				}
			} catch {
				IsConnected = false;
				throw;
			}

			return inputEnd - inputIndex;
		}

		async Task<int> ReadAheadAsync (CancellationToken cancellationToken)
		{
			AlignReadAheadBuffer (out int offset, out int count);

			try {
				var network = Stream as NetworkStream;

				cancellationToken.ThrowIfCancellationRequested ();

				int nread = await Stream.ReadAsync (input, offset, count, cancellationToken).ConfigureAwait (false);

				if (nread > 0) {
					logger.LogServer (input, offset, nread);
					inputEnd += nread;

					// Optimization hack used by ReadResponse
					input[inputEnd] = (byte) '\n';
				} else if (lastResponse is not null) {
					throw new SmtpProtocolException ($"The SMTP server has unexpectedly disconnected: {lastResponse}");
				} else {
					throw new SmtpProtocolException ("The SMTP server has unexpectedly disconnected.");
				}
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
				throw new ObjectDisposedException (nameof (SmtpStream));
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public int Read (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
#if false // Note: this code will never get called as we always use ReadResponse() instead.
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			int length = inputEnd - inputIndex;
			int n;

			if (length < count && length <= ReadAheadSize)
				await ReadAheadAsync (cancellationToken).ConfigureAwait (false);

			length = inputEnd - inputIndex;
			n = Math.Min (count, length);

			Buffer.BlockCopy (input, inputIndex, buffer, offset, n);
			inputIndex += n;

			return n;
#else
			throw new NotImplementedException ();
#endif
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override int Read (byte[] buffer, int offset, int count)
		{
			return Read (buffer, offset, count, CancellationToken.None);
		}

		/// <summary>
		/// Asynchronously reads a sequence of bytes from the stream and advances the position
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
#if false // Note: this code will never get called as we always use ReadResponse() instead.
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			int length = inputEnd - inputIndex;
			int n;

			if (length < count && length <= ReadAheadSize)
				await ReadAheadAsync (cancellationToken).ConfigureAwait (false);

			length = inputEnd - inputIndex;
			n = Math.Min (count, length);

			Buffer.BlockCopy (input, inputIndex, buffer, offset, n);
			inputIndex += n;

			return n;
#else
			throw new NotImplementedException ();
#endif
		}

		static bool TryParseStatusCode (byte[] text, int startIndex, out int code)
		{
			int endIndex = startIndex + 3;

			code = 0;

			for (int index = startIndex; index < endIndex; index++) {
				if (text[index] < (byte) '0' || text[index] > (byte) '9')
					return false;

				int digit = text[index] - (byte) '0';
				code = (code * 10) + digit;
			}

			return true;
		}

		static bool IsLegalAfterStatusCode (byte c)
		{
			return c == (byte) '-' || c == (byte) ' ' || c == (byte) '\r' || c == (byte) '\n';
		}

		bool ReadResponse (ByteArrayBuilder builder, ref bool newLine, ref bool more, ref int code)
		{
			do {
				int startIndex = inputIndex;

				if (newLine) {
					if (inputIndex + 3 < inputEnd) {
						if (!TryParseStatusCode (input, inputIndex, out int value))
							throw new SmtpProtocolException ("Unable to parse status code returned by the server.");

						inputIndex += 3;

						if (value < 100 || !IsLegalAfterStatusCode (input[inputIndex]))
							throw new SmtpProtocolException ("Invalid status code returned by the server.");

						if (code == 0) {
							code = value;
						} else if (value != code) {
							throw new SmtpProtocolException ("The status codes returned by the server did not match.");
						}

						newLine = false;

						more = input[inputIndex] == (byte) '-';
						if (more || input[inputIndex] == (byte) ' ')
							inputIndex++;

						startIndex = inputIndex;
					} else {
						// Need input.
						return true;
					}
				}

				// Note: This depends on ReadAhead[Async] setting input[inputEnd] = '\n'
				while (input[inputIndex] != (byte) '\n')
					inputIndex++;

				int endIndex = inputIndex;
				if (inputIndex > startIndex && input[inputIndex - 1] == (byte) '\r')
					endIndex--;

				builder.Append (input, startIndex, endIndex - startIndex);

				if (inputIndex < inputEnd && input[inputIndex] == (byte) '\n') {
					if (more)
						builder.Append ((byte) '\n');
					newLine = true;
					inputIndex++;
				}
			} while (more && inputIndex < inputEnd);

			return inputIndex == inputEnd;
		}

		/// <summary>
		/// Read an SMTP server response.
		/// </summary>
		/// <remarks>
		/// Reads a full command response from the SMTP server.
		/// </remarks>
		/// <returns>The response.</returns>
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
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public SmtpResponse ReadResponse (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			using (var builder = new ByteArrayBuilder (256)) {
				bool needInput = inputIndex == inputEnd;
				bool newLine = true;
				bool more = true;
				int code = 0;

				do {
					if (needInput)
						ReadAhead (cancellationToken);

					needInput = ReadResponse (builder, ref newLine, ref more, ref code);
				} while (more || !newLine);

				var message = builder.ToString ();

				lastResponse = message;

				return new SmtpResponse ((SmtpStatusCode) code, message);
			}
		}

		/// <summary>
		/// Asynchronously read an SMTP server response.
		/// </summary>
		/// <remarks>
		/// Reads a full command response from the SMTP server.
		/// </remarks>
		/// <returns>The response.</returns>
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
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public async Task<SmtpResponse> ReadResponseAsync (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			using (var builder = new ByteArrayBuilder (256)) {
				bool needInput = inputIndex == inputEnd;
				bool newLine = true;
				bool more = true;
				int code = 0;

				do {
					if (needInput)
						await ReadAheadAsync (cancellationToken).ConfigureAwait (false);

					needInput = ReadResponse (builder, ref newLine, ref more, ref code);
				} while (more || !newLine);

				var message = builder.ToString ();

				lastResponse = message;

				return new SmtpResponse ((SmtpStatusCode) code, message);
			}
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
		/// Queue a command to the SMTP server.
		/// </summary>
		/// <remarks>
		/// Queues a command to the SMTP server.
		/// </remarks>
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
		public void QueueCommand (string command, CancellationToken cancellationToken)
		{
			var encoder = Encoding.UTF8.GetEncoder ();
			int index = 0;

			while (!TryQueueCommand (encoder, command, ref index))
				Flush (cancellationToken);
		}

		/// <summary>
		/// Asynchronously queue a command to the SMTP server.
		/// </summary>
		/// <remarks>
		/// Asynchronously queues a command to the SMTP server.
		/// </remarks>
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
		public async Task QueueCommandAsync (string command, CancellationToken cancellationToken)
		{
			var encoder = Encoding.UTF8.GetEncoder ();
			int index = 0;

			while (!TryQueueCommand (encoder, command, ref index))
				await FlushAsync (cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Send a command to the SMTP server.
		/// </summary>
		/// <remarks>
		/// Sends a command to the SMTP server and reads the response.
		/// </remarks>
		/// <returns>The response.</returns>
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
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public SmtpResponse SendCommand (string command, CancellationToken cancellationToken)
		{
			QueueCommand (command, cancellationToken);
			Flush (cancellationToken);

			return ReadResponse (cancellationToken);
		}

		/// <summary>
		/// Asynchronously send a command to the SMTP server.
		/// </summary>
		/// <remarks>
		/// Asynchronously sends a command to the SMTP server and reads the response.
		/// </remarks>
		/// <returns>The response.</returns>
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
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public async Task<SmtpResponse> SendCommandAsync (string command, CancellationToken cancellationToken)
		{
			await QueueCommandAsync (command, cancellationToken).ConfigureAwait (false);
			await FlushAsync (cancellationToken).ConfigureAwait (false);

			return await ReadResponseAsync (cancellationToken).ConfigureAwait (false);
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
				IsConnected = false;
				if (ex is not OperationCanceledException)
					cancellationToken.ThrowIfCancellationRequested ();
				throw;
			}
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
		/// Asynchronously writes a sequence of bytes to the stream and advances the current
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
				IsConnected = false;
				if (ex is not OperationCanceledException)
					cancellationToken.ThrowIfCancellationRequested ();
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
				IsConnected = false;
				if (ex is not OperationCanceledException)
					cancellationToken.ThrowIfCancellationRequested ();
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
		/// Asynchronously clears all buffers for this stream and causes any buffered data to be written
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
				IsConnected = false;
				if (ex is not OperationCanceledException)
					cancellationToken.ThrowIfCancellationRequested ();
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
		/// Releases the unmanaged resources used by the <see cref="SmtpStream"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="SmtpStream"/> and
		/// optionally releases the managed resources.
		/// </remarks>
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
