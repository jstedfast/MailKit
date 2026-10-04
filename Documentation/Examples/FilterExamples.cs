//
// FilterExamples.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;
using MimeKit.IO;
using MimeKit.IO.Filters;

namespace MimeKit.Examples
{
	public static class FilterExamples
	{
		#region EncodeFile
		public static void Base64EncodeFile (string inputFileName, string outputFileName)
		{
			using (var input = File.OpenRead (inputFileName)) {
				using (var output = File.Create (outputFileName)) {
					// A FilteredStream passes all data that is written to (or read from) it through
					// each of its filters, in the order that the filters were added.
					using (var filtered = new FilteredStream (output)) {
						filtered.Add (EncoderFilter.Create (ContentEncoding.Base64));

						input.CopyTo (filtered);

						// Flush the filtered stream so that the encoder writes out any remaining
						// buffered data to the output stream.
						filtered.Flush ();
					}
				}
			}
		}
		#endregion

		#region DecodeFile
		public static void DecodeFile (string inputFileName, string outputFileName, string contentTransferEncoding)
		{
			using (var input = File.OpenRead (inputFileName)) {
				// Filters can also be used when reading from a FilteredStream. Here, the source stream
				// is the encoded input and reading from the filtered stream yields the decoded content.
				using (var filtered = new FilteredStream (input)) {
					// Create a decoder for the encoding by name, such as "base64" or "quoted-printable".
					filtered.Add (DecoderFilter.Create (contentTransferEncoding));

					using (var output = File.Create (outputFileName))
						filtered.CopyTo (output);
				}
			}
		}
		#endregion

		#region ConvertCharset
		public static string ConvertToUtf8 (Stream input, string charset)
		{
			using (var memory = new MemoryStream ()) {
				using (var filtered = new FilteredStream (memory)) {
					// Convert the text from the source charset to UTF-8 while it is being copied.
					filtered.Add (new CharsetFilter (charset, "utf-8"));

					// Normalize the line endings to Unix-style LF line endings as well.
					filtered.Add (new Dos2UnixFilter ());

					input.CopyTo (filtered);
					filtered.Flush ();
				}

				return Encoding.UTF8.GetString (memory.GetBuffer (), 0, (int) memory.Length);
			}
		}
		#endregion

		#region BestEncoding
		public static ContentEncoding GetBestEncoding (Stream content)
		{
			// The BestEncodingFilter does not modify the data passing through it. Instead, it
			// analyzes the content in order to determine the most efficient
			// Content-Transfer-Encoding that satisfies the given constraints.
			var filter = new BestEncodingFilter ();

			using (var filtered = new FilteredStream (Stream.Null)) {
				filtered.Add (filter);
				content.CopyTo (filtered);
				filtered.Flush ();
			}

			// For example, a 7-bit transport requires 8-bit text to be encoded using either
			// quoted-printable or base64. Lines longer than the specified maximum line length
			// (78 characters, in this case) will also require the content to be encoded.
			// Note: MimePart.GetBestEncoding() is a convenience method that does this for you.
			return filter.GetBestEncoding (EncodingConstraint.SevenBit, 78);
		}
		#endregion
	}
}
