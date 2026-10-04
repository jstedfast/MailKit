//
// TextConverterExamples.cs
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
using MimeKit.Text;

namespace MimeKit.Examples
{
	public static class TextConverterExamples
	{
		#region FlowedToText
		public static string GetUnflowedText (TextPart part)
		{
			// Text with a "format=flowed" Content-Type parameter (rfc3676) uses soft line breaks
			// so that the text can be re-wrapped by the receiving client. Convert it into normal
			// text by joining the flowed lines back into paragraphs.
			if (!part.IsFlowed)
				return part.Text;

			var converter = new FlowedToText ();

			// The "delsp" parameter specifies whether the space at the end of each flowed line
			// should be removed when the lines are joined.
			if (part.ContentType.Parameters.TryGetValue ("delsp", out string? delsp))
				converter.DeleteSpace = delsp.Equals ("yes", StringComparison.OrdinalIgnoreCase);

			return converter.Convert (part.Text);
		}
		#endregion

		#region TextToFlowed
		public static TextPart CreateFlowedTextPart (string text)
		{
			// Convert normal text into format=flowed text which wraps long lines using soft line
			// breaks that receiving clients will re-join.
			var converter = new TextToFlowed ();
			var part = new TextPart (TextFormat.Flowed) {
				Text = converter.Convert (text)
			};

			// TextToFlowed output requires the "delsp=yes" parameter so that receiving clients
			// remove the space that precedes each soft line break when re-joining the lines.
			part.ContentType.Parameters.Add ("delsp", "yes");

			// TextFormat.Flowed creates a text/plain part with the "format=flowed" parameter.
			Console.WriteLine (part.ContentType); // Content-Type: text/plain; format="flowed"; charset="utf-8"; delsp="yes"

			return part;
		}
		#endregion

		#region HtmlTokenizer
		public static IList<string> ExtractLinks (string html)
		{
			var links = new List<string> ();

			using (var reader = new StringReader (html)) {
				// HtmlTokenizer is a forward-only HTML5 tokenizer. It does not build a DOM which
				// makes it fast and well-suited for scanning or rewriting HTML.
				var tokenizer = new HtmlTokenizer (reader);

				while (tokenizer.ReadNextToken (out var token)) {
					if (token.Kind != HtmlTokenKind.Tag)
						continue;

					var tag = (HtmlTagToken) token;

					// Tag and attribute names are mapped to HtmlTagId and HtmlAttributeId values
					// for fast comparisons.
					if (tag.Id != HtmlTagId.A || tag.IsEndTag)
						continue;

					foreach (var attribute in tag.Attributes) {
						if (attribute.Id == HtmlAttributeId.Href && !string.IsNullOrEmpty (attribute.Value))
							links.Add (attribute.Value!);
					}
				}
			}

			return links;
		}
		#endregion

		#region GetPreviewText
		public static string GetPreviewText (MimeMessage message)
		{
			// TextPreviewer.GetPreviewText() picks the appropriate previewer (PlainTextPreviewer or
			// HtmlTextPreviewer) based on the format of the text part and returns a short, single-line
			// snippet of the text which is useful for displaying in a message list.
			var body = message.BodyParts.OfType<TextPart> ().FirstOrDefault (x => !x.IsAttachment);

			if (body == null)
				return string.Empty;

			if (body.IsHtml) {
				// The previewers can also be used directly in order to customize the length.
				var previewer = new HtmlTextPreviewer { MaximumPreviewLength = 100 };

				return previewer.GetPreviewText (body.Text);
			}

			return TextPreviewer.GetPreviewText (body);
		}
		#endregion
	}
}
