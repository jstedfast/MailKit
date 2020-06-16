//
// SmtpDataFilter.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
	/// An SMTP filter designed to format a message stream for the DATA command.
	/// </summary>
	/// <remarks>
	/// A special stream filter that escapes lines beginning with a '.' as needed when
	/// sending a message via the SMTP protocol or when saving a message to an IIS
	/// message pickup directory.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="SaveToPickupDirectory" />
	/// </example>
	public class SmtpDataFilter : MimeFilterBase
	{
		bool bol;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpDataFilter"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SmtpDataFilter"/>.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SaveToPickupDirectory" />
		/// </example>
		public SmtpDataFilter ()
		{
			bol = true;
		}

		/// <summary>
		/// Filter the specified input.
		/// </summary>
		/// <remarks>
		/// Filters the specified input buffer starting at the given index,
		/// spanning across the specified number of bytes.
		/// </remarks>
		/// <returns>The filtered output.</returns>
		/// <param name="input">The input buffer.</param>
		/// <param name="startIndex">The starting index of the input buffer.</param>
		/// <param name="length">The length of the input buffer, starting at <paramref name="startIndex"/>.</param>
		/// <param name="outputIndex">The output index.</param>
		/// <param name="outputLength">The output length.</param>
		/// <param name="flush">If set to <c>true</c>, all internally buffered data should be flushed to the output buffer.</param>
		protected override byte[] Filter (byte[] input, int startIndex, int length, out int outputIndex, out int outputLength, bool flush)
		{
			int inputEnd = startIndex + length;
			bool escape = bol;
			int ndots = 0;
			int crlf = 0;

			for (int i = startIndex; i < inputEnd; i++) {
				byte c = input[i];

				if (c == (byte) '.' && escape) {
					escape = false;
					ndots++;
				} else {
					escape = c == (byte) '\n';
				}
			}

			if (flush && !escape)
				crlf = 2;

			if (ndots + crlf == 0) {
				outputIndex = startIndex;
				outputLength = length;
				bol = escape;
				return input;
			}

			EnsureOutputSize (length + ndots + crlf, false);
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

			if (crlf > 0) {
				OutputBuffer[index++] = (byte) '\r';
				OutputBuffer[index++] = (byte) '\n';
			}

			outputLength = index;
			outputIndex = 0;

			return OutputBuffer;
		}

		/// <summary>
		/// Reset the filter.
		/// </summary>
		/// <remarks>
		/// Resets the filter.
		/// </remarks>
		public override void Reset ()
		{
			base.Reset ();
			bol = true;
		}
	}
}
