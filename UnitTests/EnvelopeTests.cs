//
// EnvelopeTests.cs
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
using MimeKit.Utils;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class EnvelopeTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => Envelope.TryParse (null, out _));
		}

		[Test]
		public void TestSerialization ()
		{
			var original = new Envelope ();
			original.Bcc.Add (new MailboxAddress ("Bcc Recipient", "unit-tests@mimekit.org"));
			original.Cc.Add (new MailboxAddress ("Routed Mailbox", new string[] { "domain.org" }, "unit-tests@mimekit.org"));
			original.Date = DateTimeOffset.Now;
			original.From.Add (new MailboxAddress ("MailKit Unit Tests", "unit-tests@mimekit.org"));
			original.InReplyTo = "<xyz@mimekit.org>";
			original.MessageId = "<xyz123@mimekit.org>";
			original.ReplyTo.Add (new MailboxAddress ("Reply-To", "unit-tests@mimekit.org"));
			original.Sender.Add (new MailboxAddress ("The Real Sender", "unit-tests@mimekit.org"));
			original.Subject = "This is the subject";
			original.To.Add (new GroupAddress ("Group Address", new MailboxAddress[] {
				new MailboxAddress ("John \"Q.\" Recipient", "unit-tests@mimekit.org"),
				new MailboxAddress ("Sarah Connor", "unit-tests@mimekit.org")
			}));
			var text = original.ToString ();
			Envelope envelope;

			Assert.That (Envelope.TryParse (text, out envelope), Is.True);
			var text2 = envelope.ToString ();

			Assert.That (text2, Is.EqualTo (text));
		}

		[Test]
		public void TestUnixAddressSerialization ()
		{
			var original = new Envelope ();
			original.Date = DateTimeOffset.Now;
			original.From.Add (new MailboxAddress ((string) null, "fejj"));
			original.To.Add (new MailboxAddress ((string) null, "notzed"));
			original.InReplyTo = "<xyz@mimekit.org>";
			original.MessageId = "<xyz123@mimekit.org>";
			original.ReplyTo.Add (new MailboxAddress ("Reply-To", "unit-tests@mimekit.org"));
			original.Sender.Add (new MailboxAddress ("The Real Sender", string.Empty));
			original.Subject = "This is the subject";
			var text = original.ToString ();

			Assert.That (Envelope.TryParse (text, out var envelope), Is.True);
			Assert.That (envelope.Sender.Mailboxes.First ().LocalPart, Is.EqualTo (string.Empty));
			Assert.That (envelope.From.Mailboxes.First ().LocalPart, Is.EqualTo ("fejj"));
			Assert.That (envelope.To.Mailboxes.First ().LocalPart, Is.EqualTo ("notzed"));
			var text2 = envelope.ToString ();

			Assert.That (text2, Is.EqualTo (text));
		}

		[Test]
		public void TestExampleEnvelopeRfc3501 ()
		{
			const string text = "(\"Wed, 17 Jul 1996 02:23:25 -0700 (PDT)\" \"IMAP4rev1 WG mtg summary and minutes\" ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((\"Terry Gray\" NIL \"gray\" \"cac.washington.edu\")) ((NIL NIL \"imap\" \"cac.washington.edu\")) ((NIL NIL \"minutes\" \"CNRI.Reston.VA.US\") (\"John Klensin\" NIL \"KLENSIN\" \"MIT.EDU\")) NIL NIL \"<B27397-0100000@cac.washington.edu>\")";
			Envelope envelope;

			Assert.That (Envelope.TryParse (text, out envelope), Is.True, "Failed to parse envelope.");

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

		[Test]
		public void TestEmptyEnvelope ()
		{
			const string expected = "(NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL)";
			var envelope = new Envelope ();

			Assert.That (envelope.ToString (), Is.EqualTo (expected));
			Assert.That (Envelope.TryParse (expected, out envelope), Is.True);
			Assert.That (envelope.ToString (), Is.EqualTo (expected));
		}

		[Test]
		public void TestGroupAddress ()
		{
			const string expected = "(NIL NIL NIL NIL NIL ((NIL NIL \"Agents of Shield\" NIL)(\"Skye\" NIL \"skye\" \"shield.gov\")(\"Leo Fitz\" NIL \"fitz\" \"shield.gov\")(\"Melinda May\" NIL \"may\" \"shield.gov\")(NIL NIL NIL NIL)) NIL NIL NIL NIL)";
			var group = GroupAddress.Parse ("Agents of Shield: Skye <skye@shield.gov>, Leo Fitz <fitz@shield.gov>, Melinda May <may@shield.gov>;");
			var envelope = new Envelope ();

			envelope.To.Add (group);

			Assert.That (envelope.ToString (), Is.EqualTo (expected));
			Assert.That (Envelope.TryParse (expected, out envelope), Is.True);
			Assert.That (envelope.ToString (), Is.EqualTo (expected));
			Assert.That (envelope.To, Has.Count.EqualTo (1));
			Assert.That (envelope.To[0].ToString (), Is.EqualTo (group.ToString ()));
		}

		[Test]
		public void TestNestedGroupAddresses ()
		{
			const string expected = "(NIL NIL NIL NIL NIL ((NIL NIL \"Agents of Shield\" NIL)(NIL NIL \"Mutants\" NIL)(\"Skye\" NIL \"skye\" \"shield.gov\")(NIL NIL NIL NIL)(\"Leo Fitz\" NIL \"fitz\" \"shield.gov\")(\"Melinda May\" NIL \"may\" \"shield.gov\")(NIL NIL NIL NIL)) NIL NIL NIL NIL)";
			var group = GroupAddress.Parse ("Agents of Shield: Mutants: Skye <skye@shield.gov>;, Leo Fitz <fitz@shield.gov>, Melinda May <may@shield.gov>;");
			var envelope = new Envelope ();

			envelope.To.Add (group);

			Assert.That (envelope.ToString (), Is.EqualTo (expected));
			Assert.That (Envelope.TryParse (expected, out envelope), Is.True);
			Assert.That (envelope.ToString (), Is.EqualTo (expected));
			Assert.That (envelope.To, Has.Count.EqualTo (1));
			Assert.That (envelope.To[0].ToString (), Is.EqualTo (group.ToString ()));
		}
	}
}
