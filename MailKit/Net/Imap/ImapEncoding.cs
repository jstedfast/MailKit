//
// Utf7Encoding.cs
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

using System.Text;

namespace MailKit.Net.Imap {
	static class ImapEncoding
	{
		const string utf7_alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+,";

		static readonly byte[] utf7_rank = {
			255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
			255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,255,
			255,255,255,255,255,255,255,255,255,255,255, 62, 63,255,255,255,
			 52, 53, 54, 55, 56, 57, 58, 59, 60, 61,255,255,255,255,255,255,
			255,  0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14,
			 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25,255,255,255,255,255,
			255, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
			 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51,255,255,255,255,255,
		};

		public static string Decode (string text)
		{
			var decoded = new StringBuilder ();
			bool shifted = false;
			int bits = 0, v = 0;
			int index = 0;
			char c;

			while (index < text.Length) {
				c = text[index++];

				if (shifted) {
					if (c == '-') {
						// shifted back out of modified UTF-7
						shifted = false;
						bits = v = 0;
					} else if (c > 127) {
						// invalid UTF-7
						return text;
					} else {
						byte rank = utf7_rank[(byte) c];

						if (rank == 0xff) {
							// invalid UTF-7
							return text;
						}

						v = (v << 6) | rank;
						bits += 6;

						if (bits >= 16) {
							char u = (char) ((v >> (bits - 16)) & 0xffff);
							decoded.Append (u);
							bits -= 16;
						}
					}
				} else if (c == '&' && index < text.Length) {
					if (text[index] == '-') {
						decoded.Append ('&');
						index++;
					} else {
						// shifted into modified UTF-7
						shifted = true;
					}
				} else {
					decoded.Append (c);
				}
			}

			return decoded.ToString ();
		}

		static void Utf7ShiftOut (StringBuilder output, int u, int bits)
		{
			if (bits > 0) {
				int x = (u << (6 - bits)) & 0x3f;
				output.Append (utf7_alphabet[x]);
			}

			output.Append ('-');
		}

		public static string Encode (string text)
		{
			var encoded = new StringBuilder ();
			bool shifted = false;
			int bits = 0, u = 0;

			for (int index = 0; index < text.Length; index++) {
				char c = text[index];

				if (c >= 0x20 && c < 0x7f) {
					// characters with octet values 0x20-0x25 and 0x27-0x7e
					// represent themselves while 0x26 ("&") is represented
					// by the two-octet sequence "&-"

					if (shifted) {
						Utf7ShiftOut (encoded, u, bits);
						shifted = false;
						bits = 0;
					}

					if (c == 0x26)
						encoded.Append ("&-");
					else
						encoded.Append (c);
				} else {
					// base64 encode
					if (!shifted) {
						encoded.Append ('&');
						shifted = true;
					}

					u = (u << 16) | (c & 0xffff);
					bits += 16;

					while (bits >= 6) {
						int x = (u >> (bits - 6)) & 0x3f;
						encoded.Append (utf7_alphabet[x]);
						bits -= 6;
					}
				}
			}

			if (shifted)
				Utf7ShiftOut (encoded, u, bits);

			return encoded.ToString ();
		}
	}
}
