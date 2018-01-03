//
// BodyPartTests.cs
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

using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;
using MimeKit.Utils;

using MailKit;

// Note: These tests are for BodyPart and Envelope's custom format. While the format is similar
//       to IMAP, it is not exactly the same. Do not assume that IMAP strings will work properly
//       with these parsers.

namespace UnitTests {
	[TestFixture]
	public class BodyPartTests
	{
		[Test]
		public void TestSimplePlainTextBody ()
		{
			const string text = "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"US-ASCII\") NIL NIL \"7BIT\" 3028 NIL NIL NIL NIL 92)";
			BodyPartText basic;
			BodyPart body;

			Assert.IsTrue (BodyPart.TryParse (text, out body), "Failed to parse body.");

			Assert.IsInstanceOf<BodyPartText> (body, "Body types did not match.");
			basic = (BodyPartText) body;

			Assert.IsTrue (body.ContentType.IsMimeType ("text", "plain"), "Content-Type did not match.");
			Assert.AreEqual ("US-ASCII", body.ContentType.Parameters["charset"], "charset param did not match");

			Assert.IsNotNull (basic, "The parsed body is not BodyPartText.");
			Assert.AreEqual ("7BIT", basic.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
			Assert.AreEqual (3028, basic.Octets, "Octet count did not match.");
			Assert.AreEqual (92, basic.Lines, "Line count did not match.");
		}

		[Test]
		public void TestExampleEnvelopeRfc3501 ()
		{
			const string text = "(\"Wed, 17 Jul 1996 02:23:25 -0700 (PDT)\" \"IMAP4rev1 WG mtg summary and minutes\" ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL \"<B27397-0100000@cac.washington.edu>\")";
			Envelope envelope;

			Assert.IsTrue (Envelope.TryParse (text, out envelope), "Failed to parse envelope.");

			Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
			Assert.AreEqual ("Wed, 17 Jul 1996 02:23:25 -0700", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
			Assert.AreEqual ("IMAP4rev1 WG mtg summary and minutes", envelope.Subject, "Subject does not match.");

			Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
			Assert.AreEqual ("\"Terry Gray\" <gray@cac.washington.edu>", envelope.From.ToString (), "From does not match.");

			Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
			Assert.AreEqual ("\"Terry Gray\" <gray@cac.washington.edu>", envelope.Sender.ToString (), "Sender does not match.");

			Assert.AreEqual (1, envelope.ReplyTo.Count, "Reply-To counts do not match.");
			Assert.AreEqual ("\"Terry Gray\" <gray@cac.washington.edu>", envelope.ReplyTo.ToString (), "Reply-To does not match.");

			Assert.AreEqual (1, envelope.To.Count, "To counts do not match.");
			Assert.AreEqual ("imap@cac.washington.edu", envelope.To.ToString (), "To does not match.");

			Assert.AreEqual (2, envelope.Cc.Count, "Cc counts do not match.");
			Assert.AreEqual ("minutes@CNRI.Reston.VA.US, \"John Klensin\" <KLENSIN@MIT.EDU>", envelope.Cc.ToString (), "Cc does not match.");

			Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

			Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");

			Assert.AreEqual ("B27397-0100000@cac.washington.edu", envelope.MessageId, "Message-Id does not match.");
		}

		[Test]
		public void TestNestedBodyStructure ()
		{
			const string text = "(((\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 28 NIL NIL NIL NIL 2) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 1707 NIL NIL NIL NIL 65) \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") NIL NIL NIL) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 641 NIL (\"attachment\" NIL) NIL NIL (\"Sat, 08 Jan 2011 14:16:36 +0100\" \"Subj 2\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) (\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 185 NIL NIL (\"cs\") NIL 18) 31) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 50592 NIL (\"attachment\" NIL) NIL NIL (\"Sat, 08 Jan 2011 13:58:39 +0100\" \"Subj 1\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) ((\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 4296 NIL NIL NIL NIL 345) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 45069 NIL NIL NIL NIL 1295) \"alternative\" (\"boundary\" \"----=_NextPart_000_0073_01CBB179.57530990\") NIL (\"cs\") NIL) 1669) \"mixed\" (\"boundary\" \"----=_NextPart_000_0077_01CBB179.57530990\") NIL (\"cs\") NIL)";
			BodyPartMultipart multipart;
			BodyPart body;

			Assert.IsTrue (BodyPart.TryParse (text, out body), "Failed to parse body.");

			Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
			multipart = (BodyPartMultipart) body;

			Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
			Assert.AreEqual ("----=_NextPart_000_0077_01CBB179.57530990", body.ContentType.Parameters["boundary"], "boundary param did not match");
			Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");
			Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[0], "The type of the first child does not match.");
			Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[1], "The type of the second child does not match.");
			Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[2], "The type of the third child does not match.");

			// FIXME: assert more stuff
		}

		static ContentType CreateContentType (string type, string subtype, string partSpecifier)
		{
			var contentType = new ContentType (type, subtype);
			contentType.Parameters.Add ("part-specifier", partSpecifier);
			return contentType;
		}

		static BodyPartMessage CreateMessage (string type, string subtype, string partSpecifier, BodyPart body)
		{
			var message = new BodyPartMessage { ContentType = CreateContentType (type, subtype, partSpecifier) };
			message.Body = body;
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

		static BodyPartBasic CreateText (string type, string subtype, string partSpecifier)
		{
			return new BodyPartText { ContentType = CreateContentType (type, subtype, partSpecifier) };
		}

		static void VerifyPartSpecifier (BodyPart part)
		{
			var expected = part.ContentType.Parameters["part-specifier"];

			Assert.AreEqual (expected, part.PartSpecifier, "The part-specifier does not match for {0}", part.ContentType.MimeType);

			var message = part as BodyPartMessage;
			if (message != null) {
				VerifyPartSpecifier (message.Body);
				return;
			}

			var multipart = part as BodyPartMultipart;
			if (multipart != null) {
				for (int i = 0; i < multipart.BodyParts.Count; i++)
					VerifyPartSpecifier (multipart.BodyParts[i]);
				return;
			}
		}

		[Test]
		public void TestComplexPartSpecifiersExampleRfc3501 ()
		{
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

			var encoded = body.ToString ();

			Assert.IsTrue (BodyPart.TryParse (encoded, out body));

			VerifyPartSpecifier (body);
		}
	}
}
