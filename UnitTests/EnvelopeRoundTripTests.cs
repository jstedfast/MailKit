//
// EnvelopeRoundTripTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Text.RegularExpressions;

using MimeKit;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests {
	[TestFixture]
	public class EnvelopeRoundTripTests
	{
		static string Quote (string? value) => value == null ? "<null>" : "[" + value + "]";

		static void Dump (StringBuilder builder, InternetAddressList list, string indent)
		{
			foreach (var address in list) {
				if (address is MailboxAddress mailbox) {
					builder.Append (indent).Append ("mailbox name=").Append (Quote (mailbox.Name));
					builder.Append (" route=").Append (string.Join (",", mailbox.Route));
					builder.Append (" addr=").Append (Quote (mailbox.Address)).Append ('\n');
				} else if (address is GroupAddress group) {
					builder.Append (indent).Append ("group name=").Append (Quote (group.Name)).Append ('\n');
					Dump (builder, group.Members, indent + "  ");
				}
			}
		}

		// Produces a canonical dump of every property so that two envelopes can be compared for equality.
		internal static string Dump (Envelope? envelope)
		{
			if (envelope == null)
				return "<null envelope>\n";

			var builder = new StringBuilder ();
			builder.Append ("date=").Append (envelope.Date?.ToString ("o") ?? "<null>").Append ('\n');
			builder.Append ("subject=").Append (Quote (envelope.Subject)).Append ('\n');
			builder.Append ("from:\n");
			Dump (builder, envelope.From, "  ");
			builder.Append ("sender:\n");
			Dump (builder, envelope.Sender, "  ");
			builder.Append ("reply-to:\n");
			Dump (builder, envelope.ReplyTo, "  ");
			builder.Append ("to:\n");
			Dump (builder, envelope.To, "  ");
			builder.Append ("cc:\n");
			Dump (builder, envelope.Cc, "  ");
			builder.Append ("bcc:\n");
			Dump (builder, envelope.Bcc, "  ");
			builder.Append ("in-reply-to=").Append (Quote (envelope.InReplyTo)).Append ('\n');
			builder.Append ("message-id=").Append (Quote (envelope.MessageId)).Append ('\n');

			return builder.ToString ();
		}

		internal static ImapEngine CreateEngine (string text)
		{
			var stream = new ImapStream (new MemoryStream (Encoding.UTF8.GetBytes (text + "\r\n"), false), new NullProtocolLogger ());
			var engine = new ImapEngine (null);
			engine.SetStream (stream);
			return engine;
		}

		static IEnumerable<string> ExtractLists (string line, string keyword)
		{
			int index = 0;

			while ((index = line.IndexOf (keyword + " (", index, StringComparison.Ordinal)) != -1) {
				int start = index + keyword.Length + 1;
				bool quoted = false, escaped = false;
				int depth = 0;
				int i;

				for (i = start; i < line.Length; i++) {
					char c = line[i];

					if (quoted) {
						if (escaped)
							escaped = false;
						else if (c == '\\')
							escaped = true;
						else if (c == '"')
							quoted = false;
					} else if (c == '"') {
						quoted = true;
					} else if (c == '(') {
						depth++;
					} else if (c == ')' && --depth == 0) {
						break;
					}
				}

				if (i < line.Length)
					yield return line.Substring (start, i - start + 1);

				index = start;
			}
		}

		// Extracts every parenthesized list following one of the given keywords from the IMAP test resources
		// as well as the string constants from ImapUtilsTests.cs. Literals are not supported, so lines that
		// contain literals are skipped. Callers must ignore any candidates that fail to parse.
		internal static IEnumerable<string> ExtractCorpus (params string[] keywords)
		{
			var literal = new Regex (@"\{\d+\+?\}", RegexOptions.Compiled);
			var root = Path.Combine (TestHelper.ProjectDir, "Net", "Imap", "Resources");

			foreach (var file in Directory.EnumerateFiles (root, "*", SearchOption.AllDirectories)) {
				foreach (var line in File.ReadLines (file)) {
					if (literal.IsMatch (line))
						continue;

					foreach (var keyword in keywords) {
						foreach (var list in ExtractLists (line, keyword))
							yield return list;
					}
				}
			}

			var source = File.ReadAllText (Path.Combine (TestHelper.ProjectDir, "Net", "Imap", "ImapUtilsTests.cs"));

			foreach (Match match in Regex.Matches (source, "const string \\w+ = \"(\\((?:[^\"\\\\]|\\\\.)*)\";")) {
				var text = match.Groups[1].Value.Replace ("\\r\\n", "").Replace ("\\\"", "\"").Replace ("\\\\", "\\");

				if (!literal.IsMatch (text))
					yield return text;
			}
		}

		static void AssertRoundTrip (Envelope original)
		{
			var text = original.ToString ();

			Assert.That (Envelope.TryParse (text, out var parsed), Is.True, $"TryParse failed: {text}");
			Assert.That (Dump (parsed), Is.EqualTo (Dump (original)), $"Parsed envelope does not match: {text}");
			Assert.That (parsed.ToString (), Is.EqualTo (text), "Re-serialized envelope does not match.");
		}

		[Test]
		public void TestImapCorpusRoundTrip ()
		{
			var failures = new List<string> ();
			int count = 0;

			foreach (var text in ExtractCorpus ("ENVELOPE")) {
				Envelope envelope;

				try {
					using var engine = CreateEngine (text);

					envelope = ImapUtils.ParseEnvelope (engine, CancellationToken.None);

					if (engine.ReadToken (CancellationToken.None).Type != ImapTokenType.Eoln)
						continue;
				} catch {
					continue;
				}

				var serialized = envelope.ToString ();
				count++;

				if (!Envelope.TryParse (serialized, out var parsed))
					failures.Add ($"TryParse failed: {serialized}");
				else if (Dump (parsed) != Dump (envelope))
					failures.Add ($"Parsed envelope does not match: {serialized}");
				else if (parsed.ToString () != serialized)
					failures.Add ($"Re-serialized envelope does not match: {serialized}");
			}

			Assert.That (count, Is.GreaterThan (50), "Expected a reasonably large corpus of envelopes.");
			Assert.That (failures, Is.Empty, string.Join ("\n", failures));
		}

		static IEnumerable<TestCaseData> EnvelopeCases ()
		{
			static TestCaseData Case (string name, Action<Envelope> initialize) => new TestCaseData (initialize).SetName ("TestEnvelopeRoundTrip(" + name + ")");

			yield return Case ("UserAtLocalhost", e => e.From.Add (new MailboxAddress ("Root", "root@localhost")));
			yield return Case ("NoDomain", e => e.From.Add (new MailboxAddress ("Fejj", "fejj")));
			yield return Case ("EmptyAddress", e => e.From.Add (new MailboxAddress (null, string.Empty)));
			yield return Case ("EmptyName", e => e.From.Add (new MailboxAddress (string.Empty, "user@example.com")));
			yield return Case ("QuotedLocalPart", e => e.From.Add (new MailboxAddress ("X", "\"a@b\"@example.com")));
			yield return Case ("InternationalDomain", e => e.From.Add (new MailboxAddress ("X", "user@例え.jp")));
			yield return Case ("Route", e => e.From.Add (new MailboxAddress ("X", new[] { "a.com", "b.com" }, "user@c.com")));
			yield return Case ("EmptyGroup", e => e.To.Add (new GroupAddress ("Undisclosed recipients")));
			yield return Case ("GroupWithEmptyName", e => e.To.Add (new GroupAddress (string.Empty, new[] { new MailboxAddress ("A", "a@example.com") })));
			yield return Case ("NestedGroups", e => e.To.Add (new GroupAddress ("Outer", new InternetAddress[] {
				new MailboxAddress ("A", "a@example.com"),
				new GroupAddress ("Inner", new[] { new MailboxAddress ("B", "b@example.com") }),
				new MailboxAddress ("C", "c@example.com")
			})));
			yield return Case ("SubjectSpecials", e => e.Subject = "a \"quoted\" \\back\\ \r\n tab\t NIL () ü 😀");
			yield return Case ("SubjectNIL", e => e.Subject = "NIL");
			yield return Case ("SubjectEmpty", e => e.Subject = string.Empty);
			yield return Case ("DateMilliseconds", e => e.Date = new DateTimeOffset (2026, 10, 11, 21, 34, 2, 955, TimeSpan.FromHours (-4)));
			yield return Case ("DateYear1", e => e.Date = new DateTimeOffset (1, 1, 1, 0, 0, 0, TimeSpan.Zero));
			yield return Case ("DateYear999", e => e.Date = new DateTimeOffset (999, 6, 15, 12, 0, 0, TimeSpan.Zero));
			yield return Case ("DateYear9999", e => e.Date = new DateTimeOffset (9999, 12, 31, 23, 59, 59, TimeSpan.FromHours (14)));
			yield return Case ("DateHalfHourOffset", e => e.Date = new DateTimeOffset (2026, 1, 2, 3, 4, 5, new TimeSpan (5, 30, 0)));

			var ids = new (string Name, string Value)[] {
				("Simple", "id@example.com"),
				("Bracketed", "<id@example.com>"),
				("NoDomain", "id"),
				("SingleChar", "a"),
				("Space", "abc def"),
				("QuotedLocalPart", "\"a b\"@example.com"),
				("TrailingBracket", "abc>"),
				("LeadingBracket", "<abc"),
				("EmptyBrackets", "<>"),
				("MultipleAts", "a@b@c"),
				("MultipleIds", "<a@b> <c@d>"),
				("DomainLiteral", "id@[1.2.3.4]"),
				("Empty", string.Empty)
			};

			foreach (var id in ids) {
				yield return Case ("MessageId" + id.Name, e => e.MessageId = id.Value);
				yield return Case ("InReplyTo" + id.Name, e => e.InReplyTo = id.Value);
			}
		}

		[TestCaseSource (nameof (EnvelopeCases))]
		public void TestEnvelopeRoundTrip (Action<Envelope> initialize)
		{
			var envelope = new Envelope ();

			initialize (envelope);

			AssertRoundTrip (envelope);
		}

		[Test]
		public void TestMailboxWithoutDomainSerializesEmptyDomain ()
		{
			var envelope = new Envelope ();
			envelope.From.Add (new MailboxAddress ("Fejj", "fejj"));

			Assert.That (envelope.ToString (), Is.EqualTo ("(NIL NIL ((\"Fejj\" NIL \"fejj\" \"\")) NIL NIL NIL NIL NIL NIL NIL)"));
		}

		[Test]
		public void TestLocalhostDomainIsNotSpecial ()
		{
			// Note: Older versions of MailKit serialized domain-less mailboxes using "localhost" as the domain.
			const string text = "(NIL NIL ((\"Root\" NIL \"root\" \"localhost\")) NIL NIL NIL NIL NIL NIL NIL)";

			Assert.That (Envelope.TryParse (text, out var envelope), Is.True);
			Assert.That (envelope.From.Mailboxes.Single ().Address, Is.EqualTo ("root@localhost"));
		}

		[Test]
		public void TestDateWithMillisecondsUsesIsoFormat ()
		{
			var envelope = new Envelope { Date = new DateTimeOffset (2026, 10, 11, 21, 34, 2, 955, TimeSpan.FromHours (-4)) };

			Assert.That (envelope.ToString (), Does.StartWith ("(\"2026-10-11T21:34:02.9550000-04:00\" "));

			envelope.Date = new DateTimeOffset (2026, 10, 11, 21, 34, 2, TimeSpan.FromHours (-4));

			Assert.That (envelope.ToString (), Does.StartWith ("(\"Sun, 11 Oct 2026 21:34:02 -0400\" "));
		}

		[Test]
		public void TestParseUnbracketedMessageIds ()
		{
			const string text = "(NIL NIL NIL NIL NIL NIL NIL NIL \"in-reply-to@example.com\" \"message-id@example.com\")";

			Assert.That (Envelope.TryParse (text, out var envelope), Is.True);
			Assert.That (envelope.InReplyTo, Is.EqualTo ("in-reply-to@example.com"));
			Assert.That (envelope.MessageId, Is.EqualTo ("message-id@example.com"));
		}

		[TestCase ("")]
		[TestCase ("(")]
		[TestCase ("(NIL")]
		[TestCase ("(NIL NIL ((")]
		[TestCase ("(NIL NIL ((NIL NIL NIL NIL)")]
		[TestCase ("(NIL NIL (()) NIL NIL NIL NIL NIL NIL NIL)")]
		[TestCase ("(\"garbage date\" NIL NIL NIL NIL NIL NIL NIL NIL NIL)")]
		[TestCase ("(NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL")]
		[TestCase ("(NIL NIL NIL NIL NIL NIL NIL NIL NIL NIL) trailing")]
		public void TestTryParseInvalidInput (string text)
		{
			Assert.That (Envelope.TryParse (text, out _), Is.False);
		}

		[Test]
		public void TestTryParseTruncatedInput ()
		{
			var envelope = new Envelope {
				Date = new DateTimeOffset (2026, 10, 11, 21, 34, 2, TimeSpan.Zero),
				Subject = "Subject \"with\" \\specials\\",
				InReplyTo = "a@example.com",
				MessageId = "b@example.com"
			};
			envelope.From.Add (new MailboxAddress ("From", "from@example.com"));
			envelope.To.Add (new GroupAddress ("Group", new[] { new MailboxAddress ("To", "to@example.com") }));

			var text = envelope.ToString ();

			for (int i = 0; i < text.Length; i++)
				Assert.That (Envelope.TryParse (text.Substring (0, i), out _), Is.False, $"Truncated at {i}: {text.Substring (0, i)}");
		}
	}
}
