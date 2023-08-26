//
// ImapStream.cs
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
using System.Threading;
using System.Net.Sockets;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit.IO;

using Buffer = System.Buffer;
using SslStream = MailKit.Net.SslStream;
using NetworkStream = MailKit.Net.NetworkStream;

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

	class ImapStream : Stream, ICancellableStream
	{
		public const string AtomSpecials    = "(){%*\\\"\n";
		public const string DefaultSpecials = "[]" + AtomSpecials;
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

		readonly Stack<ImapToken> tokens;
		readonly IProtocolLogger logger;
		int literalDataLeft;
		bool disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapStream"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ImapStream"/>.
		/// </remarks>
		/// <param name="source">The underlying network stream.</param>
		/// <param name="protocolLogger">The protocol logger.</param>
		public ImapStream (Stream source, IProtocolLogger protocolLogger)
		{
			tokens = new Stack<ImapToken> ();
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
			internal set { literalDataLeft = value; }
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

		bool AlignReadAheadBuffer (int atleast, out int left, out int start, out int end)
		{
			left = inputEnd - inputIndex;
			start = inputStart;
			end = inputEnd;

			if (left >= atleast)
				return false;

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

			return true;
		}

		int ReadAhead (int atleast, CancellationToken cancellationToken)
		{
			if (!AlignReadAheadBuffer (atleast, out int left, out int start, out int end))
				return left;

			try {
				var network = Stream as NetworkStream;
				int nread;

				cancellationToken.ThrowIfCancellationRequested ();

				network?.Poll (SelectMode.SelectRead, cancellationToken);

				if ((nread = Stream.Read (input, start, end - start)) > 0) {
					logger.LogServer (input, start, nread);
					inputEnd += nread;
				} else {
					throw new ImapProtocolException ("The IMAP server has unexpectedly disconnected.");
				}

				if (network == null)
					cancellationToken.ThrowIfCancellationRequested ();
			} catch {
				IsConnected = false;
				throw;
			}

			return inputEnd - inputIndex;
		}

		async ValueTask<int> ReadAheadAsync (int atleast, CancellationToken cancellationToken)
		{
			if (!AlignReadAheadBuffer (atleast, out int left, out int start, out int end))
				return left;

			try {
				int nread;

				cancellationToken.ThrowIfCancellationRequested ();

				if ((nread = await Stream.ReadAsync (input, start, end - start, cancellationToken).ConfigureAwait (false)) > 0) {
					logger.LogServer (input, start, nread);
					inputEnd += nread;
				} else {
					throw new ImapProtocolException ("The IMAP server has unexpectedly disconnected.");
				}

				if (Stream is not NetworkStream)
					cancellationToken.ThrowIfCancellationRequested ();
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
				throw new ObjectDisposedException (nameof (ImapStream));
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
		public int Read (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			if (Mode != ImapStreamMode.Literal)
				return 0;

			count = Math.Min (count, literalDataLeft);

			int length = inputEnd - inputIndex;
			int n;

			if (length < count && length <= ReadAheadSize)
				ReadAhead (BlockSize, cancellationToken);

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

			if (Mode != ImapStreamMode.Literal)
				return 0;

			count = Math.Min (count, literalDataLeft);

			int length = inputEnd - inputIndex;
			int n;

			if (length < count && length <= ReadAheadSize)
				await ReadAheadAsync (BlockSize, cancellationToken).ConfigureAwait (false);

			length = inputEnd - inputIndex;
			n = Math.Min (count, length);

			Buffer.BlockCopy (input, inputIndex, buffer, offset, n);
			literalDataLeft -= n;
			inputIndex += n;

			if (literalDataLeft == 0)
				Mode = ImapStreamMode.Token;

			return n;
		}

		static bool IsAtom (byte c, string specials)
		{
			return !IsCtrl (c) && !IsWhiteSpace (c) && specials.IndexOf ((char) c) == -1;
		}

		static bool IsCtrl (byte c)
		{
			return c <= 0x1f || c == 0x7f;
		}

		static bool IsWhiteSpace (byte c)
		{
			return c == (byte) ' ' || c == (byte) '\t' || c == (byte) '\r';
		}

		bool TryReadQuotedString (ByteArrayBuilder builder, ref bool escaped)
		{
			do {
				while (inputIndex < inputEnd) {
					if (input[inputIndex] == (byte) '"' && !escaped)
						break;

					if (input[inputIndex] == (byte) '\\' && !escaped) {
						escaped = true;
					} else {
						builder.Append (input[inputIndex]);
						escaped = false;
					}

					inputIndex++;
				}

				if (inputIndex + 1 < inputEnd) {
					// skip over closing '"'
					inputIndex++;

					// Note: Some IMAP servers do not properly escape double-quotes inside
					// of a qstring token and so, as an attempt at working around this
					// problem, check that the closing '"' character is not immediately
					// followed by any character that we would expect immediately following
					// a qstring token.
					//
					// See https://github.com/jstedfast/MailKit/issues/485 for details.
					if ("]) \r\n".IndexOf ((char) input[inputIndex]) != -1)
						return true;

					builder.Append ((byte) '"');
					continue;
				}

				return false;
			} while (true);
		}

		ImapToken ReadQuotedStringToken (CancellationToken cancellationToken)
		{
			bool escaped = false;

			// skip over the opening '"'
			inputIndex++;

			using (var builder = new ByteArrayBuilder (64)) {
				while (!TryReadQuotedString (builder, ref escaped))
					ReadAhead (2, cancellationToken);

				return ImapToken.Create (ImapTokenType.QString, builder);
			}
		}

		async ValueTask<ImapToken> ReadQuotedStringTokenAsync (CancellationToken cancellationToken)
		{
			bool escaped = false;

			// skip over the opening '"'
			inputIndex++;

			using (var builder = new ByteArrayBuilder (64)) {
				while (!TryReadQuotedString (builder, ref escaped))
					await ReadAheadAsync (2, cancellationToken).ConfigureAwait (false);

				return ImapToken.Create (ImapTokenType.QString, builder);
			}
		}

		bool TryReadAtomString (ImapTokenType type, ByteArrayBuilder builder, string specials)
		{
			input[inputEnd] = (byte) '\n';

			if (type == ImapTokenType.Flag && builder.Length == 1 && input[inputIndex] == (byte) '*') {
				// this is a special wildcard flag
				builder.Append (input[inputIndex++]);
			}

			while (IsAtom (input[inputIndex], specials))
				builder.Append (input[inputIndex++]);

			return inputIndex < inputEnd;
		}

		ImapToken ReadAtomString (ImapTokenType type, string specials, CancellationToken cancellationToken)
		{
			using (var builder = new ByteArrayBuilder (32)) {
				if (type == ImapTokenType.Flag)
					builder.Append ((byte) '\\');

				while (!TryReadAtomString (type, builder, specials))
					ReadAhead (1, cancellationToken);

				return ImapToken.Create (type, builder);
			}
		}

		async ValueTask<ImapToken> ReadAtomStringAsync (ImapTokenType type, string specials, CancellationToken cancellationToken)
		{
			using (var builder = new ByteArrayBuilder (32)) {
				if (type == ImapTokenType.Flag)
					builder.Append ((byte) '\\');

				while (!TryReadAtomString (type, builder, specials))
					await ReadAheadAsync (1, cancellationToken).ConfigureAwait (false);

				return ImapToken.Create (type, builder);
			}
		}

		ImapToken ReadAtomToken (string specials, CancellationToken cancellationToken)
		{
			return ReadAtomString (ImapTokenType.Atom, specials, cancellationToken);
		}

		ValueTask<ImapToken> ReadAtomTokenAsync (string specials, CancellationToken cancellationToken)
		{
			return ReadAtomStringAsync (ImapTokenType.Atom, specials, cancellationToken);
		}

		ImapToken ReadFlagToken (string specials, CancellationToken cancellationToken)
		{
			inputIndex++;

			return ReadAtomString (ImapTokenType.Flag, specials, cancellationToken);
		}

		ValueTask<ImapToken> ReadFlagTokenAsync (string specials, CancellationToken cancellationToken)
		{
			inputIndex++;

			return ReadAtomStringAsync (ImapTokenType.Flag, specials, cancellationToken);
		}

		bool TryReadLiteralTokenValue (ByteArrayBuilder builder)
		{
			input[inputEnd] = (byte) '}';

			while (input[inputIndex] != (byte) '}' && input[inputIndex] != '+')
				builder.Append (input[inputIndex++]);

			return inputIndex < inputEnd;
		}

		bool TryReadUntilCloseCurlyBrace (ByteArrayBuilder builder)
		{
			input[inputEnd] = (byte) '}';

			while (input[inputIndex] != (byte) '}')
				builder.Append (input[inputIndex++]);

			return inputIndex < inputEnd;
		}

		bool TrySkipUntilNewLine ()
		{
			input[inputEnd] = (byte) '\n';

			while (input[inputIndex] != (byte) '\n')
				inputIndex++;

			return inputIndex < inputEnd;
		}

		ImapToken ReadLiteralToken (CancellationToken cancellationToken)
		{
			using (var builder = new ByteArrayBuilder (16)) {
				// skip over the '{'
				builder.Append (input[inputIndex++]);

				while (!TryReadLiteralTokenValue (builder))
					ReadAhead (1, cancellationToken);

				int endIndex = builder.Length;

				if (input[inputIndex] == (byte) '+')
					builder.Append (input[inputIndex++]);

				// technically, we need "}\r\n", but in order to be more lenient, we'll accept "}\n"
				ReadAhead (2, cancellationToken);

				if (input[inputIndex] != (byte) '}') {
					// PROTOCOL ERROR... but maybe we can work around it?
					while (!TryReadUntilCloseCurlyBrace (builder))
						ReadAhead (1, cancellationToken);
				}

				// skip over the '}'
				builder.Append (input[inputIndex++]);

				// read until we get a new line...
				while (!TrySkipUntilNewLine ())
					ReadAhead (1, cancellationToken);

				// skip over the '\n'
				inputIndex++;

				if (!builder.TryParse (1, endIndex, out literalDataLeft))
					return ImapToken.CreateError (builder);

				Mode = ImapStreamMode.Literal;

				return ImapToken.Create (ImapTokenType.Literal, literalDataLeft);
			}
		}

		async ValueTask<ImapToken> ReadLiteralTokenAsync (CancellationToken cancellationToken)
		{
			using (var builder = new ByteArrayBuilder (16)) {
				// skip over the '{'
				builder.Append (input[inputIndex++]);

				while (!TryReadLiteralTokenValue (builder))
					await ReadAheadAsync (1, cancellationToken).ConfigureAwait (false);

				int endIndex = builder.Length;

				if (input[inputIndex] == (byte) '+')
					builder.Append (input[inputIndex++]);

				// technically, we need "}\r\n", but in order to be more lenient, we'll accept "}\n"
				await ReadAheadAsync (2, cancellationToken).ConfigureAwait (false);

				if (input[inputIndex] != (byte) '}') {
					// PROTOCOL ERROR... but maybe we can work around it?
					while (!TryReadUntilCloseCurlyBrace (builder))
						await ReadAheadAsync (1, cancellationToken).ConfigureAwait (false);
				}

				// skip over the '}'
				builder.Append (input[inputIndex++]);

				// read until we get a new line...
				while (!TrySkipUntilNewLine ())
					await ReadAheadAsync (1, cancellationToken).ConfigureAwait (false);

				// skip over the '\n'
				inputIndex++;

				if (!builder.TryParse (1, endIndex, out literalDataLeft) || literalDataLeft < 0)
					return ImapToken.CreateError (builder);

				Mode = ImapStreamMode.Literal;

				return ImapToken.Create (ImapTokenType.Literal, literalDataLeft);
			}
		}

		bool TrySkipWhiteSpace ()
		{
			input[inputEnd] = (byte) '\n';

			while (IsWhiteSpace (input[inputIndex]))
				inputIndex++;

			return inputIndex < inputEnd;
		}

		/// <summary>
		/// Reads the next available token from the stream.
		/// </summary>
		/// <returns>The token.</returns>
		/// <param name="specials">The special characters that are not allowed in an atom token.</param>
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
		public ImapToken ReadToken (string specials, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (tokens.Count > 0)
				return tokens.Pop ();

			// skip over white space between tokens...
			while (!TrySkipWhiteSpace ())
				ReadAhead (1, cancellationToken);

			char c = (char) input[inputIndex];

			if (c == '"')
				return ReadQuotedStringToken (cancellationToken);

			if (c == '{')
				return ReadLiteralToken (cancellationToken);

			if (c == '\\')
				return ReadFlagToken (specials, cancellationToken);

			if (c == '+') {
				inputIndex++;

				return ImapToken.Plus;
			}

			if (IsAtom (input[inputIndex], specials))
				return ReadAtomToken (specials, cancellationToken);

			// special character token
			inputIndex++;

			return ImapToken.Create ((ImapTokenType) c, c);
		}

		/// <summary>
		/// Asynchronously reads the next available token from the stream.
		/// </summary>
		/// <returns>The token.</returns>
		/// <param name="specials">The special characters that are not allowed in an atom token.</param>
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
		public async ValueTask<ImapToken> ReadTokenAsync (string specials, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (tokens.Count > 0)
				return tokens.Pop ();

			// skip over white space between tokens...
			while (!TrySkipWhiteSpace ())
				await ReadAheadAsync (1, cancellationToken).ConfigureAwait (false);

			char c = (char) input[inputIndex];

			if (c == '"')
				return await ReadQuotedStringTokenAsync (cancellationToken).ConfigureAwait (false);

			if (c == '{')
				return await ReadLiteralTokenAsync (cancellationToken).ConfigureAwait (false);

			if (c == '\\')
				return await ReadFlagTokenAsync (specials, cancellationToken).ConfigureAwait (false);

			if (c == '+') {
				inputIndex++;

				return ImapToken.Plus;
			}

			if (IsAtom (input[inputIndex], specials))
				return await ReadAtomTokenAsync (specials, cancellationToken).ConfigureAwait (false);

			// special character token
			inputIndex++;

			return ImapToken.Create ((ImapTokenType) c, c);
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
		public ImapToken ReadToken (CancellationToken cancellationToken)
		{
			return ReadToken (DefaultSpecials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously reads the next available token from the stream.
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
		public ValueTask<ImapToken> ReadTokenAsync (CancellationToken cancellationToken)
		{
			return ReadTokenAsync (DefaultSpecials, cancellationToken);
		}

		/// <summary>
		/// Ungets a token.
		/// </summary>
		/// <param name="token">The token.</param>
		public void UngetToken (ImapToken token)
		{
			if (token == null)
				throw new ArgumentNullException (nameof (token));

			tokens.Push (token);
		}

		unsafe bool TryReadLine (ByteArrayBuilder builder)
		{
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
					return false;
				}

				// consume the '\n'
				inputIndex++;
				count++;

				builder.Append (input, offset, count);

				return true;
			}
		}

		/// <summary>
		/// Reads a single line of input from the stream.
		/// </summary>
		/// <remarks>
		/// This method should be called in a loop until it returns <c>true</c>.
		/// </remarks>
		/// <returns><c>true</c>, if reading the line is complete, <c>false</c> otherwise.</returns>
		/// <param name="builder">The output buffer write the line data into.</param>
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
				ReadAhead (1, cancellationToken);

			return TryReadLine (builder);
		}

		/// <summary>
		/// Asynchronously reads a single line of input from the stream.
		/// </summary>
		/// <remarks>
		/// This method should be called in a loop until it returns <c>true</c>.
		/// </remarks>
		/// <returns><c>true</c>, if reading the line is complete, <c>false</c> otherwise.</returns>
		/// <param name="builder">The output buffer write the line data into.</param>
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
		internal async ValueTask<bool> ReadLineAsync (ByteArrayBuilder builder, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (inputIndex == inputEnd)
				await ReadAheadAsync (1, cancellationToken).ConfigureAwait (false);

			return TryReadLine (builder);
		}

		void AppendToOutputBuffer (byte[] buffer, ref int index, ref int left)
		{
			int n = Math.Min (BlockSize - outputIndex, left);

			if (outputIndex > 0 || n < BlockSize) {
				// append the data to the output buffer
				Buffer.BlockCopy (buffer, index, output, outputIndex, n);
				outputIndex += n;
				index += n;
				left -= n;
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
		public void Write (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			ValidateArguments (buffer, offset, count);

			try {
				var network = NetworkStream.Get (Stream);
				int index = offset;
				int left = count;

				while (left > 0) {
					AppendToOutputBuffer (buffer, ref index, ref left);

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
					AppendToOutputBuffer (buffer, ref index, ref left);

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
				IsConnected = false;
				if (ex is not OperationCanceledException)
					cancellationToken.ThrowIfCancellationRequested ();
				throw;
			}
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
		public override long Seek (long offset, SeekOrigin origin)
		{
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
		public override void SetLength (long value)
		{
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
