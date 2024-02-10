//
// SaslMechanismScramSha256Tests.cs
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

using System.Net;
using System.Text;
using System.Security.Authentication.ExtendedProtection;

using MailKit.Security;

namespace UnitTests.Security {
	[TestFixture]
	public class SaslMechanismScramSha256Tests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");

			var sasl = new SaslMechanismScramSha256 (credentials);
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256 ("username", null));

			sasl = new SaslMechanismScramSha256Plus (credentials);
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256Plus (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256Plus (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismScramSha256Plus ("username", null));
		}

		static void AssertScramSha256 (SaslMechanismScramSha256 sasl, string prefix)
		{
			const string expected = "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
			const string challenge1 = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
			const string challenge2 = "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=";
			const string entropy = "rOprNGfwEbeRWgbNEkqO";
			string token;

			sasl.cnonce = entropy;

			Assert.That (sasl.SupportsChannelBinding, Is.False, $"{prefix}: SupportsChannelBinding");
			Assert.That (sasl.SupportsInitialResponse, Is.True, $"{prefix}: SupportsInitialResponse");

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.That (challenge, Is.EqualTo ("n,,n=user,r=" + entropy), $"{prefix}: initial SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.That (challenge, Is.EqualTo (expected), $"{prefix}: second SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.That (challenge, Is.EqualTo (string.Empty), $"{prefix}: third SCRAM-SHA-256 challenge should be an empty string.");
			Assert.That (sasl.IsAuthenticated, Is.True, $"{prefix}: SCRAM-SHA-256 should be authenticated now.");
			Assert.That (sasl.NegotiatedChannelBinding, Is.False, $"{prefix}: NegotiatedChannelBinding");
			Assert.That (sasl.NegotiatedSecurityLayer, Is.False, $"{prefix}: NegotiatedSecurityLayer");

			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), $"{prefix}: challenge while authenticated.");
		}

		[Test]
		public void TestScramSha256 ()
		{
			var credentials = new NetworkCredential ("user", "pencil");
			var sasl = new SaslMechanismScramSha256 (credentials);
			var uri = new Uri ("imap://elwood.innosoft.com");

			AssertScramSha256 (sasl, "NetworkCredential");

			sasl = new SaslMechanismScramSha256 ("user", "pencil");

			AssertScramSha256 (sasl, "user/pass");
		}

		static void AssertSaslException (SaslMechanismScramSha256 sasl, string challenge, SaslErrorCode code)
		{
			var token = Encoding.ASCII.GetBytes (challenge);

			try {
				sasl.Challenge (Convert.ToBase64String (token));
			} catch (SaslException sex) {
				Assert.That (sex.ErrorCode, Is.EqualTo (code), "ErrorCode");
				return;
			} catch (Exception ex) {
				Assert.Fail ($"SaslException expected, but got: {ex.GetType ().Name}");
				return;
			}

			Assert.Fail ("SaslException expected.");
		}

		[Test]
		public void TestSaslExceptions ()
		{
			const string nonce = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0";
			const string salt = "s=W22ZaJ0SNY7soEsUEjb6gQ==";
			const string iterations = "i=4096";
			const string expected = "c=biws," + nonce + ",p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
			const string challenge1 = nonce + "," + salt + "," + iterations;
			const string challenge2 = "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4=";
			const string entropy = "rOprNGfwEbeRWgbNEkqO";
			var sasl = new SaslMechanismScramSha256 ("user", "pencil") { cnonce = entropy };
			string challenge, token;

			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.That (challenge, Is.EqualTo ("n,,n=user,r=" + entropy), "initial SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, "should not be authenticated yet.");

			AssertSaslException (sasl, challenge1.Replace (salt + ",", string.Empty), SaslErrorCode.IncompleteChallenge); // missing salt
			AssertSaslException (sasl, challenge1.Replace (nonce + ",", string.Empty), SaslErrorCode.IncompleteChallenge); // missing nonce
			AssertSaslException (sasl, challenge1.Replace ("," + iterations, string.Empty), SaslErrorCode.IncompleteChallenge); // missing iterations
			AssertSaslException (sasl, challenge1.Replace (nonce, "r=asfhajksfhkafhakhafk"), SaslErrorCode.InvalidChallenge); // invalid nonce
			AssertSaslException (sasl, challenge1.Replace (iterations, "i=abcd"), SaslErrorCode.InvalidChallenge); // invalid iterations

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.That (challenge, Is.EqualTo (expected), "second SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, "should not be authenticated yet.");

			AssertSaslException (sasl, "x=abcdefg", SaslErrorCode.InvalidChallenge);
			AssertSaslException (sasl, "v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl9", SaslErrorCode.IncorrectHash); // incorrect hash length
			AssertSaslException (sasl, "v=AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", SaslErrorCode.IncorrectHash); // incorrect hash

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.That (challenge, Is.EqualTo (string.Empty), "third SCRAM-SHA-256 challenge should be an empty string.");
			Assert.That (sasl.IsAuthenticated, Is.True, "SCRAM-SHA-256 should be authenticated now.");
			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), "challenge while authenticated.");
		}

		static void AssertScramSha256PlusTlsServerEndpoint (SaslMechanismScramSha256Plus sasl, string prefix)
		{
			const string expected = "c=cD10bHMtc2VydmVyLWVuZC1wb2ludCwsaW1hcDovL2Vsd29vZC5pbm5vc29mdC5jb20v,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=5mTXE52q6omlPLhl1OBywbbmUZoqUb8TZ0rQcSODGrg=";
			const string challenge1 = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
			const string challenge2 = "v=iTfhTdj45V52spZKUcXtZGOkyhurIJ8/UAxKKJZcmRQ=";
			const string entropy = "rOprNGfwEbeRWgbNEkqO";
			string token;

			sasl.cnonce = entropy;

			Assert.That (sasl.SupportsChannelBinding, Is.True, $"{prefix}: SupportsChannelBinding");
			Assert.That (sasl.SupportsInitialResponse, Is.True, $"{prefix}: SupportsInitialResponse");

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.That (challenge, Is.EqualTo ("p=tls-server-end-point,,n=user,r=" + entropy), $"{prefix}: initial SCRAM-SHA-256-PLUS challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.That (challenge, Is.EqualTo (expected), $"{prefix}: second SCRAM-SHA-256-PLUS challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.That (challenge, Is.EqualTo (string.Empty), $"{prefix}: third SCRAM-SHA-256-PLUS challenge should be an empty string.");
			Assert.That (sasl.IsAuthenticated, Is.True, $"{prefix}: SCRAM-SHA-256-PLUS should be authenticated now.");
			Assert.That (sasl.NegotiatedChannelBinding, Is.True, $"{prefix}: NegotiatedChannelBinding");
			Assert.That (sasl.NegotiatedSecurityLayer, Is.False, $"{prefix}: NegotiatedSecurityLayer");

			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), $"{prefix}: challenge while authenticated.");
		}

		[Test]
		public void TestScramSha256PlusTlsServerEndpoint ()
		{
			var credentials = new NetworkCredential ("user", "pencil");
			var uri = new Uri ("imap://elwood.innosoft.com");
			var context = new ChannelBindingContext (ChannelBindingKind.Endpoint, uri.ToString ());

			var sasl = new SaslMechanismScramSha256Plus (credentials) { ChannelBindingContext = context };

			AssertScramSha256PlusTlsServerEndpoint (sasl, "NetworkCredential");

			sasl = new SaslMechanismScramSha256Plus ("user", "pencil") { ChannelBindingContext = context };

			AssertScramSha256PlusTlsServerEndpoint (sasl, "user/pass");
		}

		static void AssertScramSha256PlusTlsUnique (SaslMechanismScramSha256Plus sasl, string prefix)
		{
			const string expected = "c=cD10bHMtdW5pcXVlLCxpbWFwOi8vZWx3b29kLmlubm9zb2Z0LmNvbS8=,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=r034Wumf+g5Cw9QvuK2TBTWHLa9hsT0TpUvksIr3P0I=";
			const string challenge1 = "r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096";
			const string challenge2 = "v=ZfK3fb1tbWoyTuaEXvUTM2va2RSQgBVJ8QYsympnk8o=";
			const string entropy = "rOprNGfwEbeRWgbNEkqO";
			string token;

			sasl.cnonce = entropy;

			Assert.That (sasl.SupportsChannelBinding, Is.True, $"{prefix}: SupportsChannelBinding");
			Assert.That (sasl.SupportsInitialResponse, Is.True, $"{prefix}: SupportsInitialResponse");

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.That (challenge, Is.EqualTo ("p=tls-unique,,n=user,r=" + entropy), $"{prefix}: initial SCRAM-SHA-256-PLUS challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge1));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			Assert.That (challenge, Is.EqualTo (expected), $"{prefix}: second SCRAM-SHA-256-PLUS challenge response does not match the expected string.");
			Assert.That (sasl.IsAuthenticated, Is.False, $"{prefix}: should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes (challenge2));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.That (challenge, Is.EqualTo (string.Empty), $"{prefix}: third SCRAM-SHA-256-PLUS challenge should be an empty string.");
			Assert.That (sasl.IsAuthenticated, Is.True, $"{prefix}: SCRAM-SHA-256-PLUS should be authenticated now.");
			Assert.That (sasl.NegotiatedChannelBinding, Is.True, $"{prefix}: NegotiatedChannelBinding");
			Assert.That (sasl.NegotiatedSecurityLayer, Is.False, $"{prefix}: NegotiatedSecurityLayer");

			Assert.That (sasl.Challenge (string.Empty), Is.EqualTo (string.Empty), $"{prefix}: challenge while authenticated.");
		}

		[Test]
		public void TestScramSha256PlusTlsUnique ()
		{
			var credentials = new NetworkCredential ("user", "pencil");
			var uri = new Uri ("imap://elwood.innosoft.com");
			var context = new ChannelBindingContext (ChannelBindingKind.Unique, uri.ToString ());

			var sasl = new SaslMechanismScramSha256Plus (credentials) { ChannelBindingContext = context };

			AssertScramSha256PlusTlsUnique (sasl, "NetworkCredential");

			sasl = new SaslMechanismScramSha256Plus ("user", "pencil") { ChannelBindingContext = context };

			AssertScramSha256PlusTlsUnique (sasl, "user/pass");
		}
	}
}
