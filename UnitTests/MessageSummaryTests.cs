//
// MessageSummaryTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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

using NUnit.Framework;

using MimeKit;
using MailKit;

namespace UnitTests {
	[TestFixture]
	public class MessageSummaryTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageSummary (-1));
		}

		static ContentType CreateContentType (string type, string subtype, string partSpecifier)
		{
			var contentType = new ContentType (type, subtype);
			contentType.Parameters.Add ("part-specifier", partSpecifier);
			return contentType;
		}

		static BodyPartMessage CreateMessage (string type, string subtype, string partSpecifier, BodyPart body, bool attachment)
		{
			var message = new BodyPartMessage { ContentType = CreateContentType (type, subtype, partSpecifier) };
			if (attachment)
				message.ContentDisposition = new ContentDisposition (ContentDisposition.Attachment);
			message.Body = body;
			return message;
		}

		static BodyPartMultipart CreateMultipart (string type, string subtype, string partSpecifier, params BodyPart [] bodyParts)
		{
			var multipart = new BodyPartMultipart { ContentType = CreateContentType (type, subtype, partSpecifier) };
			foreach (var bodyPart in bodyParts)
				multipart.BodyParts.Add (bodyPart);
			return multipart;
		}

		static BodyPartBasic CreateBasic (string type, string subtype, string partSpecifier, bool attachment)
		{
			var basic = new BodyPartBasic { ContentType = CreateContentType (type, subtype, partSpecifier) };
			basic.ContentDisposition = new ContentDisposition (attachment ? ContentDisposition.Attachment : ContentDisposition.Inline);
			return basic;
		}

		static BodyPartBasic CreateText (string type, string subtype, string partSpecifier, bool attachment)
		{
			var text = new BodyPartText { ContentType = CreateContentType (type, subtype, partSpecifier) };
			if (attachment)
				text.ContentDisposition = new ContentDisposition (ContentDisposition.Attachment);
			return text;
		}

		[Test]
		public void TestHtmlAndTextBodies ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateMultipart ("MULTIPART", "ALTERNATIVE", "1",
						CreateText ("TEXT", "PLAIN", "1.1", false),
						CreateMultipart ("MULTIPART", "RELATED", "1.2",
							CreateText ("TEXT", "HTML", "1.2.1", false),
							CreateBasic ("IMAGE", "JPEG", "1.2.2", false)
						)
					),
					CreateBasic ("APPLICATION", "OCTET-STREAM", "2", true),
					CreateMessage ("MESSAGE", "RFC822", "3",
						CreateMultipart ("MULTIPART", "MIXED", "3",
							CreateText ("TEXT", "PLAIN", "3.1", false),
							CreateBasic ("APPLICATION", "OCTET-STREAM", "3.2", true)
						), true
					),
					CreateBasic ("IMAGE", "GIF", "4", true)
				)
			};
			int i;

			var plain = summary.TextBody;
			Assert.AreEqual ("1.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.AreEqual ("1.2.1", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			var bodyParts = new string [] { "1.1", "1.2.1", "1.2.2", "2", "3", "4" };
			i = 0;

			foreach (var part in summary.BodyParts)
				Assert.AreEqual (bodyParts[i++], part.ContentType.Parameters["part-specifier"], "BodyParts");

			var attachments = new string[] { "2", "3", "4" };
			i = 0;

			foreach (var attachment in summary.Attachments)
				Assert.AreEqual (attachments[i++], attachment.ContentType.Parameters["part-specifier"], "Attachments");
		}
	}
}
