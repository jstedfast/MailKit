using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using MimeKit;
using MimeKit.Text;

using MailKit;
using MailKit.Net.Imap;

namespace ImapClientDemo
{
	public partial class MainWindow : Form
	{
		public MainWindow ()
		{
			InitializeComponent ();

			folderTreeView.FolderSelected += OnFolderSelected;
			messageList.MessageSelected += OnMessageSelected;
		}

		class RenderMessageCommand : ClientCommand<ImapClient>
		{
			readonly WebBrowser webBrowser;
			readonly IMailFolder folder;
			readonly UniqueId uid;
			readonly BodyPart body;

			string documentText;

			public RenderMessageCommand (ClientConnection<ImapClient> connection, IMailFolder folder, UniqueId uid, BodyPart body, WebBrowser webBrowser) : base (connection)
			{
				this.folder = folder;
				this.uid = uid;
				this.body = body;
				this.webBrowser = webBrowser;
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

						documentText = html;
					} else {
						RenderText (text);
					}
				} else {
					// we don't know how to render this type of content
					return;
				}
			}

			void DownloadMultipartRelated (IMailFolder folder, UniqueId uid, BodyPartMultipart bodyPart, CancellationToken cancellationToken)
			{
				// download the entire multipart/related for simplicity since we'll probably end up needing all of the image attachments anyway...
				var related = folder.GetBodyPart (uid, bodyPart, cancellationToken) as MultipartRelated;

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

					// the delsp parameter specifies whether or not to delete spaces at the end of flowed lines
					if (!text.ContentType.Parameters.TryGetValue ("delsp", out string delsp))
						delsp = "no";

					if (string.Compare (delsp, "yes", StringComparison.OrdinalIgnoreCase) == 0)
						converter.DeleteSpace = true;

					html = converter.Convert (text.Text);
				} else {
					html = new TextToHtml ().Convert (text.Text);
				}

				documentText = html;
			}

			void DownloadTextPart (IMailFolder folder, UniqueId uid, BodyPartText bodyPart, CancellationToken cancellationToken)
			{
				var entity = folder.GetBodyPart (uid, bodyPart, cancellationToken);

				RenderText ((TextPart) entity);
			}

			void DownloadBodyPart (IMailFolder folder, UniqueId uid, BodyPart body, CancellationToken cancellationToken)
			{
				var multipart = body as BodyPartMultipart;

				if (multipart != null && body.ContentType.IsMimeType ("multipart", "related")) {
					DownloadMultipartRelated (folder, uid, multipart, cancellationToken);
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
									DownloadBodyPart (folder, uid, multi, cancellationToken);
									return;
								}

								continue;
							}

							text = multipart.BodyParts[i - 1] as BodyPartText;

							if (text != null) {
								DownloadTextPart (folder, uid, text, cancellationToken);
								return;
							}
						}
					} else if (multipart.BodyParts.Count > 0) {
						// The main message body is usually the first part of a multipart/mixed.
						DownloadBodyPart (folder, uid, multipart.BodyParts[0], cancellationToken);
					}
				} else if (text != null) {
					DownloadTextPart (folder, uid, text, cancellationToken);
				}
			}

			public override void Run (CancellationToken cancellationToken)
			{
				if (!folder.IsOpen)
					folder.Open (FolderAccess.ReadWrite, cancellationToken);

				DownloadBodyPart (folder, uid, body, cancellationToken);

				// Proxy the DownloadBodyPart() method call to the GUI thread.
				Program.RunOnMainThread (webBrowser, Render);
			}

			void Render ()
			{
				webBrowser.DocumentText = documentText;
			}
		}

		void OnMessageSelected (object sender, MessageSelectedEventArgs e)
		{
			var command = new RenderMessageCommand (Program.ImapClientConnection, e.Folder, e.UniqueId, e.Body, webBrowser);
			Program.ImapCommandPipeline.Enqueue (command);
		}

		void OnFolderSelected (object sender, FolderSelectedEventArgs e)
		{
			messageList.OpenFolder (e.Folder);
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
