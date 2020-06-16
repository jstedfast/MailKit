//
// MessageSummaryTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Linq;
using System.Collections.Generic;

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
			Assert.Throws<ArgumentNullException> (() => new MessageSummary (null, 0));
		}

		[Test]
		public void TestDefaultValues ()
		{
			var summary = new MessageSummary (17);

			Assert.AreEqual (0, summary.Attachments.Count (), "Attachments");
			Assert.IsNull (summary.Body, "Body");
			Assert.AreEqual (0, summary.BodyParts.Count (), "BodyParts");
			Assert.AreEqual (DateTimeOffset.MinValue, summary.Date, "Date");
			Assert.IsNull (summary.Envelope, "Envelope");
			Assert.IsNull (summary.Flags, "Flags");
			Assert.IsNull (summary.GMailLabels, "GMailLabels");
			Assert.IsNull (summary.GMailMessageId, "GMailMessageId");
			Assert.IsNull (summary.GMailThreadId, "GMailThreadId");
			Assert.IsNull (summary.Headers, "Headers");
			Assert.IsNull (summary.HtmlBody, "HtmlBody");
			Assert.AreEqual (17, summary.Index, "Index");
			Assert.IsNull (summary.InternalDate, "InternalDate");
			Assert.IsFalse (summary.IsReply, "IsReply");
			Assert.IsNull (summary.ModSeq, "ModSeq");
			Assert.AreEqual (string.Empty, summary.NormalizedSubject, "NormalizedSubject");
			Assert.IsNull (summary.PreviewText, "PreviewText");
			Assert.IsNull (summary.References, "References");
			Assert.IsNull (summary.Size, "Size");
			Assert.IsNull (summary.TextBody, "TextBody");
			Assert.AreEqual (UniqueId.Invalid, summary.UniqueId, "UniqueId");
			Assert.IsNotNull (summary.UserFlags, "UserFlags");
			Assert.AreEqual (0, summary.UserFlags.Count, "UserFlags");
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

			Assert.AreEqual (0, summary.GMailLabels.Count, "GMailLabels");
			Assert.AreEqual (msgid, summary.GMailMessageId, "GMailMessageId");
			Assert.AreEqual (thrid, summary.GMailThreadId, "GMailThreadId");
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
		public void TestTextPlainBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateText ("TEXT", "PLAIN", "1", false)
			};

			var plain = summary.TextBody;
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.IsNull (html, "HtmlBody");

			Assert.AreEqual (0, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (1, summary.BodyParts.Count (), "BodyParts");
		}

		[Test]
		public void TestTextHtmlBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateText ("TEXT", "HTML", "1", false)
			};

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			var plain = summary.TextBody;
			Assert.IsNull (plain, "TextBody");

			Assert.AreEqual (0, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (1, summary.BodyParts.Count (), "BodyParts");
		}

		[Test]
		public void TestImageJpegBody ()
		{
			var summary = new MessageSummary (0) {
				Body = CreateBasic ("IMAGE", "JPEG", "1", false)
			};

			var plain = summary.TextBody;
			Assert.IsNull (plain, "TextBody");

			var html = summary.HtmlBody;
			Assert.IsNull (html, "HtmlBody");

			Assert.AreEqual (0, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (1, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("2", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (0, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (2, summary.BodyParts.Count (), "BodyParts");
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
			Assert.IsNull (plain, "TextBody");

			var html = summary.HtmlBody;
			Assert.IsNull (html, "HtmlBody");

			Assert.AreEqual (0, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (2, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.IsNull (html, "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (2, summary.BodyParts.Count (), "BodyParts");
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
			Assert.IsNull (plain, "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (2, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.IsNull (html, "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (2, summary.BodyParts.Count (), "BodyParts");
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
			Assert.IsNull (plain, "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1", html.ContentType.Parameters ["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (2, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1.2.1", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (4, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1.2.2", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (4, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1.2.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1.2.2", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (4, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1.1.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1.1.2", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (4, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1.1.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
			Assert.AreEqual ("1.1.2", html.ContentType.Parameters["part-specifier"], "HtmlBody");

			Assert.AreEqual (1, summary.Attachments.Count (), "Attachments");
			Assert.AreEqual (3, summary.BodyParts.Count (), "BodyParts");
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
			Assert.NotNull (plain, "TextBody");
			Assert.AreEqual ("1.1", plain.ContentType.Parameters["part-specifier"], "TextBody");

			var html = summary.HtmlBody;
			Assert.NotNull (html, "HtmlBody");
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
