//
// HeaderExamples.cs
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
using MimeKit.Utils;

namespace MimeKit.Examples
{
	public static class HeaderExamples
	{
		#region ModifyHeaders
		public static void ModifyHeaders (MimeMessage message)
		{
			// Add() appends a new header to the end of the list, even if a header with the same
			// name already exists.
			message.Headers.Add ("X-Mailer", "MyMailer v1.0");
			message.Headers.Add (HeaderId.Keywords, "MimeKit, Examples");

			// Replace() replaces the value of the first header with the given name, and removes
			// any other headers with that name. If no header exists, a new header is appended.
			message.Headers.Replace (HeaderId.Organization, "Central Perk");

			// The indexer may also be used to get or set the value of the first header with the
			// given name. Getting the value of a header that does not exist returns null.
			message.Headers["X-Priority"] = "1";

			string? mailer = message.Headers["X-Mailer"];

			Console.WriteLine ("X-Mailer: {0}", mailer ?? "(none)");

			// Remove() removes only the first header with the given name, while RemoveAll()
			// removes every header with that name.
			message.Headers.Remove ("X-Priority");
			message.Headers.RemoveAll (HeaderId.Received);

			// Insert() can be used to control where a header goes. Trace headers, for example,
			// are meant to be prepended to the existing headers.
			message.Headers.Insert (0, "X-Trace", "Scanned by MyVirusScanner");
		}
		#endregion

		#region EnumerateHeaders
		public static void PrintHeaders (MimeMessage message)
		{
			foreach (var header in message.Headers) {
				// Header.Field is the name of the header exactly as it appeared in the message,
				// while Header.Id is a HeaderId enum value that can be used for fast comparisons.
				// Headers that MimeKit does not know about have an Id of HeaderId.Unknown.
				if (header.Id == HeaderId.Unknown)
					Console.Write ("(custom) ");

				// Header.Value is the unfolded and decoded value of the header.
				Console.WriteLine ("{0}: {1}", header.Field, header.Value);
			}

			// Headers can also be enumerated by index which can be useful for headers that may
			// appear multiple times, such as the Received headers.
			for (int i = 0; i < message.Headers.Count; i++) {
				var header = message.Headers[i];

				if (header.Id == HeaderId.Received)
					Console.WriteLine ("Hop #{0}: {1}", i + 1, header.Value);
			}
		}
		#endregion

		#region DecodeHeaderValue
		public static string GetSubject (Stream stream)
		{
			// Only load the headers rather than the entire message.
			var headers = HeaderList.Load (stream);
			var index = headers.IndexOf (HeaderId.Subject);

			if (index == -1)
				return string.Empty;

			var subject = headers[index];

			// Some mail clients incorrectly include raw 8-bit text in headers instead of
			// encoding the text using the rfc2047 encoding scheme. By default, MimeKit decodes
			// any undeclared 8-bit text as UTF-8 (falling back to ParserOptions.CharsetEncoding).
			// If the charset is known, it can be passed to GetValue() to decode the header properly.
			return subject.GetValue ("iso-8859-1");
		}
		#endregion

		#region SetHeaderValue
		public static void SetLatin1Subject (MimeMessage message)
		{
			var index = message.Headers.IndexOf (HeaderId.Subject);
			Header subject;

			if (index == -1) {
				subject = new Header (HeaderId.Subject, string.Empty);
				message.Headers.Add (subject);
			} else {
				subject = message.Headers[index];
			}

			// By default, MimeKit encodes non-ASCII header values using UTF-8. Some legacy mail
			// clients may expect a specific charset, which can be specified via SetValue().
			// The value will be serialized as: Subject: =?iso-8859-1?b?Q2Fm6Q==?= au lait
			subject.SetValue ("iso-8859-1", "Café au lait");

			// HeaderList.Replace() and Add() also have overloads that take an Encoding.
			message.Headers.Replace (HeaderId.Comments, Encoding.UTF8, "Ça va?");
		}
		#endregion

		#region MessageIdHeaders
		public static MimeMessage CreateReply (MimeMessage message)
		{
			var reply = new MimeMessage ();

			var subject = message.Subject ?? string.Empty;

			reply.From.Add (new MailboxAddress ("Chandler Bing", "chandler@friends.com"));
			reply.To.AddRange (message.ReplyTo.Count > 0 ? message.ReplyTo : message.From);
			reply.Subject = subject.StartsWith ("Re:", StringComparison.OrdinalIgnoreCase) ? subject : "Re: " + subject;

			// Message-Id values do not include the angle brackets. MimeKit adds them when
			// the headers are serialized.
			reply.MessageId = MimeUtils.GenerateMessageId ();

			// Thread the reply by setting the In-Reply-To and References headers.
			if (!string.IsNullOrEmpty (message.MessageId)) {
				reply.InReplyTo = message.MessageId;

				// The References header contains the list of message-ids in the thread
				// starting with the original message.
				foreach (var id in message.References)
					reply.References.Add (id);

				reply.References.Add (message.MessageId);
			}

			reply.Body = new TextPart ("plain") {
				Text = "Could I *be* any more excited?"
			};

			return reply;
		}
		#endregion
	}
}
