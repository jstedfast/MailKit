//
// ImapStream.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Buffer = System.Buffer;

#if NETFX_CORE
using Windows.Storage.Streams;
using Windows.Networking.Sockets;
using Socket = Windows.Networking.Sockets.StreamSocket;
#else
using System.Net.Security;
using System.Net.Sockets;
#endif

using MimeKit.IO;

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

	class ImapStream : Stream {
		// Note: GMail's IMAP implementation is broken and does not quote strings with ']' like it should.
		public const string GMailLabelSpecials = "(){%*\\\"\n";
		public const string StringSpecials = "()]{%*\\\"\n";
		public const string AtomSpecials = "()[]{%*\\\"\n";
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
		/// <remarks>
		/// Creates a new <see cref="ImapStream"/>.
		/// </remarks>
		/// <param name="source">The underlying network stream.</param>
		/// <param name="socket">The underlying network socket.</param>
		/// <param name="protocolLogger">The protocol logger.</param>
		public ImapStream (Stream source, Socket socket, IProtocolLogger protocolLogger) {
			logger = protocolLogger;
			IsConnected = true;
			Stream = source;
			Socket = socket;
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
		/// Get the underlying network socket.
		/// </summary>
		/// <remarks>
		/// Gets the underlying network socket.
		/// </remarks>
		/// <value>The underlying network socket.</value>
		public Socket Socket {
			get; private set;
		}

		/// <summary>
		/// Get or sets the mode used for reading.
		/// </summary>
		/// <remarks>
		/// Gets or sets the mode used for reading.
		/// </remarks>
		/// <value>The mode.</value>
		public ImapStreamMode Mode {
			get; set;
		}

		/// <summary>
		/// Get the length of the literal.
		/// </summary>
		/// <remarks>
		/// Gets the length of the literal.
		/// </remarks>
		/// <value>The length of the literal.</value>
		public int LiteralLength {
			get { return literalDataLeft; }
		}

		/// <summary>
		/// Get whether or not the stream is connected.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the stream is connected.
		/// </remarks>
		/// <value><c>true</c> if the stream is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get; internal set;
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
			set { Stream.Position = value; }
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

		void Poll (SelectMode mode, CancellationToken cancellationToken) {
#if NETFX_CORE
			cancellationToken.ThrowIfCancellationRequested ();
#else
			if (!cancellationToken.CanBeCanceled)
				return;

			if (Socket != null) {
				do {
					cancellationToken.ThrowIfCancellationRequested ();
					// wait 1/4 second and then re-check for cancellation
				} while (!Socket.Poll (250000, mode));
			} else {
				cancellationToken.ThrowIfCancellationRequested ();
			}
#endif
		}

		async Task<int> ReadAhead (int atleast, CancellationToken cancellationToken) {
			int left = inputEnd - inputIndex;

			if (left >= atleast)
				return left;

			int start = inputStart;
			int end = inputEnd;
			int nread;

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

			try {
#if !NETFX_CORE
				bool buffered = Stream is SslStream;
#else
				bool buffered = true;
#endif

				if (buffered) {
					cancellationToken.ThrowIfCancellationRequested ();

					nread = await Stream.ReadAsync (input, start, end - start, cancellationToken);
				} else {
					Poll (SelectMode.SelectRead, cancellationToken);

					nread = await Stream.ReadAsync (input, start, end - start, cancellationToken);
				}

				if (nread > 0) {
					logger.LogServer (input, start, nread);
					inputEnd += nread;
				} else {
					throw new ImapProtocolException ("The IMAP server has unexpectedly disconnected.");
				}

				if (buffered)
					cancellationToken.ThrowIfCancellationRequested ();
			} catch {
				IsConnected = false;
				throw;
			}

			return inputEnd - inputIndex;
		}

		static void ValidateArguments (byte[] buffer, int offset, int count) {
			if (buffer == null)
				throw new ArgumentNullException ("buffer");

			if (offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0 || count > (buffer.Length - offset))
				throw new ArgumentOutOfRangeException ("count");
		}

		void CheckDisposed () {
			if (disposed)
				throw new ObjectDisposedException ("ImapStream");
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
		/// <exception cref="System.InvalidOperationException">
		/// The stream is in token mode (see <see cref="ImapStreamMode.Token"/>).
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		public async Task<int> Read (byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (Mode != ImapStreamMode.Literal)
				return 0;

			count = Math.Min (count, literalDataLeft);

			int length = inputEnd - inputIndex;
			int n;

			if (length < count && length <= ReadAheadSize)
				await ReadAhead (BlockSize, cancellationToken);

			length = inputEnd - inputIndex;
			n = Math.Min (count, length);

			Buffer.BlockCopy (input, inputIndex, buffer, offset, n);
			literalDataLeft -= n;
			inputIndex += n;

			if (literalDataLeft == 0)
				Mode = ImapStreamMode.Token;

			return n;
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
		    throw new Exception("Use ReadAsync.");
//			return Read (buffer, offset, count, CancellationToken.None);
		}

		public override Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			return Read (buffer, offset, count, cancellationToken);
		}

		static bool IsAtom (byte c, string specials) {
			return !IsCtrl (c) && !IsWhiteSpace (c) && specials.IndexOf ((char)c) == -1;
		}

		static bool IsCtrl (byte c) {
			return c <= 0x1f || c >= 0x7f;
		}

		static bool IsWhiteSpace (byte c) {
			return c == (byte)' ' || c == (byte)'\t' || c == (byte)'\r';
		}

	    async Task<ImapToken> ReadQuotedStringToken (CancellationToken cancellationToken) {

            bool escaped = false;

            unsafe
            {
                _inptr = _inbuf + inputIndex;
                _inend = _inbuf + inputEnd;

                // skip over the leading '"'
                _inptr++;
            }

			using (var memory = new MemoryStream ()) {
				do {
				    unsafe
				    {
                        while (_inptr < _inend)
                        {
                            if (*_inptr == (byte)'"' && !escaped)
                                break;

                            if (*_inptr == (byte)'\\' && !escaped)
                            {
                                escaped = true;
                            }
                            else
                            {
                                memory.WriteByte(*_inptr);
                                escaped = false;
                            }

                            _inptr++;
                        }

                        if (_inptr < _inend)
                        {
                            _inptr++;
                            break;
                        }

                        inputIndex = (int)(_inptr - _inbuf);
                    }

					await ReadAhead (1, cancellationToken);

				    unsafe
				    {
                        _inptr = _inbuf + inputIndex;
                        _inend = _inbuf + inputEnd;
                    }
				} while (true);

			    unsafe
			    {
                    inputIndex = (int)(_inptr - _inbuf);
                }

#if !NETFX_CORE
				var buffer = memory.GetBuffer ();
#else
				var buffer = memory.ToArray ();
#endif
				int length = (int)memory.Length;

				return new ImapToken (ImapTokenType.QString, Encoding.UTF8.GetString (buffer, 0, length));
			}
		}

	    async Task<string> ReadAtomString (bool flag, string specials, CancellationToken cancellationToken) {
			var builder = new StringBuilder ();

		    unsafe
		    {
                _inptr = _inbuf + inputIndex;
                _inend = _inbuf + inputEnd;
            }

			do {
			    unsafe
			    {
                    *_inend = (byte)'\n';

                    if (flag && builder.Length == 0 && *_inptr == (byte)'*')
                    {
                        // this is a special wildcard flag
                        inputIndex++;
                        return "*";
                    }

                    while (IsAtom(*_inptr, specials))
                        builder.Append((char)*_inptr++);

                    if (_inptr < _inend)
                        break;

                    inputIndex = (int)(_inptr - _inbuf);
                }

				await ReadAhead (1, cancellationToken);

			    unsafe
			    {
                    _inptr = _inbuf + inputIndex;
                    _inend = _inbuf + inputEnd;
                }
			} while (true);

		    unsafe
		    {
                inputIndex = (int)(_inptr - _inbuf);
            }

			return builder.ToString ();
		}

	    async Task<ImapToken> ReadAtomToken (string specials, CancellationToken cancellationToken) {
			var atom = await ReadAtomString (false, specials, cancellationToken);

			return atom == "NIL" ? new ImapToken (ImapTokenType.Nil, atom) : new ImapToken (ImapTokenType.Atom, atom);
		}

	    async Task<ImapToken> ReadFlagToken (string specials, CancellationToken cancellationToken) {
			inputIndex++;

			var flag = "\\" + await ReadAtomString (true, specials, cancellationToken);

			return new ImapToken (ImapTokenType.Flag, flag);
		}

		async Task<ImapToken> ReadLiteralToken (CancellationToken cancellationToken) {
			var builder = new StringBuilder ();

		    unsafe
		    {
                _inptr = _inbuf + inputIndex;
                _inend = _inbuf + inputEnd;

                // skip over the '{'
                _inptr++;
            }

			do {
			    unsafe
			    {
                    *_inend = (byte)'}';

                    while (*_inptr != (byte)'}' && *_inptr != '+')
                        builder.Append((char)*_inptr++);

                    if (_inptr < _inend)
                        break;

                    inputIndex = (int)(_inptr - _inbuf);
                }

				await ReadAhead (1, cancellationToken);

			    unsafe
			    {
                    _inptr = _inbuf + inputIndex;
                    _inend = _inbuf + inputEnd;
                }
			} while (true);

		    unsafe
		    {
                if (*_inptr == (byte)'+')
                    _inptr++;

                // technically, we need "}\r\n", but in order to be more lenient, we'll accept "}\n"
                inputIndex = (int)(_inptr - _inbuf);
            }

			await ReadAhead (2, cancellationToken);

            bool isEnd;

		    unsafe
		    {
                _inptr = _inbuf + inputIndex;
                _inend = _inbuf + inputEnd;

                isEnd = *_inptr != (byte)'}';
            }

		    if (isEnd) {
				// PROTOCOL ERROR... but maybe we can work around it?
				do {
				    unsafe
				    {
                        *_inend = (byte)'}';

                        while (*_inptr != (byte)'}')
                            _inptr++;

                        if (_inptr < _inend)
                            break;

                        inputIndex = (int)(_inptr - _inbuf);
                    }

					await ReadAhead (1, cancellationToken);

				    unsafe
				    {
                        _inptr = _inbuf + inputIndex;
                        _inend = _inbuf + inputEnd;
                    }
				} while (true);
			}

		    unsafe
		    {
                // skip over the '}'
                _inptr++;
            }

			// read until we get a new line...
			do {
			    unsafe
			    {
                    *_inend = (byte)'\n';

                    while (*_inptr != (byte)'\n')
                        _inptr++;

                    if (_inptr < _inend)
                        break;

                    inputIndex = (int)(_inptr - _inbuf);
                }

				await ReadAhead (1, cancellationToken);

			    unsafe
			    {
                    _inptr = _inbuf + inputIndex;
                    _inend = _inbuf + inputEnd;
                    *_inptr = (byte)'\n';
                }
			} while (true);

		    unsafe
		    {
                // skip over the '\n'
                _inptr++;

                inputIndex = (int)(_inptr - _inbuf);
            }

			if (!int.TryParse (builder.ToString (), out literalDataLeft) || literalDataLeft < 0)
				return new ImapToken (ImapTokenType.Error, builder.ToString ());

			Mode = ImapStreamMode.Literal;

			return new ImapToken (ImapTokenType.Literal, literalDataLeft);
		}

        private unsafe byte* _inbuf;
        private GCHandle _inbufHandle;

        private unsafe byte* _inptr;
        private unsafe byte* _inend;

        /// <summary>
        /// Reads the next available token from the stream.
        /// </summary>
        /// <returns>The token.</returns>
        /// <param name="specials">A list of characters that are not legal in bare string tokens.</param>
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
        public async Task<ImapToken> ReadToken (string specials, CancellationToken cancellationToken) {
			CheckDisposed ();

			if (nextToken != null) {
				var token = nextToken;
				nextToken = null;
				return token;
			}

            unsafe
            {
                _inbufHandle = GCHandle.Alloc(input, GCHandleType.Pinned);
                _inbuf = (byte*) _inbufHandle.AddrOfPinnedObject().ToPointer();
            }

            try
			{
			    unsafe
			    {
                    _inptr = _inbuf + inputIndex;
                    _inend = _inbuf + inputEnd;

                    *_inend = (byte)'\n';
                }

			    // skip over white space between tokens...
			    do
			    {
			        unsafe
			        {
                        while (IsWhiteSpace(*_inptr))
                            _inptr++;

                        if (_inptr < _inend)
                            break;

                        inputIndex = (int)(_inptr - _inbuf);
                    }

			        await ReadAhead(1, cancellationToken);

			        unsafe
			        {
                        _inptr = _inbuf + inputIndex;
                        _inend = _inbuf + inputEnd;

                        *_inend = (byte)'\n';
                    }
			    } while (true);

			    char c;
			    unsafe
			    {
			        inputIndex = (int) (_inptr - _inbuf);
			        c = (char) *_inptr;
			    }

			    if (c == '"')
                    return await ReadQuotedStringToken(cancellationToken);

                if (c == '{')
                    return await ReadLiteralToken(cancellationToken);

                if (c == '\\')
                    return await ReadFlagToken(specials, cancellationToken);

                bool isAtom;
			    unsafe
			    {
			        isAtom = IsAtom(*_inptr, specials);
			    }

                if (isAtom)
                    return await ReadAtomToken(specials, cancellationToken);

                // special character token
                inputIndex++;

                return new ImapToken((ImapTokenType)c, c);
			}
			finally
			{
                _inbufHandle.Free();
            }
		}

		/// <summary>
		/// Reads the next available token from the stream.
		/// </summary>
		/// <returns>The token.</returns>
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
		public async Task<ImapToken> ReadToken (CancellationToken cancellationToken) {
			return await ReadToken (AtomSpecials, cancellationToken);
		}

		/// <summary>
		/// Ungets a token.
		/// </summary>
		/// <param name="token">The token.</param>
		public void UngetToken (ImapToken token) {
			if (token == null)
				throw new ArgumentNullException (nameof (token));

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
		internal Task<bool> ReadLine (out byte[] buffer, out int offset, out int count, CancellationToken cancellationToken) {
			CheckDisposed ();

            // we need at least 1 byte: "\n"
            await ReadAhead(1, cancellationToken);

            unsafe
			{
				offset = inputIndex;
				buffer = input;

				start = inbuf + inputIndex;
				inend = inbuf + inputEnd;
				*inend = (byte)'\n';
				inptr = start;

				// FIXME: use SIMD to optimize this
				while (*inptr != (byte)'\n')
					inptr++;

				inputIndex = (int)(inptr - inbuf);
				count = (int)(inptr - start);

				if (inptr == inend)
					return false;

				// consume the '\n'
				inputIndex++;
				count++;

				return true;
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
		public async Task Write (byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
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
						Poll (SelectMode.SelectWrite, cancellationToken);
						await Stream.WriteAsync (output, 0, BlockSize, cancellationToken);
						logger.LogClient (output, 0, BlockSize);
						outputIndex = 0;
					}

					if (outputIndex == 0) {
						// write blocks of data to the stream without buffering
						while (left >= BlockSize) {
							Poll (SelectMode.SelectWrite, cancellationToken);
							await Stream.WriteAsync (buffer, index, BlockSize, cancellationToken);
							logger.LogClient (buffer, index, BlockSize);
							index += BlockSize;
							left -= BlockSize;
						}
					}
				}
			} catch {
				IsConnected = false;
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
		public override void Write (byte[] buffer, int offset, int count) {
			Write (buffer, offset, count, CancellationToken.None).Wait ();
		}

		public override async Task WriteAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
			await Write (buffer, offset, count, cancellationToken);
		}

		/// <summary>
		/// Clears all output buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <remarks>
		/// Clears all output buffers for this stream and causes any buffered data to be written
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
		public async Task Flush (CancellationToken cancellationToken) {
			CheckDisposed ();

			if (outputIndex == 0)
				return;

			try {
				Poll (SelectMode.SelectWrite, cancellationToken);
				await Stream.WriteAsync (output, 0, outputIndex, cancellationToken);
				await Stream.FlushAsync (cancellationToken);
				logger.LogClient (output, 0, outputIndex);
				outputIndex = 0;
			} catch {
				IsConnected = false;
				throw;
			}
		}

		/// <summary>
		/// Clears all output buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <remarks>
		/// Clears all output buffers for this stream and causes any buffered data to be written
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
		public override void Flush () {
			Flush (CancellationToken.None).Wait ();
		}

		/// <summary>
		/// Clears all output buffers for this stream and causes any buffered data to be written
		/// to the underlying device.
		/// </summary>
		/// <remarks>
		/// Clears all output buffers for this stream and causes any buffered data to be written
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
		public override async Task FlushAsync (CancellationToken cancellationToken) {
			await Flush (cancellationToken);
		}

		/// <summary>
		/// Sets the position within the current stream.
		/// </summary>
		/// <remarks>
		/// It is not possible to seek within a <see cref="ImapStream"/>.
		/// </remarks>
		/// <returns>The new position within the stream.</returns>
		/// <param name="offset">The offset into the stream relative to the <paramref name="origin"/>.</param>
		/// <param name="origin">The origin to seek from.</param>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support seeking.
		/// </exception>
		public override long Seek (long offset, SeekOrigin origin) {
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Sets the length of the stream.
		/// </summary>
		/// <remarks>
		/// It is not possible to set the length of a <see cref="ImapStream"/>.
		/// </remarks>
		/// <param name="value">The desired length of the stream in bytes.</param>
		/// <exception cref="System.NotSupportedException">
		/// The stream does not support setting the length.
		/// </exception>
		public override void SetLength (long value) {
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ImapStream"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="ImapStream"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected override void Dispose (bool disposing) {
			if (disposing && !disposed) {
				IsConnected = false;
				Stream.Dispose ();
				disposed = true;
			}

			base.Dispose (disposing);
		}
	}
}
