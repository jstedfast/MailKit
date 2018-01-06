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
using MailKit.Security.Ntlm;

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

			Assert.Throws<ArgumentNullException> (() => new SaslException (null, SaslErrorCode.MissingChallenge, "message"));

			sasl = new SaslMechanismCramMd5 (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (uri, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismCramMd5 ("username", null));
			Assert.Throws<NotSupportedException> (() => sasl.Challenge (null));

			sasl = new SaslMechanismDigestMd5 (credentials) { Uri = uri };
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismDigestMd5 ("username", null));
			Assert.Throws<NotSupportedException> (() => sasl.Challenge (null));

			sasl = new SaslMechanismLogin (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, Encoding.UTF8, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, Encoding.UTF8, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, Encoding.UTF8, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, Encoding.UTF8, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, Encoding.UTF8, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Uri) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (Encoding.UTF8, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ((Encoding) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (Encoding.UTF8, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (Encoding.UTF8, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismLogin ("username", null));
			Assert.Throws<NotSupportedException> (() => sasl.Challenge (null));

			sasl = new SaslMechanismNtlm (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ((Uri) null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ((Uri) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ("username", null));
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			sasl = new SaslMechanismOAuth2 (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 ((Uri) null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 ((Uri) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismOAuth2 ("username", null));
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			sasl = new SaslMechanismPlain (credentials);
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain ((Uri) null, Encoding.UTF8, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, Encoding.UTF8, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain ((Uri) null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain ((Uri) null, Encoding.UTF8, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, Encoding.UTF8, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, Encoding.UTF8, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain ((Uri) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (Encoding.UTF8, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain ((Encoding) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (Encoding.UTF8, null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (Encoding.UTF8, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismPlain ("username", null));
			Assert.DoesNotThrow (() => sasl.Challenge (null));

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
			}

			foreach (var mechanism in unsupported)
				Assert.IsFalse (SaslMechanism.IsSupported (mechanism), mechanism);
		}

		[Test]
		public void TestCramMd5ExampleFromRfc2195 ()
		{
			const string serverToken = "<1896.697170952@postoffice.example.net>";
			const string expected = "joe 3dbc88f0624776a737b39093f6eb6427";
			var credentials = new NetworkCredential ("joe", "tanstaaftanstaaf");
			var sasl = new SaslMechanismCramMd5 (credentials);

			var token = Encoding.ASCII.GetBytes (serverToken);
			var challenge = sasl.Challenge (Convert.ToBase64String (token));
			var decoded = Convert.FromBase64String (challenge);
			var result = Encoding.ASCII.GetString (decoded);

			Assert.AreEqual (expected, result, "CRAM-MD5 challenge response does not match the expected string.");
			Assert.IsTrue (sasl.IsAuthenticated, "CRAM-MD5 should be authenticated.");
		}

		[Test]
		public void TestDigestMd5ExampleFromRfc2831 ()
		{
			const string serverToken1 = "realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",qop=\"auth\",algorithm=md5-sess,charset=utf-8";
			const string expected1 = "username=\"chris\",realm=\"elwood.innosoft.com\",nonce=\"OA6MG9tEQGm2hh\",cnonce=\"OA6MHXh6VqTrRk\",nc=00000001,qop=\"auth\",digest-uri=\"imap/elwood.innosoft.com\",response=d388dad90d4bbd760a152321f2143af7,charset=utf-8,algorithm=md5-sess";
			const string serverToken2 = "rspauth=ea40f60335c427b5527b84dbabcdfffd";
			var credentials = new NetworkCredential ("chris", "secret");
			var uri = new Uri ("imap://elwood.innosoft.com");
			var sasl = new SaslMechanismDigestMd5 (uri, credentials, "OA6MHXh6VqTrRk");

			var token = Encoding.ASCII.GetBytes (serverToken1);
			var challenge = sasl.Challenge (Convert.ToBase64String (token));
			var decoded = Convert.FromBase64String (challenge);
			var result = Encoding.ASCII.GetString (decoded);

			Assert.AreEqual (expected1, result, "DIGEST-MD5 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "DIGEST-MD5 should not be authenticated yet.");

			token = Encoding.ASCII.GetBytes (serverToken2);
			challenge = sasl.Challenge (Convert.ToBase64String (token));
			decoded = Convert.FromBase64String (challenge);
			result = Encoding.ASCII.GetString (decoded);

			Assert.AreEqual (string.Empty, result, "Second DIGEST-MD5 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "DIGEST-MD5 should be authenticated now.");
		}

		[Test]
		public void TestLoginAuth ()
		{
			const string expected1 = "dXNlcm5hbWU=";
			const string expected2 = "cGFzc3dvcmQ=";
			var credentials = new NetworkCredential ("username", "password");
			var sasl = new SaslMechanismLogin (credentials);
			string challenge;

			challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (expected1, challenge, "LOGIN initial challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "LOGIN should not be authenticated.");

			challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (expected2, challenge, "LOGIN final challenge response does not match the expected string.");
			Assert.IsTrue (sasl.IsAuthenticated, "LOGIN should be authenticated.");
		}

		[Test]
		public void TestPlainAuth ()
		{
			const string expected = "AHVzZXJuYW1lAHBhc3N3b3Jk";
			var credentials = new NetworkCredential ("username", "password");
			var sasl = new SaslMechanismPlain (credentials);

			var challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (expected, challenge, "PLAIN challenge response does not match the expected string.");
			Assert.IsTrue (sasl.IsAuthenticated, "PLAIN should be authenticated.");
		}

		static string HexEncode (byte[] message)
		{
			var builder = new StringBuilder ();

			for (int i = 0; i < message.Length; i++)
				builder.Append (message[i].ToString ("x2"));

			return builder.ToString ();
		}

		[Test]
		public void TestNtlmTargetInfoEncode ()
		{
			var now = DateTime.Now.Ticks;

			var targetInfo = new TargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com",
				Timestamp = now,
			};

			var encoded = targetInfo.Encode (true);
			var decoded = new TargetInfo (encoded, 0, encoded.Length, true);

			Assert.AreEqual (targetInfo.DnsDomainName, decoded.DnsDomainName, "DnsDomainName does not match.");
			Assert.AreEqual (targetInfo.DnsServerName, decoded.DnsServerName, "DnsServerName does not match.");
			Assert.AreEqual (targetInfo.DomainName, decoded.DomainName, "DomainName does not match.");
			Assert.AreEqual (targetInfo.ServerName, decoded.ServerName, "ServerName does not match.");
			Assert.AreEqual (targetInfo.Timestamp, decoded.Timestamp, "Timestamp does not match.");
		}

		static readonly byte[] NtlmType1EncodedMessage = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00,
			0x07, 0x32, 0x00, 0x00, 0x06, 0x00, 0x06, 0x00, 0x33, 0x00, 0x00, 0x00,
			0x0b, 0x00, 0x0b, 0x00, 0x28, 0x00, 0x00, 0x00, 0x05, 0x00, 0x93, 0x08,
			0x00, 0x00, 0x00, 0x0f, 0x57, 0x4f, 0x52, 0x4b, 0x53, 0x54, 0x41, 0x54,
			0x49, 0x4f, 0x4e, 0x44, 0x4f, 0x4d, 0x41, 0x49, 0x4e
		};

		[Test]
		public void TestNtlmType1MessageEncode ()
		{
			var type1 = new Type1Message ("Workstation", "Domain") { OSVersion = new Version (5, 0, 2195) };
			var encoded = type1.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType1EncodedMessage);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded Type1Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType1MessageDecode ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateDomainSupplied | NtlmFlags.NegotiateWorkstationSupplied |
				NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget;
			var type1 = new Type1Message (NtlmType1EncodedMessage, 0, NtlmType1EncodedMessage.Length);

			Assert.AreEqual (flags, type1.Flags, "The expected flags do not match.");
			Assert.AreEqual ("WORKSTATION", type1.Host, "The expected workstation name does not match.");
			Assert.AreEqual ("DOMAIN", type1.Domain, "The expected domain does not match.");
			Assert.AreEqual (new Version (5, 0, 2195), type1.OSVersion, "The expected OS Version does not match.");
		}

		static readonly byte[] NtlmType2EncodedMessage = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00,
			0x0c, 0x00, 0x0c, 0x00, 0x30, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x00,
			0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x62, 0x00, 0x62, 0x00, 0x3c, 0x00, 0x00, 0x00,
			0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00,
			0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00,
			0x49, 0x00, 0x4e, 0x00, 0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x45, 0x00,
			0x52, 0x00, 0x56, 0x00, 0x45, 0x00, 0x52, 0x00, 0x04, 0x00, 0x14, 0x00,
			0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00,
			0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x03, 0x00, 0x22, 0x00,
			0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00,
			0x2e, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00,
			0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x00, 0x00,
			0x00, 0x00
		};

		[Test]
		public void TestNtlmType2MessageDecode ()
		{
			const string expectedTargetInfo = "02000c0044004f004d00410049004e0001000c005300450052005600450052000400140064006f006d00610069006e002e0063006f006d00030022007300650072007600650072002e0064006f006d00610069006e002e0063006f006d0000000000";
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo;
			var type2 = new Type2Message (NtlmType2EncodedMessage, 0, NtlmType2EncodedMessage.Length);

			Assert.AreEqual (flags, type2.Flags, "The expected flags do not match.");
			Assert.AreEqual ("DOMAIN", type2.TargetName, "The expected TargetName does not match.");

			var nonce = HexEncode (type2.Nonce);
			Assert.AreEqual ("0123456789abcdef", nonce, "The expected nonce does not match.");

			var targetInfo = HexEncode (type2.EncodedTargetInfo);
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected TargetInfo does not match.");

			Assert.AreEqual ("DOMAIN", type2.TargetInfo.DomainName, "The expected TargetInfo domain name does not match.");
			Assert.AreEqual ("SERVER", type2.TargetInfo.ServerName, "The expected TargetInfo server name does not match.");
			Assert.AreEqual ("domain.com", type2.TargetInfo.DnsDomainName, "The expected TargetInfo DNS domain name does not match.");
			Assert.AreEqual ("server.domain.com", type2.TargetInfo.DnsServerName, "The expected TargetInfo DNS server name does not match.");

			targetInfo = HexEncode (type2.TargetInfo.Encode (true));
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected re-encoded TargetInfo does not match.");
		}

		[Test]
		public void TestNtlmAuthNoDomain ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("imap://imap.gmail.com");
			var sasl = new SaslMechanismNtlm (uri, credentials);
			string challenge;
			byte[] decoded;

			challenge = sasl.Challenge (string.Empty);
			decoded = Convert.FromBase64String (challenge);

			var type1 = new Type1Message (decoded, 0, decoded.Length);

			Assert.AreEqual (Type1Message.DefaultFlags, type1.Flags, "Expected initial NTLM client challenge flags do not match.");
			Assert.AreEqual (string.Empty, type1.Domain, "Expected initial NTLM client challenge domain does not match.");
			Assert.AreEqual (string.Empty, type1.Host, "Expected initial NTLM client challenge host does not match.");
			Assert.IsFalse (sasl.IsAuthenticated, "NTLM should not be authenticated.");
		}

		[Test]
		public void TestNtlmAuthWithDomain ()
		{
			var initialFlags = Type1Message.DefaultFlags | NtlmFlags.NegotiateDomainSupplied;
			var credentials = new NetworkCredential ("domain\\username", "password");
			var uri = new Uri ("imap://imap.gmail.com");
			var sasl = new SaslMechanismNtlm (uri, credentials);
			string challenge;
			byte[] decoded;

			challenge = sasl.Challenge (string.Empty);
			decoded = Convert.FromBase64String (challenge);

			var type1 = new Type1Message (decoded, 0, decoded.Length);

			Assert.AreEqual (initialFlags, type1.Flags, "Expected initial NTLM client challenge flags do not match.");
			Assert.AreEqual ("DOMAIN", type1.Domain, "Expected initial NTLM client challenge domain does not match.");
			Assert.AreEqual (string.Empty, type1.Host, "Expected initial NTLM client challenge host does not match.");
			Assert.IsFalse (sasl.IsAuthenticated, "NTLM should not be authenticated.");
		}

		[Test]
		public void TestScramSha1 ()
		{
			const string cnonce = "fyko+d2lbbFgONRv9qkxdawL";
			var uri = new Uri ("imap://elwood.innosoft.com");
			var credentials = new NetworkCredential ("user", "pencil");
			var sasl = new SaslMechanismScramSha1 (credentials, cnonce);
			string token;

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + cnonce, challenge, "Initial SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-1 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes ("r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096"));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			const string expected = "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
			Assert.AreEqual (expected, challenge, "Second SCRAM-SHA-1 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-1 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes ("v=rmF9pqV8S7suAoZWja4dJRkFsKQ="));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "Third SCRAM-SHA-1 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-1 should be authenticated now.");
		}

		[Test]
		public void TestScramSha256 ()
		{
			const string cnonce = "rOprNGfwEbeRWgbNEkqO";
			var uri = new Uri ("imap://elwood.innosoft.com");
			var credentials = new NetworkCredential ("user", "pencil");
			var sasl = new SaslMechanismScramSha256 (credentials, cnonce);
			string token;

			var challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (null)));

			Assert.AreEqual ("n,,n=user,r=" + cnonce, challenge, "Initial SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-256 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes ("r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,s=W22ZaJ0SNY7soEsUEjb6gQ==,i=4096"));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));

			const string expected = "c=biws,r=rOprNGfwEbeRWgbNEkqO%hvYDpWUa2RaTCAfuxFIlj)hNlF$k0,p=dHzbZapWIk4jUhN+Ute9ytag9zjfMHgsqmmiz7AndVQ=";
			Assert.AreEqual (expected, challenge, "Second SCRAM-SHA-256 challenge response does not match the expected string.");
			Assert.IsFalse (sasl.IsAuthenticated, "SCRAM-SHA-256 should not be authenticated yet.");

			token = Convert.ToBase64String (Encoding.UTF8.GetBytes ("v=6rriTRBi23WpRR/wtup+mMhUZUn/dB5nLTJRsjl95G4="));
			challenge = Encoding.UTF8.GetString (Convert.FromBase64String (sasl.Challenge (token)));
			Assert.AreEqual (string.Empty, challenge, "Third SCRAM-SHA-256 challenge should be an empty string.");
			Assert.IsTrue (sasl.IsAuthenticated, "SCRAM-SHA-256 should be authenticated now.");
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
