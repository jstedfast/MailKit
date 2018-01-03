//
// SmtpDataFilter.cs
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

using MimeKit.IO.Filters;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// A special stream filter that escapes lines beginning with a '.'
	/// as needed when uploading a message to an SMTP server.
	/// </summary>
	class SmtpDataFilter : MimeFilterBase
	{
		bool bol = true;

		/// <summary>
		/// Filter the specified input.
		/// </summary>
		/// <returns>The filtered output.</returns>
		/// <param name="input">The input buffer.</param>
		/// <param name="startIndex">The starting index of the input buffer.</param>
		/// <param name="length">Length.</param>
		/// <param name="outputIndex">Output index.</param>
		/// <param name="outputLength">Output length.</param>
		/// <param name="flush">If set to <c>true</c> flush.</param>
		protected override byte[] Filter (byte[] input, int startIndex, int length, out int outputIndex, out int outputLength, bool flush)
		{
			int inputEnd = startIndex + length;
			bool escape = bol;
			int ndots = 0;

			for (int i = startIndex; i < inputEnd; i++) {
				byte c = input[i];

				if (c == (byte) '.' && escape) {
					escape = false;
					ndots++;
				} else {
					escape = c == (byte) '\n';
				}
			}

			EnsureOutputSize (length + ndots, false);
			int index = 0;

			for (int i = startIndex; i < inputEnd; i++) {
				byte c = input[i];

				if (c == (byte) '.' && bol) {
					OutputBuffer[index++] = (byte) '.';
					bol = false;
				} else {
					bol = c == (byte) '\n';
				}

				OutputBuffer[index++] = c;
			}

			outputLength = index;
			outputIndex = 0;

			return OutputBuffer;
		}

		/// <summary>
		/// Resets the filter.
		/// </summary>
		public override void Reset ()
		{
			base.Reset ();
			bol = true;
		}
	}
}
