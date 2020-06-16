//
// ImapEngineTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Threading.Tasks;

using NUnit.Framework;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapEngineTests
	{
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
				Assert.AreEqual (type, result);
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
							respCode = engine.ParseResponseCodeAsync (true, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.BadCharset, respCode.Type);
						Assert.AreEqual ("This is some free-form text", respCode.Message);

						Assert.AreEqual (3, engine.SupportedCharsets.Count);
						Assert.IsTrue (engine.SupportedCharsets.Contains ("US-ASCII"), "US-ASCII");
						Assert.IsTrue (engine.SupportedCharsets.Contains ("iso-8859-1"), "iso-8859-1");
						Assert.IsTrue (engine.SupportedCharsets.Contains ("UTF-8"), "UTF-8");
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
							respCode = engine.ParseResponseCodeAsync (true, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.BadUrl, respCode.Type);
						Assert.AreEqual ("CATENATE append has failed, one message expunged", respCode.Message);

						var badurl = (BadUrlResponseCode) respCode;
						Assert.AreEqual ("/INBOX;UIDVALIDITY=785799047/;UID=113330;section=1.5.9", badurl.BadUrl);
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
							respCode = engine.ParseResponseCodeAsync (true, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.MaxConvertMessages, respCode.Type);
						Assert.AreEqual ("This is some free-form text", respCode.Message);

						var maxconvert = (MaxConvertResponseCode) respCode;
						Assert.AreEqual (1, maxconvert.MaxConvert);
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
							respCode = engine.ParseResponseCodeAsync (true, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.MaxConvertParts, respCode.Type);
						Assert.AreEqual ("This is some free-form text", respCode.Message);

						var maxconvert = (MaxConvertResponseCode) respCode;
						Assert.AreEqual (1, maxconvert.MaxConvert);
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
							respCode = engine.ParseResponseCodeAsync (false, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.NoUpdate, respCode.Type);
						Assert.AreEqual ("Too many contexts", respCode.Message);

						var noupdate = (NoUpdateResponseCode) respCode;
						Assert.AreEqual ("B02", noupdate.Tag);
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
							respCode = engine.ParseResponseCodeAsync (true, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.NewName, respCode.Type);
						Assert.AreEqual ("This is some free-form text", respCode.Message);

						var newname = (NewNameResponseCode) respCode;
						Assert.AreEqual ("OldName", newname.OldName);
						Assert.AreEqual ("NewName", newname.NewName);
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
							respCode = engine.ParseResponseCodeAsync (true, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing RESP-CODE failed: {0}", ex);
							return;
						}

						Assert.AreEqual (ImapResponseCodeType.UndefinedFilter, respCode.Type);
						Assert.AreEqual ("This is some free-form text", respCode.Message);

						var undefined = (UndefinedFilterResponseCode) respCode;
						Assert.AreEqual ("filter-name", undefined.Name);
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
							engine.ConnectAsync (tokenizer, false, CancellationToken.None).GetAwaiter ().GetResult ();
						} catch (Exception ex) {
							Assert.Fail ("Parsing greeting failed: {0}", ex);
							return;
						}

						Assert.AreEqual (expected, engine.QuirksMode);
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
		public void TestCyrusImapDetection ()
		{
			TestGreetingDetection ("cyrus", "greeting.txt", ImapQuirksMode.Cyrus);
		}

		[Test]
		public void TestDominoImapDetection ()
		{
			TestGreetingDetection ("domino", "greeting.txt", ImapQuirksMode.Domino);
		}

		[Test]
		public void TestDovecotImapDetection ()
		{
			TestGreetingDetection ("dovecot", "greeting.txt", ImapQuirksMode.Dovecot);
		}

		[Test]
		public void TestExchangeImapDetection ()
		{
			TestGreetingDetection ("exchange", "greeting.txt", ImapQuirksMode.Exchange);
		}

		[Test]
		public void TestUWImapDetection ()
		{
			TestGreetingDetection ("uw", "greeting.txt", ImapQuirksMode.UW);
		}
	}
}
