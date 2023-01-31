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
			if (ctx.TagId != HtmlTagId.Image || ctx.IsEndTag) {
				ctx.WriteTag (htmlWriter, true);
				return;
			}

			// write the IMG tag, but don't write out the attributes.
			ctx.WriteTag (htmlWriter, false);

			// manually write the attributes so that we can replace the SRC attributes
			foreach (var attribute in ctx.Attributes) {
				if (attribute.Id == HtmlAttributeId.Src) {
					int index;
					Uri uri;

					// parse the <img src=...> attribute value into a Uri
					if (Uri.IsWellFormedUriString (attribute.Value, UriKind.Absolute))
						uri = new Uri (attribute.Value, UriKind.Absolute);
					else
						uri = new Uri (attribute.Value, UriKind.Relative);

					// locate the index of the attachment within the multipart/related (if it exists)
					if ((index = related.IndexOf (uri)) != -1) {
						var attachment = related[index] as MimePart;

						if (attachment == null) {
							// the body part is not a basic leaf part (IOW it's a multipart or message-part)
							htmlWriter.WriteAttribute (attribute);
							continue;
						}

						var data = GetDataUri (attachment);

						htmlWriter.WriteAttributeName (attribute.Name);
						htmlWriter.WriteAttributeValue (data);
					} else {
						htmlWriter.WriteAttribute (attribute);
					}
				}
			}
		}
	}
}
