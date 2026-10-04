//
// MultipartExamples.cs
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
	public static class MultipartExamples
	{
		#region CreateMultipartMixed
		public static MimeMessage CreateMessageWithAttachment (string path)
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Joey", "joey@friends.com"));
			message.To.Add (new MailboxAddress ("Alice", "alice@wonderland.com"));
			message.Subject = "How you doin?";

			var body = new TextPart (TextFormat.Plain) {
				Text = @"Hey Alice,

What are you up to this weekend? Monica is throwing one of her parties on
Saturday and I was hoping you could make it.

Will you be my +1?

-- Joey
"
			};

			var attachment = new MimePart (MimeTypes.GetMimeType (path)) {
				Content = new MimeContent (File.OpenRead (path)),
				ContentDisposition = new ContentDisposition (ContentDisposition.Attachment),
				ContentTransferEncoding = ContentEncoding.Base64,
				FileName = Path.GetFileName (path)
			};

			// A multipart/mixed is a container for a list of independent MIME parts. The
			// message body is conventionally the first child, followed by the attachments.
			// A boundary string is generated automatically.
			var multipart = new Multipart ("mixed");
			multipart.Add (body);
			multipart.Add (attachment);

			message.Body = multipart;

			return message;
		}
		#endregion

		#region CreateMultipartAlternative
		public static MimeEntity CreateAlternativeBody ()
		{
			var plain = new TextPart (TextFormat.Plain) {
				Text = "Hey Alice,\n\nWill you be my +1 on Saturday?\n\n-- Joey\n"
			};

			var html = new TextPart (TextFormat.Html) {
				Text = "<p>Hey Alice,</p><p>Will you be my <b>+1</b> on Saturday?</p><p>-- Joey</p>"
			};

			// A multipart/alternative contains multiple representations of the same content.
			// The parts must be ordered from the least to the most faithful representation
			// because mail clients will generally display the last part that they understand.
			var alternative = new MultipartAlternative ();
			alternative.Add (plain);
			alternative.Add (html);

			// The TextBody and HtmlBody properties can be used to get the text of each format.
			Console.WriteLine ("Text: {0}", alternative.TextBody);
			Console.WriteLine ("Html: {0}", alternative.HtmlBody);

			return alternative;
		}
		#endregion

		#region RemoveAttachments
		public static void RemoveAttachments (MimeMessage message)
		{
			if (message.Body is Multipart multipart)
				RemoveAttachments (multipart);
		}

		static void RemoveAttachments (Multipart multipart)
		{
			// A Multipart is an IList<MimeEntity>, so its children can be added, removed or
			// replaced just like any other list. Iterate in reverse so that removing an item
			// does not affect the indexes of the children not yet visited.
			for (int i = multipart.Count - 1; i >= 0; i--) {
				var child = multipart[i];

				if (child is Multipart nested) {
					// Recurse into nested multiparts such as multipart/alternative.
					RemoveAttachments (nested);
				} else if (child.IsAttachment) {
					// Replace the attachment with a short note so that the recipient knows that
					// something was removed.
					var fileName = (child as MimePart)?.FileName ?? "attachment";

					multipart[i] = new TextPart (TextFormat.Plain) {
						Text = string.Format ("The attachment \"{0}\" has been removed.", fileName)
					};
				}
			}
		}
		#endregion

		#region ExtractAttachedMessages
		public static IList<MimeMessage> GetAttachedMessages (MimeMessage message)
		{
			var messages = new List<MimeMessage> ();

			// Attached (or forwarded) messages are represented by MessagePart objects. The
			// BodyParts property does not traverse into attached messages, so recurse into each
			// MessagePart's Message to find messages attached to the attached messages as well.
			foreach (var part in message.BodyParts.OfType<MessagePart> ()) {
				if (part.Message != null) {
					messages.Add (part.Message);
					messages.AddRange (GetAttachedMessages (part.Message));
				}
			}

			return messages;
		}
		#endregion
	}
}
