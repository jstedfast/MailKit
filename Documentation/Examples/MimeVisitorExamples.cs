//
// MimeVisitorExamples.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
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
using System.Collections.Generic;

using MimeKit;
using MimeKit.Text;
using MimeKit.Tnef;

namespace MimeKit.Examples
{
	#region HtmlPreviewVisitor
	/// <summary>
	/// Visits a MimeMessage and generates HTML suitable to be rendered by a browser control.
	/// </summary>
	class HtmlPreviewVisitor : MimeVisitor
	{
		List<MultipartRelated> stack = new List<MultipartRelated> ();
		List<MimeEntity> attachments = new List<MimeEntity> ();
		readonly string tempDir;
		string body;

		/// <summary>
		/// Creates a new HtmlPreviewVisitor.
		/// </summary>
		/// <param name="tempDirectory">A temporary directory used for storing image files.</param>
		public HtmlPreviewVisitor (string tempDirectory)
		{
			tempDir = tempDirectory;
		}

		/// <summary>
		/// The list of attachments that were in the MimeMessage.
		/// </summary>
		public IList<MimeEntity> Attachments {
			get { return attachments; }
		}

		/// <summary>
		/// The HTML string that can be set on the BrowserControl.
		/// </summary>
		public string HtmlBody {
			get { return body ?? string.Empty; }
		}

		protected override void VisitMultipartAlternative (MultipartAlternative alternative)
		{
			// walk the multipart/alternative children backwards from greatest level of faithfulness to the least faithful
			for (int i = alternative.Count - 1; i >= 0 && body == null; i--)
				alternative[i].Accept (this);
		}

		protected override void VisitMultipartRelated (MultipartRelated related)
		{
			var root = related.Root;

			// push this multipart/related onto our stack
			stack.Add (related);

			// visit the root document
			root.Accept (this);

			// pop this multipart/related off our stack
			stack.RemoveAt (stack.Count - 1);
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

			for (int i = stack.Count - 1; i >= 0; i--) {
				if ((index = stack[i].IndexOf (uri)) == -1)
					continue;

				image = stack[i][index] as MimePart;
				return image != null;
			}

			image = null;

			return false;
		}

		// Save the image to our temp directory and return a "file://" url suitable for
		// the browser control to load.
		// Note: if you'd rather embed the image data into the HTML, you can construct a
		// "data:" url instead.
		string SaveImage (MimePart image, string url)
		{
			string fileName = url.Replace (':', '_').Replace ('\\', '_').Replace ('/', '_');

			string path = Path.Combine (tempDir, fileName);

			if (!File.Exists (path)) {
				using (var output = File.Create (path))
					image.Content.DecodeTo (output);
			}

			return "file://" + path.Replace ('\\', '/');
		}

		// Replaces <img src=...> urls that refer to images embedded within the message with
		// "file://" urls that the browser control will actually be able to load.
		void HtmlTagCallback (HtmlTagContext ctx, HtmlWriter htmlWriter)
		{
			if (ctx.TagId == HtmlTagId.Image && !ctx.IsEndTag && stack.Count > 0) {
				ctx.WriteTag (htmlWriter, false);

				// replace the src attribute with a file:// URL
				foreach (var attribute in ctx.Attributes) {
					if (attribute.Id == HtmlAttributeId.Src) {
						MimePart image;
						string url;

						if (!TryGetImage (attribute.Value, out image)) {
							htmlWriter.WriteAttribute (attribute);
							continue;
						}

						url = SaveImage (image, attribute.Value);

						htmlWriter.WriteAttributeName (attribute.Name);
						htmlWriter.WriteAttributeValue (url);
					} else {
						htmlWriter.WriteAttribute (attribute);
					}
				}
			} else if (ctx.TagId == HtmlTagId.Body && !ctx.IsEndTag) {
				ctx.WriteTag (htmlWriter, false);

				// add and/or replace oncontextmenu="return false;"
				foreach (var attribute in ctx.Attributes) {
					if (attribute.Name.ToLowerInvariant () == "oncontextmenu")
						continue;

					htmlWriter.WriteAttribute (attribute);
				}

				htmlWriter.WriteAttribute ("oncontextmenu", "return false;");
			} else {
				// pass the tag through to the output
				ctx.WriteTag (htmlWriter, true);
			}
		}

		protected override void VisitTextPart (TextPart entity)
		{
			TextConverter converter;

			if (body != null) {
				// since we've already found the body, treat this as an attachment
				attachments.Add (entity);
				return;
			}

			if (entity.IsHtml) {
				converter = new HtmlToHtml {
					HtmlTagCallback = HtmlTagCallback
				};
			} else if (entity.IsFlowed) {
				var flowed = new FlowedToHtml ();
				string delsp;

				if (entity.ContentType.Parameters.TryGetValue ("delsp", out delsp))
					flowed.DeleteSpace = delsp.ToLowerInvariant () == "yes";

				converter = flowed;
			} else {
				converter = new TextToHtml ();
			}

			body = converter.Convert (entity.Text);
		}

		protected override void VisitTnefPart (TnefPart entity)
		{
			// extract any attachments in the MS-TNEF part
			attachments.AddRange (entity.ExtractAttachments ());
		}

		protected override void VisitMessagePart (MessagePart entity)
		{
			// treat message/rfc822 parts as attachments
			attachments.Add (entity);
		}

		protected override void VisitMimePart (MimePart entity)
		{
			// realistically, if we've gotten this far, then we can treat this as an attachment
			// even if the IsAttachment property is false.
			attachments.Add (entity);
		}
	}
	#endregion

	#region ReplyVisitor
	public class ReplyVisitor : MimeVisitor
	{
		readonly Stack<Multipart> stack = new Stack<Multipart> ();
		MimeMessage message;
		MimeEntity body;

		/// <summary>
		/// Creates a new ReplyVisitor.
		/// </summary>
		public ReplyVisitor ()
		{
		}

		/// <summary>
		/// Gets the reply.
		/// </summary>
		/// <value>The reply.</value>
		public MimeEntity Body {
			get { return body; }
		}

		void Push (MimeEntity entity)
		{
			var multipart = entity as Multipart;

			if (body == null) {
				body = entity;
			} else {
				var parent = stack.Peek ();
				parent.Add (entity);
			}

			if (multipart != null)
				stack.Push (multipart);
		}

		void Pop ()
		{
			stack.Pop ();
		}

		public static string GetOnDateSenderWrote (MimeMessage message)
		{
			var sender = message.Sender != null ? message.Sender : message.From.Mailboxes.FirstOrDefault ();
			var name = sender != null ? (!string.IsNullOrEmpty (sender.Name) ? sender.Name : sender.Address) : "someone";

			return string.Format ("On {0}, {1} wrote:", message.Date.ToString ("f"), name);
		}

		/// <summary>
		/// Visit the specified message.
		/// </summary>
		/// <param name="message">The message.</param>
		public override void Visit (MimeMessage message)
		{
			this.message = message;
			stack.Clear ();

			base.Visit (message);
		}

		protected override void VisitMultipartAlternative (MultipartAlternative alternative)
		{
			var multipart = new MultipartAlternative ();

			Push (multipart);

			for (int i = 0; i < alternative.Count; i++)
				alternative[i].Accept (this);

			Pop ();
		}

		protected override void VisitMultipartRelated (MultipartRelated related)
		{
			var multipart = new MultipartRelated ();
			var root = related.Root;

			Push (multipart);

			root.Accept (this);

			for (int i = 0; i < related.Count; i++) {
				if (related[i] != root)
					related[i].Accept (this);
			}

			Pop ();
		}

		protected override void VisitMultipart (Multipart multipart)
		{
			foreach (var part in multipart) {
				if (part is MultipartAlternative)
					part.Accept (this);
				else if (part is MultipartRelated)
					part.Accept (this);
				else if (part is TextPart)
					part.Accept (this);
			}
		}

		void HtmlTagCallback (HtmlTagContext ctx, HtmlWriter htmlWriter)
		{
			if (ctx.TagId == HtmlTagId.Body && !ctx.IsEmptyElementTag) {
				if (ctx.IsEndTag) {
					// end our opening <blockquote>
					htmlWriter.WriteEndTag (HtmlTagId.BlockQuote);

					// pass the </body> tag through to the output
					ctx.WriteTag (htmlWriter, true);
				} else {
					// pass the <body> tag through to the output
					ctx.WriteTag (htmlWriter, true);

					// prepend the HTML reply with "On {DATE}, {SENDER} wrote:"
					htmlWriter.WriteStartTag (HtmlTagId.P);
					htmlWriter.WriteText (GetOnDateSenderWrote (message));
					htmlWriter.WriteEndTag (HtmlTagId.P);

					// Wrap the original content in a <blockquote>
					htmlWriter.WriteStartTag (HtmlTagId.BlockQuote);
					htmlWriter.WriteAttribute (HtmlAttributeId.Style, "border-left: 1px #ccc solid; margin: 0 0 0 .8ex; padding-left: 1ex;");

					ctx.InvokeCallbackForEndTag = true;
				}
			} else {
				// pass the tag through to the output
				ctx.WriteTag (htmlWriter, true);
			}
		}

		string QuoteText (string text)
		{
			using (var quoted = new StringWriter ()) {
				quoted.WriteLine (GetOnDateSenderWrote (message));

				using (var reader = new StringReader (text)) {
					string line;

					while ((line = reader.ReadLine ()) != null) {
						quoted.Write ("> ");
						quoted.WriteLine (line);
					}
				}

				return quoted.ToString ();
			}
		}

		protected override void VisitTextPart (TextPart entity)
		{
			string text;

			if (entity.IsHtml) {
				var converter = new HtmlToHtml {
					HtmlTagCallback = HtmlTagCallback
				};

				text = converter.Convert (entity.Text);
			} else if (entity.IsFlowed) {
				var converter = new FlowedToText ();

				text = converter.Convert (entity.Text);
				text = QuoteText (text);
			} else {
				// quote the original message text
				text = QuoteText (entity.Text);
			}

			var part = new TextPart (entity.ContentType.MediaSubtype.ToLowerInvariant ()) {
				Text = text
			};

			Push (part);
		}

		protected override void VisitMessagePart (MessagePart entity)
		{
			// don't descend into message/rfc822 parts
		}
	}
	#endregion

	public class Program
	{
		#region RenderMessage
		void Render (MimeMessage message)
		{
			var tmpDir = Path.Combine (Path.GetTempPath (), message.MessageId);
			var visitor = new HtmlPreviewVisitor (tmpDir);

			Directory.CreateDirectory (tmpDir);

			message.Accept (visitor);

			DisplayHtml (visitor.HtmlBody);
			DisplayAttachments (visitor.Attachments);
		}
		#endregion

		#region ReplySimple
		public static MimeMessage Reply (MimeMessage message, MailboxAddress from, bool replyToAll)
		{
			var reply = new MimeMessage ();

			reply.From.Add (from);

			// reply to the sender of the message
			if (message.ReplyTo.Count > 0) {
				reply.To.AddRange (message.ReplyTo);
			} else if (message.From.Count > 0) {
				reply.To.AddRange (message.From);
			} else if (message.Sender != null) {
				reply.To.Add (message.Sender);
			}

			if (replyToAll) {
				// include all of the other original recipients (removing ourselves from the list)
				reply.To.AddRange (message.To.Mailboxes.Where (x => x.Address != from.Address));
				reply.Cc.AddRange (message.Cc.Mailboxes.Where (x => x.Address != from.Address));
			}

			// set the reply subject
			if (!message.Subject.StartsWith ("Re:", StringComparison.OrdinalIgnoreCase))
				reply.Subject = "Re: " + message.Subject;
			else
				reply.Subject = message.Subject;

			// construct the In-Reply-To and References headers
			if (!string.IsNullOrEmpty (message.MessageId)) {
				reply.InReplyTo = message.MessageId;
				foreach (var id in message.References)
					reply.References.Add (id);
				reply.References.Add (message.MessageId);
			}

			// quote the original message text
			using (var quoted = new StringWriter ()) {
				var sender = message.Sender ?? message.From.Mailboxes.FirstOrDefault ();
				var name = sender != null ? (!string.IsNullOrEmpty (sender.Name) ? sender.Name : sender.Address) : "someone";

				quoted.WriteLine ("On {0}, {1} wrote:", message.Date.ToString ("f"), name);
				using (var reader = new StringReader (message.TextBody)) {
					string line;

					while ((line = reader.ReadLine ()) != null) {
						quoted.Write ("> ");
						quoted.WriteLine (line);
					}
				}

				reply.Body = new TextPart ("plain") {
					Text = quoted.ToString ()
				};
			}

			return reply;
		}
		#endregion

		#region Reply
		public static MimeMessage Reply (MimeMessage message, MailboxAddress from, bool replyToAll)
		{
			var visitor = new ReplyVisitor ();
			var reply = new MimeMessage ();

			reply.From.Add (from);

			// reply to the sender of the message
			if (message.ReplyTo.Count > 0) {
				reply.To.AddRange (message.ReplyTo);
			} else if (message.From.Count > 0) {
				reply.To.AddRange (message.From);
			} else if (message.Sender != null) {
				reply.To.Add (message.Sender);
			}

			if (replyToAll) {
				// include all of the other original recipients (removing ourselves from the list)
				reply.To.AddRange (message.To.Mailboxes.Where (x => x.Address != from.Address));
				reply.Cc.AddRange (message.Cc.Mailboxes.Where (x => x.Address != from.Address));
			}

			// set the reply subject
			if (!message.Subject.StartsWith ("Re:", StringComparison.OrdinalIgnoreCase))
				reply.Subject = "Re: " + message.Subject;
			else
				reply.Subject = message.Subject;

			// construct the In-Reply-To and References headers
			if (!string.IsNullOrEmpty (message.MessageId)) {
				reply.InReplyTo = message.MessageId;
				foreach (var id in message.References)
					reply.References.Add (id);
				reply.References.Add (message.MessageId);
			}

			visitor.Visit (message);

			reply.Body = visitor.Body ?? new TextPart ("plain") { Text = ReplyVisitor.GetOnDateSenderWrote (message) + Environment.NewLine };

			return reply;
		}
		#endregion
	}
}
