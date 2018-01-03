//
// ImapEncodingTests.cs
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

using NUnit.Framework;

using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapEncodingTests
	{
		[Test]
		public void TestArabicExample ()
		{
			const string arabic = "هل تتكلم اللغة الإنجليزية /العربية؟";

			var encoded = ImapEncoding.Encode (arabic);
			Assert.AreEqual ("&BkcGRA- &BioGKgZDBkQGRQ- &BicGRAZEBjoGKQ- &BicGRAYlBkYGLAZEBkoGMgZKBik- /&BicGRAY5BjEGKAZKBikGHw-", encoded, "UTF-7 encoded text does not match the expected value: {0}", encoded);

			var decoded = ImapEncoding.Decode (encoded);
			Assert.AreEqual (arabic, decoded, "UTF-7 decoded text does not match the original text: {0}", decoded);
		}

		[Test]
		public void TestJapaneseExample ()
		{
			const string japanese = "狂ったこの世で狂うなら気は確かだ。";

			var encoded = ImapEncoding.Encode (japanese);
			Assert.AreEqual ("&csIwYzBfMFMwbk4WMGdywjBGMGowiWwXMG94ujBLMGAwAg-", encoded, "UTF-7 encoded text does not match the expected value: {0}", encoded);

			var decoded = ImapEncoding.Decode (encoded);
			Assert.AreEqual (japanese, decoded, "UTF-7 decoded text does not match the original text: {0}", decoded);
		}

		[Test]
		public void TestSurrogatePairs ()
		{
			// Example taken from: http://stackoverflow.com/questions/14347799/how-do-i-create-a-string-with-a-surrogate-pair-inside-of-it
			// which is in turn taken from: http://msmvps.com/blogs/jon_skeet/archive/2009/11/02/omg-ponies-aka-humanity-epic-fail.aspx
			var text = "Les Mise" + char.ConvertFromUtf32 (0x301) + "rables";

			var encoded = ImapEncoding.Encode (text);
			Assert.AreEqual ("Les Mise&AwE-rables", encoded, "UTF-7 encoded text does not match the expected value: {0}", encoded);

			var decoded = ImapEncoding.Decode (encoded);
			Assert.AreEqual (text, decoded, "UTF-7 decoded text does not match the original text: {0}", decoded);
		}

		[Test]
		public void TestChineseSurrogatePairs ()
		{
			const string chinese = "‎中國哲學書電子化計劃";

			var encoded = ImapEncoding.Encode (chinese);
			Assert.AreEqual ("&IA5OLVcLVPJbeGb4lvtbUFMWighSgw-", encoded, "UTF-7 encoded text does not match the expected value: {0}", encoded);

			var decoded = ImapEncoding.Decode (encoded);
			Assert.AreEqual (chinese, decoded, "UTF-7 decoded text does not match the original text: {0}", decoded);
		}

		[Test]
		public void TestRfc3501Example ()
		{
			const string mixed = "~peter/mail/台北/日本語";

			var encoded = ImapEncoding.Encode (mixed);
			Assert.AreEqual ("~peter/mail/&U,BTFw-/&ZeVnLIqe-", encoded, "UTF-7 encoded text does not match the expected value: {0}", encoded);

			var decoded = ImapEncoding.Decode (encoded);
			Assert.AreEqual (mixed, decoded, "UTF-7 decoded text does not match the original text: {0}", decoded);
		}

		[Test]
		public void TestDecodeBadRfc3501Example ()
		{
			const string encoded = "&Jjo!";

			var decoded = ImapEncoding.Decode (encoded);
			Assert.AreEqual (encoded, decoded, "UTF-7 decoded text does not match the original text: {0}", decoded);
		}

		[Test]
		public void TestRfc3501SuperfluousShiftExample ()
		{
			const string example = "&U,BTFw-&ZeVnLIqe-";

			// Note: we may want to modify ImapEncoding.Decode() to fail and return the input text in this case
			var decoded = ImapEncoding.Decode (example);
			Assert.AreEqual ("台北日本語", decoded, "UTF-7 decoded text does not match the expected value.");

			var encoded = ImapEncoding.Encode (decoded);
			Assert.AreEqual ("&U,BTF2XlZyyKng-", encoded, "UTF-7 encoded text does not match the expected value.");
		}

		[Test]
		public void TestDecodeSurrogatePair ()
		{
			const string example = "&2DzcHA-";

			var decoded = ImapEncoding.Decode (example);
			Assert.AreEqual ("\ud83c\udc1c", decoded, "UTF-7 decoded text does not match the expected value.");

			var encoded = ImapEncoding.Encode (decoded);
			Assert.AreEqual ("&2DzcHA-", encoded, "UTF-7 encoded text does not match the expected value.");
		}
	}
}
