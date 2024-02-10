//
// BodyPartTests.cs
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

using System.Text;
using System.Collections;

using MimeKit;
using MailKit;

// Note: These tests are for BodyPart and Envelope's custom format. While the format is similar
//       to IMAP, it is not exactly the same. Do not assume that IMAP strings will work properly
//       with these parsers.

namespace UnitTests {
	[TestFixture]
	public class BodyPartTests
	{
		[Test]
		public void TestBodyPartBasic ()
		{
			var uri = new Uri ("https://www.nationalgeographic.com/travel/contests/photographer-of-the-year-2018/wallpapers/week-9-nature/2/");
			const string expected = "(\"image\" \"jpeg\" (\"name\" \"wallpaper.jpg\") \"id@localhost\" \"A majestic supercell storm approaching a house in Kansas, 2016.\" \"base64\" 0 \"8criUiOQmpfifOuOmYFtEQ==\" (\"attachment\" (\"filename\" \"wallpaper.jpg\")) (\"en\" \"fr\") \"https://www.nationalgeographic.com/travel/contests/photographer-of-the-year-2018/wallpapers/week-9-nature/2/\")";
			BodyPartBasic basic, parsed;
			BodyPart body;

			basic = new BodyPartBasic {
				ContentType = new ContentType ("image", "jpeg") {
					Name = "wallpaper.jpg"
				},
				ContentId = "id@localhost",
				ContentMd5 = "8criUiOQmpfifOuOmYFtEQ==",
				ContentLanguage = new string[] { "en", "fr" },
				ContentLocation = uri,
				ContentDescription = "A majestic supercell storm approaching a house in Kansas, 2016.",
				ContentDisposition = new ContentDisposition (ContentDisposition.Attachment) {
					FileName = "wallpaper.jpg"
				},
				ContentTransferEncoding = "base64"
			};

			Assert.That (basic.IsAttachment, Is.True);
			Assert.That (basic.FileName, Is.EqualTo ("wallpaper.jpg"));
			Assert.That (basic.ToString (), Is.EqualTo (expected));
			Assert.That (BodyPart.TryParse (expected, out body), Is.True);
			Assert.That (body, Is.InstanceOf<BodyPartBasic> ());

			parsed = (BodyPartBasic) body;
			Assert.That (parsed.ToString (), Is.EqualTo (expected));
		}

		[Test]
		public void TestNilSerialization ()
		{
			var builder = new StringBuilder ();

			BodyPart.Encode (builder, (BodyPart) null);
			Assert.That (builder.ToString (), Is.EqualTo ("NIL"), "BodyPart");

			builder.Clear ();
			BodyPart.Encode (builder, (BodyPartCollection) null);
			Assert.That (builder.ToString (), Is.EqualTo ("NIL"), "BodyPartCollection");

			builder.Clear ();
			BodyPart.Encode (builder, (ContentDisposition) null);
			Assert.That (builder.ToString (), Is.EqualTo ("NIL"), "ContentDisposition");

			//builder.Clear ();
			//BodyPart.Encode (builder, (ContentType) null);
			//Assert.That (builder.ToString (), Is.EqualTo ("NIL"), "ContentType");

			builder.Clear ();
			BodyPart.Encode (builder, (Envelope) null);
			Assert.That (builder.ToString (), Is.EqualTo ("NIL"), "Envelope");

			builder.Clear ();
			BodyPart.Encode (builder, (IList<Parameter>) null);
			Assert.That (builder.ToString (), Is.EqualTo ("NIL"), "IEnumerable<Parameter>");
		}

		[Test]
		public void TestSimplePlainTextBody ()
		{
			const string expected = "(\"text\" \"plain\" (\"charset\" \"us-ascii\" \"name\" \"body.txt\") NIL NIL \"7bit\" 3028 NIL NIL NIL NIL 92)";
			BodyPartText text, parsed;
			BodyPart body;

			text = new BodyPartText {
				ContentType = new ContentType ("text", "plain") { Charset = "us-ascii", Name = "body.txt" },
				ContentTransferEncoding = "7bit",
				Octets = 3028,
				Lines = 92,
			};

			Assert.That (text.IsPlain, Is.True);
			Assert.That (text.IsHtml, Is.False);
			Assert.That (text.IsAttachment, Is.False);
			Assert.That (text.FileName, Is.EqualTo ("body.txt"));
			Assert.That (text.ToString (), Is.EqualTo (expected));
			Assert.That (BodyPart.TryParse (expected, out body), Is.True);
			Assert.That (body, Is.InstanceOf<BodyPartText> ());

			parsed = (BodyPartText) body;
			Assert.That (parsed.ContentType.IsMimeType ("text", "plain"), Is.True, "Content-Type did not match.");
			Assert.That (parsed.ContentType.Charset, Is.EqualTo ("us-ascii"), "charset param did not match");
			Assert.That (parsed.ContentType.Name, Is.EqualTo ("body.txt"), "name param did not match");
			Assert.That (parsed.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
			Assert.That (parsed.Octets, Is.EqualTo (3028), "Octet count did not match.");
			Assert.That (parsed.Lines, Is.EqualTo (92), "Line count did not match.");
			Assert.That (parsed.ToString (), Is.EqualTo (expected));
		}

		[Test]
		public void TestBodyPartCollection ()
		{
			var text = new BodyPartText { ContentType = new ContentType ("text", "plain"), ContentLocation = new Uri ("body", UriKind.Relative) };
			var image1 = new BodyPartBasic { ContentType = new ContentType ("image", "jpeg"), ContentLocation = new Uri ("http://localhost/image1.jpg") };
			var image2 = new BodyPartBasic { ContentType = new ContentType ("image", "jpeg"), ContentId = "image2@localhost" };
			var list = new BodyPartCollection ();
			var parts = new BodyPart[3];
			int i = 0;

			Assert.Throws<ArgumentNullException> (() => list.Add (null));
			Assert.Throws<ArgumentNullException> (() => list.Remove (null));
			Assert.Throws<ArgumentNullException> (() => list.Contains (null));
			Assert.Throws<ArgumentNullException> (() => list.IndexOf (null));
			Assert.Throws<ArgumentNullException> (() => list.CopyTo (null, 0));
			Assert.Throws<ArgumentOutOfRangeException> (() => list.CopyTo (parts, -1));
			Assert.Throws<ArgumentOutOfRangeException> (() => { var x = list[0]; });

			Assert.That (list.IsReadOnly, Is.False);
			Assert.That (list, Is.Empty);

			list.Add (text);
			Assert.That (list, Has.Count.EqualTo (1));
			Assert.That (list, Does.Contain (text));
			Assert.That (list, Does.Not.Contain (image1));
			Assert.That (list.IndexOf (new Uri ("body", UriKind.Relative)), Is.EqualTo (0));
			Assert.That (list.IndexOf (new Uri ("http://localhost/image1.jpg")), Is.EqualTo (-1));
			Assert.That (list.IndexOf (new Uri ("cid:image2@localhost")), Is.EqualTo (-1));
			Assert.That (list[0], Is.EqualTo (text));

			list.Add (image1);
			Assert.That (list, Has.Count.EqualTo (2));
			Assert.That (list, Does.Contain (text));
			Assert.That (list, Does.Contain (image1));
			Assert.That (list.IndexOf (new Uri ("body", UriKind.Relative)), Is.EqualTo (0));
			Assert.That (list.IndexOf (new Uri ("http://localhost/image1.jpg")), Is.EqualTo (1));
			Assert.That (list.IndexOf (new Uri ("cid:image2@localhost")), Is.EqualTo (-1));
			Assert.That (list[0], Is.EqualTo (text));
			Assert.That (list[1], Is.EqualTo (image1));

			Assert.That (list.Remove (text), Is.True);
			Assert.That (list, Has.Count.EqualTo (1));
			Assert.That (list, Does.Not.Contain (text));
			Assert.That (list, Does.Contain (image1));
			Assert.That (list.IndexOf (new Uri ("body", UriKind.Relative)), Is.EqualTo (-1));
			Assert.That (list.IndexOf (new Uri ("http://localhost/image1.jpg")), Is.EqualTo (0));
			Assert.That (list.IndexOf (new Uri ("cid:image2@localhost")), Is.EqualTo (-1));
			Assert.That (list[0], Is.EqualTo (image1));

			list.Clear ();
			Assert.That (list, Is.Empty);

			list.Add (text);
			list.Add (image1);
			list.Add (image2);
			list.CopyTo (parts, 0);
			Assert.That (list.IndexOf (new Uri ("body", UriKind.Relative)), Is.EqualTo (0));
			Assert.That (list.IndexOf (new Uri ("http://localhost/image1.jpg")), Is.EqualTo (1));
			Assert.That (list.IndexOf (new Uri ("cid:image2@localhost")), Is.EqualTo (2));

			foreach (var part in list)
				Assert.That (part, Is.EqualTo (parts[i++]));

			i = 0;
			foreach (var part in (IEnumerable) list)
				Assert.That (part, Is.EqualTo (parts[i++]));
		}

		[Test]
		public void TestNestedBodyStructure ()
		{
			const string text = "(((\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 28 NIL NIL NIL NIL 2) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 1707 NIL NIL NIL NIL 65) \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") NIL NIL NIL) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 641 NIL (\"attachment\" NIL) NIL NIL (\"Sat, 08 Jan 2011 14:16:36 +0100\" \"Subj 2\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) (\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 185 NIL NIL (\"cs\") NIL 18) 31) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 50592 NIL (\"attachment\" NIL) NIL NIL (\"Sat, 08 Jan 2011 13:58:39 +0100\" \"Subj 1\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) ((\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 4296 NIL NIL NIL NIL 345) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 45069 NIL NIL NIL NIL 1295) \"alternative\" (\"boundary\" \"----=_NextPart_000_0073_01CBB179.57530990\") NIL (\"cs\") NIL) 1669) \"mixed\" (\"boundary\" \"----=_NextPart_000_0077_01CBB179.57530990\") NIL (\"cs\") NIL)";
			BodyPartMultipart multipart;
			BodyPart body;

			Assert.That (BodyPart.TryParse (text, out body), Is.True, "Failed to parse body.");

			Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
			multipart = (BodyPartMultipart) body;

			Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
			Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_000_0077_01CBB179.57530990"), "boundary param did not match");
			Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");
			Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
			Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartMessage> (), "The type of the second child does not match.");
			Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "The type of the third child does not match.");

			// FIXME: assert more stuff
		}

		[Test]
		public void TestMultipartWithNoChildren ()
		{
			var original = new BodyPartMultipart {
				ContentType = new ContentType ("multipart", "mixed") { Boundary = "----=_NextPart_000_001" }
			};
			original.BodyParts.Add (new BodyPartMultipart {
				ContentType = new ContentType ("multipart", "alternative") { Boundary = "----=_AlternativePart_001_001" }
			});

			var serialized = original.ToString ();

			Assert.That (BodyPart.TryParse (serialized, out var body), Is.True, "Failed to parse.");
			Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");

			var multipart = (BodyPartMultipart) body;
			Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
			Assert.That (multipart.ContentType.Boundary, Is.EqualTo (original.ContentType.Boundary), "boundary param did not match");
			Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "BodyParts count does not match.");
			Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");

			var alternative = (BodyPartMultipart) multipart.BodyParts[0];
			Assert.That (alternative.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "Inner Content-Type did not match.");
			Assert.That (alternative.ContentType.Boundary, Is.EqualTo (original.BodyParts[0].ContentType.Boundary), "Inner boundary param did not match");
			Assert.That (alternative.BodyParts, Is.Empty, "Inner BodyParts count does not match.");
		}

		static ContentType CreateContentType (string type, string subtype, string partSpecifier)
		{
			var contentType = new ContentType (type, subtype);
			contentType.Parameters.Add ("part-specifier", partSpecifier);
			return contentType;
		}

		static BodyPartMessage CreateMessage (string type, string subtype, string partSpecifier, BodyPart body)
		{
			var message = new BodyPartMessage {
				ContentType = CreateContentType (type, subtype, partSpecifier),
				Body = body
			};
			return message;
		}

		static BodyPartMultipart CreateMultipart (string type, string subtype, string partSpecifier, params BodyPart[] bodyParts)
		{
			var multipart = new BodyPartMultipart { ContentType = CreateContentType (type, subtype, partSpecifier) };
			foreach (var bodyPart in bodyParts)
				multipart.BodyParts.Add (bodyPart);
			return multipart;
		}

		static BodyPartBasic CreateBasic (string type, string subtype, string partSpecifier)
		{
			return new BodyPartBasic { ContentType = CreateContentType (type, subtype, partSpecifier) };
		}

		static BodyPartText CreateText (string type, string subtype, string partSpecifier)
		{
			return new BodyPartText { ContentType = CreateContentType (type, subtype, partSpecifier) };
		}

		static void VerifyPartSpecifier (BodyPart part)
		{
			var expected = part.ContentType.Parameters["part-specifier"];

			Assert.That (part.PartSpecifier, Is.EqualTo (expected), $"The part-specifier does not match for {part.ContentType.MimeType}");

			if (part is BodyPartMessage message) {
				VerifyPartSpecifier (message.Body);
				return;
			}

			if (part is BodyPartMultipart multipart) {
				for (int i = 0; i < multipart.BodyParts.Count; i++)
					VerifyPartSpecifier (multipart.BodyParts[i]);
				return;
			}
		}

		class TestVisitor : BodyPartVisitor
		{
			readonly StringBuilder builder = new StringBuilder ();
			int indent;

			public override void Visit (BodyPart body)
			{
				builder.Length = 0;
				indent = 0;

				base.Visit (body);
			}

			protected internal override void VisitBodyPart (BodyPart entity)
			{
				builder.Append (' ', indent);
				builder.Append (entity.ContentType.MimeType);
				builder.Append ('\n');

				base.VisitBodyPart (entity);
			}

			protected override void VisitMessage (BodyPart message)
			{
				indent++;
				base.VisitMessage (message);
				indent--;
			}

			protected override void VisitChildren (BodyPartMultipart multipart)
			{
				indent++;
				base.VisitChildren (multipart);
				indent--;
			}

			public override string ToString ()
			{
				return builder.ToString ();
			}
		}

		[Test]
		public void TestComplexPartSpecifiersExampleRfc3501 ()
		{
			const string expected = "MULTIPART/MIXED\n TEXT/PLAIN\n APPLICATION/OCTET-STREAM\n MESSAGE/RFC822\n  MULTIPART/MIXED\n   TEXT/PLAIN\n   APPLICATION/OCTET-STREAM\n MULTIPART/MIXED\n  IMAGE/GIF\n  MESSAGE/RFC822\n   MULTIPART/MIXED\n    TEXT/PLAIN\n    MULTIPART/ALTERNATIVE\n     TEXT/PLAIN\n     TEXT/RICHTEXT\n";
			var visitor = new TestVisitor ();

			BodyPart body = CreateMultipart ("MULTIPART", "MIXED", "",
				CreateText ("TEXT", "PLAIN", "1"),
				CreateBasic ("APPLICATION", "OCTET-STREAM", "2"),
				CreateMessage ("MESSAGE", "RFC822", "3",
					CreateMultipart ("MULTIPART", "MIXED", "3",
						CreateText ("TEXT", "PLAIN", "3.1"),
						CreateBasic ("APPLICATION", "OCTET-STREAM", "3.2")
					)
				),
				CreateMultipart ("MULTIPART", "MIXED", "4",
					CreateBasic ("IMAGE", "GIF", "4.1"),
					CreateMessage ("MESSAGE", "RFC822", "4.2",
						CreateMultipart ("MULTIPART", "MIXED", "4.2",
							CreateText ("TEXT", "PLAIN", "4.2.1"),
							CreateMultipart ("MULTIPART", "ALTERNATIVE", "4.2.2",
								CreateText ("TEXT", "PLAIN", "4.2.2.1"),
								CreateText ("TEXT", "RICHTEXT", "4.2.2.2")
							)
						)
					)
				)
			);

			visitor.Visit (body);

			Assert.That (visitor.ToString (), Is.EqualTo (expected));
			Assert.Throws<ArgumentNullException> (() => new BodyPartText ().Accept (null));
			Assert.Throws<ArgumentNullException> (() => new BodyPartBasic ().Accept (null));
			Assert.Throws<ArgumentNullException> (() => new BodyPartMessage ().Accept (null));
			Assert.Throws<ArgumentNullException> (() => new BodyPartMultipart ().Accept (null));

			var encoded = body.ToString ();

			Assert.Throws<ArgumentNullException> (() => BodyPart.TryParse (null, out body));
			Assert.That (BodyPart.TryParse (encoded, out body), Is.True);

			VerifyPartSpecifier (body);
		}
	}
}
