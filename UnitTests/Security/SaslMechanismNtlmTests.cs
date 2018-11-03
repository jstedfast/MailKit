//
// SaslMechanismNtlmTests.cs
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
	public class SaslMechanismNtlmTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var uri = new Uri ("smtp://localhost");

			var sasl = new SaslMechanismNtlm (credentials);
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ((Uri) null, credentials));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (uri, null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ((Uri) null, "username", "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (uri, (string) null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (uri, "username", null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ("username", null));
		}

		static byte ToXDigit (char c)
		{
			if (c >= 0x41) {
				if (c >= 0x61)
					return (byte) (c - (0x61 - 0x0a));

				return (byte) (c - (0x41 - 0x0A));
			}

			return (byte) (c - 0x30);
		}

		static byte[] HexDecode (string value)
		{
			var decoded = new byte[value.Length / 2];

			for (int i = 0; i < decoded.Length; i++) {
				var b1 = ToXDigit (value[i * 2]);
				var b2 = ToXDigit (value[i * 2 + 1]);

				decoded[i] = (byte) ((b1 << 4) | b2);
			}

			return decoded;
		}

		static string HexEncode (byte[] value)
		{
			var builder = new StringBuilder ();

			for (int i = 0; i < value.Length; i++)
				builder.Append (value[i].ToString ("x2"));

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

		static readonly byte [] NtlmType1EncodedMessage = {
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

		static readonly byte [] NtlmType2EncodedMessage = {
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
		public void TestNtlmType2MessageEncode ()
		{
			var targetInfo = new TargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com"
			};

			var type2 = new Type2Message {
				Flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo,
				Nonce = HexDecode ("0123456789abcdef"),
				TargetInfo = targetInfo,
				TargetName = "DOMAIN",
			};

			var encoded = type2.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType2EncodedMessage);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded Type2Message did not match the expected result.");
		}

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

		static void AssertNtlmAuthNoDomain (SaslMechanismNtlm sasl, string prefix)
		{
			string challenge;
			byte [] decoded;

			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);
			decoded = Convert.FromBase64String (challenge);

			var type1 = new Type1Message (decoded, 0, decoded.Length);

			Assert.AreEqual (Type1Message.DefaultFlags, type1.Flags, "{0}: Expected initial NTLM client challenge flags do not match.", prefix);
			Assert.AreEqual (string.Empty, type1.Domain, "{0}: Expected initial NTLM client challenge domain does not match.", prefix);
			Assert.AreEqual (string.Empty, type1.Host, "{0}: Expected initial NTLM client challenge host does not match.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: NTLM should not be authenticated.", prefix);
		}

		[Test]
		public void TestNtlmAuthNoDomain ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var sasl = new SaslMechanismNtlm (credentials);
			var uri = new Uri ("smtp://localhost");

			AssertNtlmAuthNoDomain (sasl, "NetworkCredential");

			sasl = new SaslMechanismNtlm ("username", "password");

			AssertNtlmAuthNoDomain (sasl, "user/pass");

			sasl = new SaslMechanismNtlm (uri, credentials);

			AssertNtlmAuthNoDomain (sasl, "uri/credentials");

			sasl = new SaslMechanismNtlm (uri, "username", "password");

			AssertNtlmAuthNoDomain (sasl, "uri/user/pass");
		}

		static void AssertNtlmAuthWithDomain (SaslMechanismNtlm sasl, string prefix)
		{
			var initialFlags = Type1Message.DefaultFlags | NtlmFlags.NegotiateDomainSupplied;
			string challenge;
			byte [] decoded;

			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);
			decoded = Convert.FromBase64String (challenge);

			var type1 = new Type1Message (decoded, 0, decoded.Length);

			Assert.AreEqual (initialFlags, type1.Flags, "{0}: Expected initial NTLM client challenge flags do not match.", prefix);
			Assert.AreEqual ("DOMAIN", type1.Domain, "{0}: Expected initial NTLM client challenge domain does not match.", prefix);
			Assert.AreEqual (string.Empty, type1.Host, "{0}: Expected initial NTLM client challenge host does not match.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: NTLM should not be authenticated.", prefix);
		}

		[Test]
		public void TestNtlmAuthWithDomain ()
		{

			var credentials = new NetworkCredential ("domain\\username", "password");
			var sasl = new SaslMechanismNtlm (credentials);
			var uri = new Uri ("smtp://localhost");

			AssertNtlmAuthWithDomain (sasl, "NetworkCredential");

			sasl = new SaslMechanismNtlm ("domain\\username", "password");

			AssertNtlmAuthWithDomain (sasl, "user/pass");

			sasl = new SaslMechanismNtlm (uri, credentials);

			AssertNtlmAuthWithDomain (sasl, "uri/credentials");

			sasl = new SaslMechanismNtlm (uri, "domain\\username", "password");

			AssertNtlmAuthWithDomain (sasl, "uri/user/pass");
		}
	}
}
