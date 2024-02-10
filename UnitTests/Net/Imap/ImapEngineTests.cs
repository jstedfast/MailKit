//
// ImapEngineTests.cs
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
using System.Globalization;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapEngineTests
	{
		[TestCase ('*', (int) ImapTokenType.Asterisk, (int) ImapTokenType.Atom)]
		[TestCase ("ATOM", (int) ImapTokenType.Atom, (int) ImapTokenType.Asterisk)]
		[TestCase ("\\Flagged", (int) ImapTokenType.Flag, (int) ImapTokenType.QString)]
		[TestCase ("QSTRING", (int) ImapTokenType.QString, (int) ImapTokenType.Atom)]
		[TestCase (123456, (int) ImapTokenType.Literal, (int) ImapTokenType.Atom)]
		[TestCase ("NIL", (int) ImapTokenType.Nil, (int) ImapTokenType.QString)]

		public void TestAssertToken (object value, int actual, int expected)
		{
			using (var builder = new ByteArrayBuilder (64)) {
				ImapToken token;

				if (value is string str) {
					foreach (var c in str)
						builder.Append ((byte) c);

					token = ImapToken.Create ((ImapTokenType) actual, builder);
				} else if (value is char c) {
					token = ImapToken.Create ((ImapTokenType) actual, c);
				} else if (value is int literal) {
					token = ImapToken.Create ((ImapTokenType) actual, literal);
				} else {
					return;
				}

				Assert.Throws<ImapProtocolException> (() => ImapEngine.AssertToken (token, (ImapTokenType) expected, "Unexpected token: {0}", token));
			}
		}

		[Test]
		public void TestParseNumber ()
		{
			using (var builder = new ByteArrayBuilder (64)) {
				ImapToken token;
				uint value;

				builder.Append ((byte) '0');

				token = ImapToken.Create (ImapTokenType.Atom, builder);
				value = ImapEngine.ParseNumber (token, false, "Unexpected number: {0}", token);
				Assert.That (value, Is.EqualTo (0), "number");

				Assert.Throws<ImapProtocolException> (() => ImapEngine.ParseNumber (token, true, "Unexpected number: {0}", token), "nz-number");

				builder.Clear ();
				var max = uint.MaxValue.ToString (CultureInfo.InvariantCulture);
				for (int i = 0; i < max.Length; i++)
					builder.Append ((byte) max[i]);

				token = ImapToken.Create (ImapTokenType.Atom, builder);
				value = ImapEngine.ParseNumber (token, false, "Unexpected number: {0}", token);
				Assert.That (value, Is.EqualTo (uint.MaxValue), "max number");
			}
		}

		[Test]
		public void TestParseNumber64 ()
		{
			using (var builder = new ByteArrayBuilder (64)) {
				ImapToken token;
				ulong value;

				builder.Append ((byte) '0');

				token = ImapToken.Create (ImapTokenType.Atom, builder);
				value = ImapEngine.ParseNumber64 (token, false, "Unexpected number: {0}", token);
				Assert.That (value, Is.EqualTo (0), "number64");

				Assert.Throws<ImapProtocolException> (() => ImapEngine.ParseNumber64 (token, true, "Unexpected number: {0}", token), "nz-number64");

				builder.Clear ();
				var max = ulong.MaxValue.ToString (CultureInfo.InvariantCulture);
				for (int i = 0; i < max.Length; i++)
					builder.Append ((byte) max[i]);

				token = ImapToken.Create (ImapTokenType.Atom, builder);
				value = ImapEngine.ParseNumber64 (token, false, "Unexpected number: {0}", token);
				Assert.That (value, Is.EqualTo (ulong.MaxValue), "max number64");
			}
		}

		[Test]
		public void TestParseUidSet ()
		{
			using (var builder = new ByteArrayBuilder (64)) {
				UniqueId? min, max;
				UniqueIdSet uids;
				ImapToken token;

				builder.Append ((byte) '0');

				token = ImapToken.Create (ImapTokenType.Atom, builder);
				Assert.Throws<ImapProtocolException> (() => ImapEngine.ParseUidSet (token, 0, out min, out max, "Unexpected uid-set: {0}", token), "0");

				builder.Clear ();
				var bytes = Encoding.ASCII.GetBytes ("1:500");
				for (int i = 0; i < bytes.Length; i++)
					builder.Append (bytes[i]);

				token = ImapToken.Create (ImapTokenType.Atom, builder);
				uids = ImapEngine.ParseUidSet (token, 0, out min, out max, "Unexpected uid-set: {0}", token);
				Assert.That (uids.ToString (), Is.EqualTo ("1:500"), "uid-set");
				Assert.That (min.ToString (), Is.EqualTo ("1"), "min");
				Assert.That (max.ToString (), Is.EqualTo ("500"), "max");
			}
		}

		[Test]
		public void TestGetResponseCodeType ()
		{
			foreach (ImapResponseCodeType type in Enum.GetValues (typeof (ImapResponseCodeType))) {
				string atom;

				switch (type) {
				case ImapResponseCodeType.ReadOnly: atom = "READ-ONLY"; break;
				case ImapResponseCodeType.ReadWrite: atom = "READ-WRITE"; break;
				case ImapResponseCodeType.UnknownCte: atom = "UNKNOWN-CTE"; break;
				case ImapResponseCodeType.UndefinedFilter: atom = "UNDEFINED-FILTER"; break;
				default: atom = type.ToString ().ToUpperInvariant (); break;
				}

				var result = ImapEngine.GetResponseCodeType (atom);
				Assert.That (result, Is.EqualTo (type));
			}
		}

		[Test]
		public void TestParseResponseCodeBadCharset ()
		{
			const string text = "BADCHARSET (US-ASCII \"iso-8859-1\" UTF-8)] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.BadCharset));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						Assert.That (engine.SupportedCharsets, Has.Count.EqualTo (3));
						Assert.That (engine.SupportedCharsets, Does.Contain ("US-ASCII"), "US-ASCII");
						Assert.That (engine.SupportedCharsets, Does.Contain ("iso-8859-1"), "iso-8859-1");
						Assert.That (engine.SupportedCharsets, Does.Contain ("UTF-8"), "UTF-8");
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeBadCharsetAsync ()
		{
			const string text = "BADCHARSET (US-ASCII \"iso-8859-1\" UTF-8)] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.BadCharset));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						Assert.That (engine.SupportedCharsets, Has.Count.EqualTo (3));
						Assert.That (engine.SupportedCharsets, Does.Contain ("US-ASCII"), "US-ASCII");
						Assert.That (engine.SupportedCharsets, Does.Contain ("iso-8859-1"), "iso-8859-1");
						Assert.That (engine.SupportedCharsets, Does.Contain ("UTF-8"), "UTF-8");
					}
				}
			}
		}

		[Test]
		public void TestParseResponseCodeBadUrl ()
		{
			const string text = "BADURL \"/INBOX;UIDVALIDITY=785799047/;UID=113330;section=1.5.9\"] CATENATE append has failed, one message expunged\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.BadUrl));
						Assert.That (respCode.Message, Is.EqualTo ("CATENATE append has failed, one message expunged"));

						var badurl = (BadUrlResponseCode) respCode;
						Assert.That (badurl.BadUrl, Is.EqualTo ("/INBOX;UIDVALIDITY=785799047/;UID=113330;section=1.5.9"));
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeBadUrlAsync ()
		{
			const string text = "BADURL \"/INBOX;UIDVALIDITY=785799047/;UID=113330;section=1.5.9\"] CATENATE append has failed, one message expunged\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.BadUrl));
						Assert.That (respCode.Message, Is.EqualTo ("CATENATE append has failed, one message expunged"));

						var badurl = (BadUrlResponseCode) respCode;
						Assert.That (badurl.BadUrl, Is.EqualTo ("/INBOX;UIDVALIDITY=785799047/;UID=113330;section=1.5.9"));
					}
				}
			}
		}

		[Test]
		public void TestParseResponseCodeMaxConvertMessages ()
		{
			const string text = "MAXCONVERTMESSAGES 1] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.MaxConvertMessages));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var maxconvert = (MaxConvertResponseCode) respCode;
						Assert.That (maxconvert.MaxConvert, Is.EqualTo (1));
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeMaxConvertMessagesAsync ()
		{
			const string text = "MAXCONVERTMESSAGES 1] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.MaxConvertMessages));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var maxconvert = (MaxConvertResponseCode) respCode;
						Assert.That (maxconvert.MaxConvert, Is.EqualTo (1));
					}
				}
			}
		}

		[Test]
		public void TestParseResponseCodeMaxConvertParts ()
		{
			const string text = "MAXCONVERTPARTS 1] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.MaxConvertParts));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var maxconvert = (MaxConvertResponseCode) respCode;
						Assert.That (maxconvert.MaxConvert, Is.EqualTo (1));
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeMaxConvertPartsAsync ()
		{
			const string text = "MAXCONVERTPARTS 1] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.MaxConvertParts));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var maxconvert = (MaxConvertResponseCode) respCode;
						Assert.That (maxconvert.MaxConvert, Is.EqualTo (1));
					}
				}
			}
		}

		[Test]
		public void TestParseResponseCodeNoUpdate ()
		{
			const string text = "NOUPDATE \"B02\"] Too many contexts\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (false, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.NoUpdate));
						Assert.That (respCode.Message, Is.EqualTo ("Too many contexts"));

						var noupdate = (NoUpdateResponseCode) respCode;
						Assert.That (noupdate.Tag, Is.EqualTo ("B02"));
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeNoUpdateAsync ()
		{
			const string text = "NOUPDATE \"B02\"] Too many contexts\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (false, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.NoUpdate));
						Assert.That (respCode.Message, Is.EqualTo ("Too many contexts"));

						var noupdate = (NoUpdateResponseCode) respCode;
						Assert.That (noupdate.Tag, Is.EqualTo ("B02"));
					}
				}
			}
		}

		[Test]
		public void TestParseResponseCodeNewName ()
		{
			const string text = "NEWNAME OldName NewName] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.NewName));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var newname = (NewNameResponseCode) respCode;
						Assert.That (newname.OldName, Is.EqualTo ("OldName"));
						Assert.That (newname.NewName, Is.EqualTo ("NewName"));
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeNewNameAsync ()
		{
			const string text = "NEWNAME OldName NewName] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.NewName));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var newname = (NewNameResponseCode) respCode;
						Assert.That (newname.OldName, Is.EqualTo ("OldName"));
						Assert.That (newname.NewName, Is.EqualTo ("NewName"));
					}
				}
			}
		}

		[Test]
		public void TestParseResponseCodeUndefinedFilter ()
		{
			const string text = "UNDEFINED-FILTER filter-name] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = engine.ParseResponseCode (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.UndefinedFilter));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var undefined = (UndefinedFilterResponseCode) respCode;
						Assert.That (undefined.Name, Is.EqualTo ("filter-name"));
					}
				}
			}
		}

		[Test]
		public async Task TestParseResponseCodeUndefinedFilterAsync ()
		{
			const string text = "UNDEFINED-FILTER filter-name] This is some free-form text\r\n";

			using (var memory = new MemoryStream (Encoding.ASCII.GetBytes (text), false)) {
				using (var tokenizer = new ImapStream (memory, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						ImapResponseCode respCode;

						engine.SetStream (tokenizer);

						try {
							respCode = await engine.ParseResponseCodeAsync (true, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing RESP-CODE failed: {ex}");
							return;
						}

						Assert.That (respCode.Type, Is.EqualTo (ImapResponseCodeType.UndefinedFilter));
						Assert.That (respCode.Message, Is.EqualTo ("This is some free-form text"));

						var undefined = (UndefinedFilterResponseCode) respCode;
						Assert.That (undefined.Name, Is.EqualTo ("filter-name"));
					}
				}
			}
		}

		void TestGreetingDetection (string server, string fileName, ImapQuirksMode expected)
		{
			using (var input = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + server + "." + fileName)) {
				using (var tokenizer = new ImapStream (input, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						try {
							engine.Connect (tokenizer, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing greeting failed: {ex}");
							return;
						}

						Assert.That (engine.QuirksMode, Is.EqualTo (expected));
					}
				}
			}
		}

		async Task TestGreetingDetectionAsync (string server, string fileName, ImapQuirksMode expected)
		{
			using (var input = GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + server + "." + fileName)) {
				using (var tokenizer = new ImapStream (input, new NullProtocolLogger ())) {
					using (var engine = new ImapEngine (null)) {
						try {
							await engine.ConnectAsync (tokenizer, CancellationToken.None);
						} catch (Exception ex) {
							Assert.Fail ($"Parsing greeting failed: {ex}");
							return;
						}

						Assert.That (engine.QuirksMode, Is.EqualTo (expected));
					}
				}
			}
		}

		[Test]
		public void TestCourierImapDetection ()
		{
			TestGreetingDetection ("courier", "greeting.txt", ImapQuirksMode.Courier);
		}

		[Test]
		public Task TestCourierImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("courier", "greeting.txt", ImapQuirksMode.Courier);
		}

		[Test]
		public void TestCyrusImapDetection ()
		{
			TestGreetingDetection ("cyrus", "greeting.txt", ImapQuirksMode.Cyrus);
		}

		[Test]
		public Task TestCyrusImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("cyrus", "greeting.txt", ImapQuirksMode.Cyrus);
		}

		[Test]
		public void TestDominoImapDetection ()
		{
			TestGreetingDetection ("domino", "greeting.txt", ImapQuirksMode.Domino);
		}

		[Test]
		public Task TestDominoImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("domino", "greeting.txt", ImapQuirksMode.Domino);
		}

		[Test]
		public void TestDovecotImapDetection ()
		{
			TestGreetingDetection ("dovecot", "greeting.txt", ImapQuirksMode.Dovecot);
		}

		[Test]
		public Task TestDovecotImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("dovecot", "greeting.txt", ImapQuirksMode.Dovecot);
		}

		[Test]
		public void TestExchangeImapDetection ()
		{
			TestGreetingDetection ("exchange", "greeting.txt", ImapQuirksMode.Exchange);
		}

		[Test]
		public Task TestExchangeImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("exchange", "greeting.txt", ImapQuirksMode.Exchange);
		}

		[Test]
		public void TestExchange2003ImapDetection ()
		{
			TestGreetingDetection ("exchange", "greeting-2003.txt", ImapQuirksMode.Exchange2003);
		}

		[Test]
		public Task TestExchange2003ImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("exchange", "greeting-2003.txt", ImapQuirksMode.Exchange2003);
		}

		[Test]
		public void TestExchange2007ImapDetection ()
		{
			TestGreetingDetection ("exchange", "greeting-2007.txt", ImapQuirksMode.Exchange2007);
		}

		[Test]
		public Task TestExchange2007ImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("exchange", "greeting-2007.txt", ImapQuirksMode.Exchange2007);
		}

		[Test]
		public void TestUWImapDetection ()
		{
			TestGreetingDetection ("uw", "greeting.txt", ImapQuirksMode.UW);
		}

		[Test]
		public Task TestUWImapDetectionAsync ()
		{
			return TestGreetingDetectionAsync ("uw", "greeting.txt", ImapQuirksMode.UW);
		}
	}
}
