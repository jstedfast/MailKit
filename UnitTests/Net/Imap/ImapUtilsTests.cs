//
// ImapBodyParsingTests.cs
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
using MimeKit.Utils;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapUtilsTests : IDisposable
	{
		readonly ImapEngine engine = new ImapEngine (null);

		public void Dispose ()
		{
			engine.Dispose ();
			GC.SuppressFinalize (this);
		}

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
			Assert.Throws<ArgumentException> (() => ImapUtils.FormatIndexSet (engine, Array.Empty<int> ()));

			Assert.Throws<ArgumentNullException> (() => ImapUtils.FormatIndexSet (engine, null, new int[1]));

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.That (actual, Is.EqualTo (expect), "Formatting a simple range of indexes failed.");
		}

		[Test]
		public void TestFormattingNonSequentialIndexes ()
		{
			int[] indexes = { 0, 2, 4, 6, 8 };
			const string expect = "1,3,5,7,9";
			string actual;

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.That (actual, Is.EqualTo (expect), "Formatting a non-sequential list of indexes.");
		}

		[Test]
		public void TestFormattingComplexSetOfIndexes ()
		{
			int[] indexes = { 0, 1, 2, 4, 5, 8, 9, 10, 11, 14, 18, 19 };
			const string expect = "1:3,5:6,9:12,15,19:20";
			string actual;

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.That (actual, Is.EqualTo (expect), "Formatting a complex list of indexes.");
		}

		[Test]
		public void TestFormattingReversedIndexes ()
		{
			int[] indexes = { 19, 18, 14, 11, 10, 9, 8, 5, 4, 2, 1, 0 };
			const string expect = "20:19,15,12:9,6:5,3:1";
			string actual;

			actual = ImapUtils.FormatIndexSet (engine, indexes);
			Assert.That (actual, Is.EqualTo (expect), "Formatting a complex list of indexes.");
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
			Assert.That (actual, Is.EqualTo (expect), "Formatting a simple range of uids failed.");
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
			Assert.That (actual, Is.EqualTo (expect), "Formatting a non-sequential list of uids.");
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
			Assert.That (actual, Is.EqualTo (expect), "Formatting a complex list of uids.");
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
			Assert.That (actual, Is.EqualTo (expect), "Formatting a complex list of uids.");
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
				Assert.That (ImapUtils.ParseInternalDate (internalDate), Is.EqualTo (DateTimeOffset.MinValue), internalDate);
		}

		[Test]
		public void TestCanonicalizeMailboxName ()
		{
			Assert.That (ImapUtils.CanonicalizeMailboxName ("Name", '.'), Is.EqualTo ("Name"), "Name");
			Assert.That (ImapUtils.CanonicalizeMailboxName ("InbOx", '.'), Is.EqualTo ("INBOX"), "InbOx");
			Assert.That (ImapUtils.CanonicalizeMailboxName ("InboxSubfolder", '.'), Is.EqualTo ("InboxSubfolder"), "InboxSubfolder");
			Assert.That (ImapUtils.CanonicalizeMailboxName ("Inbox.Subfolder", '.'), Is.EqualTo ("INBOX.Subfolder"), "Inbox.Subfolder");
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
							labels = ImapUtils.ParseLabelsList (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing X-GM-LABELS failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");
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
							labels = await ImapUtils.ParseLabelsListAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing X-GM-LABELS failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODY failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartText> (), "Body types did not match.");
						basic = (BodyPartText) body;

						Assert.That (body.ContentType.IsMimeType ("text", "plain"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["charset"], Is.EqualTo ("US-ASCII"), "charset param did not match");

						Assert.That (basic, Is.Not.Null, "The parsed body is not BodyPartText.");
						Assert.That (basic.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encoding did not match.");
						Assert.That (basic.Octets, Is.EqualTo (3028), "Octet count did not match.");
						Assert.That (basic.Lines, Is.EqualTo (92), "Line count did not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODY failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartText> (), "Body types did not match.");
						basic = (BodyPartText) body;

						Assert.That (body.ContentType.IsMimeType ("text", "plain"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["charset"], Is.EqualTo ("US-ASCII"), "charset param did not match");

						Assert.That (basic, Is.Not.Null, "The parsed body is not BodyPartText.");
						Assert.That (basic.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encoding did not match.");
						Assert.That (basic.Octets, Is.EqualTo (3028), "Octet count did not match.");
						Assert.That (basic.Lines, Is.EqualTo (92), "Line count did not match.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Wed, 17 Jul 1996 02:23:25 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("IMAP4rev1 WG mtg summary and minutes"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Reply-To does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("imap@cac.washington.edu"), "To does not match.");

						Assert.That (envelope.Cc, Has.Count.EqualTo (2), "Cc counts do not match.");
						Assert.That (envelope.Cc.ToString (), Is.EqualTo ("minutes@CNRI.Reston.VA.US, \"John Klensin\" <KLENSIN@MIT.EDU>"), "Cc does not match.");

						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("B27397-0100000@cac.washington.edu"), "Message-Id does not match.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Wed, 17 Jul 1996 02:23:25 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("IMAP4rev1 WG mtg summary and minutes"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Reply-To does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("imap@cac.washington.edu"), "To does not match.");

						Assert.That (envelope.Cc, Has.Count.EqualTo (2), "Cc counts do not match.");
						Assert.That (envelope.Cc.ToString (), Is.EqualTo ("minutes@CNRI.Reston.VA.US, \"John Klensin\" <KLENSIN@MIT.EDU>"), "Cc does not match.");

						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("B27397-0100000@cac.washington.edu"), "Message-Id does not match.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Wed, 17 Jul 1996 02:23:25 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("IMAP4rev1 WG mtg summary and minutes"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Reply-To does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("imap@cac.washington.edu"), "To does not match.");

						Assert.That (envelope.Cc, Has.Count.EqualTo (2), "Cc counts do not match.");
						Assert.That (envelope.Cc.ToString (), Is.EqualTo ("minutes@CNRI.Reston.VA.US, \"John Klensin\" <KLENSIN@MIT.EDU>"), "Cc does not match.");

						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("B27397-0100000@cac.washington.edu"), "Message-Id does not match.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Wed, 17 Jul 1996 02:23:25 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("IMAP4rev1 WG mtg summary and minutes"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Terry Gray\" <gray@cac.washington.edu>"), "Reply-To does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("imap@cac.washington.edu"), "To does not match.");

						Assert.That (envelope.Cc, Has.Count.EqualTo (2), "Cc counts do not match.");
						Assert.That (envelope.Cc.ToString (), Is.EqualTo ("minutes@CNRI.Reston.VA.US, \"John Klensin\" <KLENSIN@MIT.EDU>"), "Cc does not match.");

						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("B27397-0100000@cac.washington.edu"), "Message-Id does not match.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Thu, 29 Apr 2021 10:57:07 +0000"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("Паркинг на Данаил Дечев"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Рецепция Офис сграда \\\"Данаил Дечев\\\" №6\" <facility@xxxxxxxxxxx.com>"), "From does not match.");

						Assert.That (envelope.Sender, Is.Empty, "Sender counts do not match.");
						Assert.That (envelope.ReplyTo, Is.Empty, "Reply-To counts do not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("\"Team\" <team@xxxxxxxxxxx.com>"), "To does not match.");

						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("d0f6ca6608cfb0b680b7b90824c79118@xxxxxxxxxxx.com"), "Message-Id does not match.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Thu, 29 Apr 2021 10:57:07 +0000"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("Паркинг на Данаил Дечев"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Рецепция Офис сграда \\\"Данаил Дечев\\\" №6\" <facility@xxxxxxxxxxx.com>"), "From does not match.");

						Assert.That (envelope.Sender, Is.Empty, "Sender counts do not match.");
						Assert.That (envelope.ReplyTo, Is.Empty, "Reply-To counts do not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("\"Team\" <team@xxxxxxxxxxx.com>"), "To does not match.");

						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("d0f6ca6608cfb0b680b7b90824c79118@xxxxxxxxxxx.com"), "Message-Id does not match.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Tue, 24 Sep 2019 09:48:05 +0800"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("subject"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"From Name\" <from@example.com>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Sender Name\" <sender@example.com>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Reply-To Name\" <reply-to@example.com>"), "Reply-To does not match.");

						Assert.That (envelope.To, Is.Empty, "To counts do not match.");
						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.EqualTo ("in-reply-to@example.com"), "In-Reply-To does not match.");

						Assert.That (envelope.MessageId, Is.Null, "Message-Id is not null.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Tue, 24 Sep 2019 09:48:05 +0800"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("subject"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"From Name\" <from@example.com>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Sender Name\" <sender@example.com>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Reply-To Name\" <reply-to@example.com>"), "Reply-To does not match.");

						Assert.That (envelope.To, Is.Empty, "To counts do not match.");
						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.EqualTo ("in-reply-to@example.com"), "In-Reply-To does not match.");

						Assert.That (envelope.MessageId, Is.Null, "Message-Id is not null.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Tue, 24 Sep 2019 09:48:05 +0800"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("北京战区日报表"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"数据分析小组\" <unkonwn-name@unknown-domain>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"数据分析小组\" <unkonwn-name@unknown-domain>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"数据分析小组\" <unkonwn-name@unknown-domain>"), "Reply-To does not match.");

						Assert.That (envelope.To, Is.Empty, "To counts do not match.");
						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");
						Assert.That (envelope.MessageId, Is.Null, "Message-Id is not null.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Tue, 24 Sep 2019 09:48:05 +0800"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("北京战区日报表"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"数据分析小组\" <unkonwn-name@unknown-domain>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"数据分析小组\" <unkonwn-name@unknown-domain>"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"数据分析小组\" <unkonwn-name@unknown-domain>"), "Reply-To does not match.");

						Assert.That (envelope.To, Is.Empty, "To counts do not match.");
						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");
						Assert.That (envelope.MessageId, Is.Null, "Message-Id is not null.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Mon, 10 Apr 2017 06:04:00 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("Session 2: Building the meditation habit"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Headspace\" <members@headspace.com>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("members=headspace.com@members.headspace.com"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Headspace\" <members@headspace.com>"), "Reply-To does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("user@gmail.com"), "To does not match.");

						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("bvqyalstpemxt9y3afoqh4an62b2arcd.rcd.1491829440@members.headspace.com"), "Message-Id does not match.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Mon, 10 Apr 2017 06:04:00 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("Session 2: Building the meditation habit"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Headspace\" <members@headspace.com>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("members=headspace.com@members.headspace.com"), "Sender does not match.");

						Assert.That (envelope.ReplyTo, Has.Count.EqualTo (1), "Reply-To counts do not match.");
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Headspace\" <members@headspace.com>"), "Reply-To does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("user@gmail.com"), "To does not match.");

						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("bvqyalstpemxt9y3afoqh4an62b2arcd.rcd.1491829440@members.headspace.com"), "Message-Id does not match.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Mon, 10 Apr 2017 06:04:00 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("This is the subject"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"From_DisplayName\" <from@domain.com>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Dummyía Paández Algo\" <sender@domain.com>"), "Sender does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("\"To_DisplayName\" <to@domain.com>"), "To does not match.");

						Assert.That (envelope.ReplyTo, Is.Empty, "Reply-To counts do not match.");
						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("bvqyalstpemxt9y3afoqh4an62b2arcd@message.id"), "Message-Id does not match.");
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Date.HasValue, Is.True, "Parsed ENVELOPE date is null.");
						Assert.That (DateUtils.FormatDate (envelope.Date.Value), Is.EqualTo ("Mon, 10 Apr 2017 06:04:00 -0700"), "Date does not match.");
						Assert.That (envelope.Subject, Is.EqualTo ("This is the subject"), "Subject does not match.");

						Assert.That (envelope.From, Has.Count.EqualTo (1), "From counts do not match.");
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"From_DisplayName\" <from@domain.com>"), "From does not match.");

						Assert.That (envelope.Sender, Has.Count.EqualTo (1), "Sender counts do not match.");
						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Dummyía Paández Algo\" <sender@domain.com>"), "Sender does not match.");

						Assert.That (envelope.To, Has.Count.EqualTo (1), "To counts do not match.");
						Assert.That (envelope.To.ToString (), Is.EqualTo ("\"To_DisplayName\" <to@domain.com>"), "To does not match.");

						Assert.That (envelope.ReplyTo, Is.Empty, "Reply-To counts do not match.");
						Assert.That (envelope.Cc, Is.Empty, "Cc counts do not match.");
						Assert.That (envelope.Bcc, Is.Empty, "Bcc counts do not match.");

						Assert.That (envelope.InReplyTo, Is.Null, "In-Reply-To is not null.");

						Assert.That (envelope.MessageId, Is.EqualTo ("bvqyalstpemxt9y3afoqh4an62b2arcd@message.id"), "Message-Id does not match.");
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Subject, Is.EqualTo ("Retrieval using the IMAP4 protocol failed for the following message: 3"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Microsoft Exchange Server\" <>"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("\"username@testdomain.com\" <username@testdomain.com>"));
					}
				}
			}
		}

		[Test]
		public async Task TestParseEnvelopeWithNilMailboxAsync ()
		{
			const string text = "(NIL \"Retrieval using the IMAP4 protocol failed for the following message: 3\" ((\"Microsoft Exchange Server\" NIL NIL \".MISSING-HOST-NAME.\")) NIL NIL ((\"username@testdomain.com\" NIL \"username\" \"testdomain.com\")) NIL NIL NIL NIL)";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Subject, Is.EqualTo ("Retrieval using the IMAP4 protocol failed for the following message: 3"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Microsoft Exchange Server\" <>"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("\"username@testdomain.com\" <username@testdomain.com>"));
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Example Sender\" <sender@example.com>"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Example From\" <@route1,@route2:from@example.com>"));
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Example Reply-To\" <reply-to@example.com>"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;"));
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Example Sender\" <sender@example.com>"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Example From\" <@route1,@route2:from@example.com>"));
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Example Reply-To\" <reply-to@example.com>"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;"));
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>"));
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("xxxxxxx@xxxxx.xxx.xx"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("xxxxxxx@xxxxxxx.xxx.xx"));
						Assert.That (envelope.MessageId, Is.EqualTo ("0A9F01100712011D213C15B6D2B6DA@XXXXXXX-XXXXXXX"));
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"_XXXXXXXX_xxxxxx_xxxx_xxx_?= xxxx xx xxxxxxx xxxxxxxxxx.Xxxxxxxx xx xxx=Xxxxxx xx xx Xxs\" <xxxxxxx@xxxxxxxxxx.xxx>"));
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("xxxxxxx@xxxxx.xxx.xx"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("xxxxxxx@xxxxxxx.xxx.xx"));
						Assert.That (envelope.MessageId, Is.EqualTo ("0A9F01100712011D213C15B6D2B6DA@XXXXXXX-XXXXXXX"));
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
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Example Sender\" <sender@example.com>"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Example From\" <from@example.com>"));
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Example Reply-To\" <reply-to@example.com>"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;"));
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
							envelope = await ImapUtils.ParseEnvelopeAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ENVELOPE failed: {ex}");
							return;
						}

						Assert.That (envelope.Sender.ToString (), Is.EqualTo ("\"Example Sender\" <sender@example.com>"));
						Assert.That (envelope.From.ToString (), Is.EqualTo ("\"Example From\" <from@example.com>"));
						Assert.That (envelope.ReplyTo.ToString (), Is.EqualTo ("\"Example Reply-To\" <reply-to@example.com>"));
						Assert.That (envelope.To.ToString (), Is.EqualTo ("boys: aaron, jeff, zach;, girls: alice, hailey, jenny;"));
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_000_0077_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");
						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartMessage> (), "The type of the second child does not match.");
						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "The type of the third child does not match.");

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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_000_0077_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");
						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartMessage> (), "The type of the second child does not match.");
						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "The type of the third child does not match.");

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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("----=--_DRYORTABLE@@@_@@@8957836_03253840099.78526606923635"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");
						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartText> (), "The type of the third child did not match.");

						var related = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (related.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (related.ContentType.Parameters["boundary"], Is.EqualTo ("----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635"), "multipart/related boundary param did not match");
						Assert.That (related.ContentTransferEncoding, Is.EqualTo ("7BIT"), "multipart/related Content-Transfer-Encoding did not match.");
						Assert.That (related.Octets, Is.EqualTo (400), "multipart/related octets do not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("----=--_DRYORTABLE@@@_@@@8957836_03253840099.78526606923635"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");
						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartText> (), "The type of the third child did not match.");

						var related = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (related.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (related.ContentType.Parameters["boundary"], Is.EqualTo ("----=_@@@@BeautyqueenS87@_@147836_6893840099.85426606923635"), "multipart/related boundary param did not match");
						Assert.That (related.ContentTransferEncoding, Is.EqualTo ("7BIT"), "multipart/related Content-Transfer-Encoding did not match.");
						Assert.That (related.Octets, Is.EqualTo (400), "multipart/related octets do not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "report"), Is.True, "multipart/report Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("==IFJRGLKFGIR60132UHRUHIHD"), "boundary param did not match.");
						Assert.That (multipart.ContentType.Parameters["report-type"], Is.EqualTo ("delivery-status"), "report-type param did not match.");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child did not match.");
						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "The type of the third child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("UTF-8"), "text/plain charset param did not match.");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("base64"), "text/plain encoding did not match.");
						Assert.That (plain.Octets, Is.EqualTo (232), "text/plain octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (4), "text/plain lines did not match.");

						var dstat = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (dstat.ContentType.IsMimeType ("message", "delivery-status"), Is.True, "message/delivery-status Content-Type did not match.");
						Assert.That (dstat.ContentTransferEncoding, Is.EqualTo ("7BIT"), "message/delivery-status encoding did not match.");
						Assert.That (dstat.Octets, Is.EqualTo (421), "message/delivery-status octets did not match.");

						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.That (rfc822.ContentType.IsMimeType ("message", "rfc822"), Is.True, "message/rfc822 Content-Type did not match.");
						Assert.That (rfc822.ContentId, Is.Null, "message/rfc822 Content-Id should be NIL.");
						Assert.That (rfc822.ContentDescription, Is.Null, "message/rfc822 Content-Description should be NIL.");
						Assert.That (rfc822.Envelope.Sender, Is.Empty, "message/rfc822 Envlope.Sender should be null.");
						Assert.That (rfc822.Envelope.From, Is.Empty, "message/rfc822 Envlope.From should be null.");
						Assert.That (rfc822.Envelope.ReplyTo, Is.Empty, "message/rfc822 Envlope.ReplyTo should be null.");
						Assert.That (rfc822.Envelope.To, Is.Empty, "message/rfc822 Envlope.To should be null.");
						Assert.That (rfc822.Envelope.Cc, Is.Empty, "message/rfc822 Envlope.Cc should be null.");
						Assert.That (rfc822.Envelope.Bcc, Is.Empty, "message/rfc822 Envlope.Bcc should be null.");
						Assert.That (rfc822.Envelope.Subject, Is.Null, "message/rfc822 Envlope.Subject should be null.");
						Assert.That (rfc822.Envelope.MessageId, Is.Null, "message/rfc822 Envlope.MessageId should be null.");
						Assert.That (rfc822.Envelope.InReplyTo, Is.Null, "message/rfc822 Envlope.InReplyTo should be null.");
						Assert.That (rfc822.Envelope.Date, Is.Null, "message/rfc822 Envlope.Date should be null.");
						Assert.That (rfc822.ContentTransferEncoding, Is.EqualTo ("7BIT"), "message/rfc822 encoding did not match.");
						Assert.That (rfc822.Octets, Is.EqualTo (787), "message/rfc822 octets did not match.");
						Assert.That (rfc822.Body, Is.Null, "message/rfc822 body should be null.");
						Assert.That (rfc822.Lines, Is.EqualTo (0), "message/rfc822 lines did not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "report"), Is.True, "multipart/report Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("==IFJRGLKFGIR60132UHRUHIHD"), "boundary param did not match.");
						Assert.That (multipart.ContentType.Parameters["report-type"], Is.EqualTo ("delivery-status"), "report-type param did not match.");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child did not match.");
						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "The type of the third child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("UTF-8"), "text/plain charset param did not match.");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("base64"), "text/plain encoding did not match.");
						Assert.That (plain.Octets, Is.EqualTo (232), "text/plain octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (4), "text/plain lines did not match.");

						var dstat = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (dstat.ContentType.IsMimeType ("message", "delivery-status"), Is.True, "message/delivery-status Content-Type did not match.");
						Assert.That (dstat.ContentTransferEncoding, Is.EqualTo ("7BIT"), "message/delivery-status encoding did not match.");
						Assert.That (dstat.Octets, Is.EqualTo (421), "message/delivery-status octets did not match.");

						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.That (rfc822.ContentType.IsMimeType ("message", "rfc822"), Is.True, "message/rfc822 Content-Type did not match.");
						Assert.That (rfc822.ContentId, Is.Null, "message/rfc822 Content-Id should be NIL.");
						Assert.That (rfc822.ContentDescription, Is.Null, "message/rfc822 Content-Description should be NIL.");
						Assert.That (rfc822.Envelope.Sender, Is.Empty, "message/rfc822 Envlope.Sender should be null.");
						Assert.That (rfc822.Envelope.From, Is.Empty, "message/rfc822 Envlope.From should be null.");
						Assert.That (rfc822.Envelope.ReplyTo, Is.Empty, "message/rfc822 Envlope.ReplyTo should be null.");
						Assert.That (rfc822.Envelope.To, Is.Empty, "message/rfc822 Envlope.To should be null.");
						Assert.That (rfc822.Envelope.Cc, Is.Empty, "message/rfc822 Envlope.Cc should be null.");
						Assert.That (rfc822.Envelope.Bcc, Is.Empty, "message/rfc822 Envlope.Bcc should be null.");
						Assert.That (rfc822.Envelope.Subject, Is.Null, "message/rfc822 Envlope.Subject should be null.");
						Assert.That (rfc822.Envelope.MessageId, Is.Null, "message/rfc822 Envlope.MessageId should be null.");
						Assert.That (rfc822.Envelope.InReplyTo, Is.Null, "message/rfc822 Envlope.InReplyTo should be null.");
						Assert.That (rfc822.Envelope.Date, Is.Null, "message/rfc822 Envlope.Date should be null.");
						Assert.That (rfc822.ContentTransferEncoding, Is.EqualTo ("7BIT"), "message/rfc822 encoding did not match.");
						Assert.That (rfc822.Octets, Is.EqualTo (787), "message/rfc822 octets did not match.");
						Assert.That (rfc822.Body, Is.Null, "message/rfc822 body should be null.");
						Assert.That (rfc822.Lines, Is.EqualTo (0), "message/rfc822 lines did not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_001_0078_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.ContentDisposition.Disposition, Is.EqualTo ("inline"), "multipart/alternative disposition did not match");
						Assert.That (multipart.ContentDisposition.FileName, Is.EqualTo ("alternative.txt"), "multipart/alternative filename did not match");
						Assert.That (multipart.ContentLanguage, Is.Not.Null, "multipart/alternative Content-Language should not be null");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "multipart/alternative Content-Language count did not match");
						Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("en"), "multipart/alternative Content-Language value did not match");
						Assert.That (multipart.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/alternative.txt"), "multipart/alternative location did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/plain charset param did not match");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/plain disposition did not match");
						Assert.That (plain.ContentDisposition.FileName, Is.EqualTo ("body.txt"), "text/plain filename did not match");
						Assert.That (plain.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (plain.ContentLanguage, Is.Not.Null, "text/plain Content-Language should not be null");
						Assert.That (plain.ContentLanguage, Has.Length.EqualTo (1), "text/plain Content-Language count did not match");
						Assert.That (plain.ContentLanguage [0], Is.EqualTo ("en"), "text/plain Content-Language value did not match");
						Assert.That (plain.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.txt"), "text/plain location did not match");
						Assert.That (plain.Octets, Is.EqualTo (28), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (2), "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset param did not match");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/html disposition did not match");
						Assert.That (html.ContentDisposition.FileName, Is.EqualTo ("body.html"), "text/html filename did not match");
						Assert.That (html.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (html.ContentLanguage, Is.Not.Null, "text/html Content-Language should not be null");
						Assert.That (html.ContentLanguage, Has.Length.EqualTo (1), "text/html Content-Language count did not match");
						Assert.That (html.ContentLanguage [0], Is.EqualTo ("en"), "text/html Content-Language value did not match");
						Assert.That (html.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.html"), "text/html location did not match");
						Assert.That (html.Octets, Is.EqualTo (1707), "text/html octets did not match");
						Assert.That (html.Lines, Is.EqualTo (65), "text/html lines did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_001_0078_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.ContentDisposition.Disposition, Is.EqualTo ("inline"), "multipart/alternative disposition did not match");
						Assert.That (multipart.ContentDisposition.FileName, Is.EqualTo ("alternative.txt"), "multipart/alternative filename did not match");
						Assert.That (multipart.ContentLanguage, Is.Not.Null, "multipart/alternative Content-Language should not be null");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "multipart/alternative Content-Language count did not match");
						Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("en"), "multipart/alternative Content-Language value did not match");
						Assert.That (multipart.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/alternative.txt"), "multipart/alternative location did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/plain charset param did not match");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/plain disposition did not match");
						Assert.That (plain.ContentDisposition.FileName, Is.EqualTo ("body.txt"), "text/plain filename did not match");
						Assert.That (plain.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (plain.ContentLanguage, Is.Not.Null, "text/plain Content-Language should not be null");
						Assert.That (plain.ContentLanguage, Has.Length.EqualTo (1), "text/plain Content-Language count did not match");
						Assert.That (plain.ContentLanguage[0], Is.EqualTo ("en"), "text/plain Content-Language value did not match");
						Assert.That (plain.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.txt"), "text/plain location did not match");
						Assert.That (plain.Octets, Is.EqualTo (28), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (2), "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset param did not match");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/html disposition did not match");
						Assert.That (html.ContentDisposition.FileName, Is.EqualTo ("body.html"), "text/html filename did not match");
						Assert.That (html.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (html.ContentLanguage, Is.Not.Null, "text/html Content-Language should not be null");
						Assert.That (html.ContentLanguage, Has.Length.EqualTo (1), "text/html Content-Language count did not match");
						Assert.That (html.ContentLanguage[0], Is.EqualTo ("en"), "text/html Content-Language value did not match");
						Assert.That (html.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.html"), "text/html location did not match");
						Assert.That (html.Octets, Is.EqualTo (1707), "text/html octets did not match");
						Assert.That (html.Lines, Is.EqualTo (65), "text/html lines did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart)body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters ["boundary"], Is.EqualTo ("----=_NextPart_001_0078_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.ContentDisposition.Disposition, Is.EqualTo ("inline"), "multipart/alternative disposition did not match");
						Assert.That (multipart.ContentDisposition.FileName, Is.EqualTo ("alternative.txt"), "multipart/alternative filename did not match");
						Assert.That (multipart.ContentLanguage, Is.Not.Null, "multipart/alternative Content-Language should not be null");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "multipart/alternative Content-Language count did not match");
						Assert.That (multipart.ContentLanguage [0], Is.EqualTo ("en"), "multipart/alternative Content-Language value did not match");
						Assert.That (multipart.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/alternative.txt"), "multipart/alternative location did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts [0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts [1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");

						var plain = (BodyPartText)multipart.BodyParts [0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/plain charset param did not match");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/plain disposition did not match");
						Assert.That (plain.ContentDisposition.FileName, Is.EqualTo ("body.txt"), "text/plain filename did not match");
						Assert.That (plain.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (plain.ContentLanguage, Is.Not.Null, "text/plain Content-Language should not be null");
						Assert.That (plain.ContentLanguage, Has.Length.EqualTo (1), "text/plain Content-Language count did not match");
						Assert.That (plain.ContentLanguage [0], Is.EqualTo ("en"), "text/plain Content-Language value did not match");
						Assert.That (plain.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.txt"), "text/plain location did not match");
						Assert.That (plain.Octets, Is.EqualTo (28), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (2), "text/plain lines did not match");

						var html = (BodyPartText)multipart.BodyParts [1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset param did not match");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/html disposition did not match");
						Assert.That (html.ContentDisposition.FileName, Is.EqualTo ("body.html"), "text/html filename did not match");
						Assert.That (html.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (html.ContentLanguage, Is.Not.Null, "text/html Content-Language should not be null");
						Assert.That (html.ContentLanguage, Has.Length.EqualTo (1), "text/html Content-Language count did not match");
						Assert.That (html.ContentLanguage [0], Is.EqualTo ("en"), "text/html Content-Language value did not match");
						Assert.That (html.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.html"), "text/html location did not match");
						Assert.That (html.Octets, Is.EqualTo (1707), "text/html octets did not match");
						Assert.That (html.Lines, Is.EqualTo (65), "text/html lines did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_001_0078_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.ContentDisposition.Disposition, Is.EqualTo ("inline"), "multipart/alternative disposition did not match");
						Assert.That (multipart.ContentDisposition.FileName, Is.EqualTo ("alternative.txt"), "multipart/alternative filename did not match");
						Assert.That (multipart.ContentLanguage, Is.Not.Null, "multipart/alternative Content-Language should not be null");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "multipart/alternative Content-Language count did not match");
						Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("en"), "multipart/alternative Content-Language value did not match");
						Assert.That (multipart.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/alternative.txt"), "multipart/alternative location did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/plain charset param did not match");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/plain disposition did not match");
						Assert.That (plain.ContentDisposition.FileName, Is.EqualTo ("body.txt"), "text/plain filename did not match");
						Assert.That (plain.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (plain.ContentLanguage, Is.Not.Null, "text/plain Content-Language should not be null");
						Assert.That (plain.ContentLanguage, Has.Length.EqualTo (1), "text/plain Content-Language count did not match");
						Assert.That (plain.ContentLanguage[0], Is.EqualTo ("en"), "text/plain Content-Language value did not match");
						Assert.That (plain.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.txt"), "text/plain location did not match");
						Assert.That (plain.Octets, Is.EqualTo (28), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (2), "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset param did not match");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/html disposition did not match");
						Assert.That (html.ContentDisposition.FileName, Is.EqualTo ("body.html"), "text/html filename did not match");
						Assert.That (html.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (html.ContentLanguage, Is.Not.Null, "text/html Content-Language should not be null");
						Assert.That (html.ContentLanguage, Has.Length.EqualTo (1), "text/html Content-Language count did not match");
						Assert.That (html.ContentLanguage[0], Is.EqualTo ("en"), "text/html Content-Language value did not match");
						Assert.That (html.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.html"), "text/html location did not match");
						Assert.That (html.Octets, Is.EqualTo (1707), "text/html octets did not match");
						Assert.That (html.Lines, Is.EqualTo (65), "text/html lines did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart)body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters ["boundary"], Is.EqualTo ("----=_NextPart_001_0078_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.ContentDisposition.Disposition, Is.EqualTo ("inline"), "multipart/alternative disposition did not match");
						Assert.That (multipart.ContentDisposition.FileName, Is.EqualTo ("alternative.txt"), "multipart/alternative filename did not match");
						Assert.That (multipart.ContentLanguage, Is.Not.Null, "multipart/alternative Content-Language should not be null");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "multipart/alternative Content-Language count did not match");
						Assert.That (multipart.ContentLanguage [0], Is.EqualTo ("en"), "multipart/alternative Content-Language value did not match");
						Assert.That (multipart.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/alternative.txt"), "multipart/alternative location did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts [0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts [1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");

						var plain = (BodyPartText)multipart.BodyParts [0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/plain charset param did not match");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/plain disposition did not match");
						Assert.That (plain.ContentDisposition.FileName, Is.EqualTo ("body.txt"), "text/plain filename did not match");
						Assert.That (plain.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (plain.ContentLanguage, Is.Not.Null, "text/plain Content-Language should not be null");
						Assert.That (plain.ContentLanguage, Has.Length.EqualTo (1), "text/plain Content-Language count did not match");
						Assert.That (plain.ContentLanguage [0], Is.EqualTo ("en"), "text/plain Content-Language value did not match");
						Assert.That (plain.ContentLocation.ToString (), Is.EqualTo ("body.txt"), "text/plain location did not match");
						Assert.That (plain.Octets, Is.EqualTo (28), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (2), "text/plain lines did not match");

						var html = (BodyPartText)multipart.BodyParts [1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset param did not match");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/html disposition did not match");
						Assert.That (html.ContentDisposition.FileName, Is.EqualTo ("body.html"), "text/html filename did not match");
						Assert.That (html.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (html.ContentLanguage, Is.Not.Null, "text/html Content-Language should not be null");
						Assert.That (html.ContentLanguage, Has.Length.EqualTo (1), "text/html Content-Language count did not match");
						Assert.That (html.ContentLanguage [0], Is.EqualTo ("en"), "text/html Content-Language value did not match");
						Assert.That (html.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.html"), "text/html location did not match");
						Assert.That (html.Octets, Is.EqualTo (1707), "text/html octets did not match");
						Assert.That (html.Lines, Is.EqualTo (65), "text/html lines did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_001_0078_01CBB179.57530990"), "boundary param did not match");
						Assert.That (multipart.ContentDisposition.Disposition, Is.EqualTo ("inline"), "multipart/alternative disposition did not match");
						Assert.That (multipart.ContentDisposition.FileName, Is.EqualTo ("alternative.txt"), "multipart/alternative filename did not match");
						Assert.That (multipart.ContentLanguage, Is.Not.Null, "multipart/alternative Content-Language should not be null");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "multipart/alternative Content-Language count did not match");
						Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("en"), "multipart/alternative Content-Language value did not match");
						Assert.That (multipart.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/alternative.txt"), "multipart/alternative location did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count did not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child did not match.");
						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child did not match.");

						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/plain charset param did not match");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/plain disposition did not match");
						Assert.That (plain.ContentDisposition.FileName, Is.EqualTo ("body.txt"), "text/plain filename did not match");
						Assert.That (plain.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (plain.ContentLanguage, Is.Not.Null, "text/plain Content-Language should not be null");
						Assert.That (plain.ContentLanguage, Has.Length.EqualTo (1), "text/plain Content-Language count did not match");
						Assert.That (plain.ContentLanguage[0], Is.EqualTo ("en"), "text/plain Content-Language value did not match");
						Assert.That (plain.ContentLocation.ToString (), Is.EqualTo ("body.txt"), "text/plain location did not match");
						Assert.That (plain.Octets, Is.EqualTo (28), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (2), "text/plain lines did not match");

						var html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset param did not match");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "text/html disposition did not match");
						Assert.That (html.ContentDisposition.FileName, Is.EqualTo ("body.html"), "text/html filename did not match");
						Assert.That (html.ContentMd5, Is.EqualTo ("md5sum"), "text/html Content-Md5 did not match");
						Assert.That (html.ContentLanguage, Is.Not.Null, "text/html Content-Language should not be null");
						Assert.That (html.ContentLanguage, Has.Length.EqualTo (1), "text/html Content-Language count did not match");
						Assert.That (html.ContentLanguage[0], Is.EqualTo ("en"), "text/html Content-Language value did not match");
						Assert.That (html.ContentLocation.ToString (), Is.EqualTo ("http://www.google.com/body.html"), "text/html location did not match");
						Assert.That (html.Octets, Is.EqualTo (1707), "text/html octets did not match");
						Assert.That (html.Lines, Is.EqualTo (65), "text/html lines did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "outer multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("==alternative_xad5934455aeex"), "outer multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						broken = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.That (broken.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "inner multipart/alternative Content-Type did not match.");
						Assert.That (broken.ContentType.Parameters["boundary"], Is.EqualTo ("==alternative_xad5934455aeex"), "inner multipart/alternative boundary param did not match");
						Assert.That (broken.BodyParts, Is.Empty, "inner multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset parameter did not match");
						Assert.That (html.ContentType.Name, Is.EqualTo ("seti_letter.html"), "text/html name parameter did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "outer multipart/alternative Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("==alternative_xad5934455aeex"), "outer multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						broken = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.That (broken.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "inner multipart/alternative Content-Type did not match.");
						Assert.That (broken.ContentType.Parameters["boundary"], Is.EqualTo ("==alternative_xad5934455aeex"), "inner multipart/alternative boundary param did not match");
						Assert.That (broken.BodyParts, Is.Empty, "inner multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "text/html charset parameter did not match");
						Assert.That (html.ContentType.Name, Is.EqualTo ("seti_letter.html"), "text/html name parameter did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var mixed = (BodyPartMultipart) body;

						Assert.That (mixed.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (mixed.ContentType.Parameters["boundary"], Is.EqualTo ("94eb2c1cd0507723e6054c1ce6cd"), "multipart/mixed boundary param did not match");
						Assert.That (mixed.BodyParts, Has.Count.EqualTo (5), "multipart/mixed BodyParts count does not match.");

						Assert.That (mixed.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						var alternative = (BodyPartMultipart) mixed.BodyParts[0];
						Assert.That (alternative.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (alternative.ContentType.Parameters["boundary"], Is.EqualTo ("94eb2c1cd0507723d5054c1ce6cb"), "multipart/alternative boundary param did not match");
						Assert.That (alternative.BodyParts, Has.Count.EqualTo (2), "multipart/alternative BodyParts count does not match.");

						Assert.That (alternative.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						var plain = (BodyPartText) alternative.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("UTF-8"), "text/plain charset parameter did not match");
						Assert.That (plain.ContentType.Format, Is.EqualTo ("flowed"), "text/plain format parameter did not match");
						Assert.That (plain.ContentType.Parameters["delsp"], Is.EqualTo ("yes"), "text/plain delsp parameter did not match");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("BASE64"), "text/plain Content-Transfer-Encoding did not match");
						Assert.That (plain.Octets, Is.EqualTo (10418), "text/plain Octets do not match");
						Assert.That (plain.Lines, Is.EqualTo (133), "text/plain Lines don't match");

						Assert.That (alternative.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						var html = (BodyPartText) alternative.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("UTF-8"), "text/html charset parameter did not match");
						Assert.That (html.ContentTransferEncoding, Is.EqualTo ("BASE64"), "text/phtml Content-Transfer-Encoding did not match");
						Assert.That (html.Octets, Is.EqualTo (34544), "text/html Octets do not match");
						Assert.That (html.Lines, Is.EqualTo (442), "text/html Lines don't match");

						Assert.That (mixed.BodyParts[1], Is.InstanceOf<BodyPartMultipart> (), "The type of the second child does not match.");
						var broken1 = (BodyPartMultipart) mixed.BodyParts[1];
						Assert.That (broken1.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken1.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");

						Assert.That (mixed.BodyParts[2], Is.InstanceOf<BodyPartMultipart> (), "The type of the third child does not match.");
						var broken2 = (BodyPartMultipart) mixed.BodyParts[2];
						Assert.That (broken2.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken2.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");

						Assert.That (mixed.BodyParts[3], Is.InstanceOf<BodyPartMultipart> (), "The type of the fourth child does not match.");
						var broken3 = (BodyPartMultipart) mixed.BodyParts[3];
						Assert.That (broken3.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken3.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");

						Assert.That (mixed.BodyParts[4], Is.InstanceOf<BodyPartMultipart> (), "The type of the fifth child does not match.");
						var broken4 = (BodyPartMultipart) mixed.BodyParts[4];
						Assert.That (broken4.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken4.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var mixed = (BodyPartMultipart) body;

						Assert.That (mixed.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (mixed.ContentType.Parameters["boundary"], Is.EqualTo ("94eb2c1cd0507723e6054c1ce6cd"), "multipart/mixed boundary param did not match");
						Assert.That (mixed.BodyParts, Has.Count.EqualTo (5), "multipart/mixed BodyParts count does not match.");

						Assert.That (mixed.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						var alternative = (BodyPartMultipart) mixed.BodyParts[0];
						Assert.That (alternative.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "multipart/alternative Content-Type did not match.");
						Assert.That (alternative.ContentType.Parameters["boundary"], Is.EqualTo ("94eb2c1cd0507723d5054c1ce6cb"), "multipart/alternative boundary param did not match");
						Assert.That (alternative.BodyParts, Has.Count.EqualTo (2), "multipart/alternative BodyParts count does not match.");

						Assert.That (alternative.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						var plain = (BodyPartText) alternative.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("UTF-8"), "text/plain charset parameter did not match");
						Assert.That (plain.ContentType.Format, Is.EqualTo ("flowed"), "text/plain format parameter did not match");
						Assert.That (plain.ContentType.Parameters["delsp"], Is.EqualTo ("yes"), "text/plain delsp parameter did not match");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("BASE64"), "text/plain Content-Transfer-Encoding did not match");
						Assert.That (plain.Octets, Is.EqualTo (10418), "text/plain Octets do not match");
						Assert.That (plain.Lines, Is.EqualTo (133), "text/plain Lines don't match");

						Assert.That (alternative.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						var html = (BodyPartText) alternative.BodyParts[1];
						Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "text/html Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("UTF-8"), "text/html charset parameter did not match");
						Assert.That (html.ContentTransferEncoding, Is.EqualTo ("BASE64"), "text/phtml Content-Transfer-Encoding did not match");
						Assert.That (html.Octets, Is.EqualTo (34544), "text/html Octets do not match");
						Assert.That (html.Lines, Is.EqualTo (442), "text/html Lines don't match");

						Assert.That (mixed.BodyParts[1], Is.InstanceOf<BodyPartMultipart> (), "The type of the second child does not match.");
						var broken1 = (BodyPartMultipart) mixed.BodyParts[1];
						Assert.That (broken1.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken1.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");

						Assert.That (mixed.BodyParts[2], Is.InstanceOf<BodyPartMultipart> (), "The type of the third child does not match.");
						var broken2 = (BodyPartMultipart) mixed.BodyParts[2];
						Assert.That (broken2.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken2.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");

						Assert.That (mixed.BodyParts[3], Is.InstanceOf<BodyPartMultipart> (), "The type of the fourth child does not match.");
						var broken3 = (BodyPartMultipart) mixed.BodyParts[3];
						Assert.That (broken3.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken3.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");

						Assert.That (mixed.BodyParts[4], Is.InstanceOf<BodyPartMultipart> (), "The type of the fifth child does not match.");
						var broken4 = (BodyPartMultipart) mixed.BodyParts[4];
						Assert.That (broken4.ContentType.IsMimeType ("multipart", "related"), Is.True, "multipart/related Content-Type did not match.");
						Assert.That (broken4.BodyParts, Is.Empty, "multipart/related BodyParts count does not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("windows-1251"), "text/plain charset parameter did not match");
						Assert.That (plain.Octets, Is.EqualTo (356), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (5), "text/plain lines did not match");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						xzip = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (xzip.ContentType.IsMimeType ("application", "x-zip"), Is.True, "x-zip Content-Type did not match.");
						Assert.That (xzip.ContentType.Parameters["boundary"], Is.EqualTo (""), "x-zip boundary parameter did not match");
						Assert.That (xzip.Octets, Is.EqualTo (4096), "x-zip octets did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("windows-1251"), "text/plain charset parameter did not match");
						Assert.That (plain.Octets, Is.EqualTo (356), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (5), "text/plain lines did not match");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						xzip = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (xzip.ContentType.IsMimeType ("application", "x-zip"), Is.True, "x-zip Content-Type did not match.");
						Assert.That (xzip.ContentType.Parameters["boundary"], Is.EqualTo (""), "x-zip boundary parameter did not match");
						Assert.That (xzip.Octets, Is.EqualTo (4096), "x-zip octets did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.IsMimeType ("application", "octet-stream"), Is.True, "application/octet-stream Content-Type did not match.");
						Assert.That (basic.Octets, Is.EqualTo (356), "application/octet-stream octets did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.IsMimeType ("application", "octet-stream"), Is.True, "application/octet-stream Content-Type did not match.");
						Assert.That (basic.Octets, Is.EqualTo (356), "application/octet-stream octets did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.IsMimeType ("application", "audio"), Is.True, "application/audio Content-Type did not match.");
						Assert.That (basic.Octets, Is.EqualTo (356), "application/audio octets did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.IsMimeType ("application", "audio"), Is.True, "application/audio Content-Type did not match.");
						Assert.That (basic.Octets, Is.EqualTo (356), "application/audio octets did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.IsMimeType ("application", "message"), Is.True, "application/message Content-Type did not match.");
						Assert.That (basic.Octets, Is.EqualTo (356), "application/message octets did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.IsMimeType ("application", "message"), Is.True, "application/message Content-Type did not match.");
						Assert.That (basic.Octets, Is.EqualTo (356), "application/message octets did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("windows-1251"), "text/plain charset parameter did not match");
						Assert.That (plain.Octets, Is.EqualTo (356), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (5), "text/plain lines did not match");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "multipart/mixed Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("--cd49a2f5ed4ed0cbb6f9f1c7f125541f"), "multipart/alternative boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (1), "outer multipart/alternative BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "text/plain Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("windows-1251"), "text/plain charset parameter did not match");
						Assert.That (plain.Octets, Is.EqualTo (356), "text/plain octets did not match");
						Assert.That (plain.Lines, Is.EqualTo (5), "text/plain lines did not match");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters ["boundary"], Is.EqualTo ("----=_NextPart_000_730AD4A547.730AD4A547F40"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.MediaType, Is.EqualTo ("MOUNDARY=\"_006_5DBB50A5A54730AD4A54730AD4A54730AD4A54730AD42KOS_\""), "ContentType.MediaType does not match for first child.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (basic.ContentType.MediaType, Is.EqualTo ("MOUNDARY=\"_006_5DBB50A5D3ABEC4E85A03EAD527CA5474B3D0AF9E6EXMBXSVR02KOS_\""), "ContentType.MediaType does not match for second child.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("----=_NextPart_000_730AD4A547.730AD4A547F40"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.MediaType, Is.EqualTo ("MOUNDARY=\"_006_5DBB50A5A54730AD4A54730AD4A54730AD4A54730AD42KOS_\""), "ContentType.MediaType does not match for first child.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (basic.ContentType.MediaType, Is.EqualTo ("MOUNDARY=\"_006_5DBB50A5D3ABEC4E85A03EAD527CA5474B3D0AF9E6EXMBXSVR02KOS_\""), "ContentType.MediaType does not match for second child.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("c52bbfc0dd5365efa39b9f80eac3"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						alternative = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.That (alternative.ContentType.MediaSubtype, Is.EqualTo ("alternative"), "Content-Type did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartMultipart> (), "The type of the second child does not match.");
						xzip = (BodyPartMultipart) multipart.BodyParts[1];
						Assert.That (xzip.ContentType.MediaSubtype, Is.EqualTo ("x-zip"), "Content-Type did not match.");
						Assert.That (xzip.ContentType.Parameters, Is.Empty, "Content-Type should not have params.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("c52bbfc0dd5365efa39b9f80eac3"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "The type of the first child does not match.");
						alternative = (BodyPartMultipart) multipart.BodyParts[0];
						Assert.That (alternative.ContentType.MediaSubtype, Is.EqualTo ("alternative"), "Content-Type did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartMultipart> (), "The type of the second child does not match.");
						xzip = (BodyPartMultipart) multipart.BodyParts[1];
						Assert.That (xzip.ContentType.MediaSubtype, Is.EqualTo ("x-zip"), "Content-Type did not match.");
						Assert.That (xzip.ContentType.Parameters, Is.Empty, "Content-Type should not have params.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters ["boundary"], Is.EqualTo ("6624CFB2_17170C36_Synapse_boundary"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.MediaSubtype, Is.EqualTo ("plain"), "Content-Type did not match.");
						Assert.That (basic.ContentDescription, Is.EqualTo ("Message text"), "Content-Description does not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (basic.ContentType.MediaSubtype, Is.EqualTo ("xml"), "Content-Type did not match.");
						Assert.That (basic.ContentDescription, Is.EqualTo ("4441004299066.xml"), "Content-Description does not match.");

						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartBasic> (), "The type of the third child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[2];
						Assert.That (basic.ContentType.MediaType, Is.EqualTo ("application"), "Content-Type did not match.");
						Assert.That (basic.ContentType.MediaSubtype, Is.EqualTo ("pdf"), "Content-Type did not match.");
						Assert.That (basic.ContentDescription, Is.EqualTo ("4441004299066.pdf"), "Content-Description does not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("6624CFB2_17170C36_Synapse_boundary"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[0];
						Assert.That (basic.ContentType.MediaSubtype, Is.EqualTo ("plain"), "Content-Type did not match.");
						Assert.That (basic.ContentDescription, Is.EqualTo ("Message text"), "Content-Description does not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (basic.ContentType.MediaSubtype, Is.EqualTo ("xml"), "Content-Type did not match.");
						Assert.That (basic.ContentDescription, Is.EqualTo ("4441004299066.xml"), "Content-Description does not match.");

						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartBasic> (), "The type of the third child does not match.");
						basic = (BodyPartBasic) multipart.BodyParts[2];
						Assert.That (basic.ContentType.MediaType, Is.EqualTo ("application"), "Content-Type did not match.");
						Assert.That (basic.ContentType.MediaSubtype, Is.EqualTo ("pdf"), "Content-Type did not match.");
						Assert.That (basic.ContentDescription, Is.EqualTo ("4441004299066.pdf"), "Content-Description does not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("----=_Part_45280395_786508794.1562673197246"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.MimeType, Is.EqualTo ("text/plain"), "Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("ISO-8859-1"), "Content-Type charset parameter did not match.");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("QUOTED-PRINTABLE"), "Content-Transfer-Encoding did not match.");
						Assert.That (plain.Octets, Is.EqualTo (850), "Octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (31), "Lines did not match.");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.MimeType, Is.EqualTo ("text/html"), "Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("ISO-8859-1"), "Content-Type charset parameter did not match.");
						Assert.That (html.ContentTransferEncoding, Is.EqualTo ("QUOTED-PRINTABLE"), "Content-Transfer-Encoding did not match.");
						Assert.That (html.Octets, Is.EqualTo (14692), "Octets did not match.");
						Assert.That (html.Lines, Is.EqualTo (502), "Lines did not match.");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("----=_Part_45280395_786508794.1562673197246"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.MimeType, Is.EqualTo ("text/plain"), "Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("ISO-8859-1"), "Content-Type charset parameter did not match.");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("QUOTED-PRINTABLE"), "Content-Transfer-Encoding did not match.");
						Assert.That (plain.Octets, Is.EqualTo (850), "Octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (31), "Lines did not match.");
						Assert.That (plain.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartText> (), "The type of the second child does not match.");
						html = (BodyPartText) multipart.BodyParts[1];
						Assert.That (html.ContentType.MimeType, Is.EqualTo ("text/html"), "Content-Type did not match.");
						Assert.That (html.ContentType.Charset, Is.EqualTo ("ISO-8859-1"), "Content-Type charset parameter did not match.");
						Assert.That (html.ContentTransferEncoding, Is.EqualTo ("QUOTED-PRINTABLE"), "Content-Transfer-Encoding did not match.");
						Assert.That (html.Octets, Is.EqualTo (14692), "Octets did not match.");
						Assert.That (html.Lines, Is.EqualTo (502), "Lines did not match.");
						Assert.That (html.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("===============1176586998=="), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.MimeType, Is.EqualTo ("text/plain"), "Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "Content-Type charset parameter did not match.");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("quoted-printable"), "Content-Transfer-Encoding did not match.");
						Assert.That (plain.ContentDescription, Is.EqualTo ("Mail message body"), "Content-Description did not match.");
						Assert.That (plain.Octets, Is.EqualTo (2201), "Octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (34), "Lines did not match.");
						Assert.That (plain.ContentDisposition, Is.Null, "Content-Disposition did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (msword.ContentType.MimeType, Is.EqualTo ("application/msword"), "Content-Type did not match.");
						Assert.That (msword.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
						Assert.That (msword.Octets, Is.EqualTo (50446), "Octets did not match.");
						Assert.That (msword.ContentDisposition, Is.Null, "Content-Disposition did not match.");

						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[2];
						Assert.That (msword.ContentType.MimeType, Is.EqualTo ("application/msword"), "Content-Type did not match.");
						Assert.That (msword.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
						Assert.That (msword.Octets, Is.EqualTo (45544), "Octets did not match.");
						Assert.That (msword.ContentDisposition.Disposition, Is.EqualTo ("attachment"), "Content-Disposition did not match.");
						Assert.That (msword.ContentDisposition.FileName, Is.EqualTo ("PREIS ANSPRUCHS FORMULAR.doc"), "Filename parameters do not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (body.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
						Assert.That (body.ContentType.Parameters["boundary"], Is.EqualTo ("===============1176586998=="), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "The type of the first child does not match.");
						plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentType.MimeType, Is.EqualTo ("text/plain"), "Content-Type did not match.");
						Assert.That (plain.ContentType.Charset, Is.EqualTo ("iso-8859-1"), "Content-Type charset parameter did not match.");
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("quoted-printable"), "Content-Transfer-Encoding did not match.");
						Assert.That (plain.ContentDescription, Is.EqualTo ("Mail message body"), "Content-Description did not match.");
						Assert.That (plain.Octets, Is.EqualTo (2201), "Octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (34), "Lines did not match.");
						Assert.That (plain.ContentDisposition, Is.Null, "Content-Disposition did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (msword.ContentType.MimeType, Is.EqualTo ("application/msword"), "Content-Type did not match.");
						Assert.That (msword.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
						Assert.That (msword.Octets, Is.EqualTo (50446), "Octets did not match.");
						Assert.That (msword.ContentDisposition, Is.Null, "Content-Disposition did not match.");

						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartBasic> (), "The type of the second child does not match.");
						msword = (BodyPartBasic) multipart.BodyParts[2];
						Assert.That (msword.ContentType.MimeType, Is.EqualTo ("application/msword"), "Content-Type did not match.");
						Assert.That (msword.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
						Assert.That (msword.Octets, Is.EqualTo (45544), "Octets did not match.");
						Assert.That (msword.ContentDisposition.Disposition, Is.EqualTo ("attachment"), "Content-Disposition did not match.");
						Assert.That (msword.ContentDisposition.FileName, Is.EqualTo ("PREIS ANSPRUCHS FORMULAR.doc"), "Filename parameters do not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "related"), Is.True, "Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("b1_f0dcbd2fdb06033cba91309b09af1cd8"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "Content-Language lengths do not match.");
						Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("inline"), "Content-Language does not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "related"), Is.True, "Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["boundary"], Is.EqualTo ("b1_f0dcbd2fdb06033cba91309b09af1cd8"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "BodyParts count does not match.");
						Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "Content-Language lengths do not match.");
						Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("inline"), "Content-Language does not match.");
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
							body = ImapUtils.ParseBody (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartBasic> (), "Body types did not match.");
						basic = (BodyPartBasic) body;

						Assert.That (basic.ContentType.IsMimeType ("multipart", "digest"), Is.True, "Content-Type did not match.");
						Assert.That (basic.ContentType.Parameters["boundary"], Is.EqualTo ("ommgDs4vJ6fX2nQAghXj4aUy9wsHMMDb"), "boundary param did not match");
						Assert.That (basic.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encoding did not match.");
						Assert.That (basic.Octets, Is.EqualTo (0), "Octets did not match.");
						Assert.That (basic.ContentDisposition, Is.Null, "Content-Disposition did not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Unexpected token: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartBasic> (), "Body types did not match.");
						basic = (BodyPartBasic) body;

						Assert.That (basic.ContentType.IsMimeType ("multipart", "digest"), Is.True, "Content-Type did not match.");
						Assert.That (basic.ContentType.Parameters["boundary"], Is.EqualTo ("ommgDs4vJ6fX2nQAghXj4aUy9wsHMMDb"), "boundary param did not match");
						Assert.That (basic.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encoding did not match.");
						Assert.That (basic.Octets, Is.EqualTo (0), "Octets did not match.");
						Assert.That (basic.ContentDisposition, Is.Null, "Content-Disposition did not match.");
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
							body = ImapUtils.ParseBody (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "report"), Is.True, "Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["report-type"], Is.EqualTo ("delivery-status"), "report-type param did not match");
						Assert.That (multipart.ContentType.Boundary, Is.EqualTo ("_e0d7475d888f9882b71de053e5efb221_idea"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "multipart children did not match");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "First multipart subpart types did not match.");
						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
						Assert.That (plain.Octets, Is.EqualTo (727), "Octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (16), "Lines did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "Second multipart subpart types did not match.");
						var deliveryStatus = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (deliveryStatus.ContentType.Name, Is.EqualTo ("Delivery status"), "name param did not match");
						Assert.That (deliveryStatus.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
						Assert.That (deliveryStatus.Octets, Is.EqualTo (416), "Octets did not match.");

						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "Third multipart subpart types did not match.");
						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.That (rfc822.ContentType.Name, Is.EqualTo ("Message headers"), "name param did not match");
						Assert.That (rfc822.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
						Assert.That (rfc822.Octets, Is.EqualTo (903), "Octets did not match.");
						Assert.That (rfc822.Lines, Is.EqualTo (17), "Lines did not match.");

						Assert.That (rfc822.Body, Is.InstanceOf<BodyPartMultipart> (), "rfc822 body types did not match.");
						var alternative = (BodyPartMultipart) rfc822.Body;
						Assert.That (alternative.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "Content-Type did not match.");
						Assert.That (alternative.ContentType.Boundary, Is.EqualTo ("Apple-Mail=_352FCEEC-EB15-428F-9D8B-D3B4259DD646"), "boundary param did not match");
						Assert.That (alternative.BodyParts, Is.Empty, "alternative bodyparts count did not match.");
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
							body = await ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
						var multipart = (BodyPartMultipart) body;

						Assert.That (multipart.ContentType.IsMimeType ("multipart", "report"), Is.True, "Content-Type did not match.");
						Assert.That (multipart.ContentType.Parameters["report-type"], Is.EqualTo ("delivery-status"), "report-type param did not match");
						Assert.That (multipart.ContentType.Boundary, Is.EqualTo ("_e0d7475d888f9882b71de053e5efb221_idea"), "boundary param did not match");
						Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "multipart children did not match");

						Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "First multipart subpart types did not match.");
						var plain = (BodyPartText) multipart.BodyParts[0];
						Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
						Assert.That (plain.Octets, Is.EqualTo (727), "Octets did not match.");
						Assert.That (plain.Lines, Is.EqualTo (16), "Lines did not match.");

						Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "Second multipart subpart types did not match.");
						var deliveryStatus = (BodyPartBasic) multipart.BodyParts[1];
						Assert.That (deliveryStatus.ContentType.Name, Is.EqualTo ("Delivery status"), "name param did not match");
						Assert.That (deliveryStatus.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
						Assert.That (deliveryStatus.Octets, Is.EqualTo (416), "Octets did not match.");

						Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartMessage> (), "Third multipart subpart types did not match.");
						var rfc822 = (BodyPartMessage) multipart.BodyParts[2];
						Assert.That (rfc822.ContentType.Name, Is.EqualTo ("Message headers"), "name param did not match");
						Assert.That (rfc822.ContentTransferEncoding, Is.EqualTo ("7bit"), "Content-Transfer-Encoding did not match.");
						Assert.That (rfc822.Octets, Is.EqualTo (903), "Octets did not match.");
						Assert.That (rfc822.Lines, Is.EqualTo (17), "Lines did not match.");

						Assert.That (rfc822.Body, Is.InstanceOf<BodyPartMultipart> (), "rfc822 body types did not match.");
						var alternative = (BodyPartMultipart) rfc822.Body;
						Assert.That (alternative.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "Content-Type did not match.");
						Assert.That (alternative.ContentType.Boundary, Is.EqualTo ("Apple-Mail=_352FCEEC-EB15-428F-9D8B-D3B4259DD646"), "boundary param did not match");
						Assert.That (alternative.BodyParts, Is.Empty, "alternative bodyparts count did not match.");
					}
				}
			}
		}

		static void AssertParseBadlyFormedBodyStructureWithCompletelyNilBodyParts1 (BodyPart body)
		{
			Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
			var multipart = (BodyPartMultipart) body;

			Assert.That (multipart.ContentType.IsMimeType ("multipart", "mixed"), Is.True, "Content-Type did not match.");
			Assert.That (multipart.ContentType.Boundary, Is.EqualTo ("008_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP"), "boundary param did not match");
			Assert.That (multipart.BodyParts, Has.Count.EqualTo (2), "multipart children did not match");

			Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "First multipart/mixed subpart types did not match.");
			var related = (BodyPartMultipart) multipart.BodyParts[0];
			Assert.That (related.ContentType.IsMimeType ("multipart", "related"), Is.True, "Content-Type did not match.");
			Assert.That (related.ContentType.Parameters["type"], Is.EqualTo ("multipart/alternative"), "type param did not match");
			Assert.That (related.ContentType.Boundary, Is.EqualTo ("007_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP"), "boundary param did not match");
			Assert.That (related.BodyParts, Has.Count.EqualTo (4), "multipart children did not match");

			Assert.That (related.BodyParts[0], Is.InstanceOf<BodyPartMultipart> (), "First multipart/related subpart types did not match.");
			var alternative = (BodyPartMultipart) related.BodyParts[0];
			Assert.That (alternative.ContentType.IsMimeType ("multipart", "alternative"), Is.True, "Content-Type did not match.");
			Assert.That (alternative.ContentType.Boundary, Is.EqualTo ("000_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP"), "boundary param did not match");
			Assert.That (alternative.BodyParts, Has.Count.EqualTo (2), "multipart children did not match");

			Assert.That (alternative.BodyParts[0], Is.InstanceOf<BodyPartText> (), "First multipart/alternative subpart types did not match.");
			var plain = (BodyPartText) alternative.BodyParts[0];
			Assert.That (plain.ContentType.IsMimeType ("text", "plain"), Is.True, "Content-Type did not match.");
			Assert.That (plain.ContentType.Charset, Is.EqualTo ("us-ascii"), "Charset parameter did not match");
			Assert.That (plain.ContentTransferEncoding, Is.EqualTo ("quoted-printable"), "Content-Transfer-Encoding did not match.");
			Assert.That (plain.Octets, Is.EqualTo (44619), "Octets did not match.");
			Assert.That (plain.Lines, Is.EqualTo (793), "Lines did not match.");

			Assert.That (alternative.BodyParts[1], Is.InstanceOf<BodyPartText> (), "Second multipart/alternative subpart types did not match.");
			var html = (BodyPartText) alternative.BodyParts[1];
			Assert.That (html.ContentType.IsMimeType ("text", "html"), Is.True, "Content-Type did not match.");
			Assert.That (html.ContentTransferEncoding, Is.EqualTo ("quoted-printable"), "Content-Transfer-Encoding did not match.");
			Assert.That (html.Octets, Is.EqualTo (143984), "Octets did not match.");
			Assert.That (html.Lines, Is.EqualTo (2321), "Lines did not match.");

			Assert.That (related.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "Second multipart/related subpart types did not match.");
			var jpeg = (BodyPartBasic) related.BodyParts[1];
			Assert.That (jpeg.ContentType.IsMimeType ("image", "jpeg"), Is.True, "Content-Type did not match.");
			Assert.That (jpeg.ContentType.Name, Is.EqualTo ("~WRD0000.jpg"), "Name parameter did not match");
			Assert.That (jpeg.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Disposition did not match");
			Assert.That (jpeg.ContentDisposition.FileName, Is.EqualTo ("~WRD0000.jpg"), "Filename parameter did not match");
			Assert.That (jpeg.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
			Assert.That (jpeg.Octets, Is.EqualTo (1130), "Octets did not match.");

			Assert.That (related.BodyParts[2], Is.InstanceOf<BodyPartBasic> (), "Third multipart/related subpart types did not match.");
			var png = (BodyPartBasic) related.BodyParts[2];
			Assert.That (png.ContentType.IsMimeType ("image", "png"), Is.True, "Content-Type did not match.");
			Assert.That (png.ContentType.Name, Is.EqualTo ("image001.png"), "Name parameter did not match");
			Assert.That (png.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Disposition did not match");
			Assert.That (png.ContentDisposition.FileName, Is.EqualTo ("image001.png"), "Filename parameter did not match");
			Assert.That (png.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
			Assert.That (png.Octets, Is.EqualTo (8174), "Octets did not match.");

			Assert.That (related.BodyParts[3], Is.InstanceOf<BodyPartBasic> (), "Fourth multipart/related subpart types did not match.");
			png = (BodyPartBasic) related.BodyParts[3];
			Assert.That (png.ContentType.IsMimeType ("image", "png"), Is.True, "Content-Type did not match.");
			Assert.That (png.ContentType.Name, Is.EqualTo ("image002.png"), "Name parameter did not match");
			Assert.That (png.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Disposition did not match");
			Assert.That (png.ContentDisposition.FileName, Is.EqualTo ("image002.png"), "Filename parameter did not match");
			Assert.That (png.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encoding did not match.");
			Assert.That (png.Octets, Is.EqualTo (3524), "Octets did not match.");

			Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartMessage> (), "Second multipart/mixed subpart types did not match.");
			var rfc822 = (BodyPartMessage) multipart.BodyParts[1];
			Assert.That (rfc822.ContentType.Name, Is.EqualTo (null), "name param did not match");
			Assert.That (rfc822.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encoding did not match.");
			Assert.That (rfc822.Octets, Is.EqualTo (0), "Octets did not match.");
			Assert.That (rfc822.Lines, Is.EqualTo (0), "Lines did not match.");

			// Okay, lets skip ahead to the juicy bits...
			multipart = (BodyPartMultipart) rfc822.Body;
			Assert.That (multipart.ContentType.Boundary, Is.EqualTo ("010_18f52bea798548b88470c3df62d666bcScrubbed"), "boundary param did not match");
			Assert.That (multipart.BodyParts, Has.Count.EqualTo (4), "multipart children did not match");

			rfc822 = (BodyPartMessage) multipart.BodyParts[2];
			multipart = (BodyPartMultipart) rfc822.Body;
			alternative = (BodyPartMultipart) multipart.BodyParts[0];

			for (int i = 0; i < alternative.BodyParts.Count; i++) {
				var nils = (BodyPartBasic) alternative.BodyParts[i];

				Assert.That (nils.ContentType.IsMimeType ("application", "octet-stream"), Is.True, "Content-Type did not match.");
				Assert.That (nils.ContentDescription, Is.Null, "Content-Description should be null");
				Assert.That (nils.ContentDisposition, Is.Null, "Content-Disposition should be null");
				Assert.That (nils.ContentId, Is.Null, "Content-Id should be null");
				Assert.That (nils.ContentLanguage, Is.Null, "Content-Language should be null");
				Assert.That (nils.ContentLocation, Is.Null, "Content-Location should be null");
				Assert.That (nils.ContentMd5, Is.Null, "Content-Md5 should be null");
				Assert.That (nils.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encodings did not match");
				Assert.That (nils.Octets, Is.EqualTo (0), "Octets did not match");
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
							body = ImapUtils.ParseBody (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						AssertParseBadlyFormedBodyStructureWithCompletelyNilBodyParts1 (body);
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithCompletelyNilBodyParts1Async ()
		{
			const string text = "((((\"text\" \"plain\" (\"charset\" \"us-ascii\") NIL NIL \"quoted-printable\" 44619 793 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"us-ascii\") NIL NIL \"quoted-printable\" 143984 2321 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"000_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP\") NIL NIL)(\"image\" \"jpeg\" (\"name\" \"~WRD0000.jpg\") \"<~WRD0000.jpg>\" \"~WRD0000.jpg\" \"base64\" 1130 NIL (\"inline\" (\"filename\" \"~WRD0000.jpg\" \"size\" \"823\" \"creation-date\" \"Thu, 14 Jul 2022 17:26:49 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:16 GMT\")) NIL NIL)(\"image\" \"png\" (\"name\" \"image001.png\") \"image001.png@01D89786.45095140\" \"image001.png\" \"base64\" 8174 NIL (\"inline\" (\"filename\" \"image001.png\" \"size\" \"5973\" \"creation-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL)(\"image\" \"png\" (\"name\" \"image002.png\") \"image002.png@01D89786.45095140\" \"image002.png\" \"base64\" 3524 NIL (\"inline\" (\"filename\" \"image002.png\" \"size\" \"2572\" \"creation-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL) \"related\" (\"boundary\" \"007_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP\" \"type\" \"multipart/alternative\") NIL NIL)(\"message\" \"rfc822\" NIL NIL NIL \"7BIT\" 0 (\"Thu, 14 Jul 2022 15:12:33 +0000\" \"Scrubbed\" ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL NIL ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\") (\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL \"Scrubbed@Scrubbed.com\" \"Scrubbed@Scrubbed.com\") ((((\"text\" \"plain\" (\"charset\" \"utf-8\") NIL NIL \"base64\" 53608 688 NIL NIL NIL NIL)(\"text\" \"html\" (\"charset\" \"utf-8\") \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" NIL \"base64\" 176002 2257 NIL NIL NIL NIL) \"alternative\" (\"boundary\" \"000_18f52bea798548b88470c3df62d666bcScrubbed\") NIL NIL)(\"image\" \"png\" (\"name\" \"image001.png\") \"image001.png@01D89770.62F36800\" \"image001.png\" \"base64\" 8174 NIL (\"inline\" (\"filename\" \"image001.png\" \"size\" \"5973\" \"creation-date\" \"Thu, 14 Jul 2022 15:12:32 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL)(\"image\" \"jpeg\" (\"name\" \"image002.jpg\") \"image002.jpg@01D89770.62F36800\" \"image002.jpg\" \"base64\" 1130 NIL (\"inline\" (\"filename\" \"image002.jpg\" \"size\" \"823\" \"creation-date\" \"Thu, 14 Jul 2022 15:12:32 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL)(\"image\" \"png\" (\"name\" \"image003.png\") \"image003.png@01D89770.62F36800\" \"image003.png\" \"base64\" 3524 NIL (\"inline\" (\"filename\" \"image003.png\" \"size\" \"2572\" \"creation-date\" \"Thu, 14 Jul 2022 15:12:32 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL) \"related\" (\"boundary\" \"009_18f52bea798548b88470c3df62d666bcScrubbed\" \"type\" \"multipart/alternative\") NIL NIL)(\"application\" \"pdf\" (\"name\" \"Scrubbed.pdf\") \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" \"Scrubbed.pdf\" \"base64\" 324012 NIL (\"attachment\" (\"filename\" \"Scrubbed.pdf\" \"size\" \"236776\" \"creation-date\" \"Thu, 14 Jul 2022 14:53:00 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:17 GMT\")) NIL NIL)(\"message\" \"rfc822\" NIL \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" NIL \"7BIT\" 0 (\"Tue, 11 Jan 2022 16:34:33 +0000\" \"RE: Scrubbed\" ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL NIL ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) ((\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\") (\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\") (\"Scrubbed\" NIL \"Scrubbed\" \"Scrubbed\")) NIL \"Scrubbed@Scrubbed.com\" \"Scrubbed@Scrubbed.CANPRD01.PROD.OUTLOOK.COM\") (((NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL)(NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL)(NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL)(NIL NIL NIL NIL NIL \"7BIT\" 0 NIL NIL NIL NIL) \"related\" (\"boundary\" \"007_YT2PR01MB47524CF92A3AD1F75AFF2D25D9519YT2PR01MB4752CANP\" \"type\" \"multipart/alternative\") NIL NIL)(\"application\" \"pdf\" (\"name\" \"Scrubbed.pdf\") NIL \"Scrubbed.pdf\" \"base64\" 215638 NIL (\"attachment\" (\"filename\" \"Scrubbed.pdf\" \"size\" \"157579\" \"creation-date\" \"Wed, 02 Feb 2022 21:33:39 GMT\" \"modification-date\" \"Wed, 02 Feb 2022 21:33:39 GMT\")) NIL NIL) \"mixed\" (\"boundary\" \"008_YT2PR01MB47524CF92A3AD1F75AFF2D25D9519YT2PR01MB4752CANP\") NIL \"en-US\") 0 NIL (\"attachment\" (\"creation-date\" \"Thu, 14 Jul 2022 15:12:31 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL)(\"application\" \"pdf\" (\"name\" \"Scrubbed.pdf?=\") \"Scrubbed@NAMP221.PROD.OUTLOOK.COM\" \"Scrubbed.pdf?=\" \"base64\" 208376 NIL (\"attachment\" (\"filename\" \"Scrubbed.pdf?=\" \"size\" \"152274\" \"creation-date\" \"Thu, 14 Jul 2022 15:05:00 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL) \"mixed\" (\"boundary\" \"010_18f52bea798548b88470c3df62d666bcScrubbed\") NIL \"en-US\") 0 NIL (\"attachment\" (\"creation-date\" \"Thu, 14 Jul 2022 17:33:16 GMT\" \"modification-date\" \"Thu, 14 Jul 2022 17:33:18 GMT\")) NIL NIL) \"mixed\" (\"boundary\" \"008_BN0P221MB04483769DDD81948BC7C387DC8889BN0P221MB0448NAMP\") NIL \"en-US\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						AssertParseBadlyFormedBodyStructureWithCompletelyNilBodyParts1 (body);
					}
				}
			}
		}

		static void AssertParseBadlyFormedBodyStructureWithCompletelyNilBodyParts2 (BodyPart body)
		{
			Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
			var multipart = (BodyPartMultipart) body;

			Assert.That (multipart.ContentType.IsMimeType ("multipart", "report"), Is.True, "Content-Type did not match.");
			Assert.That (multipart.ContentType.Boundary, Is.EqualTo ("272F16D4031920.1659452466/hermes.gatewaynet.com"), "boundary param did not match");
			Assert.That (multipart.BodyParts, Has.Count.EqualTo (3), "multipart children did not match");

			Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartBasic> (), "First multipart/report subpart types did not match.");
			var nils = (BodyPartBasic) multipart.BodyParts[0];
			Assert.That (nils.ContentType.IsMimeType ("application", "octet-stream"), Is.True, "Content-Type did not match.");
			Assert.That (nils.ContentDescription, Is.Null, "Content-Description should be null");
			Assert.That (nils.ContentDisposition, Is.Null, "Content-Disposition should be null");
			Assert.That (nils.ContentId, Is.Null, "Content-Id should be null");
			Assert.That (nils.ContentLanguage, Is.Null, "Content-Language should be null");
			Assert.That (nils.ContentLocation, Is.Null, "Content-Location should be null");
			Assert.That (nils.ContentMd5, Is.Null, "Content-Md5 should be null");
			Assert.That (nils.ContentTransferEncoding, Is.EqualTo ("7BIT"), "Content-Transfer-Encodings did not match");
			Assert.That (nils.Octets, Is.EqualTo (563), "Octets did not match");
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
							body = ImapUtils.ParseBody (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						AssertParseBadlyFormedBodyStructureWithCompletelyNilBodyParts2 (body);
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithCompletelyNilBodyParts2Async ()
		{
			const string text = "((NIL NIL NIL NIL NIL \"7BIT\" 563 NIL NIL NIL NIL)(\"message\" \"delivery-status\" NIL NIL NIL \"7BIT\" 658 NIL NIL NIL NIL)(\"message\" \"rfc822\" NIL NIL NIL \"8bit\" 0 (\"Tue, 2 Aug 2022 15:00:47 +0000\" \"[POSSIBLE SPAM 11.4] Invoices now overdue - 115365#\" ((NIL NIL \"MAILBOX\" \"OUR-DOMAIN\")) NIL NIL ((NIL NIL \"accounts\" \"OTHER-DOMAIN\") (NIL NIL \"safety\" \"OTHER-DOMAIN\") (NIL NIL \"USER\" \"OUR-DOMAIN\")) NIL NIL NIL \"<1IOGPFNLIHU4.377MHPZYJQ6E3@OUR-SERVER>\") (((\"text\" \"plain\" (\"charset\" \"utf-8\") NIL NIL \"8bit\" 597 16 NIL NIL NIL NIL)((\"text\" \"html\" (\"charset\" \"utf-8\") NIL NIL \"7BIT\" 1611 26 NIL NIL NIL NIL)(\"image\" \"png\" (\"name\" \"0.dat\") \"<1KWGPFNLIHU4.4RR7HCVM8MQQ1@OUR-SERVER>\" NIL \"base64\" 14172 NIL (\"inline\" (\"filename\" \"0.dat\")) NIL \"0.dat\")(\"image\" \"png\" (\"name\" \"1.dat\") \"<1KWGPFNLIHU4.UWJ8R86RE2KA2@OUR-SERVER>\" NIL \"base64\" 486 NIL (\"inline\" (\"filename\" \"1.dat\")) NIL \"1.dat\")(\"image\" \"png\" (\"name\" \"2.dat\") \"<1KWGPFNLIHU4.EC7HN124OJC32@OUR-SERVER>\" NIL \"base64\" 506 NIL (\"inline\" (\"filename\" \"2.dat\")) NIL \"2.dat\")(\"image\" \"png\" (\"name\" \"3.dat\") \"<1KWGPFNLIHU4.WM1ALJTG745F1@OUR-SERVER>\" NIL \"base64\" 616 NIL (\"inline\" (\"filename\" \"3.dat\")) NIL \"3.dat\")(\"image\" \"png\" (\"name\" \"4.dat\") \"<1KWGPFNLIHU4.1B42S5EVSF4B2@OUR-SERVER>\" NIL \"base64\" 22470 NIL (\"inline\" (\"filename\" \"4.dat\")) NIL \"4.dat\") \"related\" (\"boundary\" \"=-5nEE2FIlRoeXkJyZAHV8UA==\" \"type\" \"text/html\") NIL NIL) \"alternative\" (\"boundary\" \"=-1sRjeMizXVbc5nGIFXbARA==\") NIL NIL)(\"application\" \"pdf\" (\"name\" \"Reminder.pdf\") \"<RJ2DSFNLIHU4.UUVSNNY5Z3ER@OUR-SERVER>\" NIL \"base64\" 359650 NIL (\"attachment\" (\"filename\" \"Reminder.pdf\" \"size\" \"262820\")) NIL NIL) \"mixed\" (\"boundary\" \"=-EJwVTfPtacyNnTqY4DPQ0A==\") NIL NIL) 0 NIL NIL NIL NIL) \"report\" (\"report-type\" \"delivery-status\" \"boundary\" \"272F16D4031920.1659452466/hermes.gatewaynet.com\") NIL NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						AssertParseBadlyFormedBodyStructureWithCompletelyNilBodyParts2 (body);
					}
				}
			}
		}

		static void AssertParseBadlyFormedBodyStructureWithEmptyParensInsteadOfContentLocation (BodyPart body)
		{
			Assert.That (body, Is.InstanceOf<BodyPartMultipart> (), "Body types did not match.");
			var multipart = (BodyPartMultipart) body;

			Assert.That (multipart.ContentType.IsMimeType ("multipart", "related"), Is.True, "Content-Type did not match.");
			Assert.That (multipart.ContentType.Boundary, Is.EqualTo ("_004_7D3F5AE184118942976793FC500B8F4A402D17DB3PRD0702MB097eu_"), "boundary param did not match");
			Assert.That (multipart.ContentDisposition, Is.Null, "Content-Disposition should be null");
			Assert.That (multipart.ContentLanguage, Has.Length.EqualTo (1), "Content-Language should not be null");
			Assert.That (multipart.ContentLanguage[0], Is.EqualTo ("de-DE"), "Content-Language did not match");
			Assert.That (multipart.ContentLocation, Is.Null, "Content-Location should be null");
			Assert.That (multipart.BodyParts, Has.Count.EqualTo (4), "multipart children did not match");

			Assert.That (multipart.BodyParts[0], Is.InstanceOf<BodyPartText> (), "First multipart/related subpart types did not match.");
			var text = (BodyPartText) multipart.BodyParts[0];
			Assert.That (text.ContentType.IsMimeType ("text", "html"), Is.True, "Content-Type did not match.");
			Assert.That (text.ContentType.Charset, Is.EqualTo ("utf-8"), "Charset param did not match");
			Assert.That (text.ContentDescription, Is.Null, "Content-Description should be null");
			Assert.That (text.ContentDisposition, Is.Null, "Content-Disposition should be null");
			Assert.That (text.ContentId, Is.Null, "Content-Id should be null");
			Assert.That (text.ContentLanguage, Is.Null, "Content-Language should be null");
			Assert.That (text.ContentLocation, Is.Null, "Content-Location should be null");
			Assert.That (text.ContentMd5, Is.Null, "Content-Md5 should be null");
			Assert.That (text.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encodings did not match");
			Assert.That (text.Octets, Is.EqualTo (38706), "Octets did not match");
			Assert.That (text.Lines, Is.EqualTo (497), "Lines did not match");

			Assert.That (multipart.BodyParts[1], Is.InstanceOf<BodyPartBasic> (), "Second multipart/related subpart types did not match.");
			var image1 = (BodyPartBasic) multipart.BodyParts[1];
			Assert.That (image1.ContentType.IsMimeType ("image", "jpeg"), Is.True, "Content-Type did not match.");
			Assert.That (image1.ContentType.Name, Is.EqualTo ("image003.jpg"), "Name parameter did not match");
			Assert.That (image1.ContentDescription, Is.EqualTo ("image003.jpg"), "Content-Description should be null");
			Assert.That (image1.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match");
			Assert.That (image1.ContentDisposition.Parameters.ToString (), Is.EqualTo ("; filename=\"image003.jpg\"; size=\"2782\"; creation-date=\"Thu, 22 Mar 2012 13:56:38 GMT\"; modification-date=\"Thu, 22 Mar 2012 13:56:38 GMT\""), "Content-Disposition parameters did not match");
			Assert.That (image1.ContentId, Is.EqualTo ("<image003.jpg@01CD0772.E9574810>"), "Content-Id did not match");
			Assert.That (image1.ContentLanguage, Is.Null, "Content-Language should be null");
			Assert.That (image1.ContentLocation, Is.Null, "Content-Location should be null");
			Assert.That (image1.ContentMd5, Is.Null, "Content-Md5 should be null");
			Assert.That (image1.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encodings did not match");
			Assert.That (image1.Octets, Is.EqualTo (3446), "Octets did not match");

			Assert.That (multipart.BodyParts[2], Is.InstanceOf<BodyPartBasic> (), "Third multipart/related subpart types did not match.");
			var image2 = (BodyPartBasic) multipart.BodyParts[2];
			Assert.That (image2.ContentType.IsMimeType ("image", "jpeg"), Is.True, "Content-Type did not match.");
			Assert.That (image2.ContentType.Name, Is.EqualTo ("image004.jpg"), "Name parameter did not match");
			Assert.That (image2.ContentDescription, Is.EqualTo ("image004.jpg"), "Content-Description should be null");
			Assert.That (image2.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match");
			Assert.That (image2.ContentDisposition.Parameters.ToString (), Is.EqualTo ("; filename=\"image004.jpg\"; size=\"2782\"; creation-date=\"Thu, 22 Mar 2012 13:56:39 GMT\"; modification-date=\"Thu, 22 Mar 2012 13:56:39 GMT\""), "Content-Disposition parameters did not match");
			Assert.That (image2.ContentId, Is.EqualTo ("<image004.jpg@01CD0772.E9574810>"), "Content-Id did not match");
			Assert.That (image2.ContentLanguage, Is.Null, "Content-Language should be null");
			Assert.That (image2.ContentLocation, Is.Null, "Content-Location should be null");
			Assert.That (image2.ContentMd5, Is.Null, "Content-Md5 should be null");
			Assert.That (image2.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encodings did not match");
			Assert.That (image2.Octets, Is.EqualTo (3446), "Octets did not match");

			Assert.That (multipart.BodyParts[3], Is.InstanceOf<BodyPartBasic> (), "Fourth multipart/related subpart types did not match.");
			var image3 = (BodyPartBasic) multipart.BodyParts[3];
			Assert.That (image3.ContentType.IsMimeType ("image", "jpeg"), Is.True, "Content-Type did not match.");
			Assert.That (image3.ContentType.Name, Is.EqualTo ("image005.jpg"), "Name parameter did not match");
			Assert.That (image3.ContentDescription, Is.EqualTo ("image005.jpg"), "Content-Description should be null");
			Assert.That (image3.ContentDisposition.Disposition, Is.EqualTo ("inline"), "Content-Disposition did not match");
			Assert.That (image3.ContentDisposition.Parameters.ToString (), Is.EqualTo ("; filename=\"image005.jpg\"; size=\"2625\"; creation-date=\"Thu, 22 Mar 2012 13:56:39 GMT\"; modification-date=\"Thu, 22 Mar 2012 13:56:39 GMT\""), "Content-Disposition parameters did not match");
			Assert.That (image3.ContentId, Is.EqualTo ("<image005.jpg@01CD0772.E9574810>"), "Content-Id did not match");
			Assert.That (image3.ContentLanguage, Is.Null, "Content-Language should be null");
			Assert.That (image3.ContentLocation, Is.Null, "Content-Location should be null");
			Assert.That (image3.ContentMd5, Is.Null, "Content-Md5 should be null");
			Assert.That (image3.ContentTransferEncoding, Is.EqualTo ("base64"), "Content-Transfer-Encodings did not match");
			Assert.That (image3.Octets, Is.EqualTo (3232), "Octets did not match");
		}

		[Test]
		public void TestParseBadlyFormedBodyStructureWithEmptyParensInsteadOfContentLocation ()
		{
			const string text = "((\"text\" \"html\" (\"charset\" \"utf-8\") NIL NIL \"base64\" 38706 497 NIL NIL NIL ()) (\"image\" \"jpeg\" (\"name\" \"image003.jpg\") \"<image003.jpg@01CD0772.E9574810>\" \"image003.jpg\" \"base64\" 3446 NIL (\"inline\" (\"filename\" \"image003.jpg\" \"size\" \"2782\" \"creation-date\" \"Thu, 22 Mar 2012 13:56:38 GMT\" \"modification-date\" \"Thu, 22 Mar 2012 13:56:38 GMT\")) NIL ()) (\"image\" \"jpeg\" (\"name\" \"image004.jpg\") \"<image004.jpg@01CD0772.E9574810>\" \"image004.jpg\" \"base64\" 3446 NIL (\"inline\" (\"filename\" \"image004.jpg\" \"size\" \"2782\" \"creation-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\" \"modification-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\")) NIL ()) (\"image\" \"jpeg\" (\"name\" \"image005.jpg\") \"<image005.jpg@01CD0772.E9574810>\" \"image005.jpg\" \"base64\" 3232 NIL (\"inline\" (\"filename\" \"image005.jpg\" \"size\" \"2625\" \"creation-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\" \"modification-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\")) NIL ()) \"related\" (\"boundary\" \"_004_7D3F5AE184118942976793FC500B8F4A402D17DB3PRD0702MB097eu_\" \"type\" \"text/html\") NIL (\"de-DE\") NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBody (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						AssertParseBadlyFormedBodyStructureWithEmptyParensInsteadOfContentLocation (body);
					}
				}
			}
		}

		[Test]
		public async Task TestParseBadlyFormedBodyStructureWithEmptyParensInsteadOfContentLocationAsync ()
		{
			const string text = "((\"text\" \"html\" (\"charset\" \"utf-8\") NIL NIL \"base64\" 38706 497 NIL NIL NIL ()) (\"image\" \"jpeg\" (\"name\" \"image003.jpg\") \"<image003.jpg@01CD0772.E9574810>\" \"image003.jpg\" \"base64\" 3446 NIL (\"inline\" (\"filename\" \"image003.jpg\" \"size\" \"2782\" \"creation-date\" \"Thu, 22 Mar 2012 13:56:38 GMT\" \"modification-date\" \"Thu, 22 Mar 2012 13:56:38 GMT\")) NIL ()) (\"image\" \"jpeg\" (\"name\" \"image004.jpg\") \"<image004.jpg@01CD0772.E9574810>\" \"image004.jpg\" \"base64\" 3446 NIL (\"inline\" (\"filename\" \"image004.jpg\" \"size\" \"2782\" \"creation-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\" \"modification-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\")) NIL ()) (\"image\" \"jpeg\" (\"name\" \"image005.jpg\") \"<image005.jpg@01CD0772.E9574810>\" \"image005.jpg\" \"base64\" 3232 NIL (\"inline\" (\"filename\" \"image005.jpg\" \"size\" \"2625\" \"creation-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\" \"modification-date\" \"Thu, 22 Mar 2012 13:56:39 GMT\")) NIL ()) \"related\" (\"boundary\" \"_004_7D3F5AE184118942976793FC500B8F4A402D17DB3PRD0702MB097eu_\" \"type\" \"text/html\") NIL (\"de-DE\") NIL)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = await ImapUtils.ParseBodyAsync (engine, "Syntax error in BODYSTRUCTURE: {0}", string.Empty, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing BODYSTRUCTURE failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						AssertParseBadlyFormedBodyStructureWithEmptyParensInsteadOfContentLocation (body);
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
						var threads = new List<MessageThread> ();

						engine.SetStream (tokenizer);

						try {
							ImapUtils.ParseThreads (engine, 0, threads, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing THREAD response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (threads, Has.Count.EqualTo (2), "Expected 2 threads.");

						Assert.That (threads[0].UniqueId.Value.Id, Is.EqualTo ((uint) 2));
						Assert.That (threads[1].UniqueId.Value.Id, Is.EqualTo ((uint) 3));

						var branches = threads[1].Children.ToArray ();
						Assert.That (branches, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (branches[0].UniqueId.Value.Id, Is.EqualTo ((uint) 6));

						branches = branches[0].Children.ToArray ();
						Assert.That (branches, Has.Length.EqualTo (2), "Expected 2 branches.");

						Assert.That (branches[0].UniqueId.Value.Id, Is.EqualTo ((uint) 4));
						Assert.That (branches[1].UniqueId.Value.Id, Is.EqualTo ((uint) 44));

						var children = branches[0].Children.ToArray ();
						Assert.That (children, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 23));
						Assert.That (children[0].Children, Is.Empty, "Expected no children.");

						children = branches[1].Children.ToArray ();
						Assert.That (children, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 7));

						children = children[0].Children.ToArray ();
						Assert.That (children, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 96));
						Assert.That (children[0].Children, Is.Empty, "Expected no children.");
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
						var threads = new List<MessageThread> ();

						engine.SetStream (tokenizer);

						try {
							await ImapUtils.ParseThreadsAsync (engine, 0, threads, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing THREAD response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (threads, Has.Count.EqualTo (2), "Expected 2 threads.");

						Assert.That (threads[0].UniqueId.Value.Id, Is.EqualTo ((uint) 2));
						Assert.That (threads[1].UniqueId.Value.Id, Is.EqualTo ((uint) 3));

						var branches = threads[1].Children.ToArray ();
						Assert.That (branches, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (branches[0].UniqueId.Value.Id, Is.EqualTo ((uint) 6));

						branches = branches[0].Children.ToArray ();
						Assert.That (branches, Has.Length.EqualTo (2), "Expected 2 branches.");

						Assert.That (branches[0].UniqueId.Value.Id, Is.EqualTo ((uint) 4));
						Assert.That (branches[1].UniqueId.Value.Id, Is.EqualTo ((uint) 44));

						var children = branches[0].Children.ToArray ();
						Assert.That (children, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 23));
						Assert.That (children[0].Children, Is.Empty, "Expected no children.");

						children = branches[1].Children.ToArray ();
						Assert.That (children, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 7));

						children = children[0].Children.ToArray ();
						Assert.That (children, Has.Length.EqualTo (1), "Expected 1 child.");
						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 96));
						Assert.That (children[0].Children, Is.Empty, "Expected no children.");
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
						var threads = new List<MessageThread> ();

						engine.SetStream (tokenizer);

						try {
							ImapUtils.ParseThreads (engine, 0, threads, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing THREAD response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (threads, Has.Count.EqualTo (40), "Expected 40 threads.");

						Assert.That (threads[0].UniqueId.Value.Id, Is.EqualTo ((uint) 3));
						Assert.That (threads[1].UniqueId.Value.Id, Is.EqualTo ((uint) 1));
						//Assert.That (threads[2].UniqueId.Value.Id, Is.EqualTo ((uint) 0));
						Assert.That (threads[2].UniqueId.HasValue, Is.False);

						var branches = threads[2].Children.ToArray ();
						Assert.That (branches, Has.Length.EqualTo (3), "Expected 3 children.");
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
						var threads = new List<MessageThread> ();

						engine.SetStream (tokenizer);

						try {
							await ImapUtils.ParseThreadsAsync (engine, 0, threads, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing THREAD response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (threads, Has.Count.EqualTo (40), "Expected 40 threads.");

						Assert.That (threads[0].UniqueId.Value.Id, Is.EqualTo ((uint) 3));
						Assert.That (threads[1].UniqueId.Value.Id, Is.EqualTo ((uint) 1));
						//Assert.That (threads[2].UniqueId.Value.Id, Is.EqualTo ((uint) 0));
						Assert.That (threads[2].UniqueId.HasValue, Is.False);

						var branches = threads[2].Children.ToArray ();
						Assert.That (branches, Has.Length.EqualTo (3), "Expected 3 children.");
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
						var threads = new List<MessageThread> ();

						engine.SetStream (tokenizer);

						try {
							ImapUtils.ParseThreads (engine, 0, threads, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing THREAD response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (threads, Has.Count.EqualTo (1), "Expected 1 thread.");

						//Assert.That (threads[0].UniqueId.Value.Id, Is.EqualTo ((uint) 0));
						Assert.That (threads[0].UniqueId.HasValue, Is.False);

						var children = threads[0].Children;
						Assert.That (children, Has.Count.EqualTo (2), "Expected 2 children.");

						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 352));
						Assert.That (children[1].UniqueId.Value.Id, Is.EqualTo ((uint) 381));
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
						var threads = new List<MessageThread> ();

						engine.SetStream (tokenizer);

						try {
							await ImapUtils.ParseThreadsAsync (engine, 0, threads, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing THREAD response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (threads, Has.Count.EqualTo (1), "Expected 1 thread.");

						//Assert.That (threads[0].UniqueId.Value.Id, Is.EqualTo ((uint) 0));
						Assert.That (threads[0].UniqueId.HasValue, Is.False);

						var children = threads[0].Children;
						Assert.That (children, Has.Count.EqualTo (2), "Expected 2 children.");

						Assert.That (children[0].UniqueId.Value.Id, Is.EqualTo ((uint) 352));
						Assert.That (children[1].UniqueId.Value.Id, Is.EqualTo ((uint) 381));
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
			Assert.That (command.ToString (), Is.EqualTo ("STORE "), "empty collection");

			annotations.Add (new Annotation (AnnotationEntry.AltSubject));

			ImapUtils.FormatAnnotations (command, annotations, args, false);
			Assert.That (command.ToString (), Is.EqualTo ("STORE "), "annotation w/o properties");
			Assert.Throws<ArgumentException> (() => ImapUtils.FormatAnnotations (command, annotations, args, true));

			command.Clear ();
			command.Append ("STORE ");
			annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "This is an alternate subject.");
			ImapUtils.FormatAnnotations (command, annotations, args, true);
			Assert.That (command.ToString (), Is.EqualTo ("STORE ANNOTATION (/altsubject (value.shared %S))"));
			Assert.That (args, Has.Count.EqualTo (1), "args");
			Assert.That (args[0], Is.EqualTo ("This is an alternate subject."), "args[0]");
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
							annotations = ImapUtils.ParseAnnotations (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ANNOTATION response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (annotations, Has.Count.EqualTo (1), "Count");
						Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "Entry");
						Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "Properties.Count");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "value.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "value.shared");
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
							annotations = await ImapUtils.ParseAnnotationsAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ANNOTATION response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (annotations, Has.Count.EqualTo (1), "Count");
						Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "Entry");
						Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "Properties.Count");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "value.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "value.shared");
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
							annotations = ImapUtils.ParseAnnotations (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ANNOTATION response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (annotations, Has.Count.EqualTo (2), "Count");
						Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
						Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "annotations[0].Properties.Count");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
						Assert.That (annotations[1].Entry, Is.EqualTo (AnnotationEntry.AltSubject), "annotations[1].Entry");
						Assert.That (annotations[1].Properties, Has.Count.EqualTo (2), "annotations[1].Properties.Count");
						Assert.That (annotations[1].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My subject"), "annotations[1] value.priv");
						Assert.That (annotations[1].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[1] value.shared");
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
							annotations = await ImapUtils.ParseAnnotationsAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ANNOTATION response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (annotations, Has.Count.EqualTo (2), "Count");
						Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
						Assert.That (annotations[0].Properties, Has.Count.EqualTo (2), "annotations[0].Properties.Count");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
						Assert.That (annotations[1].Entry, Is.EqualTo (AnnotationEntry.AltSubject), "annotations[1].Entry");
						Assert.That (annotations[1].Properties, Has.Count.EqualTo (2), "annotations[1].Properties.Count");
						Assert.That (annotations[1].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My subject"), "annotations[1] value.priv");
						Assert.That (annotations[1].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[1] value.shared");
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
							annotations = ImapUtils.ParseAnnotations (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ANNOTATION response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (annotations, Has.Count.EqualTo (1), "Count");
						Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
						Assert.That (annotations[0].Properties, Has.Count.EqualTo (4), "annotations[0].Properties.Count");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateSize], Is.EqualTo ("10"), "annotations[0] size.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedSize], Is.EqualTo ("0"), "annotations[0] size.shared");
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
							annotations = await ImapUtils.ParseAnnotationsAsync (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing ANNOTATION response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (annotations, Has.Count.EqualTo (1), "Count");
						Assert.That (annotations[0].Entry, Is.EqualTo (AnnotationEntry.Comment), "annotations[0].Entry");
						Assert.That (annotations[0].Properties, Has.Count.EqualTo (4), "annotations[0].Properties.Count");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateValue], Is.EqualTo ("My comment"), "annotations[0] value.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedValue], Is.EqualTo (null), "annotations[0] value.shared");
						Assert.That (annotations[0].Properties[AnnotationAttribute.PrivateSize], Is.EqualTo ("10"), "annotations[0] size.priv");
						Assert.That (annotations[0].Properties[AnnotationAttribute.SharedSize], Is.EqualTo ("0"), "annotations[0] size.shared");
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
							ImapUtils.ParseFolderList (engine, list, false, false, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing LIST response failed: {ex}");
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (list, Has.Count.EqualTo (1), "Count");
						Assert.That (list[0].Name, Is.EqualTo ("Da\tOggetto\tRicevuto\tDimensione\tCategorie\t"), "Name");
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
							await ImapUtils.ParseFolderListAsync (engine, list, false, false, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing LIST response failed: {ex}");
							return;
						}

						var token = await engine.ReadTokenAsync (CancellationToken.None);
						Assert.That (token.Type, Is.EqualTo (ImapTokenType.Eoln), $"Expected new-line, but got: {token}");

						Assert.That (list, Has.Count.EqualTo (1), "Count");
						Assert.That (list[0].Name, Is.EqualTo ("Da\tOggetto\tRicevuto\tDimensione\tCategorie\t"), "Name");
					}
				}
			}
		}

		[Test]
		[TestCase (2023, 5, 14, 12, 30, 45, -4, -30, "14-May-2023 12:30:45 -0430")] // Sunday
		[TestCase (2023, 5, 15, 12, 30, 45, -3, -30, "15-May-2023 12:30:45 -0330")] // Monday
		[TestCase (2023, 5, 16, 12, 30, 45, -2, 0, "16-May-2023 12:30:45 -0200")]   // Tuesday
		[TestCase (2023, 5, 17, 12, 30, 45, -1, 0, "17-May-2023 12:30:45 -0100")]   // Wednesday
		public void TestFormatInternalDateNegativeOffsets (int year, int month, int day, int hour, int minute, int second, int offsetHours, int offsetMinutes, string expected)
		{
			var date = new DateTimeOffset (year, month, day, hour, minute, second, new TimeSpan (offsetHours, offsetMinutes, 0));

			string formattedInternalDate = ImapUtils.FormatInternalDate (date);

			Assert.That (formattedInternalDate, Is.EqualTo (expected), $"Expected {expected} but got {formattedInternalDate} for date {date}.");
		}

		[Test]
		[TestCase (2023, 5, 18, 12, 30, 45, 1, 0, "18-May-2023 12:30:45 +0100")]   // Thursday
		[TestCase (2023, 5, 19, 12, 30, 45, 4, 30, "19-May-2023 12:30:45 +0430")]  // Friday
		[TestCase (2023, 5, 20, 12, 30, 45, 9, 30, "20-May-2023 12:30:45 +0930")]  // Saturday
		[TestCase (2023, 5, 21, 12, 30, 45, 12, 0, "21-May-2023 12:30:45 +1200")]  // Sunday
		public void TestFormatInternalDatePositiveOffsets (int year, int month, int day, int hour, int minute, int second, int offsetHours, int offsetMinutes, string expected)
		{
			var date = new DateTimeOffset (year, month, day, hour, minute, second, new TimeSpan (offsetHours, offsetMinutes, 0));

			string formattedInternalDate = ImapUtils.FormatInternalDate (date);

			Assert.That (formattedInternalDate, Is.EqualTo (expected), $"Expected {expected} but got {formattedInternalDate} for date {date}.");
		}

		[Test]
		[TestCase (2023, 5, 22, 12, 30, 45, 0, 0, "22-May-2023 12:30:45 +0000")]  // Monday
		public void TestFormatInternalDateZeroOffset (int year, int month, int day, int hour, int minute, int second, int offsetHours, int offsetMinutes, string expected)
		{
			var date = new DateTimeOffset (year, month, day, hour, minute, second, new TimeSpan (offsetHours, offsetMinutes, 0));

			string formattedInternalDate = ImapUtils.FormatInternalDate (date);

			Assert.That (formattedInternalDate, Is.EqualTo (expected), $"Expected {expected} but got {formattedInternalDate} for date {date}.");
		}

		[Test]
		[TestCase (2023, 5, 23, 23, 59, 59, 2, 30, "23-May-2023 23:59:59 +0230")]  // Tuesday
		[TestCase (2023, 5, 24, 0, 0, 0, -4, -30, "24-May-2023 00:00:00 -0430")]  // Wednesday
		public void TestFormatInternalDateEdgeCases (int year, int month, int day, int hour, int minute, int second, int offsetHours, int offsetMinutes, string expected)
		{
			var date = new DateTimeOffset (year, month, day, hour, minute, second, new TimeSpan (offsetHours, offsetMinutes, 0));

			string formattedInternalDate = ImapUtils.FormatInternalDate (date);

			Assert.That (formattedInternalDate, Is.EqualTo (expected), $"Expected {expected} but got {formattedInternalDate} for date {date}.");
		}
	}
}
