//
// MessageSummaryTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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
			Assert.Throws<ArgumentNullException> (() => new MessageSummary (null, 0));
		}

		[Test]
		public void TestDefaultValues ()
		{
			var summary = new MessageSummary (17);

			Assert.That (summary.Attachments.Count (), Is.EqualTo (0), "Attachments");
			Assert.That (summary.Body, Is.Null, "Body");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (0), "BodyParts");
			Assert.That (summary.Date, Is.EqualTo (DateTimeOffset.MinValue), "Date");
			Assert.That (summary.Envelope, Is.Null, "Envelope");
			Assert.That (summary.Flags, Is.Null, "Flags");
			Assert.That (summary.GMailLabels, Is.Null, "GMailLabels");
			Assert.That (summary.GMailMessageId, Is.Null, "GMailMessageId");
			Assert.That (summary.GMailThreadId, Is.Null, "GMailThreadId");
			Assert.That (summary.Headers, Is.Null, "Headers");
			Assert.That (summary.HtmlBody, Is.Null, "HtmlBody");
			Assert.That (summary.Index, Is.EqualTo (17), "Index");
			Assert.That (summary.InternalDate, Is.Null, "InternalDate");
			Assert.That (summary.IsReply, Is.False, "IsReply");
			Assert.That (summary.ModSeq, Is.Null, "ModSeq");
			Assert.That (summary.NormalizedSubject, Is.EqualTo (string.Empty), "NormalizedSubject");
			Assert.That (summary.PreviewText, Is.Null, "PreviewText");
			Assert.That (summary.References, Is.Null, "References");
			Assert.That (summary.Size, Is.Null, "Size");
			Assert.That (summary.TextBody, Is.Null, "TextBody");
			Assert.That (summary.UniqueId, Is.EqualTo (UniqueId.Invalid), "UniqueId");
			Assert.That (summary.Keywords, Is.Not.Null, "Keywords");
			Assert.That (summary.Keywords, Is.Empty, "Keywords");
		}

		[Test]
		public void TestGMailProperties ()
		{
			ulong msgid = 179111;
			ulong thrid = 7192564;
			var summary = new MessageSummary (0) {
				GMailLabels = new List<string> (),
				GMailMessageId = msgid,
				GMailThreadId = thrid
			};

			Assert.That (summary.GMailLabels, Is.Empty, "GMailLabels");
			Assert.That (summary.GMailMessageId, Is.EqualTo (msgid), "GMailMessageId");
			Assert.That (summary.GMailThreadId, Is.EqualTo (thrid), "GMailThreadId");
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

		static BodyPartText CreateText (string type, string subtype, string partSpecifier, bool attachment)
		{
			var text = new BodyPartText { ContentType = CreateContentType (type, subtype, partSpecifier) };
			if (attachment)
				text.ContentDisposition = new ContentDisposition (ContentDisposition.Attachment);
			return text;
		}

		[Test]
		public void TestTextPlainBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateText ("TEXT", "PLAIN", "1", false)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Null, "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (0), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (1), "BodyParts");
		}

		[Test]
		public void TestTextHtmlBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateText ("TEXT", "HTML", "1", false)
			};

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1"), "HtmlBody");

			var plain = summary.TextBody;
			Assert.That (plain, Is.Null, "TextBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (0), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (1), "BodyParts");
		}

		[Test]
		public void TestImageJpegBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateBasic ("IMAGE", "JPEG", "1", false)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Null, "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Null, "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (0), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (1), "BodyParts");
		}

		[Test]
		public void TestMultipartAlternative ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "ALTERNATIVE", "",
					CreateText ("TEXT", "PLAIN", "1", false),
					CreateText ("TEXT", "HTML", "2", false)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("2"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (0), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (2), "BodyParts");
		}

		[Test]
		public void TestMultipartAlternativeNoTextParts ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "ALTERNATIVE", "",
					CreateText ("TEXT", "RICHTEXT", "1", false),
					CreateBasic ("APPLICATION", "PDF", "2", false)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Null, "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Null, "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (0), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (2), "BodyParts");
		}

		[Test]
		public void TestMixedTextPlainBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateText ("TEXT", "PLAIN", "1", false),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Null, "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (2), "BodyParts");
		}

		[Test]
		public void TestMixedTextHtmlBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateText ("TEXT", "HTML", "1", false),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Null, "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (2), "BodyParts");
		}

		[Test]
		public void TestRelatedTextPlainBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "RELATED", "",
					CreateText ("TEXT", "PLAIN", "1", false),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Null, "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (2), "BodyParts");
		}

		[Test]
		public void TestRelatedTextHtmlBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "RELATED", "",
					CreateText ("TEXT", "HTML", "1", false),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Null, "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters ["part-specifier"], Is.EqualTo ("1"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (2), "BodyParts");
		}

		[Test]
		public void TestMixedAlternativeRelated ()
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
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.2.1"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (4), "BodyParts");
		}

		[Test]
		public void TestMixedAlternativeRelatedWithStartParameter ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateMultipart ("MULTIPART", "ALTERNATIVE", "1",
						CreateText ("TEXT", "PLAIN", "1.1", false),
						CreateMultipart ("MULTIPART", "RELATED", "1.2",
							CreateBasic ("IMAGE", "JPEG", "1.2.1", false),
							CreateText ("TEXT", "HTML", "1.2.2", false)
						)
					),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};
			var cid = "html@localhost.com";
			var mixed = (BodyPartMultipart) summary.Body;
			var alternative = (BodyPartMultipart) mixed.BodyParts[0];
			var related = (BodyPartMultipart) alternative.BodyParts[1];
			var html = (BodyPartText) related.BodyParts[1];

			related.ContentType.Parameters["start"] = cid;
			html.ContentLocation = new Uri ("cid:" + cid);

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1"), "TextBody");

			html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.2.2"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (4), "BodyParts");
		}

		[Test]
		public void TestMixedRelatedAlternativeWithStartParameter ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateMultipart ("MULTIPART", "RELATED", "1",
						CreateBasic ("IMAGE", "JPEG", "1.1", false),
						CreateMultipart ("MULTIPART", "ALTERNATIVE", "1.2",
							CreateText ("TEXT", "PLAIN", "1.2.1", false),
							CreateText ("TEXT", "HTML", "1.2.2", false)
						)
					),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};
			var cid = "alternative@localhost.com";
			var mixed = (BodyPartMultipart) summary.Body;
			var related = (BodyPartMultipart) mixed.BodyParts[0];
			var alternative = (BodyPartMultipart) related.BodyParts[1];

			related.ContentType.Parameters["start"] = cid;
			alternative.ContentLocation = new Uri ("cid:" + cid);

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.2.1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.2.2"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (4), "BodyParts");
		}

		[Test]
		public void TestMixedRelatedAlternative ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateMultipart ("MULTIPART", "RELATED", "1",
						CreateMultipart ("MULTIPART", "ALTERNATIVE", "1.1",
							CreateText ("TEXT", "PLAIN", "1.1.1", false),
							CreateText ("TEXT", "HTML", "1.1.2", false)
						),
						CreateBasic ("IMAGE", "JPEG", "1.2", false)
					),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1.1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1.2"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (4), "BodyParts");
		}

		[Test]
		public void TestMixedNestedAlternative ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateMultipart ("MULTIPART", "MIXED", "",
					CreateMultipart ("MULTIPART", "ALTERNATIVE", "1",
						CreateMultipart ("MULTIPART", "ALTERNATIVE", "1.1",
							CreateText ("TEXT", "PLAIN", "1.1.1", false),
							CreateText ("TEXT", "HTML", "1.1.2", false)
						)
					),
					CreateBasic ("IMAGE", "JPEG", "2", true)
				)
			};

			var plain = summary.TextBody;
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1.1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1.2"), "HtmlBody");

			Assert.That (summary.Attachments.Count (), Is.EqualTo (1), "Attachments");
			Assert.That (summary.BodyParts.Count (), Is.EqualTo (3), "BodyParts");
		}

		[Test]
		public void TestComplexBody ()
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
			Assert.That (plain, Is.Not.Null, "TextBody");
			Assert.That (plain.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.1"), "TextBody");

			var html = summary.HtmlBody;
			Assert.That (html, Is.Not.Null, "HtmlBody");
			Assert.That (html.ContentType.Parameters["part-specifier"], Is.EqualTo ("1.2.1"), "HtmlBody");

			var bodyParts = new string [] { "1.1", "1.2.1", "1.2.2", "2", "3", "4" };
			i = 0;

			foreach (var part in summary.BodyParts)
				Assert.That (part.ContentType.Parameters["part-specifier"], Is.EqualTo (bodyParts[i++]), "BodyParts");

			var attachments = new string[] { "2", "3", "4" };
			i = 0;

			foreach (var attachment in summary.Attachments)
				Assert.That (attachment.ContentType.Parameters["part-specifier"], Is.EqualTo (attachments[i++]), "Attachments");
		}
	}
}
