//
// SaslMechanismTests.cs
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
using System.Net;
using System.Text;

using NUnit.Framework;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");
			SaslMechanism sasl;

			sasl = new SaslMechanismScramSha1 (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 ((string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha1 ("username", null));
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			sasl = new SaslMechanismScramSha256 (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 ((string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 ("username", null));
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create (null, uri, Encoding.UTF8, credentials));
			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create ("PLAIN", null, Encoding.UTF8, credentials));
			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create ("PLAIN", uri, null, credentials));
			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create ("PLAIN", uri, Encoding.UTF8, null));

			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create (null, uri, credentials));
			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create ("PLAIN", null, credentials));
			Assert.Throws<ArgumentNullException> (() => SaslMechanism.Create ("PLAIN", uri, null));

			Assert.Throws<ArgumentNullException> (() => SaslMechanism.SaslPrep (null));
		}

		[Test]
		public void TestIsSupported ()
		{
			var supported = new [] { "PLAIN", "LOGIN", "CRAM-MD5", "DIGEST-MD5", "SCRAM-SHA-1", "SCRAM-SHA-256", "NTLM", "XOAUTH2" };
			var unsupported = new [] { "ANONYMOUS", "GSSAPI", "KERBEROS_V4" };
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");

			foreach (var mechanism in supported) {
				Assert.IsTrue (SaslMechanism.IsSupported (mechanism), mechanism);

				var sasl = SaslMechanism.Create (mechanism, uri, credentials);
				Assert.IsNotNull (sasl, mechanism);
				Assert.AreEqual (mechanism, sasl.MechanismName, "MechanismName");

				sasl.Reset ();
			}

			foreach (var mechanism in unsupported)
				Assert.IsFalse (SaslMechanism.IsSupported (mechanism), mechanism);
		}

		[Test]
		public void TestScramSha1 ()
		{
			const string expected = "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
			const string challenge1 = "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096";
			const string challenge2 = "v=rmF9pqV8S7suAoZWja4dJRkFsKQ=";
			const string entropy = "fyko+d2lbbFgONRv9qkxdawL";
			//var uri = new Uri ("imap://elwood.innosoft.com");
			var credentials = new NetworkCredential ("user", "pencil");
			var sasl = new SaslMechanismScramSha1 (credentials) { cnonce = entropy };
			string token;

			Assert.IsTrue (sasl.SupportsInitialResponse, "SASLIR");

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + entropy, challenge, "Initial SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-1 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.AreEqual (expected, challenge, "Second SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-1 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "Third SCRAM-SHA-1 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-1 should be authenticated now.");
			Assert.Throws<InvalidOperationException> (() => sasl.Challenge (string.Empty));

			sasl = new SaslMechanismScramSha1 ("user", "pencil") { cnonce = entropy };

			Assert.IsTrue (sasl.SupportsInitialResponse, "SASLIR");

			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + entropy, challenge, "Initial SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-1 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.AreEqual (expected, challenge, "Second SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-1 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "Third SCRAM-SHA-1 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-1 should be authenticated now.");
			Assert.Throws<InvalidOperationException> (() => sasl.Challenge (string.Empty));
		}

		[Test]
		public void TestScramSha256 ()
		{
			const string expected = "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
			const string challenge1 = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
			const string challenge2 = "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=";
			const string entropy = "rOprNGfwEbeRWgbNEkqO";
			//var uri = new Uri ("imap://elwood.innosoft.com");
			var credentials = new NetworkCredential ("user", "pencil");
			var sasl = new SaslMechanismScramSha256 (credentials) { cnonce = entropy };
			string token;

			Assert.IsTrue (sasl.SupportsInitialResponse, "SASLIR");

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + entropy, challenge, "Initial SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-256 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.AreEqual (expected, challenge, "Second SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-256 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "Third SCRAM-SHA-256 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-256 should be authenticated now.");
			Assert.Throws<InvalidOperationException> (() => sasl.Challenge (string.Empty));

			sasl = new SaslMechanismScramSha256 ("user", "pencil") { cnonce = entropy };

			Assert.IsTrue (sasl.SupportsInitialResponse, "SASLIR");

			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + entropy, challenge, "Initial SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-256 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.AreEqual (expected, challenge, "Second SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-256 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "Third SCRAM-SHA-256 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-256 should be authenticated now.");
			Assert.Throws<InvalidOperationException> (() => sasl.Challenge (string.Empty));
		}

		[Test]
		public void TestSaslPrep ()
		{
			// The following examples are from rfc4013, Section 3.
			// #  Input            Output     Comments
			// -  -----            ------     --------
			// 1  I<U+00AD>X       IX         SOFT HYPHEN mapped to nothing
			Assert.AreEqual ("IX", SaslMechanism.SaslPrep ("I\u00ADX"), "1");
			// 2  user             user       no transformation
			Assert.AreEqual ("user", SaslMechanism.SaslPrep ("user"), "2");
			// 3  USER             USER       case preserved, will not match #2
			Assert.AreEqual ("USER", SaslMechanism.SaslPrep ("USER"), "3");
			// 4  <U+00AA>         a          output is NFKC, input in ISO 8859-1
			Assert.AreEqual ("a", SaslMechanism.SaslPrep ("\u00AA"), "4");
			// 5  <U+2168>         IX         output is NFKC, will match #1
			Assert.AreEqual ("IX", SaslMechanism.SaslPrep ("\u2168"), "5");
			// 6  <U+0007>                    Error - prohibited character
			try {
				SaslMechanism.SaslPrep ("\u0007");
				Assert.Fail ("6");
			} catch (ArgumentException) {
			}
			// 7  <U+0627><U+0031>            Error - bidirectional check
			//try {
			//	SaslMechanism.SaslPrep ("\u0627\u0031");
			//	Assert.Fail ("7");
			//} catch (ArgumentException) {
			//}

			var prohibited = new char [] { '\uF8FF', '\uDFFF', '\uFFFD', '\u2FFB', '\u200E' };
			foreach (var c in prohibited) {
				try {
					SaslMechanism.SaslPrep (c.ToString ());
					Assert.Fail ("prohibited: '\\u{0:X}'", c);
				} catch (ArgumentException) {
				}
			}

			Assert.AreEqual (string.Empty, SaslMechanism.SaslPrep (string.Empty));
			Assert.AreEqual ("a b", SaslMechanism.SaslPrep ("a\u00A0b"));
		}
	}
}
