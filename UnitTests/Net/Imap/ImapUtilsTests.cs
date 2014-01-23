//
// ImapBodyParsingTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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
using System.Text;
using System.Threading;

using NUnit.Framework;

using MimeKit;
using MimeKit.Utils;

using MailKit.Net.Imap;
using MailKit;

namespace UnitTests {
	[TestFixture]
	public class ImapUtilsTests
	{
		[Test]
		public void TestParseSimpleBodyRfc3501 ()
		{
			const string text = "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"US-ASCII\") NIL NIL \"7BIT\" 3028 92)\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine ()) {
						BodyPartText basic;
						BodyPart body;

						engine.SetStream (tokenizer);

						try {
							body = ImapUtils.ParseBody (engine, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ("Parsing BODY failed: {0}", ex);
							return;
						}

						var token = engine.ReadToken (CancellationToken.None);
						Assert.AreEqual (ImapTokenType.Eoln, token.Type, "Expected new-line, but got: {0}", token);

						Assert.IsInstanceOfType (typeof (BodyPartText), body, "Body types did not match.");
						basic = (BodyPartText) body;

						Assert.IsTrue (body.ContentType.Matches ("text", "plain"), "Content-Type did not match.");
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
		public void TestParseEnvelopeRfc3501 ()
		{
			const string text = "(\"Wed, 17 Jul 1996 02:23:25 -0700 (PDT)\" \"IMAP4rev1 WG mtg summary and minutes\" ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL \"<B27397-0100000@cac.washington.edu>\")\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine ()) {
						Envelope envelope;

						engine.SetStream (tokenizer);

						try {
							envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);
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

						Assert.AreEqual (0, envelope.InReplyTo.Count, "In-Reply-To count does not match.");

						Assert.AreEqual ("B27397-0100000@cac.washington.edu", envelope.MessageId, "Message-Id does not match.");
					}
				}
			}
		}
	}
}
