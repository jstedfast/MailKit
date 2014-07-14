using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

using HtmlAgilityPack;
using MailKit;
using MimeKit;

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

		async void RenderRelated (IMailFolder folder, UniqueId uid, BodyPartMultipart related)
		{
			var start = related.ContentType.Parameters["start"];
			BodyPartText root = null;

			if (!string.IsNullOrEmpty (start)) {
				// if the 'start' parameter is set, it overrides the default behavior of using the first
				// body part as the main document.
				root = related.BodyParts.OfType<BodyPartText> ().FirstOrDefault (x => x.ContentId == start);
			} else if (related.BodyParts.Count > 0) {
				// this will generally either be a text/html part (which is what we are looking for) or a multipart/alternative
				var multipart = related.BodyParts[0] as BodyPartMultipart;

				if (multipart != null) {
					if (multipart.ContentType.Matches ("multipart", "alternative") && multipart.BodyParts.Count > 0) {
						// find the last text/html part (which will be the closest to what the sender saw in their WYSIWYG editor)
						// or, failing that, the last text part.
						for (int i = multipart.BodyParts.Count; i > 0; i--) {
							var bodyPart = multipart.BodyParts[i - 1] as BodyPartText;

							if (bodyPart == null)
								continue;

							if (bodyPart.ContentType.Matches ("text", "html")) {
								root = bodyPart;
								break;
							}

							if (root == null)
								root = bodyPart;
						}
					}
				} else {
					root = related.BodyParts[0] as BodyPartText;
				}
			}

			if (root == null)
				return;

			var text = await folder.GetBodyPartAsync (uid, root) as TextPart;

			if (text != null && text.ContentType.Matches ("text", "html")) {
				var doc = new HtmlAgilityPack.HtmlDocument ();
				var saved = new Dictionary<MimePart, string> ();
				TextPart html;

				doc.LoadHtml (text.Text);

				// find references to related MIME parts and replace them with links to links to the saved attachments
				foreach (var img in doc.DocumentNode.SelectNodes ("//img[@src]")) {
					var src = img.Attributes["src"];
					int index;
					Uri uri;

					if (src == null || src.Value == null)
						continue;

					// parse the <img src=...> attribute value into a Uri
					if (Uri.IsWellFormedUriString (src.Value, UriKind.Absolute))
						uri = new Uri (src.Value, UriKind.Absolute);
					else
						uri = new Uri (src.Value, UriKind.Relative);

					// locate the index of the attachment within the multipart/related (if it exists)
					if ((index = related.BodyParts.IndexOf (uri)) != -1) {
						var bodyPart = related.BodyParts[index] as BodyPartBasic;

						if (bodyPart == null) {
							// the body part is not a basic leaf part (IOW it's a multipart or message-part)
							continue;
						}

						var attachment = await folder.GetBodyPartAsync (uid, bodyPart) as MimePart;

						// make sure the referenced part is a MimePart (as opposed to another Multipart or MessagePart)
						if (attachment == null)
							continue;

						string fileName;

						// save the attachment (if we haven't already saved it)
						if (!saved.TryGetValue (attachment, out fileName)) {
							fileName = attachment.FileName;

							if (string.IsNullOrEmpty (fileName))
								fileName = Guid.NewGuid ().ToString ();

							fileName = Path.Combine (uid.ToString (), fileName);

							using (var stream = File.Create (fileName))
								attachment.ContentObject.DecodeTo (stream);

							saved.Add (attachment, fileName);
						}

						// replace the <img src=...> value with the local file name
						src.Value = "file://" + Path.GetFullPath (fileName);
					}
				}

				if (saved.Count > 0) {
					// we had to make some modifications to the original html part, so create a new
					// (temporary) text/html part to render
					html = new TextPart ("html");
					using (var writer = new StringWriter ()) {
						doc.Save (writer);

						html.Text = writer.GetStringBuilder ().ToString ();
					}
				} else {
					html = text;
				}

				RenderText (html);
			} else if (text != null) {
				RenderText (text);
			}
		}

		void RenderText (TextPart text)
		{
			string html;

			if (!text.ContentType.Matches ("text", "html")) {
				var builder = new StringBuilder ("<html><body><p>");
				var plain = text.Text;

				for (int i = 0; i < plain.Length; i++) {
					switch (plain[i]) {
					case ' ': builder.Append ("&nbsp;"); break;
					case '"': builder.Append ("&quot;"); break;
					case '&': builder.Append ("&amp;"); break;
					case '<': builder.Append ("&lt;"); break;
					case '>': builder.Append ("&gt;"); break;
					case '\r': break;
					case '\n': builder.Append ("<p>"); break;
					case '\t':
						for (int j = 0; j < 8; j++)
							builder.Append ("&nbsp;");
						break;
					default:
						if (char.IsControl (plain[i]) || plain[i] > 127) {
							int unichar;

							if (i + 1 < plain.Length && char.IsSurrogatePair (plain[i], plain[i + 1]))
								unichar = char.ConvertToUtf32 (plain[i], plain[i + 1]);
							else
								unichar = plain[i];

							builder.AppendFormat ("&#{0};", unichar);
						} else {
							builder.Append (plain[i]);
						}
						break;
					}
				}

				builder.Append ("</body></html>");
				html = builder.ToString ();
			} else {
				html = text.Text;
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

			if (multipart != null && body.ContentType.Matches ("multipart", "related")) {
				RenderRelated (folder, uid, multipart);
				return;
			}

			var text = body as BodyPartText;

			if (multipart != null) {
				if (multipart.ContentType.Matches ("multipart", "alternative")) {
					// A multipart/alternative is just a collection of alternate views.
					// The last part is the format that most closely matches what the
					// user saw in his or her email client's WYSIWYG editor.
					for (int i = multipart.BodyParts.Count; i > 0; i--) {
						var multi = multipart.BodyParts[i - 1] as BodyPartMultipart;

						if (multi != null && multi.ContentType.Matches ("multipart", "related")) {
							if (multi.BodyParts.Count == 0)
								continue;

							var start = multi.ContentType.Parameters["start"];
							var root = multi.BodyParts[0];

							if (!string.IsNullOrEmpty (start)) {
								// if the 'start' parameter is set, it overrides the default behavior of using the first
								// body part as the main document.
								root = multi.BodyParts.OfType<BodyPartText> ().FirstOrDefault (x => x.ContentId == start);
							}

							if (root != null && root.ContentType.Matches ("text", "html")) {
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
