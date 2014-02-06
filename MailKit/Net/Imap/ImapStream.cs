//
// ImapStream.cs
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
using System.Text;
using System.Threading;
using System.Net.Sockets;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An enumeration of the possible IMAP streaming modes.
	/// </summary>
	/// <remarks>
	/// Normal operation is done in the <see cref="ImapStreamMode.Token"/> mode,
	/// but when reading literal string data, the
	/// <see cref="ImapStreamMode.Literal"/> mode should be used.
	/// </remarks>
	enum ImapStreamMode {
		/// <summary>
		/// Reads 1 token at a time.
		/// </summary>
		Token,

		/// <summary>
		/// Reads literal string data.
		/// </summary>
		Literal
	}

	class ImapStream : Stream
	{
		const int ReadAheadSize = 128;
		const int BlockSize = 4096;
		const int PadSize = 4;

		// I/O buffering
		readonly byte[] input = new byte[ReadAheadSize + BlockSize + PadSize];
		const int inputStart = ReadAheadSize;
		int inputIndex = ReadAheadSize;
		int inputEnd = ReadAheadSize;

		readonly byte[] output = new byte[BlockSize];
		int outputIndex;

		readonly IProtocolLogger logger;
		int literalDataLeft;
		ImapToken nextToken;
		bool disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapStream"/> class.
		/// </summary>
		/// <param name="source">The underlying network stream.</param>
		/// <param name="protocolLogger">The protocol logger.</param>
		public ImapStream (Stream source, IProtocolLogger protocolLogger)
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
		public ImapStreamMode Mode {
			get; set;
		}

		/// <summary>
		/// Gets the length of the literal.
		/// </summary>
		/// <value>The length of the literal.</value>
		public int LiteralLength {
			get { return literalDataLeft; }
		}

		/// <summary>
		/// Gets whether or not the stream is connected.
		/// </summary>
		/// <value><c>true</c> if the stream is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
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

		unsafe int ReadAhead (byte* inbuf, int atleast)
		{
			int left = inputEnd - inputIndex;

			if (left >= atleast)
				return left;

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
		/// The stream is in token mode (see <see cref="ImapStreamMode.Token"/>).
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public override int Read (byte[] buffer, int offset, int count)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (Mode != ImapStreamMode.Literal)
				return 0;

			count = Math.Min (count, literalDataLeft);

			int length = inputEnd - inputIndex;
			int n;

			if (length < count && length <= ReadAheadSize) {
				unsafe {
					fixed (byte* inbuf = input) {
						ReadAhead (inbuf, BlockSize);
					}
				}
			}

			length = inputEnd - inputIndex;
			n = Math.Min (count, length);

			Buffer.BlockCopy (input, inputIndex, buffer, offset, n);
			literalDataLeft -= n;
			inputIndex += n;

			if (literalDataLeft == 0)
				Mode = ImapStreamMode.Token;

			return n;
		}

		static bool IsAtom (byte c)
		{
			return !IsCtrl (c) && !IsWhiteSpace (c) && "()[]*%\\\"\n".IndexOf ((char) c) == -1;
		}

		static bool IsCtrl (byte c)
		{
			return c <= 0x1f || c >= 0x7f;
		}

		static bool IsWhiteSpace (byte c)
		{
			return c == ' ' || c == (byte) '\t' || c == (byte) '\r';
		}

		unsafe ImapToken ReadQuotedStringToken (byte* inbuf, CancellationToken cancellationToken)
		{
			byte* inptr = inbuf + inputIndex;
			byte* inend = inbuf + inputEnd;
			bool escaped = false;

			// skip over the leading '"'
			inptr++;

			using (var memory = new MemoryStream ()) {
				do {
					while (inptr < inend) {
						if (*inptr == (byte) '"' && !escaped)
							break;

						if (*inptr == (byte) '\\' && !escaped) {
							escaped = true;
						} else {
							memory.WriteByte (*inptr);
							escaped = false;
						}

						inptr++;
					}

					if (inptr < inend) {
						inptr++;
						break;
					}

					inputIndex = (int) (inptr - inbuf);

					cancellationToken.ThrowIfCancellationRequested ();
					ReadAhead (inbuf, 1);

					inptr = inbuf + inputIndex;
					inend = inbuf + inputEnd;
				} while (true);

				inputIndex = (int) (inptr - inbuf);

				var buffer = memory.GetBuffer ();
				int length = (int) memory.Length;

				return new ImapToken (ImapTokenType.QString, Encoding.UTF8.GetString (buffer, 0, length));
			}
		}

		unsafe string ReadAtomString (byte* inbuf, bool flag, CancellationToken cancellationToken)
		{
			var builder = new StringBuilder ();
			byte* inptr = inbuf + inputIndex;
			byte* inend = inbuf + inputEnd;

			do {
				*inend = (byte) '\n';

				if (flag && builder.Length == 0 && *inptr == (byte) '*') {
					// this is a special wildcard flag
					inputIndex++;
					return "*";
				}

				while (IsAtom (*inptr))
					builder.Append ((char) *inptr++);

				if (inptr < inend)
					break;

				inputIndex = (int) (inptr - inbuf);

				cancellationToken.ThrowIfCancellationRequested ();
				ReadAhead (inbuf, 1);

				inptr = inbuf + inputIndex;
				inend = inbuf + inputEnd;
			} while (true);

			inputIndex = (int) (inptr - inbuf);

			return builder.ToString ();
		}

		unsafe ImapToken ReadAtomToken (byte* inbuf, CancellationToken cancellationToken)
		{
			var atom = ReadAtomString (inbuf, false, cancellationToken);

			return atom == "NIL" ? new ImapToken (ImapTokenType.Nil, atom) : new ImapToken (ImapTokenType.Atom, atom);
		}

		unsafe ImapToken ReadFlagToken (byte* inbuf, CancellationToken cancellationToken)
		{
			inputIndex++;

			var flag = "\\" + ReadAtomString (inbuf, true, cancellationToken);

			return new ImapToken (ImapTokenType.Flag, flag);
		}

		unsafe ImapToken ReadLiteralToken (byte* inbuf, CancellationToken cancellationToken)
		{
			var builder = new StringBuilder ();
			byte* inptr = inbuf + inputIndex;
			byte* inend = inbuf + inputEnd;

			// skip over the '{'
			inptr++;

			do {
				*inend = (byte) '}';

				while (*inptr != (byte) '}' && *inptr != '+')
					builder.Append ((char) *inptr++);

				if (inptr < inend)
					break;

				inputIndex = (int) (inptr - inbuf);

				cancellationToken.ThrowIfCancellationRequested ();
				ReadAhead (inbuf, 1);

				inptr = inbuf + inputIndex;
				inend = inbuf + inputEnd;
			} while (true);

			if (*inptr == (byte) '+')
				inptr++;

			// technically, we need "}\r\n", but in order to be more lenient, we'll accept "}\n"
			inputIndex = (int) (inptr - inbuf);

			cancellationToken.ThrowIfCancellationRequested ();
			ReadAhead (inbuf, 2);

			inptr = inbuf + inputIndex;
			inend = inbuf + inputEnd;

			if (*inptr != (byte) '}') {
				// PROTOCOL ERROR... but maybe we can work around it?
				do {
					*inend = (byte) '}';

					while (*inptr != (byte) '}')
						inptr++;

					if (inptr < inend)
						break;

					inputIndex = (int) (inptr - inbuf);

					cancellationToken.ThrowIfCancellationRequested ();
					ReadAhead (inbuf, 1);

					inptr = inbuf + inputIndex;
					inend = inbuf + inputEnd;
				} while (true);
			}

			// skip over the '}'
			inptr++;

			// read until we get a new line...
			do {
				*inend = (byte) '\n';

				while (*inptr != (byte) '\n')
					inptr++;

				if (inptr < inend)
					break;

				inputIndex = (int) (inptr - inbuf);

				cancellationToken.ThrowIfCancellationRequested ();
				ReadAhead (inbuf, 1);

				inptr = inbuf + inputIndex;
				inend = inbuf + inputEnd;
				*inptr = (byte) '\n';
			} while (true);

			// skip over the '\n'
			inptr++;

			inputIndex = (int) (inptr - inbuf);

			if (!int.TryParse (builder.ToString (), out literalDataLeft) || literalDataLeft < 0)
				return new ImapToken (ImapTokenType.Error, builder.ToString ());

			Mode = ImapStreamMode.Literal;

			return new ImapToken (ImapTokenType.Literal, literalDataLeft);
		}

		/// <summary>
		/// Reads the next available token from the stream.
		/// </summary>
		/// <returns>The token.</returns>
		/// <exception cref="System.ObjectDisposedException">
		/// The stream has been disposed.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public ImapToken ReadToken (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (nextToken != null) {
				var token = nextToken;
				nextToken = null;
				return token;
			}

			unsafe {
				fixed (byte* inbuf = input) {
					byte* inptr = inbuf + inputIndex;
					byte* inend = inbuf + inputEnd;

					*inend = (byte) '\n';

					// skip over white space between tokens...
					do {
						while (IsWhiteSpace (*inptr))
							inptr++;

						if (inptr < inend)
							break;

						inputIndex = (int) (inptr - inbuf);

						cancellationToken.ThrowIfCancellationRequested ();
						ReadAhead (inbuf, 1);

						inptr = inbuf + inputIndex;
						inend = inbuf + inputEnd;

						*inend = (byte) '\n';
					} while (true);

					inputIndex = (int) (inptr - inbuf);
					char c = (char) *inptr;

					if (c == '"')
						return ReadQuotedStringToken (inbuf, cancellationToken);

					if (c == '{')
						return ReadLiteralToken (inbuf, cancellationToken);

					if (c == '\\')
						return ReadFlagToken (inbuf, cancellationToken);

					if (c != '+' && IsAtom (*inptr))
						return ReadAtomToken (inbuf, cancellationToken);

					// special character token
					inputIndex++;

					return new ImapToken ((ImapTokenType) c, c);
				}
			}
		}

		/// <summary>
		/// Ungets a token.
		/// </summary>
		/// <param name="token">The token.</param>
		public void UngetToken (ImapToken token)
		{
			if (token == null)
				throw new ArgumentNullException ("token");

			nextToken = token;
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

					// we need at least 1 byte: "\n"
					ReadAhead (inbuf, 1);

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

					if (inptr == inend)
						return false;

					// consume the '\n'
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
						Stream.Write (output, 0, BlockSize);
						logger.LogClient (output, 0, BlockSize);
						outputIndex = 0;
					}

					if (outputIndex == 0) {
						// write blocks of data to the stream without buffering
						while (left >= BlockSize) {
							Stream.Write (buffer, index, BlockSize);
							logger.LogClient (buffer, index, BlockSize);
							index += BlockSize;
							left -= BlockSize;
						}
					}
				}
			} catch (IOException) {
				IsConnected = false;
				throw;
			}
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

			if (outputIndex == 0)
				return;

			try {
				Stream.Write (output, 0, outputIndex);
				logger.LogClient (output, 0, outputIndex);
				outputIndex = 0;
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
