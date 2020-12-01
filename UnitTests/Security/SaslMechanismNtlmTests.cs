//
// SaslMechanismNtlmTests.cs
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
				TargetName = "TARGET",
				DnsTreeName = "target.domain.com",
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com",
				Timestamp = now,
				Flags = 1776
			};

			var encoded = targetInfo.Encode (true);
			var decoded = new TargetInfo (encoded, 0, encoded.Length, true);

			Assert.AreEqual (targetInfo.DnsDomainName, decoded.DnsDomainName, "DnsDomainName does not match.");
			Assert.AreEqual (targetInfo.DnsServerName, decoded.DnsServerName, "DnsServerName does not match.");
			Assert.AreEqual (targetInfo.DnsTreeName, decoded.DnsTreeName, "DnsTreeName does not match.");
			Assert.AreEqual (targetInfo.DomainName, decoded.DomainName, "DomainName does not match.");
			Assert.AreEqual (targetInfo.ServerName, decoded.ServerName, "ServerName does not match.");
			Assert.AreEqual (targetInfo.TargetName, decoded.TargetName, "TargetName does not match.");
			Assert.AreEqual (targetInfo.Timestamp, decoded.Timestamp, "Timestamp does not match.");
			Assert.AreEqual (targetInfo.Flags, decoded.Flags, "Flags does not match.");
		}

		static readonly byte [] NtlmType1EncodedMessage = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00,
			0x07, 0x32, 0x00, 0x02, 0x06, 0x00, 0x06, 0x00, 0x33, 0x00, 0x00, 0x00,
			0x0b, 0x00, 0x0b, 0x00, 0x28, 0x00, 0x00, 0x00, 0x05, 0x00, 0x93, 0x08,
			0x00, 0x00, 0x00, 0x0f, 0x57, 0x4f, 0x52, 0x4b, 0x53, 0x54, 0x41, 0x54,
			0x49, 0x4f, 0x4e, 0x44, 0x4f, 0x4d, 0x41, 0x49, 0x4e
		};

		[Test]
		public void TestNtlmType1MessageEncode ()
		{
			var type1 = new Type1Message ("Workstation", "Domain", new Version (5, 0, 2195));
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
				NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateVersion;
			var type1 = new Type1Message (NtlmType1EncodedMessage, 0, NtlmType1EncodedMessage.Length);
			var osVersion = new Version (5, 0, 2195);

			Assert.AreEqual (flags, type1.Flags, "The expected flags do not match.");
			Assert.AreEqual ("WORKSTATION", type1.Host, "The expected workstation name does not match.");
			Assert.AreEqual ("DOMAIN", type1.Domain, "The expected domain does not match.");
			Assert.AreEqual (osVersion, type1.OSVersion, "The expected OS Version does not match.");
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

			Assert.Throws<ArgumentNullException> (() => type2.Nonce = null);
			Assert.Throws<ArgumentException> (() => type2.Nonce = new byte[0]);

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

		static readonly byte[] NtlmType2EncodedMessageWithOSVersion = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00,
			0x0c, 0x00, 0x0c, 0x00, 0x40, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x02,
			0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x6e, 0x00, 0x6e, 0x00, 0x4c, 0x00, 0x00, 0x00,
			0x06, 0x03, 0x80, 0x25, 0x00, 0x00, 0x00, 0x0f, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00,
			0x49, 0x00, 0x4e, 0x00, 0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x4f, 0x00,
			0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x01, 0x00, 0x0c, 0x00,
			0x53, 0x00, 0x45, 0x00, 0x52, 0x00, 0x56, 0x00, 0x45, 0x00, 0x52, 0x00,
			0x04, 0x00, 0x14, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00,
			0x03, 0x00, 0x22, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00,
			0x65, 0x00, 0x72, 0x00, 0x2e, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00,
			0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00,
			0x6d, 0x00, 0x07, 0x00, 0x08, 0x00, 0xd2, 0x02, 0x96, 0x49, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00
		};

		[Test]
		public void TestNtlmType2MessageEncodeWithOSVersion ()
		{
			var targetInfo = new TargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com",
				Timestamp = 1234567890
			};

			var type2 = new Type2Message (new Version (6, 3, 9600)) {
				Flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo | NtlmFlags.NegotiateVersion,
				Nonce = HexDecode ("0123456789abcdef"),
				TargetInfo = targetInfo,
				TargetName = "DOMAIN",
			};

			Assert.Throws<ArgumentNullException> (() => type2.Nonce = null);
			Assert.Throws<ArgumentException> (() => type2.Nonce = new byte[0]);

			var encoded = type2.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType2EncodedMessageWithOSVersion);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded Type2Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType2MessageDecodeWithOSVersion ()
		{
			const string expectedTargetInfo = "02000c0044004f004d00410049004e0001000c005300450052005600450052000400140064006f006d00610069006e002e0063006f006d00030022007300650072007600650072002e0064006f006d00610069006e002e0063006f006d0007000800d20296490000000000000000";
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo | NtlmFlags.NegotiateVersion;
			var type2 = new Type2Message (NtlmType2EncodedMessageWithOSVersion, 0, NtlmType2EncodedMessageWithOSVersion.Length);

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
			Assert.AreEqual (1234567890, type2.TargetInfo.Timestamp, "The expected TargetInfo Timestamp does not match.");

			targetInfo = HexEncode (type2.TargetInfo.Encode (true));
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected re-encoded TargetInfo does not match.");
		}

		[Test]
		public void TestNtlmType3MessageEncode ()
		{
			const string expected = "TlRMTVNTUAADAAAAGAAYAGoAAAAYABgAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACaAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAJje97h/iKpdr+Lfd5aIoXLe8Rx9XM3vE91UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var token = Convert.FromBase64String (challenge2);
			var type2 = new Type2Message (token, 0, token.Length);
			var type3 = new Type3Message (type2, null, NtlmAuthLevel.LM_and_NTLM, "user", "password", "WORKSTATION");
			var actual = Convert.ToBase64String (type3.Encode ());

			Assert.AreEqual (expected, actual, "The encoded Type3Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType3MessageDecode ()
		{
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAGoAAAAYABgAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACaAAAAAQIAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAJje97h/iKpdr+Lfd5aIoXLe8Rx9XM3vE91UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			var flags = NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateUnicode;
			var token = Convert.FromBase64String (challenge3);
			var type3 = new Type3Message (token, 0, token.Length);

			Assert.AreEqual (flags, type3.Flags, "The expected flags do not match.");
			Assert.AreEqual ("DOMAIN", type3.Domain, "The expected Domain does not match.");
			Assert.AreEqual ("WORKSTATION", type3.Host, "The expected Host does not match.");
			Assert.AreEqual ("user", type3.Username, "The expected Username does not match.");

			var nt = HexEncode (type3.NT);
			Assert.AreEqual ("dd5428b01e86f4dfcabeac394946dbd43ee88f794dd63255", nt, "The NT payload does not match.");

			var lm = HexEncode (type3.LM);
			Assert.AreEqual ("98def7b87f88aa5dafe2df779688a172def11c7d5ccdef13", lm, "The LM payload does not match.");
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

		static Type1Message DecodeType1Message (string token)
		{
			var message = Convert.FromBase64String (token);

			return new Type1Message (message, 0, message.Length);
		}

		static Type2Message DecodeType2Message (string token)
		{
			var message = Convert.FromBase64String (token);

			return new Type2Message (message, 0, message.Length);
		}

		static Type3Message DecodeType3Message (string token)
		{
			var message = Convert.FromBase64String (token);

			return new Type3Message (message, 0, message.Length);
		}

		static void AssertLmAndNtlm (SaslMechanismNtlm sasl, string challenge1, string challenge2, string challenge3)
		{
			var challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (challenge1, challenge, "Initial challenge");
			Assert.IsFalse (sasl.IsAuthenticated, "IsAuthenticated");

			challenge = sasl.Challenge (challenge2);

			Assert.AreEqual (challenge3, challenge, "Final challenge");
			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
		}

		[Test]
		public void TestAuthenticationLmAndNtlm ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABwIAAAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAFQAAAAYABgAbAAAAAwADABAAAAACAAIAEwAAAAAAAAAVAAAAAAAAACEAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAmN73uH+Iql2v4t93loihct7xHH1cze8T3VQosB6G9N/Kvqw5SUbb1D7oj3lN1jJV";

			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.LM_and_NTLM };

			AssertLmAndNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmWithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABxIAAAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAFQAAAAYABgAbAAAAAwADABAAAAACAAIAEwAAAAAAAAAVAAAAAAAAACEAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAmN73uH+Iql2v4t93loihct7xHH1cze8T3VQosB6G9N/Kvqw5SUbb1D7oj3lN1jJV";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.LM_and_NTLM };

			AssertLmAndNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmWithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABzIAAAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAGoAAAAYABgAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACaAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAJje97h/iKpdr+Lfd5aIoXLe8Rx9XM3vE91UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Workstation = "WORKSTATION", Level = NtlmAuthLevel.LM_and_NTLM };

			AssertLmAndNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmSessionFallback ()
		{
			// Note: this will fallback to LN_and_NTLM because the type2 message does not contain the NegotiateNtlm2Key flag
			const string challenge1 = "TlRMTVNTUAABAAAABwIAAAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAFQAAAAYABgAbAAAAAwADABAAAAACAAIAEwAAAAAAAAAVAAAAAAAAACEAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAmN73uH+Iql2v4t93loihct7xHH1cze8T3VQosB6G9N/Kvqw5SUbb1D7oj3lN1jJV";

			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session };

			AssertLmAndNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmSessionFallbackWithDomain ()
		{
			// Note: this will fallback to LN_and_NTLM because the type2 message does not contain the NegotiateNtlm2Key flag
			const string challenge1 = "TlRMTVNTUAABAAAABxIAAAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAFQAAAAYABgAbAAAAAwADABAAAAACAAIAEwAAAAAAAAAVAAAAAAAAACEAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAmN73uH+Iql2v4t93loihct7xHH1cze8T3VQosB6G9N/Kvqw5SUbb1D7oj3lN1jJV";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session };

			AssertLmAndNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmSessionFallbackWithDomainAndWorkstation ()
		{
			// Note: this will fallback to LN_and_NTLM because the type2 message does not contain the NegotiateNtlm2Key flag
			const string challenge1 = "TlRMTVNTUAABAAAABzIAAAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAGoAAAAYABgAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACaAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAJje97h/iKpdr+Lfd5aIoXLe8Rx9XM3vE91UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Workstation = "WORKSTATION", Level = NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session };

			AssertLmAndNtlm (sasl, challenge1, challenge2, challenge3);
		}

		static void AssertNtlm2Key (SaslMechanismNtlm sasl, string challenge1, string challenge2)
		{
			var challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (challenge1, challenge, "Initial challenge");
			Assert.IsFalse (sasl.IsAuthenticated, "IsAuthenticated");

			challenge = sasl.Challenge (challenge2);

			var token = Convert.FromBase64String (challenge2);
			var type2 = new Type2Message (token, 0, token.Length);
			var type3 = new Type3Message (type2, null, sasl.Level, sasl.Credentials.UserName, sasl.Credentials.Password, sasl.Workstation);
			var ignoreLength = 48;

			var actual = Convert.FromBase64String (challenge);
			var expected = type3.Encode ();

			Assert.AreEqual (expected.Length, actual.Length, "Final challenge differs in length: {0} vs {1}", expected.Length, actual.Length);

			for (int i = 0; i < expected.Length - ignoreLength; i++)
				Assert.AreEqual (expected[i], actual[i], "Final challenge differs at index {0}", i);

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
		}

		[Test]
		public void TestAuthenticationLmAndNtlmSessionNegotiateNtlm2Key ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABwIAAAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAokAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session };

			AssertNtlm2Key (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmSessionNegotiateNtlm2KeyWithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABxIAAAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAokAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session };

			AssertNtlm2Key (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationLmAndNtlmSessionNegotiateNtlm2KeyWithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABzIAAAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAokAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Workstation = "WORKSTATION", Level = NtlmAuthLevel.LM_and_NTLM_and_try_NTLMv2_Session };

			AssertNtlm2Key (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmNegotiateNtlm2Key ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABwIAAAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAokAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.NTLM_only };

			AssertNtlm2Key (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmNegotiateNtlm2KeyWithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABxIAAAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAokAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.NTLM_only };

			AssertNtlm2Key (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmNegotiateNtlm2KeyWithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABzIAAAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAokAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Workstation = "WORKSTATION", Level = NtlmAuthLevel.NTLM_only };

			AssertNtlm2Key (sasl, challenge1, challenge2);
		}

		static void AssertNtlm (SaslMechanismNtlm sasl, string challenge1, string challenge2, string challenge3)
		{
			var challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (challenge1, challenge, "Initial challenge");
			Assert.IsFalse (sasl.IsAuthenticated, "IsAuthenticated");

			challenge = sasl.Challenge (challenge2);

			Assert.AreEqual (challenge3, challenge, "Final challenge");
			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
		}

		[Test]
		public void TestAuthenticationNtlm ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABwIAAAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAAAAAAFQAAAAYABgAVAAAAAwADABAAAAACAAIAEwAAAAAAAAAVAAAAAAAAABsAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIA3VQosB6G9N/Kvqw5SUbb1D7oj3lN1jJV";
			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.NTLM_only };

			AssertNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationNtlmWithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABxIAAAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAAAAAAFQAAAAYABgAVAAAAAwADABAAAAACAAIAEwAAAAAAAAAVAAAAAAAAABsAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIA3VQosB6G9N/Kvqw5SUbb1D7oj3lN1jJV";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.NTLM_only };

			AssertNtlm (sasl, challenge1, challenge2, challenge3);
		}

		[Test]
		public void TestAuthenticationNtlmWithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABzIAAAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			const string challenge3 = "TlRMTVNTUAADAAAAAAAAAGoAAAAYABgAagAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACCAAAAAQKAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAN1UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Workstation = "WORKSTATION", Level = NtlmAuthLevel.NTLM_only };

			AssertNtlm (sasl, challenge1, challenge2, challenge3);
		}

		static void AssertNtlmv2 (SaslMechanismNtlm sasl, string challenge1, string challenge2)
		{
			var challenge = sasl.Challenge (string.Empty);

			Assert.AreEqual (challenge1, challenge, "Initial challenge");
			Assert.IsFalse (sasl.IsAuthenticated, "IsAuthenticated");

			challenge = sasl.Challenge (challenge2);

			var token = Convert.FromBase64String (challenge2);
			var type2 = new Type2Message (token, 0, token.Length);
			var type3 = new Type3Message (type2, null, sasl.Level, sasl.Credentials.UserName, sasl.Credentials.Password, sasl.Workstation);
			var ignoreLength = type2.EncodedTargetInfo.Length + 28 + 16;

			var actual = Convert.FromBase64String (challenge);
			var expected = type3.Encode ();
			var ntlmBufferIndex = expected.Length - ignoreLength;
			var targetInfoIndex = ntlmBufferIndex + 16 /* md5 hash */ + 28;

			Assert.AreEqual (expected.Length, actual.Length, "Final challenge differs in length: {0} vs {1}", expected.Length, actual.Length);

			for (int i = 0; i < expected.Length - ignoreLength; i++)
				Assert.AreEqual (expected[i], actual[i], "Final challenge differs at index {0}", i);

			// now compare the TargetInfo blobs
			for (int i = targetInfoIndex; i < expected.Length; i++)
				Assert.AreEqual (expected[i], actual[i], "Final challenge differs at index {0}", i);

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
		}

		[Test]
		public void TestAuthenticationNtlmv2 ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABwIAAAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.NTLMv2_only };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmv2WithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABxIAAAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Level = NtlmAuthLevel.NTLMv2_only };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmv2WithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAABzIAAAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { Workstation = "WORKSTATION", Level = NtlmAuthLevel.NTLMv2_only };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}
	}
}
