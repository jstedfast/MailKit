//
// ContentTypeExamples.cs
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

namespace MimeKit.Examples
{
	public static class ContentTypeExamples
	{
		#region ParseContentType
		public static void ParseContentType ()
		{
			// Parse() throws a ParseException if the text cannot be parsed.
			var contentType = ContentType.Parse ("text/plain; charset=\"iso-8859-1\"; format=flowed");

			Console.WriteLine ("MimeType: {0}", contentType.MimeType);         // text/plain
			Console.WriteLine ("MediaType: {0}", contentType.MediaType);       // text
			Console.WriteLine ("MediaSubtype: {0}", contentType.MediaSubtype); // plain
			Console.WriteLine ("Charset: {0}", contentType.Charset);           // iso-8859-1
			Console.WriteLine ("Format: {0}", contentType.Format);             // flowed

			// TryParse() can be used instead when the value comes from an untrusted source.
			if (ContentType.TryParse ("application/octet-stream; name=\"data.bin\"", out var type))
				Console.WriteLine ("Name: {0}", type.Name);                    // data.bin
		}
		#endregion

		#region IsMimeType
		public static void ProcessEntity (MimeEntity entity)
		{
			var contentType = entity.ContentType;

			// IsMimeType() performs a case-insensitive comparison and supports "*" as a wildcard
			// for either the media type or the media subtype.
			if (contentType.IsMimeType ("text", "calendar")) {
				Console.WriteLine ("Found a calendar invitation.");
			} else if (contentType.IsMimeType ("text", "*")) {
				Console.WriteLine ("Found a text part with charset: {0}", contentType.Charset ?? "us-ascii");
			} else if (contentType.IsMimeType ("image", "*")) {
				Console.WriteLine ("Found an image: {0}", contentType.Name ?? "(unnamed)");
			} else if (contentType.IsMimeType ("multipart", "*")) {
				Console.WriteLine ("Found a multipart with boundary: {0}", contentType.Boundary);
			}
		}
		#endregion

		#region ContentTypeParameters
		public static MimePart CreateCsvAttachment (string fileName, Stream content)
		{
			var contentType = new ContentType ("text", "csv") {
				// The Charset, Name, Format and Boundary properties are shortcuts for getting
				// and setting the corresponding parameters.
				Charset = "utf-8",
				Name = Path.GetFileName (fileName)
			};

			// Any other parameter can be accessed via the Parameters collection.
			contentType.Parameters.Add ("header", "present");

			// Parameters with non-ASCII values will be encoded automatically, using the rfc2231
			// encoding by default. The FormatOptions.ParameterEncodingMethod property can be used
			// to control this.
			return new MimePart (contentType) {
				Content = new MimeContent (content),
				ContentDisposition = new ContentDisposition (ContentDisposition.Attachment),
				ContentTransferEncoding = ContentEncoding.Base64,
				FileName = Path.GetFileName (fileName)
			};
		}
		#endregion

		#region MimeTypes
		public static MimePart CreateAttachment (string path)
		{
			// MimeTypes.GetMimeType() maps a file extension to a MIME type. If the file
			// extension is unknown, "application/octet-stream" is returned.
			var mimeType = MimeTypes.GetMimeType (path);

			// Register any custom file extensions that your application uses.
			MimeTypes.Register ("application/x-my-app-document", ".myappdoc");

			// TryGetExtension() does the reverse mapping which is useful for picking a file
			// name for an attachment that does not have one.
			if (MimeTypes.TryGetExtension ("image/png", out var extension))
				Console.WriteLine ("image/png files use the extension: {0}", extension); // .png

			return new MimePart (ContentType.Parse (mimeType)) {
				Content = new MimeContent (File.OpenRead (path)),
				ContentDisposition = new ContentDisposition (ContentDisposition.Attachment),
				ContentTransferEncoding = ContentEncoding.Base64,
				FileName = Path.GetFileName (path)
			};
		}
		#endregion
	}
}
