//
// Pop3Stream.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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

namespace MailKit.Net.Pop3 {
	public enum Pop3StreamMode {
		Line,
		Data
	}

	public class Pop3Stream : Stream
	{
		const int ReadAheadSize = 128;
		const int BlockSize = 4096;
		const int PadSize = 4;

		// I/O buffering
		readonly byte[] input = new byte[ReadAheadSize + BlockSize + PadSize];
		const int inputStart = ReadAheadSize;
		int inputIndex = ReadAheadSize;
		int inputEnd = ReadAheadSize;
		bool midline;

		public Pop3Stream (Stream source)
		{
			IsConnected = true;
			Stream = source;
		}

		public Stream Stream {
			get; set;
		}

		public Pop3StreamMode Mode {
			get; set;
		}

		public bool IsConnected {
			get; private set;
		}

		public bool IsEndOfData {
			get; set;
		}

		public override bool CanRead {
			get { return Stream.CanRead; }
		}

		public override bool CanWrite {
			get { return Stream.CanWrite; }
		}

		public override bool CanSeek {
			get { return Stream.CanSeek; }
		}

		public override bool CanTimeout {
			get { return Stream.CanTimeout; }
		}

		public override int ReadTimeout {
			get { return Stream.ReadTimeout; }
			set { Stream.ReadTimeout = value; }
		}

		public override int WriteTimeout {
			get { return Stream.WriteTimeout; }
			set { Stream.WriteTimeout = value; }
		}

		public override long Position {
			get { return Stream.Position; }
			set { Stream.Position = value; }
		}

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

			int index = inputIndex;
			int start = inputStart;
			int end = inputEnd;
			int nread;

			// attempt to align the end of the remaining input with ReadAheadSize
			if (index >= start) {
				start -= left < ReadAheadSize ? left : ReadAheadSize;
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
				if ((nread = Stream.Read (input, start, end - start)) > 0)
					inputEnd += nread;
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

		public override int Read (byte[] buffer, int offset, int count)
		{
			ValidateArguments (buffer, offset, count);

			unsafe {
				fixed (byte* inbuf = input, outbuf = buffer) {
					int left = inputEnd - inputIndex;
					byte* outptr = outbuf + offset;
					byte* inptr, inend, outend;
					byte c = 0;
					int max;

					if (Mode == Pop3StreamMode.Line) {
						if (!midline && left < 3 /* ".\r\n" */)
							left = ReadAhead (inbuf, 3);

						max = Math.Min (count, left);
						inptr = inbuf + inputIndex;
						inend = inptr + max;

						while (inptr < inend && c != (byte) '\n')
							c = *outptr++ = *inptr++;

						// keep midline state
						midline = c != (byte) '\n';

						inputIndex = (int) (inptr - inbuf);

						return (int) (outptr - outbuf) - offset;
					}

					if (IsEndOfData)
						return 0;

					if (left < ReadAheadSize)
						left = ReadAhead (inbuf, ReadAheadSize);

					inptr = inbuf + inputIndex;
					inend = inbuf + inputEnd;
					*inend = (byte) '\r';

					outend = outptr + count;

					do {
						// read until end-of-line
						while (midline && outptr < outend) {
							c = 0;

							while (outptr < outend && c != (byte) '\r' && c != (byte) '\n')
								c = *outptr++ = *inptr++;

							if (outptr == outend) {
								// we're done... and we're still in the middle of a line
								inputIndex = (int) (inptr - inbuf);

								return (int) (outptr - outbuf) - offset;
							}

							// convert CRLF to LF
							if (*inptr == (byte) '\r') {
								if (inptr + 1 >= inend) {
									// not enough buffered data to finish the line...
									inputIndex = (int) (inptr - inbuf);

									return (int) (outptr - outbuf) - offset;
								}

								if (*(inptr + 1) == (byte) '\n') {
									*outptr++ = (byte) '\n';
									midline = false;
									inptr += 2;
								} else {
									// '\r' in the middle of a line? odd...
									*outptr++ = *inptr++;
								}
							} else {
								*outptr++ = *inptr++;
								midline = false;
							}
						}

						if (inptr == inend) {
							// out of buffered data
							inputIndex = (int) (inptr - inbuf);
							midline = true;

							return (int) (outptr - outbuf) - offset;
						}

						if (*inptr != (byte) '.') {
							// no special processing required...
							midline = true;
							continue;
						}

						// special processing is required for lines beginning with '.'

						if ((inptr + 1) == inend) {
							// stop here. we don't have enough buffered to continue
							inputIndex = (int) (inptr - inbuf);

							return (int) (outptr - outbuf) - offset;
						}

						if (*(inptr + 1) == (byte) '\r') {
							if ((inptr + 2) == inend) {
								// stop here. we don't have enough buffered to continue
								inputIndex = (int) (inptr - inbuf);

								return (int) (outptr - outbuf) - offset;
							}

							if (*(inptr + 2) == (byte) '\n') {
								// this indicates the end of a multi-line response
								inputIndex = (int) (inptr - inbuf) + 2;
								IsEndOfData = true;

								return (int) (outptr - outbuf) - offset;
							}
						} else if (*(inptr + 1) == (byte) '.') {
							// unescape ".." to "."
							inptr++;
						}

						// we might not technically be midline, but any Beginning-Of-Line
						// processing that needed to be done has been done so for all
						// intents and purposes, we are midline.
						midline = true;
					} while (outptr < outend);

					inputIndex = (int) (inptr - inbuf);

					return (int) (outptr - outbuf) - offset;
				}
			}
		}

		public bool ReadLine (out byte[] buffer, out int offset, out int count)
		{
			unsafe {
				fixed (byte* inbuf = input) {
					int left = inputEnd - inputIndex;
					byte* inptr, inend;

					if (left < 3)
						left = ReadAhead (inbuf, ReadAheadSize);

					offset = inputIndex;
					buffer = input;

					inptr = inbuf + inputIndex;
					inend = inbuf + inputEnd;
					*inend = (byte) '\n';

					// FIXME: use SIMD to optimize this
					while (*inptr != (byte) '\n')
						inptr++;

					count = (int) (inptr - inbuf) - inputIndex;
					inputIndex = (int) (inptr - inbuf);

					if (inptr < inend) {
						// consume the '\n'
						midline = false;
						inputIndex++;
						count++;
					} else {
						midline = true;
					}

					return !midline;
				}
			}
		}

		public override void Write (byte[] buffer, int offset, int count)
		{
			try {
				Stream.Write (buffer, offset, count);
			} catch (IOException) {
				IsConnected = false;
				throw;
			}

			IsEndOfData = false;
		}

		public override void Flush ()
		{
			try {
				Stream.Flush ();
			} catch (IOException) {
				IsConnected = false;
				throw;
			}
		}

		public override long Seek (long offset, SeekOrigin origin)
		{
			throw new NotSupportedException ();
		}

		public override void SetLength (long value)
		{
			throw new NotSupportedException ();
		}

		public override void Close ()
		{
			IsConnected = false;
			Stream.Close ();
		}

		protected override void Dispose (bool disposing)
		{
			if (disposing) {
				IsConnected = false;
				Stream.Dispose ();
			}

			base.Dispose (disposing);
		}
	}
}
