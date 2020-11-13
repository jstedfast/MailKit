//
// SaslMechanismScramSha1Tests.cs
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
using System.Net;
using System.Text;

using NUnit.Framework;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismScramSha1Tests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");

			var sasl = new SaslMechanismScramSha1 (credentials);
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 ((string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 ("username", null));
		}

		static void AssertScramSha1 (SaslMechanismScramSha1 sasl, string prefix)
		{
			const string expected = "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
			const string challenge1 = "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096";
			const string challenge2 = "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=";
			const string entropy = "fyko+d2lbbFgONRv9qkxdawL";
			string token;

			sasl.cnonce = entropy;

			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + entropy, challenge, "{0}: initial SCRAM-SHA-1 challenge response does not match the expected string.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: should not be authenticated yet.", prefix);

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.AreEqual (expected, challenge, "{0}: second SCRAM-SHA-1 challenge response does not match the expected string.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: should not be authenticated yet.", prefix);

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "{0}: third SCRAM-SHA-1 challenge should be an empty string.", prefix);
			Assert.IsTrue (sasl.IsAuthenticated, "{0}: SCRAM-SHA-1 should be authenticated now.", prefix);
			Assert.AreEqual (string.Empty, sasl.Challenge (string.Empty));
		}

		[Test]
		public void TestScramSha1 ()
		{
			var credentials = new NetworkCredential ("user", "pencil");
			var sasl = new SaslMechanismScramSha1 (credentials);
			var uri = new Uri ("imap://elwood.innosoft.com");

			AssertScramSha1 (sasl, "NetworkCredential");

			sasl = new SaslMechanismScramSha1 ("user", "pencil");

			AssertScramSha1 (sasl, "user/pass");

			sasl = new SaslMechanismScramSha1 (uri, credentials);

			AssertScramSha1 (sasl, "uri/credentials");

			sasl = new SaslMechanismScramSha1 (uri, "user", "pencil");

			AssertScramSha1 (sasl, "uri/user/pass");
		}

		static void AssertSaslException (SaslMechanismScramSha1 sasl, string challenge, SaslErrorCode code)
		{
			var token = Encoding.ASCII.GetBytes (challenge);

			try {
				sasl.Challenge (Convert.ToBase64String (token));
			} catch (SaslException sex) {
				Assert.AreEqual (code, sex.ErrorCode, "ErrorCode");
				return;
			} catch (Exception ex) {
				Assert.Fail ("SaslException expected, but got: {0}", ex.GetType ().Name);
				return;
			}

			Assert.Fail ("SaslException expected.");
		}

		[Test]
		public void TestSaslExceptions ()
		{
			const string nonce = "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j";
			const string salt = "s=QSXCR+Q6sek8bf92";
			const string iterations = "i=4096";
			const string expected = "c=biws," + nonce + ",p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
			const string challenge1 = nonce + "," + salt + "," + iterations;
			const string challenge2 = "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=";
			const string entropy = "fyko+d2lbbFgONRv9qkxdawL";
			var sasl = new SaslMechanismScramSha1 ("user", "pencil") { cnonce = entropy };
			string challenge, token;

			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + entropy, challenge, "initial SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "should not be authenticated yet.");

			AssertSaslException (sasl, challenge1.Replace (salt + ",", string.Empty), SaslErrorCode.IncompleteChallenge); // missing salt
			AssertSaslException (sasl, challenge1.Replace (nonce + ",", string.Empty), SaslErrorCode.IncompleteChallenge); // missing nonce
			AssertSaslException (sasl, challenge1.Replace ("," + iterations, string.Empty), SaslErrorCode.IncompleteChallenge); // missing iterations
			AssertSaslException (sasl, challenge1.Replace (nonce, "r=asfhajksfhkafhakhafk"), SaslErrorCode.InvalidChallenge); // invalid nonce
			AssertSaslException (sasl, challenge1.Replace (iterations, "i=abcd"), SaslErrorCode.InvalidChallenge); // invalid iterations

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.AreEqual (expected, challenge, "second SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "should not be authenticated yet.");

			AssertSaslException (sasl, "x=abcdefg", SaslErrorCode.InvalidChallenge);
			AssertSaslException (sasl, "v=rmF9pqV8S7suAoZWja4dJRkF", SaslErrorCode.IncorrectHash); // incorrect hash length
			AssertSaslException (sasl, "v=AAAAAAAAAAAAAAAAAAAAAAAAAAA=", SaslErrorCode.IncorrectHash); // incorrect hash

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "third SCRAM-SHA-1 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-1 should be authenticated now.");
			Assert.AreEqual (string.Empty, sasl.Challenge (string.Empty));
		}
	}
}
