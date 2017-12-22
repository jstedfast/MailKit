using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using MimeKit;
using MimeKit.Text;

using MailKit;

namespace ImapClientDemo
{
	public partial class MainWindow : Form
	{
		public MainWindow ()
		{
			InitializeComponent ();

			folderTreeView.FolderSelected += FolderSelected;
			messageList.MessageSelected += MessageSelected;
		}

		void FolderSelected (object sender, FolderSelectedEventArgs e)
		{
			messageList.OpenFolder (e.Folder);
		}

		void MessageSelected (object sender, MessageSelectedEventArgs e)
		{
			Render (e.Folder, e.UniqueId, e.Body);
		}

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

		void RenderMultipartRelated (MultipartRelated related)
		{
			var root = related.Root;
			var multipart = root as Multipart;
			var text = root as TextPart;

			if (multipart != null) {
				// Note: the root document can sometimes be a multipart/alternative.
				// A multipart/alternative is just a collection of alternate views.
				// The last part is the format that most closely matches what the
				// user saw in his or her email client's WYSIWYG editor.
				for (int i = multipart.Count; i > 0; i--) {
					var body = multipart[i - 1] as TextPart;

					if (body == null)
						continue;

					// our preferred mime-type is text/html
					if (body.ContentType.IsMimeType ("text", "html")) {
						text = body;
						break;
					}

					if (text == null)
						text = body;
				}
			}

			// check if we have a text/html document
			if (text != null) {
				if (text.ContentType.IsMimeType ("text", "html")) {
					// replace image src urls that refer to related MIME parts with "data:" urls
					// Note: we could also save the related MIME part content to disk and use
					// file:// urls instead.
					var ctx = new MultipartRelatedImageContext (related);
					var converter = new HtmlToHtml () { HtmlTagCallback = ctx.HtmlTagCallback };
					var html = converter.Convert (text.Text);

					webBrowser.DocumentText = html;
				} else {
					RenderText (text);
				}
			} else {
				// we don't know how to render this type of content
				return;
			}
		}

		async void RenderMultipartRelated (IMailFolder folder, UniqueId uid, BodyPartMultipart bodyPart)
		{
			// download the entire multipart/related for simplicity since we'll probably end up needing all of the image attachments anyway...
			var related = await folder.GetBodyPartAsync (uid, bodyPart) as MultipartRelated;

			RenderMultipartRelated (related);
		}

		void RenderText (TextPart text)
		{
			string html;

			if (text.IsHtml) {
				// the text content is already in HTML format
				html = text.Text;
			} else if (text.IsFlowed) {
				var converter = new FlowedToHtml ();
				string delsp;

				// the delsp parameter specifies whether or not to delete spaces at the end of flowed lines
				if (!text.ContentType.Parameters.TryGetValue ("delsp", out delsp))
					delsp = "no";

				if (string.Compare (delsp, "yes", StringComparison.OrdinalIgnoreCase) == 0)
					converter.DeleteSpace = true;

				html = converter.Convert (text.Text);
			} else {
				html = new TextToHtml ().Convert (text.Text);
			}

			webBrowser.DocumentText = html;
		}

		async void RenderText (IMailFolder folder, UniqueId uid, BodyPartText bodyPart)
		{
			var entity = await folder.GetBodyPartAsync (uid, bodyPart);

			RenderText ((TextPart) entity);
		}

		void Render (IMailFolder folder, UniqueId uid, BodyPart body)
		{
			var multipart = body as BodyPartMultipart;

			if (multipart != null && body.ContentType.IsMimeType ("multipart", "related")) {
				RenderMultipartRelated (folder, uid, multipart);
				return;
			}

			var text = body as BodyPartText;

			if (multipart != null) {
				if (multipart.ContentType.IsMimeType ("multipart", "alternative")) {
					// A multipart/alternative is just a collection of alternate views.
					// The last part is the format that most closely matches what the
					// user saw in his or her email client's WYSIWYG editor.
					for (int i = multipart.BodyParts.Count; i > 0; i--) {
						var multi = multipart.BodyParts[i - 1] as BodyPartMultipart;

						if (multi != null && multi.ContentType.IsMimeType ("multipart", "related")) {
							if (multi.BodyParts.Count == 0)
								continue;

							var start = multi.ContentType.Parameters["start"];
							var root = multi.BodyParts[0];

							if (!string.IsNullOrEmpty (start)) {
								// if the 'start' parameter is set, it overrides the default behavior of using the first
								// body part as the main document.
								root = multi.BodyParts.OfType<BodyPartText> ().FirstOrDefault (x => x.ContentId == start);
							}

							if (root != null && root.ContentType.IsMimeType ("text", "html")) {
								Render (folder, uid, multi);
								return;
							}

							continue;
						}

						text = multipart.BodyParts[i - 1] as BodyPartText;

						if (text != null) {
							RenderText (folder, uid, text);
							return;
						}
					}
				} else if (multipart.BodyParts.Count > 0) {
					// The main message body is usually the first part of a multipart/mixed.
					Render (folder, uid, multipart.BodyParts[0]);
				}
			} else if (text != null) {
				RenderText (folder, uid, text);
			}
		}

		public void LoadContent ()
		{
			folderTreeView.LoadFolders ();
		}

		protected override void OnClosed (EventArgs e)
		{
			base.OnClosed (e);

			Application.Exit ();
		}
	}
}
