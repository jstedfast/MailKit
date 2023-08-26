//
// ByteArrayBuilder.cs
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
using System.Text;
using System.Buffers;

namespace MailKit
{
	class ByteArrayBuilder : IDisposable
	{
		byte[] buffer;
		int length;

		public ByteArrayBuilder (int initialCapacity)
		{
			buffer = ArrayPool<byte>.Shared.Rent (initialCapacity);
			length = 0;
		}

		public int Length {
			get { return length; }
		}

		public byte this[int index] {
			get { return buffer[index]; }
		}

		public byte[] GetBuffer ()
		{
			return buffer;
		}

		void EnsureCapacity (int capacity)
		{
			if (capacity > buffer.Length) {
				var resized = ArrayPool<byte>.Shared.Rent (capacity);
				Buffer.BlockCopy (buffer, 0, resized, 0, length);
				ArrayPool<byte>.Shared.Return (buffer);
				buffer = resized;
			}
		}

		public void Append (byte c)
		{
			EnsureCapacity (length + 1);
			buffer[length++] = c;
		}

		public void Append (byte[] text, int startIndex, int count)
		{
			EnsureCapacity (length + count);
			Buffer.BlockCopy (text, startIndex, buffer, length, count);
			length += count;
		}

		public void Clear ()
		{
			length = 0;
		}

		public byte[] ToArray ()
		{
			var array = new byte[length];

			Buffer.BlockCopy (buffer, 0, array, 0, length);

			return array;
		}

		public string ToString (Encoding encoding, Encoding fallback)
		{
			try {
				return encoding.GetString (buffer, 0, length);
			} catch (DecoderFallbackException) {
				return fallback.GetString (buffer, 0, length);
			}
		}

		public override string ToString ()
		{
			return ToString (TextEncodings.UTF8, TextEncodings.Latin1);
		}

		public bool Equals (string value, bool ignoreCase = false)
		{
			if (length == value.Length) {
				if (ignoreCase) {
					for (int i = 0; i < length; i++) {
						uint a = (uint) buffer[i];
						uint b = (uint) value[i];

						if ((a - 'a') <= 'z' - 'a')
							a -= 0x20;
						if ((b - 'a') <= 'z' - 'a')
							b -= 0x20;

						if (a != b)
							return false;
					}
				} else {
					for (int i = 0; i < length; i++) {
						if (value[i] != (char) buffer[i])
							return false;
					}
				}

				return true;
			}

			return false;
		}

		public void TrimNewLine ()
		{
			// Trim the <CR><LF> sequence from the end of the line.
			if (length > 0 && buffer[length - 1] == (byte) '\n') {
				length--;

				if (length > 0 && buffer[length - 1] == (byte) '\r')
					length--;
			}
		}

		// FIXME: This should be moved somewhere else...
		internal static bool TryParse (byte[] text, ref int index, int endIndex, out int value)
		{
			int startIndex = index;

			value = 0;

			while (index < endIndex && text[index] >= (byte) '0' && text[index] <= (byte) '9') {
				int digit = text[index] - (byte) '0';

				if (value > int.MaxValue / 10) {
					// integer overflow
					return false;
				}

				if (value == int.MaxValue / 10 && digit > int.MaxValue % 10) {
					// integer overflow
					return false;
				}

				value = (value * 10) + digit;
				index++;
			}

			return index > startIndex;
		}

		// FIXME: Does this make sense to have here? Or should I have an extensions class for byte[] that has this?
		public bool TryParse (int startIndex, int endIndex, out int value)
		{
			int index = startIndex;

			return TryParse (buffer, ref index, endIndex, out value);
		}

		public void Dispose ()
		{
			if (buffer != null) {
				ArrayPool<byte>.Shared.Return (buffer);
				buffer = null;
			}
		}
	}
}
