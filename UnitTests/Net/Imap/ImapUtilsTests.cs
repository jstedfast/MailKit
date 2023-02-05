//
// ImapBodyParsingTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2023 .NET Foundation and Contributors
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

using NUnit.Framework;

using MimeKit;
using MimeKit.Utils;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapUtilsTests
	{
		readonly ImapEngine engine = new ImapEngine (null);

		[Test]
		public void TestResponseCodeCreation ()
		{
			foreach (ImapResponseCodeType type in Enum.GetValues (typeof (ImapResponseCodeType)))
				Assert.DoesNotThrow (() => ImapResponseCode.Create (type));
		}

		[Test]
		public void TestFormattingSimpleIndexRange ()
		{
			int[] indexes = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
			const string expect = "1:9";
			string actual;

			Assert.Throws<ArgumentNullException> (() => ImapUtils.FormatIndexSet (null, new int[1]));
			Assert.Throws<ArgumentNullException> (() => ImapUtils.FormatIndexSet (engine, null));
			Assert.Throws<ArgumentException> (() => ImapUtils.FormatIndexSet (engine, new int[0]));

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.AreEqual (expect, actual, "Formatting a simple range of indexes failed.");
		}

		[Test]
		public void TestFormattingNonSequentialIndexes ()
		{
			int[] indexes = { 0, 2, 4, 6, 8 };
			const string expect = "1,3,5,7,9";
			string actual;

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.AreEqual (expect, actual, "Formatting a non-sequential list of indexes.");
		}

		[Test]
		public void TestFormattingComplexSetOfIndexes ()
		{
			int[] indexes = { 0, 1, 2, 4, 5, 8, 9, 10, 11, 14, 18, 19 };
			const string expect = "1:3,5:6,9:12,15,19:20";
			string actual;

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.AreEqual (expect, actual, "Formatting a complex list of indexes.");
		}

		[Test]
		public void TestFormattingReversedIndexes ()
		{
			int[] indexes = { 19, 18, 14, 11, 10, 9, 8, 5, 4, 2, 1, 0 };
			const string expect = "20:19,15,12:9,6:5,3:1";
			string actual;

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.AreEqual (expect, actual, "Formatting a complex list of indexes.");
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

			actual = UniqueIdSet.ToString (uids);
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

			actual = UniqueIdSet.ToString (uids);
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

			actual = UniqueIdSet.ToString (uids);
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

			actual = UniqueIdSet.ToString (uids);
			Assert.AreEqual (expect, actual, "Formatting a complex list of uids.");
		}

		[Test]
		public void TestParseInvalidInternalDates ()
		{
			var internalDates = new string [] {
				"00-Jan-0000 00:00:00 +0000", // Note: This example is taken from an actual response from a Domino IMAP server. Likely represents an uninitialized value.
				"98765432100-OCT-2018 13:41:57 -0400",
				"27-JAG-2018 13:41:57 -0400",
				"27-OCT-1909 13:41:57 -0400",
				"27-OCT-2018 33:41:57 -0400",
				"27-OCT-2018 13:411:57 -0400",
				"27-OCT-2018 13:41:577 -0400",
				"27-OCT-2018 13:41:577 -98765432100",
				"27-OCT-2018 13:41:57 -0400 XYZ",
			};

			foreach (var internalDate in internalDates)
				Assert.AreEqual (DateTimeOffset.MinValue, ImapUtils.ParseInternalDate (internalDate), internalDate);
		}

		[Test]
		public void TestCanonicalizeMailboxName ()
		{
			Assert.AreEqual ("Name", ImapUtils.CanonicalizeMailboxName ("Name", '.'), "Name");
			Assert.AreEqual ("INBOX", ImapUtils.CanonicalizeMailboxName ("InbOx", '.'), "InbOx");
			Assert.AreEqual ("InboxSubfolder", ImapUtils.CanonicalizeMailboxName ("InboxSubfolder", '.'), "InboxSubfolder");
			Assert.AreEqual ("INBOX.Subfolder", ImapUtils.CanonicalizeMailboxName ("Inbox.Subfolder", '.'), "Inbox.Subfolder");
		}

		[Test]
		public void TestParseLabelsListWithNIL ()
		{
			const string text = "(atom-label \\flag-label \"quoted-label\" NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseLabelsListWithNILAsync ()
		{
			const string text = "(atom-label \\flag-label \"quoted-label\" NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<string> labels;

						engine.SetStream (tokenizer);

						try {
							labels = await ImapUtils.ParseLabelsListAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing X-GM-LABELS failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseExampleBodyRfc3501Async ()
		{
			const string text = "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"US-ASCII\") NIL NIL \"7BIT\" 3028 92)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartText basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODY failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseExampleEnvelopeRfc3501Async ()
		{
			const string text = "(\"Wed, 17 Jul 1996 02:23:25 -0700 (PDT)\" \"IMAP4rev1 WG mtg summary and minutes\" ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL \"<B27397-0100000@cac.washington.edu>\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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
		public void TestParseExampleEnvelopeRfc3501WithLiterals ()
		{
			const string text = "({37}\r\nWed, 17 Jul 1996 02:23:25 -0700 (PDT) {36}\r\nIMAP4rev1 WG mtg summary and minutes (({10}\r\nTerry Gray NIL {4}\r\ngray \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL {35}\r\n<B27397-0100000@cac.washington.edu>)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseExampleEnvelopeRfc3501WithLiteralsAsync ()
		{
			const string text = "({37}\r\nWed, 17 Jul 1996 02:23:25 -0700 (PDT) {36}\r\nIMAP4rev1 WG mtg summary and minutes (({10}\r\nTerry Gray NIL {4}\r\ngray \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL {35}\r\n<B27397-0100000@cac.washington.edu>)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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

		// This tests the work-around for issue #1369
		[Test]
		public void TestParseEnvelopeWithMiscalculatedLiteralMailboxName ()
		{
			const string text = "(\"Thu, 29 Apr 2021 10:57:07 +0000\" \"=?utf-8?B?0J/QsNGA0LrQuNC90LMg0L3QsCDQlNCw0L3QsNC40Lsg0JTQtdGH0LXQsg==?=\" (({38}\r\nРецепция Офис сграда \"Данаил Дечев\" №6 NIL \"facility\" \"xxxxxxxxxxx.com\")) NIL NIL ((\"Team\" NIL \"team\" \"xxxxxxxxxxx.com\")) NIL NIL NIL \"<d0f6ca6608cfb0b680b7b90824c79118@xxxxxxxxxxx.com>\")\r\n";

			// Note: The server appears to have calculated the literal length as the number of unicode *characters* as opposed to *bytes*. The actual literal length *should be* 69, not 38.
			using (var memory = new MemoryStream (Encoding.UTF8.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
						Assert.AreEqual ("Thu, 29 Apr 2021 10:57:07 +0000", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("Паркинг на Данаил Дечев", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"Рецепция Офис сграда \\\"Данаил Дечев\\\" №6\" <facility@xxxxxxxxxxx.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (0, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual (0, envelope.ReplyTo.Count, "Reply-To counts do not match.");

						Assert.AreEqual (1, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual ("\"Team\" <team@xxxxxxxxxxx.com>", envelope.To.ToString (), "To does not match.");

						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");

						Assert.AreEqual ("d0f6ca6608cfb0b680b7b90824c79118@xxxxxxxxxxx.com", envelope.MessageId, "Message-Id does not match.");
					}
				}
			}
		}

		// This tests the work-around for issue #1369
		[Test]
		public async Task TestParseEnvelopeWithMiscalculatedLiteralMailboxNameAsync ()
		{
			const string text = "(\"Thu, 29 Apr 2021 10:57:07 +0000\" \"=?utf-8?B?0J/QsNGA0LrQuNC90LMg0L3QsCDQlNCw0L3QsNC40Lsg0JTQtdGH0LXQsg==?=\" (({38}\r\nРецепция Офис сграда \"Данаил Дечев\" №6 NIL \"facility\" \"xxxxxxxxxxx.com\")) NIL NIL ((\"Team\" NIL \"team\" \"xxxxxxxxxxx.com\")) NIL NIL NIL \"<d0f6ca6608cfb0b680b7b90824c79118@xxxxxxxxxxx.com>\")\r\n";

			// Note: The server appears to have calculated the literal length as the number of unicode *characters* as opposed to *bytes*. The actual literal length *should be* 69, not 38.
			using (var memory = new MemoryStream (Encoding.UTF8.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
						Assert.AreEqual ("Thu, 29 Apr 2021 10:57:07 +0000", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("Паркинг на Данаил Дечев", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"Рецепция Офис сграда \\\"Данаил Дечев\\\" №6\" <facility@xxxxxxxxxxx.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (0, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual (0, envelope.ReplyTo.Count, "Reply-To counts do not match.");

						Assert.AreEqual (1, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual ("\"Team\" <team@xxxxxxxxxxx.com>", envelope.To.ToString (), "To does not match.");

						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");

						Assert.AreEqual ("d0f6ca6608cfb0b680b7b90824c79118@xxxxxxxxxxx.com", envelope.MessageId, "Message-Id does not match.");
					}
				}
			}
		}

		// This tests the work-around for issue #669
		[Test]
		public void TestParseEnvelopeWithMissingMessageId ()
		{
			const string text = "(\"Tue, 24 Sep 2019 09:48:05 +0800\" \"subject\" ((\"From Name\" NIL \"from\" \"example.com\")) ((\"Sender Name\" NIL \"sender\" \"example.com\")) ((\"Reply-To Name\" NIL \"reply-to\" \"example.com\")) NIL NIL NIL \"<in-reply-to@example.com>\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
						Assert.AreEqual ("Tue, 24 Sep 2019 09:48:05 +0800", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("subject", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"From Name\" <from@example.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("\"Sender Name\" <sender@example.com>", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual ("\"Reply-To Name\" <reply-to@example.com>", envelope.ReplyTo.ToString (), "Reply-To does not match.");

						Assert.AreEqual (0, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.AreEqual ("in-reply-to@example.com", envelope.InReplyTo, "In-Reply-To does not match.");

						Assert.IsNull (envelope.MessageId, "Message-Id is not null.");
					}
				}
			}
		}

		// This tests the work-around for issue #669
		[Test]
		public async Task TestParseEnvelopeWithMissingMessageIdAsync ()
		{
			const string text = "(\"Tue, 24 Sep 2019 09:48:05 +0800\" \"subject\" ((\"From Name\" NIL \"from\" \"example.com\")) ((\"Sender Name\" NIL \"sender\" \"example.com\")) ((\"Reply-To Name\" NIL \"reply-to\" \"example.com\")) NIL NIL NIL \"<in-reply-to@example.com>\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
						Assert.AreEqual ("Tue, 24 Sep 2019 09:48:05 +0800", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("subject", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"From Name\" <from@example.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("\"Sender Name\" <sender@example.com>", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual ("\"Reply-To Name\" <reply-to@example.com>", envelope.ReplyTo.ToString (), "Reply-To does not match.");

						Assert.AreEqual (0, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.AreEqual ("in-reply-to@example.com", envelope.InReplyTo, "In-Reply-To does not match.");

						Assert.IsNull (envelope.MessageId, "Message-Id is not null.");
					}
				}
			}
		}

		// This tests the work-around for issue #932
		[Test]
		public void TestParseEnvelopeWithMissingInReplyTo ()
		{
			const string text = "(\"Tue, 24 Sep 2019 09:48:05 +0800\" \"=?GBK?B?sbG+qdW9x/jI1bGose0=?=\" ((\"=?GBK?B?yv2+3bfWzvbQodfp?=\" NIL \"unkonwn-name\" \"unknown-domain\")) ((\"=?GBK?B?yv2+3bfWzvbQodfp?=\" NIL \"unkonwn-name\" \"unknown-domain\")) ((\"=?GBK?B?yv2+3bfWzvbQodfp?=\" NIL \"unkonwn-name\" \"unknown-domain\")) NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
						Assert.AreEqual ("Tue, 24 Sep 2019 09:48:05 +0800", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("北京战区日报表", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"数据分析小组\" <unkonwn-name@unknown-domain>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("\"数据分析小组\" <unkonwn-name@unknown-domain>", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual ("\"数据分析小组\" <unkonwn-name@unknown-domain>", envelope.ReplyTo.ToString (), "Reply-To does not match.");

						Assert.AreEqual (0, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");
						Assert.IsNull (envelope.MessageId, "Message-Id is not null.");
					}
				}
			}
		}

		// This tests the work-around for issue #932
		[Test]
		public async Task TestParseEnvelopeWithMissingInReplyToAsync ()
		{
			const string text = "(\"Tue, 24 Sep 2019 09:48:05 +0800\" \"=?GBK?B?sbG+qdW9x/jI1bGose0=?=\" ((\"=?GBK?B?yv2+3bfWzvbQodfp?=\" NIL \"unkonwn-name\" \"unknown-domain\")) ((\"=?GBK?B?yv2+3bfWzvbQodfp?=\" NIL \"unkonwn-name\" \"unknown-domain\")) ((\"=?GBK?B?yv2+3bfWzvbQodfp?=\" NIL \"unkonwn-name\" \"unknown-domain\")) NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
						Assert.AreEqual ("Tue, 24 Sep 2019 09:48:05 +0800", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("北京战区日报表", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"数据分析小组\" <unkonwn-name@unknown-domain>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("\"数据分析小组\" <unkonwn-name@unknown-domain>", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual ("\"数据分析小组\" <unkonwn-name@unknown-domain>", envelope.ReplyTo.ToString (), "Reply-To does not match.");

						Assert.AreEqual (0, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");
						Assert.IsNull (envelope.MessageId, "Message-Id is not null.");
					}
				}
			}
		}

		[Test]
		public void TestParseMalformedMailboxAddressInEnvelope ()
		{
			const string text = "(\"Mon, 10 Apr 2017 06:04:00 -0700\" \"Session 2: Building the meditation habit\" ((\"Headspace\" NIL \"members\" \"headspace.com\")) ((NIL NIL \"<members=headspace.com\" \"members.headspace.com>\")) ((\"Headspace\" NIL \"members\" \"headspace.com\")) ((NIL NIL \"user\" \"gmail.com\")) NIL NIL NIL \"<bvqyalstpemxt9y3afoqh4an62b2arcd.rcd.1491829440@members.headspace.com>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseMalformedMailboxAddressInEnvelopeAsync ()
		{
			const string text = "(\"Mon, 10 Apr 2017 06:04:00 -0700\" \"Session 2: Building the meditation habit\" ((\"Headspace\" NIL \"members\" \"headspace.com\")) ((NIL NIL \"<members=headspace.com\" \"members.headspace.com>\")) ((\"Headspace\" NIL \"members\" \"headspace.com\")) ((NIL NIL \"user\" \"gmail.com\")) NIL NIL NIL \"<bvqyalstpemxt9y3afoqh4an62b2arcd.rcd.1491829440@members.headspace.com>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
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

		// This tests a work-around for a bug in Gmail in which the sender header is in a correct format
		// (Sender: Name <sender@domain.com>) but in the FETCH response is not ((("<sender@domain.com>" NIL "Name" NIL)))
		[Test]
		public void TestParseGMailMalformedSenderInEnvelope ()
		{
			const string text = "(\"Mon, 10 Apr 2017 06:04:00 -0700\" \"This is the subject\" ((\"From_DisplayName\" NIL \"from\" \"domain.com\")) ((\"<sender@domain.com>\" NIL \"=?UTF-8?Q?\"Dummy=C3=ADa_Pa=C3=A1ndez_Algo\"?=\" NIL)) NIL ((\"To_DisplayName\" NIL \"to\" \"domain.com\")) NIL NIL NIL \"<bvqyalstpemxt9y3afoqh4an62b2arcd@message.id>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);
						engine.QuirksMode = ImapQuirksMode.GMail;

						try {
							envelope = ImapUtils.ParseEnvelopeAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
						Assert.AreEqual ("Mon, 10 Apr 2017 06:04:00 -0700", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("This is the subject", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"From_DisplayName\" <from@domain.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("\"Dummyía Paández Algo\" <sender@domain.com>", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual ("\"To_DisplayName\" <to@domain.com>", envelope.To.ToString (), "To does not match.");

						Assert.AreEqual (0, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");

						Assert.AreEqual ("bvqyalstpemxt9y3afoqh4an62b2arcd@message.id", envelope.MessageId, "Message-Id does not match.");
					}
				}
			}
		}

		// This tests a work-around for a bug in Gmail in which the sender header is in a correct format
		// (Sender: Name <sender@domain.com>) but in the FETCH response is not ((("<sender@domain.com>" NIL "Name" NIL)))
		[Test]
		public async Task TestParseGMailMalformedSenderInEnvelopeAsync ()
		{
			const string text = "(\"Mon, 10 Apr 2017 06:04:00 -0700\" \"This is the subject\" ((\"From_DisplayName\" NIL \"from\" \"domain.com\")) ((\"<sender@domain.com>\" NIL \"=?UTF-8?Q?\"Dummy=C3=ADa_Pa=C3=A1ndez_Algo\"?=\" NIL)) NIL ((\"To_DisplayName\" NIL \"to\" \"domain.com\")) NIL NIL NIL \"<bvqyalstpemxt9y3afoqh4an62b2arcd@message.id>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);
						engine.QuirksMode = ImapQuirksMode.GMail;

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.IsTrue (envelope.Date.HasValue, "Parsed ENVELOPE date is null.");
						Assert.AreEqual ("Mon, 10 Apr 2017 06:04:00 -0700", DateUtils.FormatDate (envelope.Date.Value), "Date does not match.");
						Assert.AreEqual ("This is the subject", envelope.Subject, "Subject does not match.");

						Assert.AreEqual (1, envelope.From.Count, "From counts do not match.");
						Assert.AreEqual ("\"From_DisplayName\" <from@domain.com>", envelope.From.ToString (), "From does not match.");

						Assert.AreEqual (1, envelope.Sender.Count, "Sender counts do not match.");
						Assert.AreEqual ("\"Dummyía Paández Algo\" <sender@domain.com>", envelope.Sender.ToString (), "Sender does not match.");

						Assert.AreEqual (1, envelope.To.Count, "To counts do not match.");
						Assert.AreEqual ("\"To_DisplayName\" <to@domain.com>", envelope.To.ToString (), "To does not match.");

						Assert.AreEqual (0, envelope.ReplyTo.Count, "Reply-To counts do not match.");
						Assert.AreEqual (0, envelope.Cc.Count, "Cc counts do not match.");
						Assert.AreEqual (0, envelope.Bcc.Count, "Bcc counts do not match.");

						Assert.IsNull (envelope.InReplyTo, "In-Reply-To is not null.");

						Assert.AreEqual ("bvqyalstpemxt9y3afoqh4an62b2arcd@message.id", envelope.MessageId, "Message-Id does not match.");
					}
				}
			}
		}

		// This tests issue #1451
		[Test]
		public void TestParseEnvelopeWithNilMailbox ()
		{
			const string text = "(NIL \"Retrieval using the IMAP4 protocol failed for the following message: 3\" ((\"Microsoft Exchange Server\" NIL NIL \".MISSING-HOST-NAME.\")) NIL NIL ((\"username@testdomain.com\" NIL \"username\" \"testdomain.com\")) NIL NIL NIL NIL)";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = ImapUtils.ParseEnvelopeAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.AreEqual ("Retrieval using the IMAP4 protocol failed for the following message: 3", envelope.Subject);
						Assert.AreEqual ("\"Microsoft Exchange Server\" <>", envelope.From.ToString ());
						Assert.AreEqual ("\"username@testdomain.com\" <username@testdomain.com>", envelope.To.ToString ());
					}
				}
			}
		}

		[Test]
		public void TestParseEnvelopeWithRoutedMailboxes ()
		{
			const string text = "(\"Mon, 13 Jul 2015 21:15:32 -0400\" \"Test message\" ((\"Example From\" \"@route1,@route2\" \"from\" \"example.com\")) ((\"Example Sender\" NIL \"sender\" \"example.com\")) ((\"Example Reply-To\" NIL \"reply-to\" \"example.com\")) ((NIL NIL \"boys\" NIL)(NIL NIL \"aaron\" \"MISSING_DOMAIN\")(NIL NIL \"jeff\" \"MISSING_DOMAIN\")(NIL NIL \"zach\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)(NIL NIL \"girls\" NIL)(NIL NIL \"alice\" \"MISSING_DOMAIN\")(NIL NIL \"hailey\" \"MISSING_DOMAIN\")(NIL NIL \"jenny\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)) NIL NIL NIL \"<MV4F9T0FLVT4.2CZLZPO4HZ8B3@Jeffreys-MacBook-Air.local>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
						Assert.AreEqual ("\"Example From\" <@route1,@route2:from@example.com>", envelope.From.ToString ());
						Assert.AreEqual ("\"Example Reply-To\" <reply-to@example.com>", envelope.ReplyTo.ToString ());
						Assert.AreEqual ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;", envelope.To.ToString ());
					}
				}
			}
		}

		[Test]
		public async Task TestParseEnvelopeWithRoutedMailboxesAsync ()
		{
			const string text = "(\"Mon, 13 Jul 2015 21:15:32 -0400\" \"Test message\" ((\"Example From\" \"@route1,@route2\" \"from\" \"example.com\")) ((\"Example Sender\" NIL \"sender\" \"example.com\")) ((\"Example Reply-To\" NIL \"reply-to\" \"example.com\")) ((NIL NIL \"boys\" NIL)(NIL NIL \"aaron\" \"MISSING_DOMAIN\")(NIL NIL \"jeff\" \"MISSING_DOMAIN\")(NIL NIL \"zach\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)(NIL NIL \"girls\" NIL)(NIL NIL \"alice\" \"MISSING_DOMAIN\")(NIL NIL \"hailey\" \"MISSING_DOMAIN\")(NIL NIL \"jenny\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)) NIL NIL NIL \"<MV4F9T0FLVT4.2CZLZPO4HZ8B3@Jeffreys-MacBook-Air.local>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.AreEqual ("\"Example Sender\" <sender@example.com>", envelope.Sender.ToString ());
						Assert.AreEqual ("\"Example From\" <@route1,@route2:from@example.com>", envelope.From.ToString ());
						Assert.AreEqual ("\"Example Reply-To\" <reply-to@example.com>", envelope.ReplyTo.ToString ());
						Assert.AreEqual ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;", envelope.To.ToString ());
					}
				}
			}
		}

		// This tests the work-around for issue #991
		[Test]
		public void TestParseEnvelopeWithNilAddress ()
		{
			const string text = "(\"Thu, 18 Jul 2019 01:29:32 -0300\" \"Xxx xxx xxx xxx..\" (NIL ({123}\r\n_XXXXXXXX_xxxxxx_xxxx_xxx_?= =?iso-8859-1?Q?xxxx_xx_xxxxxxx_xxxxxxxxxx.Xxxxxxxx_xx_xxx=Xxxxxx_xx_xx_Xx?= =?iso-8859-1?Q?s?= NIL \"xxxxxxx\" \"xxxxxxxxxx.xxx\")) (NIL ({123}\r\n_XXXXXXXX_xxxxxx_xxxx_xxx_?= =?iso-8859-1?Q?xxxx_xx_xxxxxxx_xxxxxxxxxx.Xxxxxxxx_xx_xxx=Xxxxxx_xx_xx_Xx?= =?iso-8859-1?Q?s?= NIL \"xxxxxxx\" \"xxxxxxxxxx.xxx\")) ((NIL NIL \"xxxxxxx\" \"xxxxx.xxx.xx\")) ((NIL NIL \"xxxxxxx\" \"xxxxxxx.xxx.xx\")) NIL NIL NIL \"<0A9F01100712011D213C15B6D2B6DA@XXXXXXX-XXXXXXX>\"))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = ImapUtils.ParseEnvelopeAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.AreEqual ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>", envelope.Sender.ToString ());
						Assert.AreEqual ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>", envelope.From.ToString ());
						Assert.AreEqual ("xxxxxxx@xxxxx.xxx.xx", envelope.ReplyTo.ToString ());
						Assert.AreEqual ("xxxxxxx@xxxxxxx.xxx.xx", envelope.To.ToString ());
						Assert.AreEqual ("0A9F01100712011D213C15B6D2B6DA@XXXXXXX-XXXXXXX", envelope.MessageId);
					}
				}
			}
		}

		// This tests the work-around for issue #991
		[Test]
		public async Task TestParseEnvelopeWithNilAddressAsync ()
		{
			const string text = "(\"Thu, 18 Jul 2019 01:29:32 -0300\" \"Xxx xxx xxx xxx..\" (NIL ({123}\r\n_XXXXXXXX_xxxxxx_xxxx_xxx_?= =?iso-8859-1?Q?xxxx_xx_xxxxxxx_xxxxxxxxxx.Xxxxxxxx_xx_xxx=Xxxxxx_xx_xx_Xx?= =?iso-8859-1?Q?s?= NIL \"xxxxxxx\" \"xxxxxxxxxx.xxx\")) (NIL ({123}\r\n_XXXXXXXX_xxxxxx_xxxx_xxx_?= =?iso-8859-1?Q?xxxx_xx_xxxxxxx_xxxxxxxxxx.Xxxxxxxx_xx_xxx=Xxxxxx_xx_xx_Xx?= =?iso-8859-1?Q?s?= NIL \"xxxxxxx\" \"xxxxxxxxxx.xxx\")) ((NIL NIL \"xxxxxxx\" \"xxxxx.xxx.xx\")) ((NIL NIL \"xxxxxxx\" \"xxxxxxx.xxx.xx\")) NIL NIL NIL \"<0A9F01100712011D213C15B6D2B6DA@XXXXXXX-XXXXXXX>\"))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ENVELOPE failed: {0}", ex);
							return;
						}

						Assert.AreEqual ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>", envelope.Sender.ToString ());
						Assert.AreEqual ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>", envelope.From.ToString ());
						Assert.AreEqual ("xxxxxxx@xxxxx.xxx.xx", envelope.ReplyTo.ToString ());
						Assert.AreEqual ("xxxxxxx@xxxxxxx.xxx.xx", envelope.To.ToString ());
						Assert.AreEqual ("0A9F01100712011D213C15B6D2B6DA@XXXXXXX-XXXXXXX", envelope.MessageId);
					}
				}
			}
		}

		[Test]
		public void TestParseDovcotEnvelopeWithGroupAddresses ()
		{
			const string text = "(\"Mon, 13 Jul 2015 21:15:32 -0400\" \"Test message\" ((\"Example From\" NIL \"from\" \"example.com\")) ((\"Example Sender\" NIL \"sender\" \"example.com\")) ((\"Example Reply-To\" NIL \"reply-to\" \"example.com\")) ((NIL NIL \"boys\" NIL)(NIL NIL \"aaron\" \"MISSING_DOMAIN\")(NIL NIL \"jeff\" \"MISSING_DOMAIN\")(NIL NIL \"zach\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)(NIL NIL \"girls\" NIL)(NIL NIL \"alice\" \"MISSING_DOMAIN\")(NIL NIL \"hailey\" \"MISSING_DOMAIN\")(NIL NIL \"jenny\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)) NIL NIL NIL \"<MV4F9T0FLVT4.2CZLZPO4HZ8B3@Jeffreys-MacBook-Air.local>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseDovcotEnvelopeWithGroupAddressesAsync ()
		{
			const string text = "(\"Mon, 13 Jul 2015 21:15:32 -0400\" \"Test message\" ((\"Example From\" NIL \"from\" \"example.com\")) ((\"Example Sender\" NIL \"sender\" \"example.com\")) ((\"Example Reply-To\" NIL \"reply-to\" \"example.com\")) ((NIL NIL \"boys\" NIL)(NIL NIL \"aaron\" \"MISSING_DOMAIN\")(NIL NIL \"jeff\" \"MISSING_DOMAIN\")(NIL NIL \"zach\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)(NIL NIL \"girls\" NIL)(NIL NIL \"alice\" \"MISSING_DOMAIN\")(NIL NIL \"hailey\" \"MISSING_DOMAIN\")(NIL NIL \"jenny\" \"MISSING_DOMAIN\")(NIL NIL NIL NIL)) NIL NIL NIL \"<MV4F9T0FLVT4.2CZLZPO4HZ8B3@Jeffreys-MacBook-Air.local>\")";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, true, CancellationToken.None);
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
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

		[Test]
		public async Task TestParseExampleMultiLevelDovecotBodyStructureAsync ()
		{
			const string text = "(((\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 28 2 NIL NIL NIL NIL) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 1707 65 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") NIL NIL NIL) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 641 (\"Sat, 8 Jan 2011 14:16:36 +0100\" \"Subj 2\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) (\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 185 18 NIL NIL (\"cs\") NIL) 31 NIL (\"attachment\" NIL) NIL NIL) (\"message\" \"rfc822\" NIL NIL NIL \"7bit\" 50592 (\"Sat, 8 Jan 2011 13:58:39 +0100\" \"Subj 1\" ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Some Name, SOMECOMPANY\" NIL \"recipient\" \"example.com\")) ((\"Recipient\" NIL \"example\" \"gmail.com\")) NIL NIL NIL NIL) ( (\"text\" \"plain\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 4296 345 NIL NIL NIL NIL) (\"text\" \"html\" (\"charset\" \"iso-8859-2\") NIL NIL \"quoted-printable\" 45069 1295 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"----=_NextPart_000_0073_01CBB179.57530990\") NIL (\"cs\") NIL) 1669 NIL (\"attachment\" NIL) NIL NIL) \"mixed\" (\"boundary\" \"----=_NextPart_000_0077_01CBB179.57530990\") NIL (\"cs\") NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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

		// This tests the work-around for issue #878
		[Test]
		public void TestParseBodyStructureWithBrokenMultipartRelated ()
		{
			const string text = "((\"multipart\" \"related\" (\"boundary\" \"----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635\") NIL NIL \"7BIT\" 400 (\"boundary\" \"----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635\") NIL NIL NIL)(\"TEXT\" \"html\" (\"charset\" \"UTF8\") NIL NIL \"7BIT\" 1115 70 NIL NIL NIL NIL)(\"TEXT\" \"html\" (\"charset\" \"UTF8\") NIL NIL \"QUOTED-PRINTABLE\" 16 2 NIL NIL NIL NIL) \"mixed\" (\"boundary\" \"----=--_DRYORTABLE@@@_@@@8957836_03253840099.78526606923635\") NIL NIL NIL)\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
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
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("----=--_DRYORTABLE@@@_@@@8957836_03253840099.78526606923635", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[2], "The type of the third child did not match.");

						var related = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (related.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual ("----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635", related.ContentType.Parameters["boundary"], "multipart/related boundary param did not match");
						Assert.AreEqual ("7BIT", related.ContentTransferEncoding, "multipart/related Content-Transfer-Encoding did not match.");
						Assert.AreEqual (400, related.Octets, "multipart/related octets do not match.");
					}
				}
			}
		}

		// This tests the work-around for issue #878
		[Test]
		public async Task TestParseBodyStructureWithBrokenMultipartRelatedAsync ()
		{
			const string text = "((\"multipart\" \"related\" (\"boundary\" \"----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635\") NIL NIL \"7BIT\" 400 (\"boundary\" \"----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635\") NIL NIL NIL)(\"TEXT\" \"html\" (\"charset\" \"UTF8\") NIL NIL \"7BIT\" 1115 70 NIL NIL NIL NIL)(\"TEXT\" \"html\" (\"charset\" \"UTF8\") NIL NIL \"QUOTED-PRINTABLE\" 16 2 NIL NIL NIL NIL) \"mixed\" (\"boundary\" \"----=--_DRYORTABLE@@@_@@@8957836_03253840099.78526606923635\") NIL NIL NIL)\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("----=--_DRYORTABLE@@@_@@@8957836_03253840099.78526606923635", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[2], "The type of the third child did not match.");

						var related = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (related.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual ("----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635", related.ContentType.Parameters["boundary"], "multipart/related boundary param did not match");
						Assert.AreEqual ("7BIT", related.ContentTransferEncoding, "multipart/related Content-Transfer-Encoding did not match.");
						Assert.AreEqual (400, related.Octets, "multipart/related octets do not match.");
					}
				}
			}
		}

		// This tests the work-around for issue #944
		[Test]
		public void TestParseBodyStructureWithEmptyParenListAsMessageRfc822BodyToken ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"UTF-8\") NIL NIL \"base64\" 232 4 NIL NIL NIL)(\"message\" \"delivery-status\" NIL NIL NIL \"7BIT\" 421 NIL NIL NIL)(\"message\" \"rfc822\" NIL NIL NIL \"7BIT\" 787 (NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL) () 0 NIL NIL NIL) \"report\" (\"report-type\" \"delivery-status\" \"boundary\" \"==IFJRGLKFGIR60132UHRUHIHD\") NIL NIL)\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
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
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "report"), "multipart/report Content-Type did not match.");
						Assert.AreEqual ("==IFJRGLKFGIR60132UHRUHIHD", multipart.ContentType.Parameters["boundary"], "boundary param did not match.");
						Assert.AreEqual ("delivery-status", multipart.ContentType.Parameters["report-type"], "report-type param did not match.");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child did not match.");
						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[2], "The type of the third child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("UTF-8", plain.ContentType.Charset, "text/plain charset param did not match.");
						Assert.AreEqual ("base64", plain.ContentTransferEncoding, "text/plain encoding did not match.");
						Assert.AreEqual (232, plain.Octets, "text/plain octets did not match.");
						Assert.AreEqual (4, plain.Lines, "text/plain lines did not match.");

						var dstat = (BodyPartBasic) multipart.BodyParts[1];
						Assert.IsTrue (dstat.ContentType.IsMimeType ("message", "delivery-status"), "message/delivery-status Content-Type did not match.");
						Assert.AreEqual ("7BIT", dstat.ContentTransferEncoding, "message/delivery-status encoding did not match.");
						Assert.AreEqual (421, dstat.Octets, "message/delivery-status octets did not match.");

						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.IsTrue (rfc822.ContentType.IsMimeType ("message", "rfc822"), "message/rfc822 Content-Type did not match.");
						Assert.IsNull (rfc822.ContentId, "message/rfc822 Content-Id should be NIL.");
						Assert.IsNull (rfc822.ContentDescription, "message/rfc822 Content-Description should be NIL.");
						Assert.AreEqual (0, rfc822.Envelope.Sender.Count, "message/rfc822 Envlope.Sender should be null.");
						Assert.AreEqual (0, rfc822.Envelope.From.Count, "message/rfc822 Envlope.From should be null.");
						Assert.AreEqual (0, rfc822.Envelope.ReplyTo.Count, "message/rfc822 Envlope.ReplyTo should be null.");
						Assert.AreEqual (0, rfc822.Envelope.To.Count, "message/rfc822 Envlope.To should be null.");
						Assert.AreEqual (0, rfc822.Envelope.Cc.Count, "message/rfc822 Envlope.Cc should be null.");
						Assert.AreEqual (0, rfc822.Envelope.Bcc.Count, "message/rfc822 Envlope.Bcc should be null.");
						Assert.IsNull (rfc822.Envelope.Subject, "message/rfc822 Envlope.Subject should be null.");
						Assert.IsNull (rfc822.Envelope.MessageId, "message/rfc822 Envlope.MessageId should be null.");
						Assert.IsNull (rfc822.Envelope.InReplyTo, "message/rfc822 Envlope.InReplyTo should be null.");
						Assert.IsNull (rfc822.Envelope.Date, "message/rfc822 Envlope.Date should be null.");
						Assert.AreEqual ("7BIT", rfc822.ContentTransferEncoding, "message/rfc822 encoding did not match.");
						Assert.AreEqual (787, rfc822.Octets, "message/rfc822 octets did not match.");
						Assert.IsNull (rfc822.Body, "message/rfc822 body should be null.");
						Assert.AreEqual (0, rfc822.Lines, "message/rfc822 lines did not match.");
					}
				}
			}
		}

		// This tests the work-around for issue #944
		[Test]
		public async Task TestParseBodyStructureWithEmptyParenListAsMessageRfc822BodyTokenAsync ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"UTF-8\") NIL NIL \"base64\" 232 4 NIL NIL NIL)(\"message\" \"delivery-status\" NIL NIL NIL \"7BIT\" 421 NIL NIL NIL)(\"message\" \"rfc822\" NIL NIL NIL \"7BIT\" 787 (NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL) () 0 NIL NIL NIL) \"report\" (\"report-type\" \"delivery-status\" \"boundary\" \"==IFJRGLKFGIR60132UHRUHIHD\") NIL NIL)\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "report"), "multipart/report Content-Type did not match.");
						Assert.AreEqual ("==IFJRGLKFGIR60132UHRUHIHD", multipart.ContentType.Parameters["boundary"], "boundary param did not match.");
						Assert.AreEqual ("delivery-status", multipart.ContentType.Parameters["report-type"], "report-type param did not match.");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child did not match.");
						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[2], "The type of the third child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("UTF-8", plain.ContentType.Charset, "text/plain charset param did not match.");
						Assert.AreEqual ("base64", plain.ContentTransferEncoding, "text/plain encoding did not match.");
						Assert.AreEqual (232, plain.Octets, "text/plain octets did not match.");
						Assert.AreEqual (4, plain.Lines, "text/plain lines did not match.");

						var dstat = (BodyPartBasic) multipart.BodyParts[1];
						Assert.IsTrue (dstat.ContentType.IsMimeType ("message", "delivery-status"), "message/delivery-status Content-Type did not match.");
						Assert.AreEqual ("7BIT", dstat.ContentTransferEncoding, "message/delivery-status encoding did not match.");
						Assert.AreEqual (421, dstat.Octets, "message/delivery-status octets did not match.");

						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.IsTrue (rfc822.ContentType.IsMimeType ("message", "rfc822"), "message/rfc822 Content-Type did not match.");
						Assert.IsNull (rfc822.ContentId, "message/rfc822 Content-Id should be NIL.");
						Assert.IsNull (rfc822.ContentDescription, "message/rfc822 Content-Description should be NIL.");
						Assert.AreEqual (0, rfc822.Envelope.Sender.Count, "message/rfc822 Envlope.Sender should be null.");
						Assert.AreEqual (0, rfc822.Envelope.From.Count, "message/rfc822 Envlope.From should be null.");
						Assert.AreEqual (0, rfc822.Envelope.ReplyTo.Count, "message/rfc822 Envlope.ReplyTo should be null.");
						Assert.AreEqual (0, rfc822.Envelope.To.Count, "message/rfc822 Envlope.To should be null.");
						Assert.AreEqual (0, rfc822.Envelope.Cc.Count, "message/rfc822 Envlope.Cc should be null.");
						Assert.AreEqual (0, rfc822.Envelope.Bcc.Count, "message/rfc822 Envlope.Bcc should be null.");
						Assert.IsNull (rfc822.Envelope.Subject, "message/rfc822 Envlope.Subject should be null.");
						Assert.IsNull (rfc822.Envelope.MessageId, "message/rfc822 Envlope.MessageId should be null.");
						Assert.IsNull (rfc822.Envelope.InReplyTo, "message/rfc822 Envlope.InReplyTo should be null.");
						Assert.IsNull (rfc822.Envelope.Date, "message/rfc822 Envlope.Date should be null.");
						Assert.AreEqual ("7BIT", rfc822.ContentTransferEncoding, "message/rfc822 encoding did not match.");
						Assert.AreEqual (787, rfc822.Octets, "message/rfc822 octets did not match.");
						Assert.IsNull (rfc822.Body, "message/rfc822 body should be null.");
						Assert.AreEqual (0, rfc822.Lines, "message/rfc822 lines did not match.");
					}
				}
			}
		}

		[Test]
		public void TestParseBodyStructureWithContentMd5DspLanguageAndLocation ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 28 2 \"md5sum\" (\"inline\" (\"filename\" \"body.txt\")) \"en\" \"http://www.google.com/body.txt\") (\"text\" \"html\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 1707 65 \"md5sum\" (\"inline\" (\"filename\" \"body.html\")) \"en\" \"http://www.google.com/body.html\") \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") (\"inline\" (\"filename\" \"alternative.txt\")) \"en\" \"http://www.google.com/alternative.txt\")\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
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
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_001_0078_01CBB179.57530990", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual ("inline", multipart.ContentDisposition.Disposition, "multipart/alternative disposition did not match");
						Assert.AreEqual ("alternative.txt", multipart.ContentDisposition.FileName, "multipart/alternative filename did not match");
						Assert.NotNull (multipart.ContentLanguage, "multipart/alternative Content-Language should not be null");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "multipart/alternative Content-Language count did not match");
						Assert.AreEqual ("en", multipart.ContentLanguage[0], "multipart/alternative Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/alternative.txt", multipart.ContentLocation.ToString (), "multipart/alternative location did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "text/plain charset param did not match");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "text/plain disposition did not match");
						Assert.AreEqual ("body.txt", plain.ContentDisposition.FileName, "text/plain filename did not match");
						Assert.AreEqual ("md5sum", plain.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (plain.ContentLanguage, "text/plain Content-Language should not be null");
						Assert.AreEqual (1, plain.ContentLanguage.Length, "text/plain Content-Language count did not match");
						Assert.AreEqual ("en", plain.ContentLanguage [0], "text/plain Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.txt", plain.ContentLocation.ToString (), "text/plain location did not match");
						Assert.AreEqual (28, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (2, plain.Lines, "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset param did not match");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "text/html disposition did not match");
						Assert.AreEqual ("body.html", html.ContentDisposition.FileName, "text/html filename did not match");
						Assert.AreEqual ("md5sum", html.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (html.ContentLanguage, "text/html Content-Language should not be null");
						Assert.AreEqual (1, html.ContentLanguage.Length, "text/html Content-Language count did not match");
						Assert.AreEqual ("en", html.ContentLanguage [0], "text/html Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.html", html.ContentLocation.ToString (), "text/html location did not match");
						Assert.AreEqual (1707, html.Octets, "text/html octets did not match");
						Assert.AreEqual (65, html.Lines, "text/html lines did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBodyStructureWithContentMd5DspLanguageAndLocationAsync ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 28 2 \"md5sum\" (\"inline\" (\"filename\" \"body.txt\")) \"en\" \"http://www.google.com/body.txt\") (\"text\" \"html\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 1707 65 \"md5sum\" (\"inline\" (\"filename\" \"body.html\")) \"en\" \"http://www.google.com/body.html\") \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") (\"inline\" (\"filename\" \"alternative.txt\")) \"en\" \"http://www.google.com/alternative.txt\")\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_001_0078_01CBB179.57530990", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual ("inline", multipart.ContentDisposition.Disposition, "multipart/alternative disposition did not match");
						Assert.AreEqual ("alternative.txt", multipart.ContentDisposition.FileName, "multipart/alternative filename did not match");
						Assert.NotNull (multipart.ContentLanguage, "multipart/alternative Content-Language should not be null");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "multipart/alternative Content-Language count did not match");
						Assert.AreEqual ("en", multipart.ContentLanguage[0], "multipart/alternative Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/alternative.txt", multipart.ContentLocation.ToString (), "multipart/alternative location did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "text/plain charset param did not match");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "text/plain disposition did not match");
						Assert.AreEqual ("body.txt", plain.ContentDisposition.FileName, "text/plain filename did not match");
						Assert.AreEqual ("md5sum", plain.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (plain.ContentLanguage, "text/plain Content-Language should not be null");
						Assert.AreEqual (1, plain.ContentLanguage.Length, "text/plain Content-Language count did not match");
						Assert.AreEqual ("en", plain.ContentLanguage[0], "text/plain Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.txt", plain.ContentLocation.ToString (), "text/plain location did not match");
						Assert.AreEqual (28, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (2, plain.Lines, "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset param did not match");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "text/html disposition did not match");
						Assert.AreEqual ("body.html", html.ContentDisposition.FileName, "text/html filename did not match");
						Assert.AreEqual ("md5sum", html.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (html.ContentLanguage, "text/html Content-Language should not be null");
						Assert.AreEqual (1, html.ContentLanguage.Length, "text/html Content-Language count did not match");
						Assert.AreEqual ("en", html.ContentLanguage[0], "text/html Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.html", html.ContentLocation.ToString (), "text/html location did not match");
						Assert.AreEqual (1707, html.Octets, "text/html octets did not match");
						Assert.AreEqual (65, html.Lines, "text/html lines did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseBodyStructureWithLiterals_LiteralsEverywhere ()
		{
			const string text = "(({4}\r\ntext {5}\r\nplain ({7}\r\ncharset {10}\r\niso-8859-1) NIL NIL {16}\r\nquoted-printable 28 2 {6}\r\nmd5sum ({6}\r\ninline ({8}\r\nfilename {8}\r\nbody.txt)) {2}\r\nen {30}\r\nhttp://www.google.com/body.txt) (\"text\" \"html\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 1707 65 \"md5sum\" (\"inline\" (\"filename\" \"body.html\")) \"en\" \"http://www.google.com/body.html\") {11}\r\nalternative (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") (\"inline\" (\"filename\" \"alternative.txt\")) \"en\" \"http://www.google.com/alternative.txt\")\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
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
						var multipart = (BodyPartMultipart)body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_001_0078_01CBB179.57530990", multipart.ContentType.Parameters ["boundary"], "boundary param did not match");
						Assert.AreEqual ("inline", multipart.ContentDisposition.Disposition, "multipart/alternative disposition did not match");
						Assert.AreEqual ("alternative.txt", multipart.ContentDisposition.FileName, "multipart/alternative filename did not match");
						Assert.NotNull (multipart.ContentLanguage, "multipart/alternative Content-Language should not be null");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "multipart/alternative Content-Language count did not match");
						Assert.AreEqual ("en", multipart.ContentLanguage [0], "multipart/alternative Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/alternative.txt", multipart.ContentLocation.ToString (), "multipart/alternative location did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts [0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts [1], "The type of the second child did not match.");

						var plain = (BodyPartText)multipart.BodyParts [0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "text/plain charset param did not match");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "text/plain disposition did not match");
						Assert.AreEqual ("body.txt", plain.ContentDisposition.FileName, "text/plain filename did not match");
						Assert.AreEqual ("md5sum", plain.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (plain.ContentLanguage, "text/plain Content-Language should not be null");
						Assert.AreEqual (1, plain.ContentLanguage.Length, "text/plain Content-Language count did not match");
						Assert.AreEqual ("en", plain.ContentLanguage [0], "text/plain Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.txt", plain.ContentLocation.ToString (), "text/plain location did not match");
						Assert.AreEqual (28, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (2, plain.Lines, "text/plain lines did not match");

						var html = (BodyPartText)multipart.BodyParts [1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset param did not match");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "text/html disposition did not match");
						Assert.AreEqual ("body.html", html.ContentDisposition.FileName, "text/html filename did not match");
						Assert.AreEqual ("md5sum", html.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (html.ContentLanguage, "text/html Content-Language should not be null");
						Assert.AreEqual (1, html.ContentLanguage.Length, "text/html Content-Language count did not match");
						Assert.AreEqual ("en", html.ContentLanguage [0], "text/html Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.html", html.ContentLocation.ToString (), "text/html location did not match");
						Assert.AreEqual (1707, html.Octets, "text/html octets did not match");
						Assert.AreEqual (65, html.Lines, "text/html lines did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBodyStructureWithLiterals_LiteralsEverywhereAsync ()
		{
			const string text = "(({4}\r\ntext {5}\r\nplain ({7}\r\ncharset {10}\r\niso-8859-1) NIL NIL {16}\r\nquoted-printable 28 2 {6}\r\nmd5sum ({6}\r\ninline ({8}\r\nfilename {8}\r\nbody.txt)) {2}\r\nen {30}\r\nhttp://www.google.com/body.txt) (\"text\" \"html\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 1707 65 \"md5sum\" (\"inline\" (\"filename\" \"body.html\")) \"en\" \"http://www.google.com/body.html\") {11}\r\nalternative (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") (\"inline\" (\"filename\" \"alternative.txt\")) \"en\" \"http://www.google.com/alternative.txt\")\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_001_0078_01CBB179.57530990", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual ("inline", multipart.ContentDisposition.Disposition, "multipart/alternative disposition did not match");
						Assert.AreEqual ("alternative.txt", multipart.ContentDisposition.FileName, "multipart/alternative filename did not match");
						Assert.NotNull (multipart.ContentLanguage, "multipart/alternative Content-Language should not be null");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "multipart/alternative Content-Language count did not match");
						Assert.AreEqual ("en", multipart.ContentLanguage[0], "multipart/alternative Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/alternative.txt", multipart.ContentLocation.ToString (), "multipart/alternative location did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "text/plain charset param did not match");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "text/plain disposition did not match");
						Assert.AreEqual ("body.txt", plain.ContentDisposition.FileName, "text/plain filename did not match");
						Assert.AreEqual ("md5sum", plain.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (plain.ContentLanguage, "text/plain Content-Language should not be null");
						Assert.AreEqual (1, plain.ContentLanguage.Length, "text/plain Content-Language count did not match");
						Assert.AreEqual ("en", plain.ContentLanguage[0], "text/plain Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.txt", plain.ContentLocation.ToString (), "text/plain location did not match");
						Assert.AreEqual (28, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (2, plain.Lines, "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset param did not match");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "text/html disposition did not match");
						Assert.AreEqual ("body.html", html.ContentDisposition.FileName, "text/html filename did not match");
						Assert.AreEqual ("md5sum", html.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (html.ContentLanguage, "text/html Content-Language should not be null");
						Assert.AreEqual (1, html.ContentLanguage.Length, "text/html Content-Language count did not match");
						Assert.AreEqual ("en", html.ContentLanguage[0], "text/html Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.html", html.ContentLocation.ToString (), "text/html location did not match");
						Assert.AreEqual (1707, html.Octets, "text/html octets did not match");
						Assert.AreEqual (65, html.Lines, "text/html lines did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseBodyStructureWithBodyExtensions ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 28 2 \"md5sum\" (\"inline\" (\"filename\" \"body.txt\")) \"en\" \"body.txt\" \"extension1\" 123 (\"extension2\" (\"nested-extension3\" \"nested-extension4\"))) (\"text\" \"html\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 1707 65 \"md5sum\" (\"inline\" (\"filename\" \"body.html\")) \"en\" \"http://www.google.com/body.html\") \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") (\"inline\" (\"filename\" \"alternative.txt\")) \"en\" \"http://www.google.com/alternative.txt\" {10}\r\nextension1 123 (\"extension2\" (\"nested-extension3\" \"nested-extension4\")))\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
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
						var multipart = (BodyPartMultipart)body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_001_0078_01CBB179.57530990", multipart.ContentType.Parameters ["boundary"], "boundary param did not match");
						Assert.AreEqual ("inline", multipart.ContentDisposition.Disposition, "multipart/alternative disposition did not match");
						Assert.AreEqual ("alternative.txt", multipart.ContentDisposition.FileName, "multipart/alternative filename did not match");
						Assert.NotNull (multipart.ContentLanguage, "multipart/alternative Content-Language should not be null");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "multipart/alternative Content-Language count did not match");
						Assert.AreEqual ("en", multipart.ContentLanguage [0], "multipart/alternative Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/alternative.txt", multipart.ContentLocation.ToString (), "multipart/alternative location did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts [0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts [1], "The type of the second child did not match.");

						var plain = (BodyPartText)multipart.BodyParts [0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "text/plain charset param did not match");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "text/plain disposition did not match");
						Assert.AreEqual ("body.txt", plain.ContentDisposition.FileName, "text/plain filename did not match");
						Assert.AreEqual ("md5sum", plain.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (plain.ContentLanguage, "text/plain Content-Language should not be null");
						Assert.AreEqual (1, plain.ContentLanguage.Length, "text/plain Content-Language count did not match");
						Assert.AreEqual ("en", plain.ContentLanguage [0], "text/plain Content-Language value did not match");
						Assert.AreEqual ("body.txt", plain.ContentLocation.ToString (), "text/plain location did not match");
						Assert.AreEqual (28, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (2, plain.Lines, "text/plain lines did not match");

						var html = (BodyPartText)multipart.BodyParts [1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset param did not match");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "text/html disposition did not match");
						Assert.AreEqual ("body.html", html.ContentDisposition.FileName, "text/html filename did not match");
						Assert.AreEqual ("md5sum", html.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (html.ContentLanguage, "text/html Content-Language should not be null");
						Assert.AreEqual (1, html.ContentLanguage.Length, "text/html Content-Language count did not match");
						Assert.AreEqual ("en", html.ContentLanguage [0], "text/html Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.html", html.ContentLocation.ToString (), "text/html location did not match");
						Assert.AreEqual (1707, html.Octets, "text/html octets did not match");
						Assert.AreEqual (65, html.Lines, "text/html lines did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBodyStructureWithBodyExtensionsAsync ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 28 2 \"md5sum\" (\"inline\" (\"filename\" \"body.txt\")) \"en\" \"body.txt\" \"extension1\" 123 (\"extension2\" (\"nested-extension3\" \"nested-extension4\"))) (\"text\" \"html\" (\"charset\" \"iso-8859-1\") NIL NIL \"quoted-printable\" 1707 65 \"md5sum\" (\"inline\" (\"filename\" \"body.html\")) \"en\" \"http://www.google.com/body.html\") \"alternative\" (\"boundary\" \"----=_NextPart_001_0078_01CBB179.57530990\") (\"inline\" (\"filename\" \"alternative.txt\")) \"en\" \"http://www.google.com/alternative.txt\" {10}\r\nextension1 123 (\"extension2\" (\"nested-extension3\" \"nested-extension4\")))\r\n";
			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_001_0078_01CBB179.57530990", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual ("inline", multipart.ContentDisposition.Disposition, "multipart/alternative disposition did not match");
						Assert.AreEqual ("alternative.txt", multipart.ContentDisposition.FileName, "multipart/alternative filename did not match");
						Assert.NotNull (multipart.ContentLanguage, "multipart/alternative Content-Language should not be null");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "multipart/alternative Content-Language count did not match");
						Assert.AreEqual ("en", multipart.ContentLanguage[0], "multipart/alternative Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/alternative.txt", multipart.ContentLocation.ToString (), "multipart/alternative location did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child did not match.");
						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "text/plain charset param did not match");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "text/plain disposition did not match");
						Assert.AreEqual ("body.txt", plain.ContentDisposition.FileName, "text/plain filename did not match");
						Assert.AreEqual ("md5sum", plain.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (plain.ContentLanguage, "text/plain Content-Language should not be null");
						Assert.AreEqual (1, plain.ContentLanguage.Length, "text/plain Content-Language count did not match");
						Assert.AreEqual ("en", plain.ContentLanguage[0], "text/plain Content-Language value did not match");
						Assert.AreEqual ("body.txt", plain.ContentLocation.ToString (), "text/plain location did not match");
						Assert.AreEqual (28, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (2, plain.Lines, "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset param did not match");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "text/html disposition did not match");
						Assert.AreEqual ("body.html", html.ContentDisposition.FileName, "text/html filename did not match");
						Assert.AreEqual ("md5sum", html.ContentMd5, "text/html Content-Md5 did not match");
						Assert.NotNull (html.ContentLanguage, "text/html Content-Language should not be null");
						Assert.AreEqual (1, html.ContentLanguage.Length, "text/html Content-Language count did not match");
						Assert.AreEqual ("en", html.ContentLanguage[0], "text/html Content-Language value did not match");
						Assert.AreEqual ("http://www.google.com/body.html", html.ContentLocation.ToString (), "text/html location did not match");
						Assert.AreEqual (1707, html.Octets, "text/html octets did not match");
						Assert.AreEqual (65, html.Lines, "text/html lines did not match");
					}
				}
			}
		}

		// This tests the work-around for issue #205
		[Test]
		public void TestParseGMailBadlyFormedMultipartBodyStructure ()
		{
			const string text = "((\"ALTERNATIVE\" (\"BOUNDARY\" \"==alternative_xad5934455aeex\") NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"iso-8859-1\" \"NAME\" \"seti_letter.html\") NIL NIL \"7BIT\" 6769 171 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" \"==alternative_xad5934455aeex\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart, broken;
						BodyPartText html;
						BodyPart body;

						engine.SetStream (tokenizer);
						engine.QuirksMode = ImapQuirksMode.GMail;

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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "outer multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("==alternative_xad5934455aeex", multipart.ContentType.Parameters["boundary"], "outer multipart/alternative boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[0], "The type of the first child does not match.");
						broken = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.IsTrue (broken.ContentType.IsMimeType ("multipart", "alternative"), "inner multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("==alternative_xad5934455aeex", broken.ContentType.Parameters["boundary"], "inner multipart/alternative boundary param did not match");
						Assert.AreEqual (0, broken.BodyParts.Count, "inner multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset parameter did not match");
						Assert.AreEqual ("seti_letter.html", html.ContentType.Name, "text/html name parameter did not match");
					}
				}
			}
		}

		// This tests the work-around for issue #205
		[Test]
		public async Task TestParseGMailBadlyFormedMultipartBodyStructureAsync ()
		{
			const string text = "((\"ALTERNATIVE\" (\"BOUNDARY\" \"==alternative_xad5934455aeex\") NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"iso-8859-1\" \"NAME\" \"seti_letter.html\") NIL NIL \"7BIT\" 6769 171 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" \"==alternative_xad5934455aeex\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart, broken;
						BodyPartText html;
						BodyPart body;

						engine.SetStream (tokenizer);
						engine.QuirksMode = ImapQuirksMode.GMail;

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "alternative"), "outer multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("==alternative_xad5934455aeex", multipart.ContentType.Parameters["boundary"], "outer multipart/alternative boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[0], "The type of the first child does not match.");
						broken = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.IsTrue (broken.ContentType.IsMimeType ("multipart", "alternative"), "inner multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("==alternative_xad5934455aeex", broken.ContentType.Parameters["boundary"], "inner multipart/alternative boundary param did not match");
						Assert.AreEqual (0, broken.BodyParts.Count, "inner multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", html.ContentType.Charset, "text/html charset parameter did not match");
						Assert.AreEqual ("seti_letter.html", html.ContentType.Name, "text/html name parameter did not match");
					}
				}
			}
		}

		// This tests the work-around for issue #777
		[Test]
		public void TestParseGMailBadlyFormedMultipartBodyStructure2 ()
		{
			const string text = "(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"UTF-8\" \"DELSP\" \"yes\" \"FORMAT\" \"flowed\") NIL NIL \"BASE64\" 10418 133 NIL NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"BASE64\" 34544 442 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" \"94eb2c1cd0507723d5054c1ce6cb\") NIL NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL) \"MIXED\" (\"BOUNDARY\" \"94eb2c1cd0507723e6054c1ce6cd\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);
						engine.QuirksMode = ImapQuirksMode.GMail;

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var mixed = (BodyPartMultipart) body;

						Assert.IsTrue (mixed.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("94eb2c1cd0507723e6054c1ce6cd", mixed.ContentType.Parameters["boundary"], "multipart/mixed boundary param did not match");
						Assert.AreEqual (5, mixed.BodyParts.Count, "multipart/mixed BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[0], "The type of the first child does not match.");
						var alternative = (BodyPartMultipart) mixed.BodyParts[0];
						Assert.IsTrue (alternative.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("94eb2c1cd0507723d5054c1ce6cb", alternative.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (2, alternative.BodyParts.Count, "multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (alternative.BodyParts[0], "The type of the second child does not match.");
						var plain = (BodyPartText) alternative.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("UTF-8", plain.ContentType.Charset, "text/plain charset parameter did not match");
						Assert.AreEqual ("flowed", plain.ContentType.Format, "text/plain format parameter did not match");
						Assert.AreEqual ("yes", plain.ContentType.Parameters["delsp"], "text/plain delsp parameter did not match");
						Assert.AreEqual ("BASE64", plain.ContentTransferEncoding, "text/plain Content-Transfer-Encoding did not match");
						Assert.AreEqual (10418, plain.Octets, "text/plain Octets do not match");
						Assert.AreEqual (133, plain.Lines, "text/plain Lines don't match");

						Assert.IsInstanceOf<BodyPartText> (alternative.BodyParts[1], "The type of the second child does not match.");
						var html = (BodyPartText) alternative.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("UTF-8", html.ContentType.Charset, "text/html charset parameter did not match");
						Assert.AreEqual ("BASE64", html.ContentTransferEncoding, "text/phtml Content-Transfer-Encoding did not match");
						Assert.AreEqual (34544, html.Octets, "text/html Octets do not match");
						Assert.AreEqual (442, html.Lines, "text/html Lines don't match");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[1], "The type of the second child does not match.");
						var broken1 = (BodyPartMultipart) mixed.BodyParts[1];
						Assert.IsTrue (broken1.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken1.BodyParts.Count, "multipart/related BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[2], "The type of the third child does not match.");
						var broken2 = (BodyPartMultipart) mixed.BodyParts[2];
						Assert.IsTrue (broken2.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken2.BodyParts.Count, "multipart/related BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[3], "The type of the fourth child does not match.");
						var broken3 = (BodyPartMultipart) mixed.BodyParts[3];
						Assert.IsTrue (broken3.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken3.BodyParts.Count, "multipart/related BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[4], "The type of the fifth child does not match.");
						var broken4 = (BodyPartMultipart) mixed.BodyParts[4];
						Assert.IsTrue (broken4.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken4.BodyParts.Count, "multipart/related BodyParts count does not match.");
					}
				}
			}
		}

		// This tests the work-around for issue #777
		[Test]
		public async Task TestParseGMailBadlyFormedMultipartBodyStructure2Async ()
		{
			const string text = "(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"UTF-8\" \"DELSP\" \"yes\" \"FORMAT\" \"flowed\") NIL NIL \"BASE64\" 10418 133 NIL NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"BASE64\" 34544 442 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" \"94eb2c1cd0507723d5054c1ce6cb\") NIL NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL)(\"RELATED\" NIL (\"ATTACHMENT\" NIL) NIL) \"MIXED\" (\"BOUNDARY\" \"94eb2c1cd0507723e6054c1ce6cd\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);
						engine.QuirksMode = ImapQuirksMode.GMail;

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var mixed = (BodyPartMultipart) body;

						Assert.IsTrue (mixed.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("94eb2c1cd0507723e6054c1ce6cd", mixed.ContentType.Parameters["boundary"], "multipart/mixed boundary param did not match");
						Assert.AreEqual (5, mixed.BodyParts.Count, "multipart/mixed BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[0], "The type of the first child does not match.");
						var alternative = (BodyPartMultipart) mixed.BodyParts[0];
						Assert.IsTrue (alternative.ContentType.IsMimeType ("multipart", "alternative"), "multipart/alternative Content-Type did not match.");
						Assert.AreEqual ("94eb2c1cd0507723d5054c1ce6cb", alternative.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (2, alternative.BodyParts.Count, "multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (alternative.BodyParts[0], "The type of the second child does not match.");
						var plain = (BodyPartText) alternative.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("UTF-8", plain.ContentType.Charset, "text/plain charset parameter did not match");
						Assert.AreEqual ("flowed", plain.ContentType.Format, "text/plain format parameter did not match");
						Assert.AreEqual ("yes", plain.ContentType.Parameters["delsp"], "text/plain delsp parameter did not match");
						Assert.AreEqual ("BASE64", plain.ContentTransferEncoding, "text/plain Content-Transfer-Encoding did not match");
						Assert.AreEqual (10418, plain.Octets, "text/plain Octets do not match");
						Assert.AreEqual (133, plain.Lines, "text/plain Lines don't match");

						Assert.IsInstanceOf<BodyPartText> (alternative.BodyParts[1], "The type of the second child does not match.");
						var html = (BodyPartText) alternative.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "text/html Content-Type did not match.");
						Assert.AreEqual ("UTF-8", html.ContentType.Charset, "text/html charset parameter did not match");
						Assert.AreEqual ("BASE64", html.ContentTransferEncoding, "text/phtml Content-Transfer-Encoding did not match");
						Assert.AreEqual (34544, html.Octets, "text/html Octets do not match");
						Assert.AreEqual (442, html.Lines, "text/html Lines don't match");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[1], "The type of the second child does not match.");
						var broken1 = (BodyPartMultipart) mixed.BodyParts[1];
						Assert.IsTrue (broken1.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken1.BodyParts.Count, "multipart/related BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[2], "The type of the third child does not match.");
						var broken2 = (BodyPartMultipart) mixed.BodyParts[2];
						Assert.IsTrue (broken2.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken2.BodyParts.Count, "multipart/related BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[3], "The type of the fourth child does not match.");
						var broken3 = (BodyPartMultipart) mixed.BodyParts[3];
						Assert.IsTrue (broken3.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken3.BodyParts.Count, "multipart/related BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (mixed.BodyParts[4], "The type of the fifth child does not match.");
						var broken4 = (BodyPartMultipart) mixed.BodyParts[4];
						Assert.IsTrue (broken4.ContentType.IsMimeType ("multipart", "related"), "multipart/related Content-Type did not match.");
						Assert.AreEqual (0, broken4.BodyParts.Count, "multipart/related BodyParts count does not match.");
					}
				}
			}
		}

		// Note: This tests the work-around for issue #371 (except that the example from issue #371 is also missing body-fld-enc and body-fld-octets)
		[Test]
		public void TestParseBadlyFormedBodyStructureWithEmptyStringMediaType ()
		{
			const string text = "((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"windows-1251\") NIL NIL \"base64\" 356 5)( \"X-ZIP\" (\"BOUNDARY\" \"\") NIL NIL \"base64\" 4096) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic xzip;
						BodyPartText plain;
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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("windows-1251", plain.ContentType.Charset, "text/plain charset parameter did not match");
						Assert.AreEqual (356, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (5, plain.Lines, "text/plain lines did not match");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child does not match.");
						xzip = (BodyPartBasic) multipart.BodyParts[1];
						Assert.IsTrue (xzip.ContentType.IsMimeType ("application", "x-zip"), "x-zip Content-Type did not match.");
						Assert.AreEqual ("", xzip.ContentType.Parameters["boundary"], "x-zip boundary parameter did not match");
						Assert.AreEqual (4096, xzip.Octets, "x-zip octets did not match");
					}
				}
			}
		}

		// Note: This tests the work-around for issue #371 (except that the example from issue #371 is also missing body-fld-enc and body-fld-octets)
		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithEmptyStringMediaTypeAsync ()
		{
			const string text = "((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"windows-1251\") NIL NIL \"base64\" 356 5)( \"X-ZIP\" (\"BOUNDARY\" \"\") NIL NIL \"base64\" 4096) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic xzip;
						BodyPartText plain;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("windows-1251", plain.ContentType.Charset, "text/plain charset parameter did not match");
						Assert.AreEqual (356, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (5, plain.Lines, "text/plain lines did not match");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child does not match.");
						xzip = (BodyPartBasic) multipart.BodyParts[1];
						Assert.IsTrue (xzip.ContentType.IsMimeType ("application", "x-zip"), "x-zip Content-Type did not match.");
						Assert.AreEqual ("", xzip.ContentType.Parameters["boundary"], "x-zip boundary parameter did not match");
						Assert.AreEqual (4096, xzip.Octets, "x-zip octets did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeApplication ()
		{
			const string text = "((\"APPLICATION\" NIL NIL NIL \"base64\" 356) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (basic.ContentType.IsMimeType ("application", "octet-stream"), "application/octet-stream Content-Type did not match.");
						Assert.AreEqual (356, basic.Octets, "application/octet-stream octets did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeApplicationAsync ()
		{
			const string text = "((\"APPLICATION\" NIL NIL NIL \"base64\" 356) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (basic.ContentType.IsMimeType ("application", "octet-stream"), "application/octet-stream Content-Type did not match.");
						Assert.AreEqual (356, basic.Octets, "application/octet-stream octets did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeAudio ()
		{
			const string text = "((\"AUDIO\" NIL NIL NIL \"base64\" 356) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (basic.ContentType.IsMimeType ("application", "audio"), "application/audio Content-Type did not match.");
						Assert.AreEqual (356, basic.Octets, "application/audio octets did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeAudioAsync ()
		{
			const string text = "((\"AUDIO\" NIL NIL NIL \"base64\" 356) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (basic.ContentType.IsMimeType ("application", "audio"), "application/audio Content-Type did not match.");
						Assert.AreEqual (356, basic.Octets, "application/audio octets did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeMessage ()
		{
			const string text = "((\"MESSAGE\" NIL NIL NIL \"base64\" 356) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (basic.ContentType.IsMimeType ("application", "message"), "application/message Content-Type did not match.");
						Assert.AreEqual (356, basic.Octets, "application/message octets did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeMessageAsync ()
		{
			const string text = "((\"MESSAGE\" NIL NIL NIL \"base64\" 356) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (basic.ContentType.IsMimeType ("application", "message"), "application/message Content-Type did not match.");
						Assert.AreEqual (356, basic.Octets, "application/message octets did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeText ()
		{
			const string text = "((\"TEXT\" (\"CHARSET\" \"windows-1251\") NIL NIL \"base64\" 356 5) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartText plain;
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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("windows-1251", plain.ContentType.Charset, "text/plain charset parameter did not match");
						Assert.AreEqual (356, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (5, plain.Lines, "text/plain lines did not match");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithMissingMediaSubtypeTextAsync ()
		{
			const string text = "((\"TEXT\" (\"CHARSET\" \"windows-1251\") NIL NIL \"base64\" 356 5) \"MIXED\" (\"BOUNDARY\" \"--cd49a2f5ed4ed0cbb6f9f1c7f125541f\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartText plain;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "multipart/mixed Content-Type did not match.");
						Assert.AreEqual ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f", multipart.ContentType.Parameters["boundary"], "multipart/alternative boundary param did not match");
						Assert.AreEqual (1, multipart.BodyParts.Count, "outer multipart/alternative BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "text/plain Content-Type did not match.");
						Assert.AreEqual ("windows-1251", plain.ContentType.Charset, "text/plain charset parameter did not match");
						Assert.AreEqual (356, plain.Octets, "text/plain octets did not match");
						Assert.AreEqual (5, plain.Lines, "text/plain lines did not match");
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
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

		// Note: This tests the work-around for issue #485
		[Test]
		public async Task TestParseBadlyQuotedBodyStructureAsync ()
		{
			const string text = "((\"MOUNDARY=\"_006_5DBB50A5A54730AD4A54730AD4A54730AD4A54730AD42KOS_\"\" \"OCTET-STREAM\" (\"name\" \"test.dat\") NIL NIL \"quoted-printable\" 383137 NIL (\"attachment\" (\"filename\" \"test.dat\")))(\"MOUNDARY=\"_006_5DBB50A5D3ABEC4E85A03EAD527CA5474B3D0AF9E6EXMBXSVR02KOS_\"\" \"OCTET-STREAM\" (\"name\" \"test.dat\") NIL NIL \"quoted-printable\" 383137 NIL (\"attachment\" (\"filename\" \"test.dat\"))) \"MIXED\" (\"boundary\" \"----=_NextPart_000_730AD4A547.730AD4A547F40\"))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("----=_NextPart_000_730AD4A547.730AD4A547F40", body.ContentType.Parameters["boundary"], "boundary param did not match");
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
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
		public async Task TestParseMultipartBodyStructureWithNilBodyFldParamAsync ()
		{
			const string text = "(((\"text\" \"plain\" (\"charset\" \"UTF-8\") NIL NIL \"7bit\" 148 12 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"UTF-8\") NIL NIL \"quoted-printable\" 337 6 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"6c7f221bed92d80548353834d8e2\") NIL NIL NIL)((\"text\" \"plain\" (\"charset\" \"us-ascii\") NIL NIL \"7bit\" 0 0) \"x-zip\" NIL (\"attachment\" (\"filename\" \"YSOZ 265230.ZIP\")) NIL NIL) \"mixed\" (\"boundary\" \"c52bbfc0dd5365efa39b9f80eac3\") NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart, alternative, xzip;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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
		public void TestParseMultipartBodyStructureWithoutBodyFldDsp ()
		{
			// Test case from https://stackoverflow.com/questions/33481604/mailkit-fetch-unexpected-token-in-imap-response-qstring-multipart-message
			const string text = "((\"text\" \"plain\" (\"charset\" \"UTF-8\") NIL \"Message text\" \"Quoted-printable\" 209 6 NIL (\"inline\" NIL) NIL NIL)(\"text\" \"xml\" (\"name\" \"4441004299066.xml\") NIL \"4441004299066.xml\" \"Base64\" 10642 137 NIL (\"inline\" (\"filename\" \"4441004299066.xml\")) NIL NIL)(\"application\" \"pdf\" (\"name\" \"4441004299066.pdf\") NIL \"4441004299066.pdf\" \"Base64\" 48448 NIL (\"inline\" (\"filename\" \"4441004299066.pdf\")) NIL NIL) \"mixed\" (\"boundary\" \"6624CFB2_17170C36_Synapse_boundary\") \"Multipart message\" NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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
						Assert.AreEqual ("6624CFB2_17170C36_Synapse_boundary", body.ContentType.Parameters ["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.AreEqual ("plain", basic.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual ("Message text", basic.ContentDescription, "Content-Description does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("xml", basic.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual ("4441004299066.xml", basic.ContentDescription, "Content-Description does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[2], "The type of the third child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[2];
						Assert.AreEqual ("application", basic.ContentType.MediaType, "Content-Type did not match.");
						Assert.AreEqual ("pdf", basic.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual ("4441004299066.pdf", basic.ContentDescription, "Content-Description does not match.");
					}
				}
			}
		}

		[Test]
		public async Task TestParseMultipartBodyStructureWithoutBodyFldDspAsync ()
		{
			// Test case from https://stackoverflow.com/questions/33481604/mailkit-fetch-unexpected-token-in-imap-response-qstring-multipart-message
			const string text = "((\"text\" \"plain\" (\"charset\" \"UTF-8\") NIL \"Message text\" \"Quoted-printable\" 209 6 NIL (\"inline\" NIL) NIL NIL)(\"text\" \"xml\" (\"name\" \"4441004299066.xml\") NIL \"4441004299066.xml\" \"Base64\" 10642 137 NIL (\"inline\" (\"filename\" \"4441004299066.xml\")) NIL NIL)(\"application\" \"pdf\" (\"name\" \"4441004299066.pdf\") NIL \"4441004299066.pdf\" \"Base64\" 48448 NIL (\"inline\" (\"filename\" \"4441004299066.pdf\")) NIL NIL) \"mixed\" (\"boundary\" \"6624CFB2_17170C36_Synapse_boundary\") \"Multipart message\" NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("6624CFB2_17170C36_Synapse_boundary", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.AreEqual ("plain", basic.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual ("Message text", basic.ContentDescription, "Content-Description does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("xml", basic.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual ("4441004299066.xml", basic.ContentDescription, "Content-Description does not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[2], "The type of the third child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[2];
						Assert.AreEqual ("application", basic.ContentType.MediaType, "Content-Type did not match.");
						Assert.AreEqual ("pdf", basic.ContentType.MediaSubtype, "Content-Type did not match.");
						Assert.AreEqual ("4441004299066.pdf", basic.ContentDescription, "Content-Description does not match.");
					}
				}
			}
		}

		// Note: This tests the work-around for issue #919
		[Test]
		public void TestParseBodyStructureWithNonParenthesizedBodyFldDsp ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"ISO-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 850 31 NIL \"inline\" NIL NIL)(\"text\" \"html\" (\"charset\" \"ISO-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 14692 502 NIL \"inline\" NIL NIL) \"alternative\" (\"boundary\" \"----=_Part_45280395_786508794.1562673197246\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartText plain, html;
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

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "alternative"), "Content-Type did not match.");
						Assert.AreEqual ("----=_Part_45280395_786508794.1562673197246", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.AreEqual ("text/plain", plain.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("ISO-8859-1", plain.ContentType.Charset, "Content-Type charset parameter did not match.");
						Assert.AreEqual ("QUOTED-PRINTABLE", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (850, plain.Octets, "Octets did not match.");
						Assert.AreEqual (31, plain.Lines, "Lines did not match.");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "Content-Disposition did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.AreEqual ("text/html", html.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("ISO-8859-1", html.ContentType.Charset, "Content-Type charset parameter did not match.");
						Assert.AreEqual ("QUOTED-PRINTABLE", html.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (14692, html.Octets, "Octets did not match.");
						Assert.AreEqual (502, html.Lines, "Lines did not match.");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "Content-Disposition did not match.");
					}
				}
			}
		}

		// Note: This tests the work-around for issue #919
		[Test]
		public async Task TestParseBodyStructureWithNonParenthesizedBodyFldDspAsync ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"ISO-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 850 31 NIL \"inline\" NIL NIL)(\"text\" \"html\" (\"charset\" \"ISO-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 14692 502 NIL \"inline\" NIL NIL) \"alternative\" (\"boundary\" \"----=_Part_45280395_786508794.1562673197246\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartText plain, html;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "alternative"), "Content-Type did not match.");
						Assert.AreEqual ("----=_Part_45280395_786508794.1562673197246", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.AreEqual ("text/plain", plain.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("ISO-8859-1", plain.ContentType.Charset, "Content-Type charset parameter did not match.");
						Assert.AreEqual ("QUOTED-PRINTABLE", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (850, plain.Octets, "Octets did not match.");
						Assert.AreEqual (31, plain.Lines, "Lines did not match.");
						Assert.AreEqual ("inline", plain.ContentDisposition.Disposition, "Content-Disposition did not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[1], "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.AreEqual ("text/html", html.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("ISO-8859-1", html.ContentType.Charset, "Content-Type charset parameter did not match.");
						Assert.AreEqual ("QUOTED-PRINTABLE", html.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (14692, html.Octets, "Octets did not match.");
						Assert.AreEqual (502, html.Lines, "Lines did not match.");
						Assert.AreEqual ("inline", html.ContentDisposition.Disposition, "Content-Disposition did not match.");
					}
				}
			}
		}

		// Note: This tests the work-around for an Exchange bug
		[Test]
		public void TestParseBodyStructureWithNilNilBodyFldDsp ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"iso-8859-1\") NIL \"Mail message body\" \"quoted-printable\" 2201 34 NIL NIL NIL NIL)(\"application\" \"msword\" NIL NIL NIL \"base64\" 50446 NIL (NIL NIL) NIL NIL)(\"application\" \"msword\" NIL NIL NIL \"base64\" 45544 NIL (\"attachment\" (\"filename\" \"PREIS ANSPRUCHS FORMULAR.doc\")) NIL NIL) \"mixed\" (\"boundary\" \"===============1176586998==\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic msword;
						BodyPartText plain;
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
						Assert.AreEqual ("===============1176586998==", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.AreEqual ("text/plain", plain.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "Content-Type charset parameter did not match.");
						Assert.AreEqual ("quoted-printable", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual ("Mail message body", plain.ContentDescription, "Content-Description did not match.");
						Assert.AreEqual (2201, plain.Octets, "Octets did not match.");
						Assert.AreEqual (34, plain.Lines, "Lines did not match.");
						Assert.IsNull (plain.ContentDisposition, "Content-Disposition did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("application/msword", msword.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("base64", msword.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (50446, msword.Octets, "Octets did not match.");
						Assert.IsNull (msword.ContentDisposition, "Content-Disposition did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[2], "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[2];
						Assert.AreEqual ("application/msword", msword.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("base64", msword.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (45544, msword.Octets, "Octets did not match.");
						Assert.AreEqual ("attachment", msword.ContentDisposition.Disposition, "Content-Disposition did not match.");
						Assert.AreEqual ("PREIS ANSPRUCHS FORMULAR.doc", msword.ContentDisposition.FileName, "Filename parameters do not match.");
					}
				}
			}
		}

		// Note: This tests the work-around for an Exchange bug
		[Test]
		public async Task TestParseBodyStructureWithNilNilBodyFldDspAsync ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"iso-8859-1\") NIL \"Mail message body\" \"quoted-printable\" 2201 34 NIL NIL NIL NIL)(\"application\" \"msword\" NIL NIL NIL \"base64\" 50446 NIL (NIL NIL) NIL NIL)(\"application\" \"msword\" NIL NIL NIL \"base64\" 45544 NIL (\"attachment\" (\"filename\" \"PREIS ANSPRUCHS FORMULAR.doc\")) NIL NIL) \"mixed\" (\"boundary\" \"===============1176586998==\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPartBasic msword;
						BodyPartText plain;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (body.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("===============1176586998==", body.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.AreEqual ("text/plain", plain.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("iso-8859-1", plain.ContentType.Charset, "Content-Type charset parameter did not match.");
						Assert.AreEqual ("quoted-printable", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual ("Mail message body", plain.ContentDescription, "Content-Description did not match.");
						Assert.AreEqual (2201, plain.Octets, "Octets did not match.");
						Assert.AreEqual (34, plain.Lines, "Lines did not match.");
						Assert.IsNull (plain.ContentDisposition, "Content-Disposition did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("application/msword", msword.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("base64", msword.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (50446, msword.Octets, "Octets did not match.");
						Assert.IsNull (msword.ContentDisposition, "Content-Disposition did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[2], "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[2];
						Assert.AreEqual ("application/msword", msword.ContentType.MimeType, "Content-Type did not match.");
						Assert.AreEqual ("base64", msword.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (45544, msword.Octets, "Octets did not match.");
						Assert.AreEqual ("attachment", msword.ContentDisposition.Disposition, "Content-Disposition did not match.");
						Assert.AreEqual ("PREIS ANSPRUCHS FORMULAR.doc", msword.ContentDisposition.FileName, "Filename parameters do not match.");
					}
				}
			}
		}

		[Test]
		public void TestParseBodyStructureWithSwappedBodyFldDspAndBodyFldLang ()
		{
			const string text = "(((\"text\" \"plain\" (\"format\" \"flowed\" \"charset\" \"UTF-8\") NIL NIL \"8bit\" 314 8 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"UTF-8\") NIL NIL \"8bit\" 763 18 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"b3_f0dcbd2fdb06033cba91309b09af1cd8\") NIL NIL NIL NIL)(\"image\" \"jpeg\" (\"name\" \"18e5ca259ceb18af6dd3ea0659f83a4c\") \"<18e5ca259ceb18af6dd3ea0659f83a4c>\" NIL \"base64\" 334384 NIL NIL NIL NIL)(\"image\" \"png\" (\"name\" \"87c487a1ff757e32ee27ff267d28af35\") \"<87c487a1ff757e32ee27ff267d28af35>\" NIL \"base64\" 375634 NIL NIL NIL NIL) \"related\" (\"type\" \"multipart/alternative\" \"charset\" \"UTF-8\" \"boundary\" \"b1_f0dcbd2fdb06033cba91309b09af1cd8\") NIL (\"inline\" NIL) NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "related"), "Content-Type did not match.");
						Assert.AreEqual ("b1_f0dcbd2fdb06033cba91309b09af1cd8", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "Content-Language lengths do not match.");
						Assert.AreEqual ("inline", multipart.ContentLanguage[0], "Content-Language does not match.");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBodyStructureWithSwappedBodyFldDspAndBodyFldLangAsync ()
		{
			const string text = "(((\"text\" \"plain\" (\"format\" \"flowed\" \"charset\" \"UTF-8\") NIL NIL \"8bit\" 314 8 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"UTF-8\") NIL NIL \"8bit\" 763 18 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"b3_f0dcbd2fdb06033cba91309b09af1cd8\") NIL NIL NIL NIL)(\"image\" \"jpeg\" (\"name\" \"18e5ca259ceb18af6dd3ea0659f83a4c\") \"<18e5ca259ceb18af6dd3ea0659f83a4c>\" NIL \"base64\" 334384 NIL NIL NIL NIL)(\"image\" \"png\" (\"name\" \"87c487a1ff757e32ee27ff267d28af35\") \"<87c487a1ff757e32ee27ff267d28af35>\" NIL \"base64\" 375634 NIL NIL NIL NIL) \"related\" (\"type\" \"multipart/alternative\" \"charset\" \"UTF-8\" \"boundary\" \"b1_f0dcbd2fdb06033cba91309b09af1cd8\") NIL (\"inline\" NIL) NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartMultipart multipart;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "related"), "Content-Type did not match.");
						Assert.AreEqual ("b1_f0dcbd2fdb06033cba91309b09af1cd8", multipart.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "BodyParts count does not match.");
						Assert.AreEqual (1, multipart.ContentLanguage.Length, "Content-Language lengths do not match.");
						Assert.AreEqual ("inline", multipart.ContentLanguage[0], "Content-Language does not match.");
					}
				}
			}
		}

		// This tests a work-around for a bug in Exchange that was reported via email.
		[Test]
		public void TestParseBodyStructureWithNegativeOctetValue ()
		{
			const string text = "(\"multipart\" \"digest\" (\"boundary\" \"ommgDs4vJ6fX2nQAghXj4aUy9wsHMMDb\") NIL NIL \"7BIT\" -1 NIL NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
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

						Assert.IsInstanceOf<BodyPartBasic> (body, "Body types did not match.");
						basic = (BodyPartBasic) body;

						Assert.IsTrue (basic.ContentType.IsMimeType ("multipart", "digest"), "Content-Type did not match.");
						Assert.AreEqual ("ommgDs4vJ6fX2nQAghXj4aUy9wsHMMDb", basic.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual ("7BIT", basic.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (0, basic.Octets, "Octets did not match.");
						Assert.IsNull (basic.ContentDisposition, "Content-Disposition did not match.");
					}
				}
			}
		}

		// This tests a work-around for a bug in Exchange that was reported via email.
		[Test]
		public async Task TestParseBodyStructureWithNegativeOctetValueAsync ()
		{
			const string text = "(\"multipart\" \"digest\" (\"boundary\" \"ommgDs4vJ6fX2nQAghXj4aUy9wsHMMDb\") NIL NIL \"7BIT\" -1 NIL NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPartBasic basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartBasic> (body, "Body types did not match.");
						basic = (BodyPartBasic) body;

						Assert.IsTrue (basic.ContentType.IsMimeType ("multipart", "digest"), "Content-Type did not match.");
						Assert.AreEqual ("ommgDs4vJ6fX2nQAghXj4aUy9wsHMMDb", basic.ContentType.Parameters["boundary"], "boundary param did not match");
						Assert.AreEqual ("7BIT", basic.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (0, basic.Octets, "Octets did not match.");
						Assert.IsNull (basic.ContentDisposition, "Content-Disposition did not match.");
					}
				}
			}
		}

		[Test]
		public void TestParseBodyStructureWithNilMultipartBody ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"utf-8\") NIL NIL \"7bit\" 727 16 NIL NIL NIL NIL)(\"message\" \"delivery-status\" (\"name\" \"Delivery status\") NIL NIL \"7bit\" 416 NIL NIL NIL NIL)(\"message\" \"rfc822\" (\"name\" \"Message headers\") NIL NIL \"7bit\" 903 (\"Mon, 17 Nov 2014 13:29:21 +0100\" \"Re: Adresy\" ((\"username\" NIL \"e.username\" \"example.com\")) ((\"username\" NIL \"e.username\" \"example.com\")) ((\"username\" NIL \"e.username\" \"example.com\")) ((\"=?utf-8?Q?Justyna?=\" NIL \"salesde\" \"some-company.eu\")) ((NIL NIL \"saleseu\" \"some-company.eu\")(\"Bogdan\" NIL \"bogdan\" \"some-company.eu\")) NIL \"<004901d00260$35405970$9fc10c50$@some-company.eu>\" \"<D6173425-9D71-4D78-8C3C-1CEB03BCB4D0@example.com>\") (NIL \"alternative\" (\"boundary\" \"Apple-Mail=_352FCEEC-EB15-428F-9D8B-D3B4259DD646\") NIL NIL NIL) 17 NIL NIL NIL NIL) \"report\" (\"report-type\" \"delivery-status\" \"boundary\" \"_e0d7475d888f9882b71de053e5efb221_idea\") NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "report"), "Content-Type did not match.");
						Assert.AreEqual ("delivery-status", multipart.ContentType.Parameters["report-type"], "report-type param did not match");
						Assert.AreEqual ("_e0d7475d888f9882b71de053e5efb221_idea", multipart.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "multipart children did not match");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "First multipart subpart types did not match.");
						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.AreEqual ("7bit", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (727, plain.Octets, "Octets did not match.");
						Assert.AreEqual (16, plain.Lines, "Lines did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "Second multipart subpart types did not match.");
						var deliveryStatus = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("Delivery status", deliveryStatus.ContentType.Name, "name param did not match");
						Assert.AreEqual ("7bit", deliveryStatus.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (416, deliveryStatus.Octets, "Octets did not match.");

						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[2], "Third multipart subpart types did not match.");
						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.AreEqual ("Message headers", rfc822.ContentType.Name, "name param did not match");
						Assert.AreEqual ("7bit", rfc822.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (903, rfc822.Octets, "Octets did not match.");
						Assert.AreEqual (17, rfc822.Lines, "Lines did not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (rfc822.Body, "rfc822 body types did not match.");
						var alternative = (BodyPartMultipart) rfc822.Body;
						Assert.IsTrue (alternative.ContentType.IsMimeType ("multipart", "alternative"), "Content-Type did not match.");
						Assert.AreEqual ("Apple-Mail=_352FCEEC-EB15-428F-9D8B-D3B4259DD646", alternative.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (0, alternative.BodyParts.Count, "alternative bodyparts count did not match.");
					}
				}
			}
		}

		[Test]
		public async Task TestParseBodyStructureWithNilMultipartBodyAsync ()
		{
			const string text = "((\"text\" \"plain\" (\"charset\" \"utf-8\") NIL NIL \"7bit\" 727 16 NIL NIL NIL NIL)(\"message\" \"delivery-status\" (\"name\" \"Delivery status\") NIL NIL \"7bit\" 416 NIL NIL NIL NIL)(\"message\" \"rfc822\" (\"name\" \"Message headers\") NIL NIL \"7bit\" 903 (\"Mon, 17 Nov 2014 13:29:21 +0100\" \"Re: Adresy\" ((\"username\" NIL \"e.username\" \"example.com\")) ((\"username\" NIL \"e.username\" \"example.com\")) ((\"username\" NIL \"e.username\" \"example.com\")) ((\"=?utf-8?Q?Justyna?=\" NIL \"salesde\" \"some-company.eu\")) ((NIL NIL \"saleseu\" \"some-company.eu\")(\"Bogdan\" NIL \"bogdan\" \"some-company.eu\")) NIL \"<004901d00260$35405970$9fc10c50$@some-company.eu>\" \"<D6173425-9D71-4D78-8C3C-1CEB03BCB4D0@example.com>\") (NIL \"alternative\" (\"boundary\" \"Apple-Mail=_352FCEEC-EB15-428F-9D8B-D3B4259DD646\") NIL NIL NIL) 17 NIL NIL NIL NIL) \"report\" (\"report-type\" \"delivery-status\" \"boundary\" \"_e0d7475d888f9882b71de053e5efb221_idea\") NIL NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "report"), "Content-Type did not match.");
						Assert.AreEqual ("delivery-status", multipart.ContentType.Parameters["report-type"], "report-type param did not match");
						Assert.AreEqual ("_e0d7475d888f9882b71de053e5efb221_idea", multipart.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "multipart children did not match");

						Assert.IsInstanceOf<BodyPartText> (multipart.BodyParts[0], "First multipart subpart types did not match.");
						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.AreEqual ("7bit", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (727, plain.Octets, "Octets did not match.");
						Assert.AreEqual (16, plain.Lines, "Lines did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[1], "Second multipart subpart types did not match.");
						var deliveryStatus = (BodyPartBasic) multipart.BodyParts[1];
						Assert.AreEqual ("Delivery status", deliveryStatus.ContentType.Name, "name param did not match");
						Assert.AreEqual ("7bit", deliveryStatus.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (416, deliveryStatus.Octets, "Octets did not match.");

						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[2], "Third multipart subpart types did not match.");
						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.AreEqual ("Message headers", rfc822.ContentType.Name, "name param did not match");
						Assert.AreEqual ("7bit", rfc822.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (903, rfc822.Octets, "Octets did not match.");
						Assert.AreEqual (17, rfc822.Lines, "Lines did not match.");

						Assert.IsInstanceOf<BodyPartMultipart> (rfc822.Body, "rfc822 body types did not match.");
						var alternative = (BodyPartMultipart) rfc822.Body;
						Assert.IsTrue (alternative.ContentType.IsMimeType ("multipart", "alternative"), "Content-Type did not match.");
						Assert.AreEqual ("Apple-Mail=_352FCEEC-EB15-428F-9D8B-D3B4259DD646", alternative.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (0, alternative.BodyParts.Count, "alternative bodyparts count did not match.");
					}
				}
			}
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithCompletelyNilBodyParts1 ()
		{
			const string text = "((((\"text\" \"plain\" (\"charset\" \"us-ascii\") NIL NIL \"quoted-printable\" 44619 793 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"us-ascii\") NIL NIL \"quoted-printable\" 143984 2321 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"000_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP\") NIL NIL)(\"image\" \"jpeg\" (\"name\" \"~WRD0000.jpg\") \"<~WRD0000.jpg>\" \"~WRD0000.jpg\" \"base64\" 1130 NIL (\"inline\" (\"filename\" \"~WRD0000.jpg\" \"size\" \"823\" \"creation-date\" \"Thu, 14 Jul 2022 17:26:49 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:16 GMT\")) NIL NIL)(\"image\" \"png\" (\"name\" \"image001.png\") \"image001.png@01D89786.45095140\" \"image001.png\" \"base64\" 8174 NIL (\"inline\" (\"filename\" \"image001.png\" \"size\" \"5973\" \"creation-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL)(\"image\" \"png\" (\"name\" \"image002.png\") \"image002.png@01D89786.45095140\" \"image002.png\" \"base64\" 3524 NIL (\"inline\" (\"filename\" \"image002.png\" \"size\" \"2572\" \"creation-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL) \"related\" (\"boundary\" \"007_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP\" \"type\" \"multipart/alternative\") NIL NIL)(\"message\" \"rfc822\" NIL NIL NIL \"7BIT\" 0 (\"Thu, 14 Jul 2022 15:12:33 +0000\" \"Scrubbed\" ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL NIL ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\") (\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL \"Scrubbed@Scrubbed.com\" \"Scrubbed@Scrubbed.com\") ((((\"text\" \"plain\" (\"charset\" \"utf-8\") NIL NIL \"base64\" 53608 688 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"utf-8\") \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" NIL \"base64\" 176002 2257 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"000_18f52bea798548b88470c3df62d666bcScrubbed\") NIL NIL)(\"image\" \"png\" (\"name\" \"image001.png\") \"image001.png@01D89770.62F36800\" \"image001.png\" \"base64\" 8174 NIL (\"inline\" (\"filename\" \"image001.png\" \"size\" \"5973\" \"creation-date\" \"Thu, 14 Jul 2022 15:12:32 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL)(\"image\" \"jpeg\" (\"name\" \"image002.jpg\") \"image002.jpg@01D89770.62F36800\" \"image002.jpg\" \"base64\" 1130 NIL (\"inline\" (\"filename\" \"image002.jpg\" \"size\" \"823\" \"creation-date\" \"Thu, 14 Jul 2022 15:12:32 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL)(\"image\" \"png\" (\"name\" \"image003.png\") \"image003.png@01D89770.62F36800\" \"image003.png\" \"base64\" 3524 NIL (\"inline\" (\"filename\" \"image003.png\" \"size\" \"2572\" \"creation-date\" \"Thu, 14 Jul 2022 15:12:32 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL) \"related\" (\"boundary\" \"009_18f52bea798548b88470c3df62d666bcScrubbed\" \"type\" \"multipart/alternative\") NIL NIL)(\"application\" \"pdf\" (\"name\" \"Scrubbed.pdf\") \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" \"Scrubbed.pdf\" \"base64\" 324012 NIL (\"attachment\" (\"filename\" \"Scrubbed.pdf\" \"size\" \"236776\" \"creation-date\" \"Thu, 14 Jul 2022 14:53:00 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL)(\"message\" \"rfc822\" NIL \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" NIL \"7BIT\" 0 (\"Tue, 11 Jan 2022 16:34:33 +0000\" \"RE: Scrubbed\" ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL NIL ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\") (\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\") (\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL \"Scrubbed@Scrubbed.com\" \"Scrubbed@Scrubbed.CANPRD01.PROD.OUTLOOK.COM\") (((NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL)(NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL)(NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL)(NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL) \"related\" (\"boundary\" \"007_YT2PR01MB47524CF92A3AD1F75AFF2D25D9519YT2PR01MB4752CANP\" \"type\" \"multipart/alternative\") NIL NIL)(\"application\" \"pdf\" (\"name\" \"Scrubbed.pdf\") NIL \"Scrubbed.pdf\" \"base64\" 215638 NIL (\"attachment\" (\"filename\" \"Scrubbed.pdf\" \"size\" \"157579\" \"creation-date\" \"Wed, 02 Feb 2022 21:33:39 GMT\" \"modification-date\" \"Wed, 02 Feb 2022 21:33:39 GMT\")) NIL NIL) \"mixed\" (\"boundary\" \"008_YT2PR01MB47524CF92A3AD1F75AFF2D25D9519YT2PR01MB4752CANP\") NIL \"en-US\") 0 NIL (\"attachment\" (\"creation-date\" \"Thu, 14 Jul 2022 15:12:31 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL)(\"application\" \"pdf\" (\"name\" \"Scrubbed.pdf?=\") \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" \"Scrubbed.pdf?=\" \"base64\" 208376 NIL (\"attachment\" (\"filename\" \"Scrubbed.pdf?=\" \"size\" \"152274\" \"creation-date\" \"Thu, 14 Jul 2022 15:05:00 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL) \"mixed\" (\"boundary\" \"010_18f52bea798548b88470c3df62d666bcScrubbed\") NIL \"en-US\") 0 NIL (\"attachment\" (\"creation-date\" \"Thu, 14 Jul 2022 17:33:16 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL) \"mixed\" (\"boundary\" \"008_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP\") NIL \"en-US\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "mixed"), "Content-Type did not match.");
						Assert.AreEqual ("008_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP", multipart.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (2, multipart.BodyParts.Count, "multipart children did not match");

						Assert.IsInstanceOf<BodyPartMultipart> (multipart.BodyParts[0], "First multipart/mixed subpart types did not match.");
						var related = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.IsTrue (related.ContentType.IsMimeType ("multipart", "related"), "Content-Type did not match.");
						Assert.AreEqual ("multipart/alternative", related.ContentType.Parameters["type"], "type param did not match");
						Assert.AreEqual ("007_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP", related.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (4, related.BodyParts.Count, "multipart children did not match");

						Assert.IsInstanceOf<BodyPartMultipart> (related.BodyParts[0], "First multipart/related subpart types did not match.");
						var alternative = (BodyPartMultipart) related.BodyParts[0];
						Assert.IsTrue (alternative.ContentType.IsMimeType ("multipart", "alternative"), "Content-Type did not match.");
						Assert.AreEqual ("000_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP", alternative.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (2, alternative.BodyParts.Count, "multipart children did not match");

						Assert.IsInstanceOf<BodyPartText> (alternative.BodyParts[0], "First multipart/alternative subpart types did not match.");
						var plain = (BodyPartText) alternative.BodyParts[0];
						Assert.IsTrue (plain.ContentType.IsMimeType ("text", "plain"), "Content-Type did not match.");
						Assert.AreEqual ("us-ascii", plain.ContentType.Charset, "Charset parameter did not match");
						Assert.AreEqual ("quoted-printable", plain.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (44619, plain.Octets, "Octets did not match.");
						Assert.AreEqual (793, plain.Lines, "Lines did not match.");

						Assert.IsInstanceOf<BodyPartText> (alternative.BodyParts[1], "Second multipart/alternative subpart types did not match.");
						var html = (BodyPartText) alternative.BodyParts[1];
						Assert.IsTrue (html.ContentType.IsMimeType ("text", "html"), "Content-Type did not match.");
						Assert.AreEqual ("quoted-printable", html.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (143984, html.Octets, "Octets did not match.");
						Assert.AreEqual (2321, html.Lines, "Lines did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (related.BodyParts[1], "Second multipart/related subpart types did not match.");
						var jpeg = (BodyPartBasic) related.BodyParts[1];
						Assert.IsTrue (jpeg.ContentType.IsMimeType ("image", "jpeg"), "Content-Type did not match.");
						Assert.AreEqual ("~WRD0000.jpg", jpeg.ContentType.Name, "Name parameter did not match");
						Assert.AreEqual ("inline", jpeg.ContentDisposition.Disposition, "Disposition did not match");
						Assert.AreEqual ("~WRD0000.jpg", jpeg.ContentDisposition.FileName, "Filename parameter did not match");
						Assert.AreEqual ("base64", jpeg.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (1130, jpeg.Octets, "Octets did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (related.BodyParts[2], "Third multipart/related subpart types did not match.");
						var png = (BodyPartBasic) related.BodyParts[2];
						Assert.IsTrue (png.ContentType.IsMimeType ("image", "png"), "Content-Type did not match.");
						Assert.AreEqual ("image001.png", png.ContentType.Name, "Name parameter did not match");
						Assert.AreEqual ("inline", png.ContentDisposition.Disposition, "Disposition did not match");
						Assert.AreEqual ("image001.png", png.ContentDisposition.FileName, "Filename parameter did not match");
						Assert.AreEqual ("base64", png.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (8174, png.Octets, "Octets did not match.");

						Assert.IsInstanceOf<BodyPartBasic> (related.BodyParts[3], "Fourth multipart/related subpart types did not match.");
						png = (BodyPartBasic) related.BodyParts[3];
						Assert.IsTrue (png.ContentType.IsMimeType ("image", "png"), "Content-Type did not match.");
						Assert.AreEqual ("image002.png", png.ContentType.Name, "Name parameter did not match");
						Assert.AreEqual ("inline", png.ContentDisposition.Disposition, "Disposition did not match");
						Assert.AreEqual ("image002.png", png.ContentDisposition.FileName, "Filename parameter did not match");
						Assert.AreEqual ("base64", png.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (3524, png.Octets, "Octets did not match.");

						Assert.IsInstanceOf<BodyPartMessage> (multipart.BodyParts[1], "Second multipart/mixed subpart types did not match.");
						var rfc822 = (BodyPartMessage) multipart.BodyParts[1];
						Assert.AreEqual (null, rfc822.ContentType.Name, "name param did not match");
						Assert.AreEqual ("7BIT", rfc822.ContentTransferEncoding, "Content-Transfer-Encoding did not match.");
						Assert.AreEqual (0, rfc822.Octets, "Octets did not match.");
						Assert.AreEqual (0, rfc822.Lines, "Lines did not match.");

						// Okay, lets skip ahead to the juicy bits...
						multipart = (BodyPartMultipart) rfc822.Body;
						Assert.AreEqual ("010_18f52bea798548b88470c3df62d666bcScrubbed", multipart.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (4, multipart.BodyParts.Count, "multipart children did not match");

						rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						multipart = (BodyPartMultipart) rfc822.Body;
						alternative = (BodyPartMultipart) multipart.BodyParts[0];

						for (int i = 0; i < alternative.BodyParts.Count; i++) {
							var nils = (BodyPartBasic) alternative.BodyParts[i];

							Assert.IsTrue (nils.ContentType.IsMimeType ("application", "octet-stream"), "Content-Type did not match.");
							Assert.IsNull (nils.ContentDescription, "Content-Description should be null");
							Assert.IsNull (nils.ContentDisposition, "Content-Disposition should be null");
							Assert.IsNull (nils.ContentId, "Content-Id should be null");
							Assert.IsNull (nils.ContentLanguage, "Content-Language should be null");
							Assert.IsNull (nils.ContentLocation, "Content-Location should be null");
							Assert.IsNull (nils.ContentMd5, "Content-Md5 should be null");
							Assert.AreEqual ("7BIT", nils.ContentTransferEncoding, "Content-Transfer-Encodings did not match");
							Assert.AreEqual (0, nils.Octets, "Octets did not match");
						}
					}
				}
			}
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithCompletelyNilBodyParts2 ()
		{
			const string text = "((NIL NIL NIL NIL NIL \"7BIT\" 563 NIL NIL NIL NIL)(\"message\" \"delivery-status\" NIL NIL NIL \"7BIT\" 658 NIL NIL NIL NIL)(\"message\" \"rfc822\" NIL NIL NIL \"8bit\" 0 (\"Tue, 2 Aug 2022 15:00:47 +0000\" \"[POSSIBLE SPAM 11.4] Invoices now overdue - 115365#\" ((NIL NIL \"MAILBOX\" \"OUR-DOMAIN\")) NIL NIL ((NIL NIL \"accounts\" \"OTHER-DOMAIN\") (NIL NIL \"safety\" \"OTHER-DOMAIN\") (NIL NIL \"USER\" \"OUR-DOMAIN\")) NIL NIL NIL \"<1IOGPFNLIHU4.377MHPZYJQ6E3@OUR-SERVER>\") (((\"text\" \"plain\" (\"charset\" \"utf-8\") NIL NIL \"8bit\" 597 16 NIL NIL NIL NIL)((\"text\" \"html\" (\"charset\" \"utf-8\") NIL NIL \"7BIT\" 1611 26 NIL NIL NIL NIL)(\"image\" \"png\" (\"name\" \"0.dat\") \"<1KWGPFNLIHU4.4RR7HCVM8MQQ1@OUR-SERVER>\" NIL \"base64\" 14172 NIL (\"inline\" (\"filename\" \"0.dat\")) NIL \"0.dat\")(\"image\" \"png\" (\"name\" \"1.dat\") \"<1KWGPFNLIHU4.UWJ8R86RE2KA2@OUR-SERVER>\" NIL \"base64\" 486 NIL (\"inline\" (\"filename\" \"1.dat\")) NIL \"1.dat\")(\"image\" \"png\" (\"name\" \"2.dat\") \"<1KWGPFNLIHU4.EC7HN124OJC32@OUR-SERVER>\" NIL \"base64\" 506 NIL (\"inline\" (\"filename\" \"2.dat\")) NIL \"2.dat\")(\"image\" \"png\" (\"name\" \"3.dat\") \"<1KWGPFNLIHU4.WM1ALJTG745F1@OUR-SERVER>\" NIL \"base64\" 616 NIL (\"inline\" (\"filename\" \"3.dat\")) NIL \"3.dat\")(\"image\" \"png\" (\"name\" \"4.dat\") \"<1KWGPFNLIHU4.1B42S5EVSF4B2@OUR-SERVER>\" NIL \"base64\" 22470 NIL (\"inline\" (\"filename\" \"4.dat\")) NIL \"4.dat\") \"related\" (\"boundary\" \"=-5nEE2FIlRoeXkJyZAHV8UA==\" \"type\" \"text/html\") NIL NIL) \"alternative\" (\"boundary\" \"=-1sRjeMizXVbc5nGIFXbARA==\") NIL NIL)(\"application\" \"pdf\" (\"name\" \"Reminder.pdf\") \"<RJ2DSFNLIHU4.UUVSNNY5Z3ER@OUR-SERVER>\" NIL \"base64\" 359650 NIL (\"attachment\" (\"filename\" \"Reminder.pdf\" \"size\" \"262820\")) NIL NIL) \"mixed\" (\"boundary\" \"=-EJwVTfPtacyNnTqY4DPQ0A==\") NIL NIL) 0 NIL NIL NIL NIL) \"report\" (\"report-type\" \"delivery-status\" \"boundary\" \"272F16D4031920.1659452466/hermes.gatewaynet.com\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODYSTRUCTURE failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOf<BodyPartMultipart> (body, "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.IsTrue (multipart.ContentType.IsMimeType ("multipart", "report"), "Content-Type did not match.");
						Assert.AreEqual ("272F16D4031920.1659452466/hermes.gatewaynet.com", multipart.ContentType.Boundary, "boundary param did not match");
						Assert.AreEqual (3, multipart.BodyParts.Count, "multipart children did not match");

						Assert.IsInstanceOf<BodyPartBasic> (multipart.BodyParts[0], "First multipart/report subpart types did not match.");
						var nils = (BodyPartBasic) multipart.BodyParts[0];
						Assert.IsTrue (nils.ContentType.IsMimeType ("application", "octet-stream"), "Content-Type did not match.");
						Assert.IsNull (nils.ContentDescription, "Content-Description should be null");
						Assert.IsNull (nils.ContentDisposition, "Content-Disposition should be null");
						Assert.IsNull (nils.ContentId, "Content-Id should be null");
						Assert.IsNull (nils.ContentLanguage, "Content-Language should be null");
						Assert.IsNull (nils.ContentLocation, "Content-Location should be null");
						Assert.IsNull (nils.ContentMd5, "Content-Md5 should be null");
						Assert.AreEqual ("7BIT", nils.ContentTransferEncoding, "Content-Transfer-Encodings did not match");
						Assert.AreEqual (563, nils.Octets, "Octets did not match");
					}
				}
			}
		}

		[Test]
		public void TestParseExampleThreads ()
		{
			const string text = "(2)(3 6 (4 23)(44 7 96))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

		[Test]
		public async Task TestParseExampleThreadsAsync ()
		{
			const string text = "(2)(3 6 (4 23)(44 7 96))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<MessageThread> threads;

						engine.SetStream (tokenizer);

						try {
							threads = await ImapUtils.ParseThreadsAsync (engine, 0, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing THREAD response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
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

		[Test]
		public void TestParseLongDovecotExampleThread ()
		{
			const string text = "(3 4 5 6 7)(1)((2)(8)(15))(9)(16)(10)(11)(12 13)(14)(17)(18)(19)(20)(21)(22)(23)(24)(25 (26)(29 39)(31)(32))(27)(28)(38 35)(30 33 34)(37)(36)(40)(41)((42 43)(44)(48)(49)(50)(51 52))(45)((46)(55))(47)(53)(54)(56)(57 (58)(59)(60)(63))((61)(62))(64)(65)(70)((66)(67)(68)(69)(71))(72 73 (74)(75)(76 77))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

						Assert.AreEqual (40, threads.Count, "Expected 40 threads.");

						Assert.AreEqual ((uint) 3, threads[0].UniqueId.Value.Id);
						Assert.AreEqual ((uint) 1, threads[1].UniqueId.Value.Id);
						//Assert.AreEqual ((uint) 0, threads[2].UniqueId.Value.Id);
						Assert.IsFalse (threads[2].UniqueId.HasValue);

						var branches = threads[2].Children.ToArray ();
						Assert.AreEqual (3, branches.Length, "Expected 3 children.");
					}
				}
			}
		}

		[Test]
		public async Task TestParseLongDovecotExampleThreadAsync ()
		{
			const string text = "(3 4 5 6 7)(1)((2)(8)(15))(9)(16)(10)(11)(12 13)(14)(17)(18)(19)(20)(21)(22)(23)(24)(25 (26)(29 39)(31)(32))(27)(28)(38 35)(30 33 34)(37)(36)(40)(41)((42 43)(44)(48)(49)(50)(51 52))(45)((46)(55))(47)(53)(54)(56)(57 (58)(59)(60)(63))((61)(62))(64)(65)(70)((66)(67)(68)(69)(71))(72 73 (74)(75)(76 77))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<MessageThread> threads;

						engine.SetStream (tokenizer);

						try {
							threads = await ImapUtils.ParseThreadsAsync (engine, 0, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing THREAD response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (40, threads.Count, "Expected 40 threads.");

						Assert.AreEqual ((uint) 3, threads[0].UniqueId.Value.Id);
						Assert.AreEqual ((uint) 1, threads[1].UniqueId.Value.Id);
						//Assert.AreEqual ((uint) 0, threads[2].UniqueId.Value.Id);
						Assert.IsFalse (threads[2].UniqueId.HasValue);

						var branches = threads[2].Children.ToArray ();
						Assert.AreEqual (3, branches.Length, "Expected 3 children.");
					}
				}
			}
		}

		[Test]
		public void TestParseShortDovecotExampleThread ()
		{
			const string text = "((352)(381))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
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

						Assert.AreEqual (1, threads.Count, "Expected 1 thread.");

						//Assert.AreEqual ((uint) 0, threads[0].UniqueId.Value.Id);
						Assert.IsFalse (threads[0].UniqueId.HasValue);

						var children = threads[0].Children;
						Assert.AreEqual (2, children.Count, "Expected 2 children.");

						Assert.AreEqual ((uint) 352, children[0].UniqueId.Value.Id);
						Assert.AreEqual ((uint) 381, children[1].UniqueId.Value.Id);
					}
				}
			}
		}

		[Test]
		public async Task TestParseShortDovecotExampleThreadAsync ()
		{
			const string text = "((352)(381))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<MessageThread> threads;

						engine.SetStream (tokenizer);

						try {
							threads = await ImapUtils.ParseThreadsAsync (engine, 0, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing THREAD response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, threads.Count, "Expected 1 thread.");

						//Assert.AreEqual ((uint) 0, threads[0].UniqueId.Value.Id);
						Assert.IsFalse (threads[0].UniqueId.HasValue);

						var children = threads[0].Children;
						Assert.AreEqual (2, children.Count, "Expected 2 children.");

						Assert.AreEqual ((uint) 352, children[0].UniqueId.Value.Id);
						Assert.AreEqual ((uint) 381, children[1].UniqueId.Value.Id);
					}
				}
			}
		}

		[Test]
		public void TestFormatAnnotations ()
		{
			var annotations = new List<Annotation> ();
			var command = new StringBuilder ("STORE ");
			var args = new List<object> ();

			ImapUtils.FormatAnnotations (command, annotations, args, false);
			Assert.AreEqual ("STORE ", command.ToString (), "empty collection");

			annotations.Add (new Annotation (AnnotationEntry.AltSubject));

			ImapUtils.FormatAnnotations (command, annotations, args, false);
			Assert.AreEqual ("STORE ", command.ToString (), "annotation w/o properties");
			Assert.Throws<ArgumentException> (() => ImapUtils.FormatAnnotations (command, annotations, args, true));

			command.Clear ();
			command.Append ("STORE ");
			annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "This is an alternate subject.");
			ImapUtils.FormatAnnotations (command, annotations, args, true);
			Assert.AreEqual ("STORE ANNOTATION (/altsubject (value.shared %S))", command.ToString ());
			Assert.AreEqual (1, args.Count, "args");
			Assert.AreEqual ("This is an alternate subject.", args[0], "args[0]");
		}

		[Test]
		public void TestParseAnnotationsExample1 ()
		{
			const string text = "(/comment (value.priv \"My comment\" value.shared NIL))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<Annotation> annotations;

						engine.SetStream (tokenizer);

						try {
							annotations = ImapUtils.ParseAnnotationsAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ANNOTATION response failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, annotations.Count, "Count");
						Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "Entry");
						Assert.AreEqual (2, annotations[0].Properties.Count, "Properties.Count");
						Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "value.priv");
						Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "value.shared");
					}
				}
			}
		}

		[Test]
		public async Task TestParseAnnotationsExample1Async ()
		{
			const string text = "(/comment (value.priv \"My comment\" value.shared NIL))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<Annotation> annotations;

						engine.SetStream (tokenizer);

						try {
							annotations = await ImapUtils.ParseAnnotationsAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ANNOTATION response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, annotations.Count, "Count");
						Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "Entry");
						Assert.AreEqual (2, annotations[0].Properties.Count, "Properties.Count");
						Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "value.priv");
						Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "value.shared");
					}
				}
			}
		}

		[Test]
		public void TestParseAnnotationsExample2 ()
		{
			const string text = "(/comment (value.priv \"My comment\" value.shared NIL) /altsubject (value.priv \"My subject\" value.shared NIL))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<Annotation> annotations;

						engine.SetStream (tokenizer);

						try {
							annotations = ImapUtils.ParseAnnotationsAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ANNOTATION response failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (2, annotations.Count, "Count");
						Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
						Assert.AreEqual (2, annotations[0].Properties.Count, "annotations[0].Properties.Count");
						Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
						Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
						Assert.AreEqual (AnnotationEntry.AltSubject, annotations[1].Entry, "annotations[1].Entry");
						Assert.AreEqual (2, annotations[1].Properties.Count, "annotations[1].Properties.Count");
						Assert.AreEqual ("My subject", annotations[1].Properties[AnnotationAttribute.PrivateValue], "annotations[1] value.priv");
						Assert.AreEqual (null, annotations[1].Properties[AnnotationAttribute.SharedValue], "annotations[1] value.shared");
					}
				}
			}
		}

		[Test]
		public async Task TestParseAnnotationsExample2Async ()
		{
			const string text = "(/comment (value.priv \"My comment\" value.shared NIL) /altsubject (value.priv \"My subject\" value.shared NIL))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<Annotation> annotations;

						engine.SetStream (tokenizer);

						try {
							annotations = await ImapUtils.ParseAnnotationsAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ANNOTATION response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (2, annotations.Count, "Count");
						Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
						Assert.AreEqual (2, annotations[0].Properties.Count, "annotations[0].Properties.Count");
						Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
						Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
						Assert.AreEqual (AnnotationEntry.AltSubject, annotations[1].Entry, "annotations[1].Entry");
						Assert.AreEqual (2, annotations[1].Properties.Count, "annotations[1].Properties.Count");
						Assert.AreEqual ("My subject", annotations[1].Properties[AnnotationAttribute.PrivateValue], "annotations[1] value.priv");
						Assert.AreEqual (null, annotations[1].Properties[AnnotationAttribute.SharedValue], "annotations[1] value.shared");
					}
				}
			}
		}

		[Test]
		public void TestParseAnnotationsExample3 ()
		{
			const string text = "(/comment (value.priv \"My comment\" value.shared NIL size.priv \"10\" size.shared \"0\"))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<Annotation> annotations;

						engine.SetStream (tokenizer);

						try {
							annotations = ImapUtils.ParseAnnotationsAsync (engine, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing ANNOTATION response failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, annotations.Count, "Count");
						Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
						Assert.AreEqual (4, annotations[0].Properties.Count, "annotations[0].Properties.Count");
						Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
						Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
						Assert.AreEqual ("10", annotations[0].Properties[AnnotationAttribute.PrivateSize], "annotations[0] size.priv");
						Assert.AreEqual ("0", annotations[0].Properties[AnnotationAttribute.SharedSize], "annotations[0] size.shared");
					}
				}
			}
		}

		[Test]
		public async Task TestParseAnnotationsExample3Async ()
		{
			const string text = "(/comment (value.priv \"My comment\" value.shared NIL size.priv \"10\" size.shared \"0\"))\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						IList<Annotation> annotations;

						engine.SetStream (tokenizer);

						try {
							annotations = await ImapUtils.ParseAnnotationsAsync (engine, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing ANNOTATION response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, annotations.Count, "Count");
						Assert.AreEqual (AnnotationEntry.Comment, annotations[0].Entry, "annotations[0].Entry");
						Assert.AreEqual (4, annotations[0].Properties.Count, "annotations[0].Properties.Count");
						Assert.AreEqual ("My comment", annotations[0].Properties[AnnotationAttribute.PrivateValue], "annotations[0] value.priv");
						Assert.AreEqual (null, annotations[0].Properties[AnnotationAttribute.SharedValue], "annotations[0] value.shared");
						Assert.AreEqual ("10", annotations[0].Properties[AnnotationAttribute.PrivateSize], "annotations[0] size.priv");
						Assert.AreEqual ("0", annotations[0].Properties[AnnotationAttribute.SharedSize], "annotations[0] size.shared");
					}
				}
			}
		}

		ImapFolder CreateImapFolder (ImapFolderConstructorArgs args)
		{
			return new ImapFolder (args);
		}

		// Tests the work-around for issue #945
		[Test]
		public void TestParseFolderListWithFolderNameContainingUnquotedTabs ()
		{
			const string text = " (\\HasNoChildren) \"/\" INBOX/Da\tOggetto\tRicevuto\tDimensione\tCategorie\t\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (CreateImapFolder)) {
						var list = new List<ImapFolder> ();

						engine.QuirksMode = ImapQuirksMode.Exchange;
						engine.SetStream (tokenizer);

						try {
							ImapUtils.ParseFolderListAsync (engine, list, false, false, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing LIST response failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, list.Count, "Count");
						Assert.AreEqual ("Da\tOggetto\tRicevuto\tDimensione\tCategorie\t", list[0].Name, "Name");
					}
				}
			}
		}

		[Test]
		public async Task TestParseFolderListWithFolderNameContainingUnquotedTabsAsync ()
		{
			const string text = " (\\HasNoChildren) \"/\" INBOX/Da\tOggetto\tRicevuto\tDimensione\tCategorie\t\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (CreateImapFolder)) {
						var list = new List<ImapFolder> ();

						engine.QuirksMode = ImapQuirksMode.Exchange;
						engine.SetStream (tokenizer);

						try {
							await ImapUtils.ParseFolderListAsync (engine, list, false, false, true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing LIST response failed: {0}", ex);
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.AreEqual (1, list.Count, "Count");
						Assert.AreEqual ("Da\tOggetto\tRicevuto\tDimensione\tCategorie\t", list[0].Name, "Name");
					}
				}
			}
		}
	}
}
