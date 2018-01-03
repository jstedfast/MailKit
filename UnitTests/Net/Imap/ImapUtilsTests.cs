//
// ImapBodyParsingTests.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;
using MimeKit.Utils;

using MailKit.Net.Imap;
using MailKit;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapUtilsTests
	{
		[Test]
		public void TestResponseCodeCreation ()
		{
			foreach (ImapResponseCodeType type in Enum.GetValues (typeof (ImapResponseCodeType)))
				Assert.DoesNotThrow (() => ImapResponseCode.Create (type));
		}

		[Test]
		public void TestFormattingSimpleUidRange ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (4), new UniqueId (5), new UniqueId (6),
				new UniqueId (7), new UniqueId (8), new UniqueId (9)
			};
			const string expect = "1:9";
			string actual;

			actual = ImapUtils.FormatUidSet (uids);
			Assert.AreEqual (expect, actual, "Formatting a simple range of uids failed.");
		}

		[Test]
		public void TestFormattingNonSequentialUids ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (3), new UniqueId (5),
				new UniqueId (7), new UniqueId (9)
			};
			const string expect = "1,3,5,7,9";
			string actual;

			actual = ImapUtils.FormatUidSet (uids);
			Assert.AreEqual (expect, actual, "Formatting a non-sequential list of uids.");
		}

		[Test]
		public void TestFormattingComplexSetOfUids ()
		{
			UniqueId[] uids = {
				new UniqueId (1), new UniqueId (2), new UniqueId (3),
				new UniqueId (5), new UniqueId (6), new UniqueId (9),
				new UniqueId (10), new UniqueId (11), new UniqueId (12),
				new UniqueId (15), new UniqueId (19), new UniqueId (20)
			};
			const string expect = "1:3,5:6,9:12,15,19:20";
			string actual;

			actual = ImapUtils.FormatUidSet (uids);
			Assert.AreEqual (expect, actual, "Formatting a complex list of uids.");
		}

		[Test]
		public void TestFormattingReversedUids ()
		{
			UniqueId[] uids = {
				new UniqueId (20), new UniqueId (19), new UniqueId (15),
				new UniqueId (12), new UniqueId (11), new UniqueId (10),
				new UniqueId (9), new UniqueId (6), new UniqueId (5),
				new UniqueId (3), new UniqueId (2), new UniqueId (1)
			};
			const string expect = "20:19,15,12:9,6:5,3:1";
			string actual;

			actual = ImapUtils.FormatUidSet (uids);
			Assert.AreEqual (expect, actual, "Formatting a complex list of uids.");
		}

		[Test]
		public void TestParseLabelsListWithNIL ()
		{
			const string text = "(atom-label \\flag-label \"quoted-label\" NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<string> labels;

						engine.SetStream (tokenizer);

						try {
							labels = ImapUtils.ParseLabelsListAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing X-GM-LABELS failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);
					}
				}
			}
		}

		[Test]
		public void TestParseExampleBodyRfc3501 ()
		{
			const string text = "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"US-ASCII\") NIL NIL \"7BIT\" 3028 92)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartText basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODY failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartText> (body, "Body types did not match.");
						basic = (BodyPartText) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("text", "plain"), "Content-Type did not match.");
						Assert.AreEqual ("US-ASCII", body.ContentType.Parameters["charset"], "charset param did not match");

						Assert.IsNotNull (basic, "The parsed body is not BodyPartText.");
						Assert.AreEqual ("7BIT", basic.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (3028, basic.Octets, "Octet count did not match.");
						Assert.AreEqual (92, basic.Lines, "Line count did not match.");
					}
				}
			}
		}

		[Test]
		public void TestParseExampleEnvelopeRfc3501 ()
		{
			const string text = "(\"Wed, 17 Jul 1996 02:23:25 -0700 (PDT)\" \"IMAP4rev1 WG mtg summary and minutes\" ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL \"<B27397-0100000@cac.washington.edu>\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = ImapUtils.ParseEnvelopeAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

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
				}
			}
		}

		[Test]
		public void TestParseMalformedMailboxAddressInEnvelope ()
		{
			const string text = "(\"Mon, 10 Apr 2017 06:04:00 -0700\" \"Session 2: Building the meditation habit\" ((\"Headspace\" NIL \"members\" \"headspace.com\")) ((NIL NIL \"<members=headspace.com\" \"members.headspace.com>\")) ((\"Headspace\" NIL \"members\" \"headspace.com\")) ((NIL NIL \"user\" \"gmail.com\")) NIL NIL NIL \"<bvqyalstpemxt9y3afoqh4an62b2arcd.rcd.1491829440@members.headspace.com>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = ImapUtils.ParseEnvelopeAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
						Assert.AreEqual ("Mon, 10 Apr 2017 06:04:00 -0700", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("Session 2: Building the meditation habit", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"Headspace\" <members@headspace.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("members=headspace.com@members.headspace.com", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual ("\"Headspace\" <members@headspace.com>", envelope.ReplyTo.ToString (), "Reply-To does not match.");

						Assert.AreEqual (1, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual ("user@gmail.com", envelope.To.ToString (), "To does not match.");

						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");

						Assert.AreEqual ("bvqyalstpemxt9y3afoqh4an62b2arcd.rcd.1491829440@members.headspace.com", envelope.MessageId, "Message-Id does not match.");
					}
				}
			}
		}

		[Test]
		public void TestParseDovcotEnvelopeWithGroupAddresses ()
		{
			const string text = "(\"Mon, 13 Jul 2015 21:15:32 -0400\" \"Test message\" ((\"Example From\" NIL \"from\" \"example.com\")) ((\"Example Sender\" NIL \"sender\" \"example.com\")) ((\"Example Reply-To\" NIL \"reply-to\" \"example.com\")) ((NIL NIL \"boys\" NIL)(NIL NIL \"aaron\" \"MISSING_DOMAIN\")(NIL NIL \"jeff\" \"MISSING_DOMAIN\")(NIL NIL \"zach\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)(NIL NIL \"girls\" NIL)(NIL NIL \"alice\" \"MISSING_DOMAIN\")(NIL NIL \"hailey\" \"MISSING_DOMAIN\")(NIL NIL \"jenny\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)) NIL NIL NIL \"<MV4F9T0FLVT4.2CZLZPO4HZ8B3@Jeffreys-MacBook-Air.local>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = ImapUtils.ParseEnvelopeAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.AreEqual ("\"Example Sender\" <sender@example.com>", envelope.Sender.ToString ());
						Assert.AreEqual ("\"Example From\" <from@example.com>", envelope.From.ToString ());
						Assert.AreEqual ("\"Example Reply-To\" <reply-to@example.com>", envelope.ReplyTo.ToString ());
						Assert.AreEqual ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;", envelope.To.ToString ());
					}
				}
			}
		}

		[Test]
		public void TestParseExampleMultiLevelDovecotBodyStructure ()
		{
			const string text = "(((\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 28 2 NIL NIL NIL NIL) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 1707 65 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") NIL NIL NIL) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 641 (\"Sat, 8 Jan 2011 14:16:36 +0100\" \"Subj 2\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) (\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 185 18 NIL NIL (\"cs\") NIL) 31 NIL (\"attachment\" NIL) NIL NIL) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 50592 (\"Sat, 8 Jan 2011 13:58:39 +0100\" \"Subj 1\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) ( (\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 4296 345 NIL NIL NIL NIL) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 45069 1295 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"----=_NextPart_000_0073_01CBB179.57530990\") NIL (\"cs\") NIL) 1669 NIL (\"attachment\" NIL) NIL NIL) \"mixed\" (\"boundary\" \"----=_NextPart_000_0077_01CBB179.57530990\") NIL (\"cs\") NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_000_0077_01CBB179.57530990", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");
						Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[0], "The type of the first child does not match.");
						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[1], "The type of the second child does not match.");
						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[2], "The type of the third child does not match.");

						// FIXME: assert more stuff?
					}
				}
			}
		}

		// Note: This tests the work-around for issue #485
		[Test]
		public void TestParseBadlyQuotedBodyStructure ()
		{
			const string text = "((\"MOUNDARY=\"_006_5DBB50A5A54730AD4A54730AD4A54730AD4A54730AD42KOS_\"\" \"OCTET-STREAM\" (\"name\" \"test.dat\") NIL NIL \"quoted-printable\" 383137 NIL (\"attachment\" (\"filename\" \"test.dat\")))(\"MOUNDARY=\"_006_5DBB50A5D3ABEC4E85A03EAD527CA5474B3D0AF9E6EXMBXSVR02KOS_\"\" \"OCTET-STREAM\" (\"name\" \"test.dat\") NIL NIL \"quoted-printable\" 383137 NIL (\"attachment\" (\"filename\" \"test.dat\"))) \"MIXED\" (\"boundary\" \"----=_NextPart_000_730AD4A547.730AD4A547F40\"))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_000_730AD4A547.730AD4A547F40", body.ContentType.Parameters ["boundary"], "boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.AreEqual ("MOUNDARY=\"_006_5DBB50A5A54730AD4A54730AD4A54730AD4A54730AD42KOS_\"", basic.ContentType.MediaType, "ContentType.MediaType does not match for first child.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("MOUNDARY=\"_006_5DBB50A5D3ABEC4E85A03EAD527CA5474B3D0AF9E6EXMBXSVR02KOS_\"", basic.ContentType.MediaType, "ContentType.MediaType does not match for second child.");
					}
				}
			}
		}

		[Test]
		public void TestParseMultipartBodyStructureWithNilBodyFldParam ()
		{
			const string text = "(((\"text\" \"plain\" (\"charset\" \"UTF-8\") NIL NIL \"7bit\" 148 12 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"UTF-8\") NIL NIL \"quoted-printable\" 337 6 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"6c7f221bed92d80548353834d8e2\") NIL NIL NIL)((\"text\" \"plain\" (\"charset\" \"us-ascii\") NIL NIL \"7bit\" 0 0) \"x-zip\" NIL (\"attachment\" (\"filename\" \"YSOZ 265230.ZIP\")) NIL NIL) \"mixed\" (\"boundary\" \"c52bbfc0dd5365efa39b9f80eac3\") NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart, alternative, xzip;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("c52bbfc0dd5365efa39b9f80eac3", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[0], "The type of the first child does not match.");
						alternative = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.AreEqual ("alternative", alternative.ContentType.MediaSubtype, "Content-Type did not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[1], "The type of the second child does not match.");
						xzip = (BodyPartMultipart) multipart.BodyParts[1];
						Assert.AreEqual ("x-zip", xzip.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual (0, xzip.ContentType.Parameters.Count, "Content-Type should not have params.");
					}
				}
			}
		}

		[Test]
		public void TestParseExampleThreads ()
		{
			const string text = "(2)(3 6 (4 23)(44 7 96))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, null, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<MessageThread> threads;

						engine.SetStream (tokenizer);

						try {
							threads = ImapUtils.ParseThreadsAsync (engine, 0, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing THREAD response failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (2, threads.Count, "Expected 2 threads.");

						Assert.AreEqual ((uint) 2, threads[0].UniqueId.Value.Id);
						Assert.AreEqual ((uint) 3, threads[1].UniqueId.Value.Id);

						var branches = threads[1].Children.ToArray ();
						Assert.AreEqual (1, branches.Length, "Expected 1 child.");
						Assert.AreEqual ((uint) 6, branches[0].UniqueId.Value.Id);

						branches = branches[0].Children.ToArray ();
						Assert.AreEqual (2, branches.Length, "Expected 2 branches.");

						Assert.AreEqual ((uint) 4, branches[0].UniqueId.Value.Id);
						Assert.AreEqual ((uint) 44, branches[1].UniqueId.Value.Id);

						var children = branches[0].Children.ToArray ();
						Assert.AreEqual (1, children.Length, "Expected 1 child.");
						Assert.AreEqual ((uint) 23, children[0].UniqueId.Value.Id);
						Assert.AreEqual (0, children[0].Children.Count (), "Expected no children.");

						children = branches[1].Children.ToArray ();
						Assert.AreEqual (1, children.Length, "Expected 1 child.");
						Assert.AreEqual ((uint) 7, children[0].UniqueId.Value.Id);

						children = children[0].Children.ToArray ();
						Assert.AreEqual (1, children.Length, "Expected 1 child.");
						Assert.AreEqual ((uint) 96, children[0].UniqueId.Value.Id);
						Assert.AreEqual (0, children[0].Children.Count (), "Expected no children.");
					}
				}
			}
		}
	}
}
