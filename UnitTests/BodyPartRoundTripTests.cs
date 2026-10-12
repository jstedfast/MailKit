//
// BodyPartRoundTripTests.cs
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

using MimeKit;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests {
	[TestFixture]
	public class BodyPartRoundTripTests
	{
		static string Quote (string? value) => value == null ? "<null>" : "[" + value + "]";

		static void Dump (StringBuilder builder, ParameterList parameters)
		{
			foreach (var parameter in parameters)
				builder.Append (' ').Append (Quote (parameter.Name)).Append ('=').Append (Quote (parameter.Value));
		}

		static void Dump (StringBuilder builder, ContentDisposition? disposition, string[]? languages, Uri? location)
		{
			builder.Append (" disposition=");

			if (disposition != null) {
				builder.Append (Quote (disposition.Disposition));
				Dump (builder, disposition.Parameters);
			} else {
				builder.Append ("<null>");
			}

			builder.Append (" language=").Append (languages == null ? "<null>" : "{" + string.Join ("|", languages) + "}");
			builder.Append (" location=");

			if (location != null)
				builder.Append (Quote (location.OriginalString)).Append (location.IsAbsoluteUri ? " (absolute)" : " (relative)");
			else
				builder.Append ("<null>");

			builder.Append ('\n');
		}

		static void Dump (StringBuilder builder, BodyPart? part, string indent)
		{
			if (part == null) {
				builder.Append (indent).Append ("<null part>\n");
				return;
			}

			builder.Append (indent).Append (part.GetType ().Name).Append (" specifier=").Append (Quote (part.PartSpecifier));
			builder.Append (" type=").Append (Quote (part.ContentType.MediaType)).Append ('/').Append (Quote (part.ContentType.MediaSubtype));
			Dump (builder, part.ContentType.Parameters);
			builder.Append ('\n');

			if (part is BodyPartMultipart multipart) {
				builder.Append (indent);
				Dump (builder, multipart.ContentDisposition, multipart.ContentLanguage, multipart.ContentLocation);

				foreach (var child in multipart.BodyParts)
					Dump (builder, child, indent + "  ");
			} else if (part is BodyPartBasic basic) {
				builder.Append (indent).Append (" id=").Append (Quote (basic.ContentId));
				builder.Append (" description=").Append (Quote (basic.ContentDescription));
				builder.Append (" encoding=").Append (Quote (basic.ContentTransferEncoding));
				builder.Append (" octets=").Append (basic.Octets);
				builder.Append (" md5=").Append (Quote (basic.ContentMd5));
				Dump (builder, basic.ContentDisposition, basic.ContentLanguage, basic.ContentLocation);

				if (part is BodyPartText text) {
					builder.Append (indent).Append (" lines=").Append (text.Lines).Append ('\n');
				} else if (part is BodyPartMessage message) {
					builder.Append (indent).Append (" lines=").Append (message.Lines).Append ('\n');
					builder.Append (EnvelopeRoundTripTests.Dump (message.Envelope));
					Dump (builder, message.Body, indent + "  ");
				}
			}
		}

		// Produces a canonical dump of every property so that two body parts can be compared for equality.
		static string Dump (BodyPart? part)
		{
			var builder = new StringBuilder ();

			Dump (builder, part, string.Empty);

			return builder.ToString ();
		}

		static BodyPartBasic CreateBasic (string mediaType, string mediaSubtype)
		{
			return new BodyPartBasic (new ContentType (mediaType, mediaSubtype), string.Empty) {
				ContentTransferEncoding = "base64",
				Octets = 10
			};
		}

		static void AssertRoundTrip (BodyPart original)
		{
			var text = original.ToString ();

			Assert.That (BodyPart.TryParse (text, out var parsed), Is.True, $"TryParse failed: {text}");
			Assert.That (Dump (parsed), Is.EqualTo (Dump (original)), $"Parsed body does not match: {text}");
			Assert.That (parsed.ToString (), Is.EqualTo (text), "Re-serialized body does not match.");

			// Make sure that TryParse never throws on truncated input.
			for (int i = 0; i < text.Length; i++)
				Assert.That (BodyPart.TryParse (text.Substring (0, i), out _), Is.False, $"Truncated at {i}: {text.Substring (0, i)}");
		}

		[Test]
		public void TestImapCorpusRoundTrip ()
		{
			var failures = new List<string> ();
			int count = 0;

			foreach (var text in EnvelopeRoundTripTests.ExtractCorpus ("BODYSTRUCTURE", "BODY")) {
				BodyPart? body;

				try {
					using var engine = EnvelopeRoundTripTests.CreateEngine (text);

					body = ImapUtils.ParseBody (engine, "{0}", string.Empty, CancellationToken.None);

					if (engine.ReadToken (CancellationToken.None).Type != ImapTokenType.Eoln)
						continue;
				} catch {
					continue;
				}

				if (body == null)
					continue;

				var serialized = body.ToString ();
				count++;

				if (!BodyPart.TryParse (serialized, out var parsed))
					failures.Add ($"TryParse failed: {serialized}");
				else if (Dump (parsed) != Dump (body))
					failures.Add ($"Parsed body does not match: {serialized}");
				else if (parsed.ToString () != serialized)
					failures.Add ($"Re-serialized body does not match: {serialized}");
			}

			Assert.That (count, Is.GreaterThan (50), "Expected a reasonably large corpus of body structures.");
			Assert.That (failures, Is.Empty, string.Join ("\n", failures));
		}

		static IEnumerable<TestCaseData> BodyPartCases ()
		{
			static TestCaseData Case (string name, Func<BodyPart> create) => new TestCaseData (create).SetName ("TestBodyPartRoundTrip(" + name + ")");

			yield return Case ("AbsoluteLocationWithEscapes", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLocation = new Uri ("http://example.com/a%20b.png", UriKind.Absolute);
				return part;
			});
			yield return Case ("AbsoluteLocationWithUnicode", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLocation = new Uri ("http://example.com/ü.png", UriKind.Absolute);
				return part;
			});
			yield return Case ("RelativeLocationWithEscapes", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLocation = new Uri ("a%20b.png", UriKind.Relative);
				return part;
			});
			yield return Case ("RelativeLocation", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLocation = new Uri ("images/a.png", UriKind.Relative);
				return part;
			});
			yield return Case ("ContentIdLocation", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLocation = new Uri ("cid:foo@bar", UriKind.Absolute);
				return part;
			});
			yield return Case ("IllFormedLocation", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLocation = new Uri ("http://example.com/a b.png");
				return part;
			});
			yield return Case ("EmptyLanguage", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLanguage = Array.Empty<string> ();
				return part;
			});
			yield return Case ("MultipleLanguages", () => {
				var part = CreateBasic ("image", "png");
				part.ContentLanguage = new[] { "en", "fr" };
				return part;
			});
			yield return Case ("BasicWithTextType", () => CreateBasic ("text", "plain"));
			yield return Case ("BasicWithTextTypeAndExtensions", () => {
				var part = CreateBasic ("text", "plain");
				part.ContentMd5 = "Q2hlY2s=";
				part.ContentLanguage = new[] { "en" };
				return part;
			});
			yield return Case ("BasicWithMessageType", () => CreateBasic ("message", "rfc822"));
			yield return Case ("BasicWithMultipartType", () => CreateBasic ("multipart", "mixed"));
			yield return Case ("TextWithMaxValues", () => new BodyPartText (new ContentType ("text", "plain"), string.Empty) { Octets = uint.MaxValue, Lines = long.MaxValue });
			yield return Case ("TextWithNonTextType", () => new BodyPartText (new ContentType ("application", "x-foo"), string.Empty) { Lines = 5 });
			yield return Case ("EmptyMessage", () => new BodyPartMessage (new ContentType ("message", "rfc822"), string.Empty));
			yield return Case ("GlobalMessage", () => new BodyPartMessage (new ContentType ("message", "global"), string.Empty) {
				Envelope = new Envelope { Subject = "global" },
				Body = new BodyPartText (new ContentType ("text", "plain"), string.Empty) { Lines = 1 },
				Lines = 7
			});
			yield return Case ("EmptyMultipart", () => new BodyPartMultipart (new ContentType ("multipart", "mixed"), string.Empty));
			yield return Case ("Parameters", () => {
				var part = CreateBasic ("application", "octet-stream");
				part.ContentType.Parameters.Add ("name", "a \"b\" \\c ü.txt");
				part.ContentType.Parameters.Add ("x-empty", string.Empty);
				return part;
			});
			yield return Case ("Disposition", () => {
				var part = CreateBasic ("application", "pdf");
				part.ContentDisposition = new ContentDisposition ("attachment") { FileName = "ü \"x\".pdf" };
				return part;
			});
			yield return Case ("UnusualDisposition", () => {
				var part = CreateBasic ("application", "pdf");
				ContentDisposition.TryParse ("x-weird.thing; a=b", out var disposition);
				part.ContentDisposition = disposition;
				return part;
			});
			yield return Case ("Strings", () => {
				var part = CreateBasic ("application", "pdf");
				part.ContentId = "<id@example.com>";
				part.ContentDescription = "NIL \"x\" \\ ()";
				part.ContentMd5 = "Q2hlY2s=";
				part.ContentTransferEncoding = null;
				return part;
			});
			yield return Case ("Nested", () => {
				var alternative = new BodyPartMultipart (new ContentType ("multipart", "alternative"), "2");
				alternative.BodyParts.Add (new BodyPartText (new ContentType ("text", "plain"), "2.1") { Lines = 3 });
				alternative.BodyParts.Add (new BodyPartText (new ContentType ("text", "html"), "2.2") { Lines = 4 });

				var message = new BodyPartMessage (new ContentType ("message", "rfc822"), "2") {
					Envelope = new Envelope { Subject = "inner" },
					Body = alternative,
					Lines = 9
				};

				var mixed = new BodyPartMultipart (new ContentType ("multipart", "mixed"), string.Empty);
				mixed.BodyParts.Add (new BodyPartText (new ContentType ("text", "plain"), "1") { Lines = 1 });
				mixed.BodyParts.Add (message);

				return mixed;
			});
		}

		[TestCaseSource (nameof (BodyPartCases))]
		public void TestBodyPartRoundTrip (Func<BodyPart> create)
		{
			AssertRoundTrip (create ());
		}

		[Test]
		public void TestBodyPartTypeIsPreserved ()
		{
			Assert.That (BodyPart.TryParse (CreateBasic ("text", "plain").ToString (), out var part), Is.True);
			Assert.That (part, Is.TypeOf<BodyPartBasic> ());

			Assert.That (BodyPart.TryParse (CreateBasic ("message", "rfc822").ToString (), out part), Is.True);
			Assert.That (part, Is.TypeOf<BodyPartBasic> ());

			Assert.That (BodyPart.TryParse (new BodyPartText (new ContentType ("application", "x-foo"), string.Empty).ToString (), out part), Is.True);
			Assert.That (part, Is.TypeOf<BodyPartText> ());

			Assert.That (BodyPart.TryParse (new BodyPartMessage (new ContentType ("message", "global"), string.Empty).ToString (), out part), Is.True);
			Assert.That (part, Is.TypeOf<BodyPartMessage> ());
		}

		[TestCase ("")]
		[TestCase ("(")]
		[TestCase ("()")]
		[TestCase ("(NIL")]
		[TestCase ("(\"text\"")]
		[TestCase ("(\"text\" \"plain\" (\"a\" NIL) NIL NIL NIL 0 NIL NIL NIL NIL 0)")]
		[TestCase ("(\"text\" \"plain\" NIL NIL NIL NIL 99999999999 NIL NIL NIL NIL 0)")]
		[TestCase ("(\"text\" \"plain\" NIL NIL NIL NIL ٣ NIL NIL NIL NIL 0)")]
		[TestCase ("(\"text\" \"plain\" NIL NIL NIL NIL 0 NIL NIL NIL NIL 99999999999999999999)")]
		[TestCase ("(\"text\" \"plain\" (\"a b\" \"c\") NIL NIL NIL 0 NIL NIL NIL NIL 0)")]
		[TestCase ("(\"text\" \"plain\" (\"a\" \"b\" \"a\" \"c\") NIL NIL NIL 0 NIL NIL NIL NIL 0)")]
		[TestCase ("(\"x\" \"y\" NIL NIL NIL NIL 0 NIL (\"a b\" NIL) NIL NIL)")]
		[TestCase ("(\"x\" \"y\" NIL NIL NIL NIL 0 NIL (\"\" NIL) NIL NIL)")]
		[TestCase ("(\"x\" \"y\" NIL NIL NIL NIL 0 NIL (\"attachment\" (\"a\" \"b\" \"a\" \"c\")) NIL NIL)")]
		public void TestTryParseInvalidInput (string text)
		{
			Assert.That (BodyPart.TryParse (text, out _), Is.False);
		}
	}
}
