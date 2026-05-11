using System;
using System.IO;

using MimeKit;
using MimeKit.Text;

namespace ImapClientDemo
{
	class MultipartRelatedImageContext
	{
		readonly MultipartRelated related;

		public MultipartRelatedImageContext (MultipartRelated related)
		{
			this.related = related;
		}

		// look up the image based on the img src url within our multipart/related stack
		bool TryGetImage (string url, out MimePart image)
		{
			UriKind kind;
			int index;
			Uri uri;

			if (Uri.IsWellFormedUriString (url, UriKind.Absolute))
				kind = UriKind.Absolute;
			else if (Uri.IsWellFormedUriString (url, UriKind.Relative))
				kind = UriKind.Relative;
			else
				kind = UriKind.RelativeOrAbsolute;

			try {
				uri = new Uri (url, kind);
			} catch {
				image = null;
				return false;
			}

			if ((index = related.IndexOf (uri)) == -1) {
				image = null;
				return false;
			}

			image = related[index] as MimePart;

			return image != null;
		}

		string GetDataUri (MimePart attachment)
		{
			using (var memory = new MemoryStream ()) {
				attachment.Content.DecodeTo (memory);
				var buffer = memory.GetBuffer ();
				var length = (int) memory.Length;
				var base64 = Convert.ToBase64String (buffer, 0, length);

				return string.Format ("data:{0};base64,{1}", attachment.ContentType.MimeType, base64);
			}
		}

		public void HtmlTagCallback (HtmlTagContext ctx, HtmlWriter htmlWriter)
		{
			if (ctx.TagId == HtmlTagId.Meta && !ctx.IsEndTag) {
				bool isContentType = false;

				ctx.WriteTag (htmlWriter, false);

				// replace charsets with "utf-8" since our output will be in utf-8 (and not whatever the original charset was)
				foreach (var attribute in ctx.Attributes) {
					if (attribute.Id == HtmlAttributeId.Charset) {
						htmlWriter.WriteAttributeName (attribute.Name);
						htmlWriter.WriteAttributeValue ("utf-8");
					} else if (isContentType && attribute.Id == HtmlAttributeId.Content) {
						htmlWriter.WriteAttributeName (attribute.Name);
						htmlWriter.WriteAttributeValue ("text/html; charset=utf-8");
						isContentType = false;
					} else if (attribute.Id == HtmlAttributeId.HttpEquiv) {
						if (attribute.Value != null) {
							if (attribute.Value.Equals ("Content-Type", StringComparison.OrdinalIgnoreCase)) {
								htmlWriter.WriteAttribute (attribute);
								isContentType = true;
							} else if (!attribute.Value.Equals ("refresh", StringComparison.OrdinalIgnoreCase)) {
								// <meta http-equiv="refresh"> can be used as an XSS attack vector - filter it out
								htmlWriter.WriteAttribute (attribute);
							}
						}
					} else {
						htmlWriter.WriteAttribute (attribute);
					}
				}
			} else if (ctx.TagId == HtmlTagId.Image && !ctx.IsEndTag) {
				ctx.WriteTag (htmlWriter, false);

				// replace the src attribute with a "data:" URL
				foreach (var attribute in ctx.Attributes) {
					if (attribute.Id == HtmlAttributeId.Src) {
						if (!TryGetImage (attribute.Value, out var image)) {
							htmlWriter.WriteAttribute (attribute);
							continue;
						}

						var dataUri = GetDataUri (image);

						htmlWriter.WriteAttributeName (attribute.Name);
						htmlWriter.WriteAttributeValue (dataUri);
					} else if (!attribute.Name.StartsWith ("on", StringComparison.OrdinalIgnoreCase)) {
						// filter out "onclick", "onmouseover", etc. event handlers which can be used as XSS attack vectors
						htmlWriter.WriteAttribute (attribute);
					}
				}
			} else if (!ctx.IsEndTag) {
				ctx.WriteTag (htmlWriter, false);

				// filter out "onload", "onclick", "onmouseover", etc. event handlers which can be used as XSS attack vectors
				foreach (var attribute in ctx.Attributes) {
					if (attribute.Name.Equals ("on", StringComparison.OrdinalIgnoreCase))
						continue;

					htmlWriter.WriteAttribute (attribute);
				}

				// if this is the <body> tag, explicitly add an oncontextmenu event handler that simply returns false
				if (ctx.TagId == HtmlTagId.Body)
					htmlWriter.WriteAttribute ("oncontextmenu", "return false;");
			} else {
				// Write the end tag
				ctx.WriteTag (htmlWriter);
			}
		}
	}
}
