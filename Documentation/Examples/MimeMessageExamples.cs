//
// MimeMessageExamples.cs
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
using MimeKit.Utils;

namespace MimeKit.Examples
{
	public static class MimeMessageExamples
	{
		#region CreateMessage
		public static MimeMessage CreateMessage ()
		{
			var message = new MimeMessage ();

			// Add the sender and the recipients. Each of these properties is an InternetAddressList,
			// so any number of addresses may be added.
			message.From.Add (new MailboxAddress ("Joey Tribbiani", "joey@friends.com"));
			message.To.Add (new MailboxAddress ("Chandler Bing", "chandler@friends.com"));
			message.Cc.Add (new MailboxAddress ("Monica Geller", "monica@friends.com"));
			message.Subject = "How you doin'?";

			// Optional: MimeMessage generates a Message-Id and Date automatically, but they may
			// be set explicitly if needed.
			message.MessageId = MimeUtils.GenerateMessageId ("friends.com");
			message.Date = DateTimeOffset.Now;

			// Optional: flag the message as important.
			message.Importance = MessageImportance.High;
			message.Priority = MessagePriority.Urgent;

			// Set the body of the message. For anything more complicated than a single text
			// part, consider using the BodyBuilder class.
			message.Body = new TextPart (TextFormat.Plain) {
				Text = @"Hey Chandler,

I just wanted to let you know that Monica and I were going to go play some paintball, you in?

-- Joey"
			};

			return message;
		}
		#endregion

		#region LoadMessage
		public static void PrintMessageSummary (string fileName)
		{
			// Load a message from a file. The message must be a standalone message (such as an *.eml
			// file) and not an mbox; use the MimeParser to parse mbox files.
			var message = MimeMessage.Load (fileName);

			Console.WriteLine ("From: {0}", message.From);
			Console.WriteLine ("To: {0}", message.To);
			Console.WriteLine ("Subject: {0}", message.Subject);
			Console.WriteLine ("Date: {0}", message.Date);
			Console.WriteLine ("Message-Id: {0}", message.MessageId);
			Console.WriteLine ("Attachments: {0}", message.Attachments.Count ());
		}
		#endregion

		#region LoadMessageAsync
		public static async Task<MimeMessage> LoadMessageAsync (Stream stream, CancellationToken cancellationToken = default)
		{
			// Use custom ParserOptions to control how leniently the message is parsed. The
			// ParserOptions.Default instance is used when no options are specified.
			var options = new ParserOptions {
				AddressParserComplianceMode = RfcComplianceMode.Looser,
				MaxMimeDepth = 64
			};

			// LoadAsync() reads the stream asynchronously which makes it ideal for parsing messages
			// directly from a network stream.
			return await MimeMessage.LoadAsync (options, stream, cancellationToken).ConfigureAwait (false);
		}
		#endregion

		#region WriteTo
		public static void SaveMessage (MimeMessage message, string fileName)
		{
			// Clone the default FormatOptions so that we do not change the global defaults.
			var options = FormatOptions.Default.Clone ();

			// Always write files using Unix line endings, regardless of the current platform.
			options.NewLineFormat = NewLineFormat.Unix;

			// Do not write the Bcc header to the file.
			options.HiddenHeaders.Add (HeaderId.Bcc);

			message.WriteTo (options, fileName);
		}
		#endregion

		#region WriteToAsync
		public static async Task<byte[]> GetMessageBytesAsync (MimeMessage message, CancellationToken cancellationToken = default)
		{
			using (var memory = new MemoryStream ()) {
				// When sending a message via SMTP, the line endings must be CRLF. FormatOptions.Default
				// uses the platform's native line endings, so create options that use DOS line endings.
				var options = FormatOptions.Default.Clone ();
				options.NewLineFormat = NewLineFormat.Dos;

				await message.WriteToAsync (options, memory, cancellationToken).ConfigureAwait (false);

				return memory.ToArray ();
			}
		}
		#endregion

		#region GetMessageText
		public static string GetMessageText (MimeMessage message)
		{
			// The TextBody and HtmlBody convenience properties find the text/plain and text/html
			// bodies of the message, respectively, taking multipart/alternative and multipart/related
			// structures into consideration. They return null if no suitable body exists.
			if (message.TextBody != null)
				return message.TextBody;

			if (message.HtmlBody != null) {
				// The message only has an HTML body. Convert it to plain text by stripping
				// out the HTML markup.
				var previewer = new HtmlTextPreviewer { MaximumPreviewLength = int.MaxValue };

				return previewer.GetPreviewText (message.HtmlBody);
			}

			// GetTextBody() can be used to look for other text formats, such as text/enriched.
			return message.GetTextBody (TextFormat.Enriched) ?? string.Empty;
		}
		#endregion

		#region Prepare
		public static void PrepareForSending (MimeMessage message)
		{
			// Before sending a message via a transport that only supports 7-bit data and lines
			// no longer than 998 characters, make sure that each MIME part has an appropriate
			// Content-Transfer-Encoding. SmtpClient in MailKit does this automatically based on
			// the extensions supported by the SMTP server.
			message.Prepare (EncodingConstraint.SevenBit);

			// Prepare() also verifies that the line lengths are within the limit, so it is
			// equally useful for other transports that have line-length restrictions.
			message.Prepare (EncodingConstraint.EightBit, 78);
		}
		#endregion
	}
}
