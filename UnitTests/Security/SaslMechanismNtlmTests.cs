//
// SaslMechanismNtlmTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2023 .NET Foundation and Contributors
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
using System.Security;
using System.Security.Authentication.ExtendedProtection;

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

			var sasl = new SaslMechanismNtlm (credentials);
			Assert.DoesNotThrow (() => sasl.Challenge (null));

			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (null));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm (null, "password"));
			Assert.Throws<ArgumentNullException> (() => new SaslMechanismNtlm ("username", null));
		}

#if false
		static string ToCSharpByteArrayInitializer (string name, byte[] buffer)
		{
			var builder = new System.Text.StringBuilder ();
			int index = 0;

			builder.AppendLine ($"static readonly byte[] {name} = {{");
			while (index < buffer.Length) {
				builder.Append ('\t');
				for (int i = 0; i < 16 && index < buffer.Length; i++, index++)
					builder.AppendFormat ("0x{0}, ", buffer[index].ToString ("x2"));
				builder.Length--;
				if (index == buffer.Length)
					builder.Length--;
				builder.AppendLine ();
			}
			builder.AppendLine ($"}};");

			return builder.ToString ();
		}
#endif

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

		static NtlmNegotiateMessage DecodeNegotiateMessage (string token)
		{
			var message = Convert.FromBase64String (token);

			return new NtlmNegotiateMessage (message, 0, message.Length);
		}

		static NtlmChallengeMessage DecodeChallengeMessage (string token)
		{
			var message = Convert.FromBase64String (token);

			return new NtlmChallengeMessage (message, 0, message.Length);
		}

		static NtlmAuthenticateMessage DecodeAuthenticateMessage (string token)
		{
			var message = Convert.FromBase64String (token);

			return new NtlmAuthenticateMessage (message, 0, message.Length);
		}

		[Test]
		public void TestNtlmTargetInfoEncode ()
		{
			var now = DateTime.Now.Ticks;

			var targetInfo = new NtlmTargetInfo {
				ChannelBinding = Encoding.ASCII.GetBytes ("channel-binding"),
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
			var decoded = new NtlmTargetInfo (encoded, 0, encoded.Length, true);

			Assert.AreEqual ("channel-binding", Encoding.ASCII.GetString (decoded.ChannelBinding), "ChannelBinding does not match.");
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
			0x07, 0xb2, 0x08, 0x20, 0x06, 0x00, 0x06, 0x00, 0x33, 0x00, 0x00, 0x00,
			0x0b, 0x00, 0x0b, 0x00, 0x28, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x57, 0x4f, 0x52, 0x4b, 0x53, 0x54, 0x41, 0x54,
			0x49, 0x4f, 0x4e, 0x44, 0x4f, 0x4d, 0x41, 0x49, 0x4e
		};

		static readonly byte[] NtlmType1EncodedMessageWithVersion = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00,
			0x07, 0x82, 0x08, 0x22, 0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x05, 0x00, 0x93, 0x08,
			0x00, 0x00, 0x00, 0x0f,
		};

		[Test]
		public void TestNtlmNegotiateMessageEncode ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;
			var negotiate = new NtlmNegotiateMessage (flags, "Domain", "Workstation");
			var encoded = negotiate.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType1EncodedMessage);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded NegotiateMessage did not match the expected result.");
		}

		[Test]
		public void TestNtlmNegotiateMessageEncodeWithVersion ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;
			var negotiate = new NtlmNegotiateMessage (flags, "Domain", "Workstation", new Version (5, 0, 2195));
			var encoded = negotiate.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType1EncodedMessageWithVersion);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded NegotiateMessage did not match the expected result.");
		}

		[Test]
		public void TestNtlmNegotiateMessageDecode ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | 
				NtlmFlags.NegotiateDomainSupplied | NtlmFlags.NegotiateWorkstationSupplied | NtlmFlags.NegotiateAlwaysSign |
				NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;
			var negotiate = new NtlmNegotiateMessage (NtlmType1EncodedMessage, 0, NtlmType1EncodedMessage.Length);

			Assert.AreEqual (flags, negotiate.Flags, "The expected flags do not match.");
			Assert.AreEqual ("WORKSTATION", negotiate.Workstation, "The expected workstation name does not match.");
			Assert.AreEqual ("DOMAIN", negotiate.Domain, "The expected domain does not match.");
			Assert.AreEqual (null, negotiate.OSVersion, "The expected OS Version does not match.");
		}

		[Test]
		public void TestNtlmNegotiateMessageDecodeWithVersion ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm |
				NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.NegotiateVersion |
				NtlmFlags.Negotiate128;
			var negotiate = new NtlmNegotiateMessage (NtlmType1EncodedMessageWithVersion, 0, NtlmType1EncodedMessageWithVersion.Length);
			var osVersion = new Version (5, 0, 2195);

			Assert.AreEqual (flags, negotiate.Flags, "The expected flags do not match.");
			Assert.AreEqual (string.Empty, negotiate.Workstation, "The expected workstation name does not match.");
			Assert.AreEqual (string.Empty, negotiate.Domain, "The expected domain does not match.");
			Assert.AreEqual (osVersion, negotiate.OSVersion, "The expected OS Version does not match.");
		}

		static readonly byte[] NtlmType2EncodedMessage = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00,
			0x0c, 0x00, 0x0c, 0x00, 0x38, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x00,
			0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x62, 0x00, 0x62, 0x00, 0x44, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x44, 0x00, 0x4f, 0x00,
			0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x02, 0x00, 0x0c, 0x00,
			0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00,
			0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x45, 0x00, 0x52, 0x00, 0x56, 0x00,
			0x45, 0x00, 0x52, 0x00, 0x04, 0x00, 0x14, 0x00, 0x64, 0x00, 0x6f, 0x00,
			0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00,
			0x6f, 0x00, 0x6d, 0x00, 0x03, 0x00, 0x22, 0x00, 0x73, 0x00, 0x65, 0x00,
			0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x2e, 0x00, 0x64, 0x00,
			0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00,
			0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x00, 0x00, 0x00, 0x00
		};

		[Test]
		public void TestNtlmChallengeMessageEncode ()
		{
			var targetInfo = new NtlmTargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com"
			};

			var challenge = new NtlmChallengeMessage (NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo) {
				ServerChallenge = HexDecode ("0123456789abcdef"),
				TargetInfo = targetInfo,
				TargetName = "DOMAIN",
			};

			Assert.Throws<ArgumentNullException> (() => challenge.ServerChallenge = null);
			Assert.Throws<ArgumentException> (() => challenge.ServerChallenge = new byte[0]);

			var encoded = challenge.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType2EncodedMessage);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded ChallengeMessage did not match the expected result.");
		}

		[Test]
		public void TestNtlmChallengeMessageDecode ()
		{
			const string expectedTargetInfo = "02000c0044004f004d00410049004e0001000c005300450052005600450052000400140064006f006d00610069006e002e0063006f006d00030022007300650072007600650072002e0064006f006d00610069006e002e0063006f006d0000000000";
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo;
			var challenge = new NtlmChallengeMessage (NtlmType2EncodedMessage, 0, NtlmType2EncodedMessage.Length);

			Assert.AreEqual (flags, challenge.Flags, "The expected flags do not match.");
			Assert.AreEqual ("DOMAIN", challenge.TargetName, "The expected TargetName does not match.");

			var nonce = HexEncode (challenge.ServerChallenge);
			Assert.AreEqual ("0123456789abcdef", nonce, "The expected nonce does not match.");

			var targetInfo = HexEncode (challenge.GetEncodedTargetInfo ());
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected TargetInfo does not match.");

			Assert.AreEqual ("DOMAIN", challenge.TargetInfo.DomainName, "The expected TargetInfo domain name does not match.");
			Assert.AreEqual ("SERVER", challenge.TargetInfo.ServerName, "The expected TargetInfo server name does not match.");
			Assert.AreEqual ("domain.com", challenge.TargetInfo.DnsDomainName, "The expected TargetInfo DNS domain name does not match.");
			Assert.AreEqual ("server.domain.com", challenge.TargetInfo.DnsServerName, "The expected TargetInfo DNS server name does not match.");

			targetInfo = HexEncode (challenge.TargetInfo.Encode (true));
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected re-encoded TargetInfo does not match.");
		}

		static readonly byte[] NtlmType2EncodedMessageWithOSVersion = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00,
			0x38, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x02, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x6e, 0x00, 0x6e, 0x00, 0x44, 0x00, 0x00, 0x00,
			0x06, 0x03, 0x80, 0x25, 0x00, 0x00, 0x00, 0x0f, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00,
			0x49, 0x00, 0x4e, 0x00, 0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00,
			0x49, 0x00, 0x4e, 0x00, 0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x45, 0x00, 0x52, 0x00, 0x56, 0x00,
			0x45, 0x00, 0x52, 0x00, 0x04, 0x00, 0x14, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x03, 0x00, 0x22, 0x00,
			0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x2e, 0x00, 0x64, 0x00,
			0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00,
			0x6d, 0x00, 0x07, 0x00, 0x08, 0x00, 0xd2, 0x02, 0x96, 0x49, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00
		};

		[Test]
		public void TestNtlmChallengeMessageEncodeWithOSVersion ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo | NtlmFlags.NegotiateVersion;

			var targetInfo = new NtlmTargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com",
				Timestamp = 1234567890
			};

			var challenge = new NtlmChallengeMessage (flags, new Version (6, 3, 9600)) {
				ServerChallenge = HexDecode ("0123456789abcdef"),
				TargetInfo = targetInfo,
				TargetName = "DOMAIN",
			};

			Assert.Throws<ArgumentNullException> (() => challenge.ServerChallenge = null);
			Assert.Throws<ArgumentException> (() => challenge.ServerChallenge = new byte[0]);

			var encoded = challenge.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType2EncodedMessageWithOSVersion);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded ChallengeMessage did not match the expected result.");
		}

		[Test]
		public void TestNtlmChallengeMessageDecodeWithOSVersion ()
		{
			const string expectedTargetInfo = "02000c0044004f004d00410049004e0001000c005300450052005600450052000400140064006f006d00610069006e002e0063006f006d00030022007300650072007600650072002e0064006f006d00610069006e002e0063006f006d0007000800d20296490000000000000000";
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo | NtlmFlags.NegotiateVersion;
			var challenge = new NtlmChallengeMessage (NtlmType2EncodedMessageWithOSVersion, 0, NtlmType2EncodedMessageWithOSVersion.Length);

			Assert.AreEqual (flags, challenge.Flags, "The expected flags do not match.");
			Assert.AreEqual ("DOMAIN", challenge.TargetName, "The expected TargetName does not match.");

			var nonce = HexEncode (challenge.ServerChallenge);
			Assert.AreEqual ("0123456789abcdef", nonce, "The expected nonce does not match.");

			var targetInfo = HexEncode (challenge.GetEncodedTargetInfo ());
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected TargetInfo does not match.");

			Assert.AreEqual ("DOMAIN", challenge.TargetInfo.DomainName, "The expected TargetInfo domain name does not match.");
			Assert.AreEqual ("SERVER", challenge.TargetInfo.ServerName, "The expected TargetInfo server name does not match.");
			Assert.AreEqual ("domain.com", challenge.TargetInfo.DnsDomainName, "The expected TargetInfo DNS domain name does not match.");
			Assert.AreEqual ("server.domain.com", challenge.TargetInfo.DnsServerName, "The expected TargetInfo DNS server name does not match.");
			Assert.AreEqual (1234567890, challenge.TargetInfo.Timestamp, "The expected TargetInfo Timestamp does not match.");

			targetInfo = HexEncode (challenge.TargetInfo.Encode (true));
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected re-encoded TargetInfo does not match.");
		}

		[Test]
		public void TestNtlmAuthenticateMessageEncode ()
		{
			const string expected = "TlRMTVNTUAADAAAAGAAYAHIAAACqAKoAigAAAAwADABIAAAACAAIAFQAAAAWABYAXAAAAAAAAAA0AQAAAQKBAAAAAAAAAAAARABPAE0AQQBJAE4AdQBzAGUAcgBXAE8AUgBLAFMAVABBAFQASQBPAE4AAVVqop20rpFRR3q+GrYyeQECAwQFBQYHIi2wRrPzFiGEtyUoETB4jQEBAAAAAAAAAJBUA+mR1wEBAgMEBQUGBwAAAAACAAwARABPAE0AQQBJAE4AAQAMAFMARQBSAFYARQBSAAQAFABkAG8AbQBhAGkAbgAuAGMAbwBtAAMAIgBzAGUAcgB2AGUAcgAuAGQAbwBtAGEAaQBuAC4AYwBvAG0ACQAAAAoAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo;
			var timestamp = new DateTime (2021, 08, 15, 15, 20, 00, DateTimeKind.Utc).Ticks;
			var nonce = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x05, 0x06, 0x07 };
			var negotiate = new NtlmNegotiateMessage (flags, null, null, new Version (10, 0, 19043));
			var challenge = DecodeChallengeMessage (challenge2);
			var authenticate = new NtlmAuthenticateMessage (negotiate, challenge, "user", "password", null, "WORKSTATION") {
				ClientChallenge = nonce,
				Timestamp = timestamp
			};

			authenticate.ComputeNtlmV2 (null, false, null);
			var actual = Convert.ToBase64String (authenticate.Encode ());

			//var expectedAuthenticate = DecodeAuthenticateMessage (expected);

			Assert.AreEqual (expected, actual, "The encoded Type3Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType3MessageDecode ()
		{
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAGoAAAAYABgAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACaAAAAAQIAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAJje97h/iKpdr+Lfd5aIoXLe8Rx9XM3vE91UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			var flags = NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateUnicode;
			var authenticate = DecodeAuthenticateMessage (challenge3);

			Assert.AreEqual (flags, authenticate.Flags, "The expected flags do not match.");
			Assert.AreEqual ("DOMAIN", authenticate.Domain, "The expected Domain does not match.");
			Assert.AreEqual ("WORKSTATION", authenticate.Workstation, "The expected Workstation does not match.");
			Assert.AreEqual ("user", authenticate.UserName, "The expected Username does not match.");

			var nt = HexEncode (authenticate.NtChallengeResponse);
			Assert.AreEqual ("dd5428b01e86f4dfcabeac394946dbd43ee88f794dd63255", nt, "The NT payload does not match.");

			var lm = HexEncode (authenticate.LmChallengeResponse);
			Assert.AreEqual ("98def7b87f88aa5dafe2df779688a172def11c7d5ccdef13", lm, "The LM payload does not match.");
		}

		static void AssertNtlmAuthNoDomain (SaslMechanismNtlm sasl, string prefix)
		{
			string challenge;

			Assert.IsTrue (sasl.SupportsChannelBinding, "{0}: SupportsChannelBinding", prefix);
			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);

			var type1 = DecodeNegotiateMessage (challenge);

			Assert.AreEqual (NtlmNegotiateMessage.DefaultFlags, type1.Flags, "{0}: Expected initial NTLM client challenge flags do not match.", prefix);
			Assert.AreEqual (string.Empty, type1.Domain, "{0}: Expected initial NTLM client challenge domain does not match.", prefix);
			Assert.AreEqual (string.Empty, type1.Workstation, "{0}: Expected initial NTLM client challenge workstation does not match.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: NTLM should not be authenticated.", prefix);
		}

		[Test]
		public void TestNtlmAuthNoDomain ()
		{
			var credentials = new NetworkCredential ("username", "password");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmAuthNoDomain (sasl, "NetworkCredential");

			sasl = new SaslMechanismNtlm ("username", "password") { OSVersion = null, Workstation = null };

			AssertNtlmAuthNoDomain (sasl, "user/pass");
		}

		static void AssertNtlmAuthWithDomain (SaslMechanismNtlm sasl, string prefix)
		{
			var initialFlags = NtlmNegotiateMessage.DefaultFlags | NtlmFlags.NegotiateDomainSupplied;
			string challenge;

			Assert.IsTrue (sasl.SupportsChannelBinding, "{0}: SupportsChannelBinding", prefix);
			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);

			var negotiate = DecodeNegotiateMessage (challenge);

			Assert.AreEqual (initialFlags, negotiate.Flags, "{0}: Expected initial NTLM client challenge flags do not match.", prefix);
			Assert.AreEqual ("DOMAIN", negotiate.Domain, "{0}: Expected initial NTLM client challenge domain does not match.", prefix);
			Assert.AreEqual (string.Empty, negotiate.Workstation, "{0}: Expected initial NTLM client challenge workstation does not match.", prefix);
			Assert.IsFalse (sasl.IsAuthenticated, "{0}: NTLM should not be authenticated.", prefix);
		}

		[Test]
		public void TestNtlmAuthWithDomain ()
		{
			var credentials = new NetworkCredential ("domain\\username", "password");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmAuthWithDomain (sasl, "NetworkCredential");

			sasl = new SaslMechanismNtlm ("domain\\username", "password") { OSVersion = null, Workstation = null };

			AssertNtlmAuthWithDomain (sasl, "user/pass");
		}

		[Test]
		public void TestNtlmAuthWithAtDomain ()
		{
			var credentials = new NetworkCredential ("username@domain", "password");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmAuthWithDomain (sasl, "NetworkCredential");

			sasl = new SaslMechanismNtlm ("username@domain", "password") { OSVersion = null, Workstation = null };

			AssertNtlmAuthWithDomain (sasl, "user/pass");
		}

		static void AssertNtlmv2 (SaslMechanismNtlm sasl, string challenge1, string challenge2)
		{
			Assert.IsTrue (sasl.SupportsChannelBinding, "SupportsChannelBinding");
			Assert.IsTrue (sasl.SupportsInitialResponse, "SupportsInitialResponse");

			var response = sasl.Challenge (string.Empty);
			var timestamp = DateTime.UtcNow.Ticks;
			var nonce = NtlmUtils.NONCE (8);

			sasl.Timestamp = timestamp;
			sasl.Nonce = nonce;

			Assert.AreEqual (challenge1, response, "Initial challenge");
			Assert.IsFalse (sasl.IsAuthenticated, "IsAuthenticated");

			response = sasl.Challenge (challenge2);

			var negotiate = DecodeNegotiateMessage (challenge1);
			var challenge = DecodeChallengeMessage (challenge2);
			var authenticate = new NtlmAuthenticateMessage (negotiate, challenge, sasl.Credentials.UserName, sasl.Credentials.Password, null, sasl.Workstation) {
				ClientChallenge = nonce,
				Timestamp = timestamp
			};
			authenticate.ComputeNtlmV2 (null, false, null);

			var actual = Convert.FromBase64String (response);
			var expected = authenticate.Encode ();

			Assert.AreEqual (expected.Length, actual.Length, "Final challenge differs in length: {0} vs {1}", expected.Length, actual.Length);

			for (int i = 0; i < expected.Length; i++)
				Assert.AreEqual (expected[i], actual[i], "Final challenge differs at index {0}", i);

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.IsFalse (sasl.NegotiatedChannelBinding, "NegotiatedChannelBinding");
			Assert.IsFalse (sasl.NegotiatedSecurityLayer, "NegotiatedSecurityLayer");
		}

		[Test]
		public void TestAuthenticationNtlmv2 ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB4IIoAAAAAAoAAAAAAAAACgAAAAAAAAAAAAAAA==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmv2WithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB5IIoAYABgAoAAAAAAAAACgAAAAAAAAAAAAAAERPTUFJTg==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmv2WithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB7IIoAYABgAzAAAACwALACgAAAAAAAAAAAAAAFdPUktTVEFUSU9ORE9NQUlO";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = "WORKSTATION" };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		// From Section 4.2.4.3
		static byte[] ExampleNtlmV2ChallengeMessage = new byte[] {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00,
			0x38, 0x00, 0x00, 0x00, 0x33, 0x82, 0x8a, 0xe2, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x24, 0x00, 0x24, 0x00, 0x44, 0x00, 0x00, 0x00,
			0x06, 0x00, 0x70, 0x17, 0x00, 0x00, 0x00, 0x0f, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00,
			0x65, 0x00, 0x72, 0x00, 0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00,
			0x65, 0x00, 0x72, 0x00, 0x00, 0x00, 0x00, 0x00
		};

		// From Section 4.2.4.3
		static byte[] ExampleNtlmV2AuthenticateMessageOriginal = new byte[] {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x03, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00,
			0x6c, 0x00, 0x00, 0x00, 0x54, 0x00, 0x54, 0x00, 0x84, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00,
			0x48, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x00, 0x54, 0x00, 0x00, 0x00, 0x10, 0x00, 0x10, 0x00,
			0x5c, 0x00, 0x00, 0x00, 0x10, 0x00, 0x10, 0x00, 0xd8, 0x00, 0x00, 0x00, 0x35, 0x82, 0x88, 0xe2,
			0x05, 0x01, 0x28, 0x0a, 0x00, 0x00, 0x00, 0x0f, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x55, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x43, 0x00, 0x4f, 0x00,
			0x4d, 0x00, 0x50, 0x00, 0x55, 0x00, 0x54, 0x00, 0x45, 0x00, 0x52, 0x00, 0x86, 0xc3, 0x50, 0x97,
			0xac, 0x9c, 0xec, 0x10, 0x25, 0x54, 0x76, 0x4a, 0x57, 0xcc, 0xcc, 0x19, 0xaa, 0xaa, 0xaa, 0xaa,
			0xaa, 0xaa, 0xaa, 0xaa, 0x68, 0xcd, 0x0a, 0xb8, 0x51, 0xe5, 0x1c, 0x96, 0xaa, 0xbc, 0x92, 0x7b,
			0xeb, 0xef, 0x6a, 0x1c, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0x00, 0x00, 0x00, 0x00,
			0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00,
			0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xc5, 0xda, 0xd2, 0x54, 0x4f, 0xc9, 0x79, 0x90,
			0x94, 0xce, 0x1c, 0xe9, 0x0b, 0xc9, 0xd0, 0x3e
		};

		// This is the modified version that includes the ChannelBinding=Z16 and TargetName="" string in the TargetInfo embedded in the NTChallengeResponse.
		static readonly byte[] ExampleNtlmV2AuthenticateMessage = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x03, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00,
			0x6c, 0x00, 0x00, 0x00, 0x6c, 0x00, 0x6c, 0x00, 0x84, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00,
			0x48, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x00, 0x54, 0x00, 0x00, 0x00, 0x10, 0x00, 0x10, 0x00,
			0x5c, 0x00, 0x00, 0x00, 0x10, 0x00, 0x10, 0x00, 0xf0, 0x00, 0x00, 0x00, 0x35, 0x82, 0x88, 0xe2,
			0x05, 0x01, 0x28, 0x0a, 0x00, 0x00, 0x00, 0x0f, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x55, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x43, 0x00, 0x4f, 0x00,
			0x4d, 0x00, 0x50, 0x00, 0x55, 0x00, 0x54, 0x00, 0x45, 0x00, 0x52, 0x00, 0x86, 0xc3, 0x50, 0x97,
			0xac, 0x9c, 0xec, 0x10, 0x25, 0x54, 0x76, 0x4a, 0x57, 0xcc, 0xcc, 0x19, 0xaa, 0xaa, 0xaa, 0xaa,
			0xaa, 0xaa, 0xaa, 0xaa, 0xf3, 0x37, 0xab, 0x75, 0x7a, 0xbc, 0x12, 0xf7, 0x68, 0x10, 0x55, 0x60,
			0xb4, 0xcb, 0x30, 0x17, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0x00, 0x00, 0x00, 0x00,
			0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00,
			0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00,
			0x09, 0x00, 0x00, 0x00, 0x0a, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
		};

		static NtlmTargetInfo GetNtChallengeResponseTargetInfo (byte[] ntChallengeResponse)
		{
			int index = 0;

			// Proof (16-bytes) HMACMD5 of the following data
			index += 16;

			// 2 bytes of version info
			index += 2;

			// Z6
			index += 6;

			// Timestamp
			index += 8;

			// ClientChallenge
			index += 8;

			// Z4
			index += 4;

			// TargetInfo followed by Z4
			int targetInfoLength = (ntChallengeResponse.Length - 4) - index;

			return new NtlmTargetInfo (ntChallengeResponse, index, targetInfoLength, true);
		}

		[Test]
		public void TestNtlmv2Example ()
		{
			var flags = NtlmFlags.RequestTarget | NtlmFlags.NegotiateKeyExchange | NtlmFlags.Negotiate56 | NtlmFlags.Negotiate128 | NtlmFlags.NegotiateVersion | NtlmFlags.NegotiateTargetInfo | NtlmFlags.NegotiateExtendedSessionSecurity |
				NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateSeal | NtlmFlags.NegotiateSign | NtlmFlags.NegotiateOem | NtlmFlags.NegotiateUnicode;
			var negotiate = new NtlmNegotiateMessage (flags, "", "", new Version (5, 1, 2600));

			var challenge = new NtlmChallengeMessage (ExampleNtlmV2ChallengeMessage, 0, ExampleNtlmV2ChallengeMessage.Length);
			Assert.AreEqual ("Server", challenge.TargetName, "TargetName");
			Assert.AreEqual ("Server", challenge.TargetInfo.ServerName, "ServerName");
			Assert.AreEqual ("Domain", challenge.TargetInfo.DomainName, "DomainName");

			// Note: Had to reverse engineer these values from the example. The nonce is the last 8 bytes of the lmChallengeResponse
			// and the timestamp was bytes 8-16 of the 'temp' buffer.
			var nonce = new byte[] { 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa };
			var timestamp = DateTime.FromFileTimeUtc (0).Ticks;

			//var expectedType3 = new NtlmAuthenticateMessage (ExampleNtlmV2AuthenticateMessage, 0, ExampleNtlmV2AuthenticateMessage.Length);
			//var expectedTargetInfo = GetNtChallengeResponseTargetInfo (expectedType3.NtChallengeResponse);
			var authenticate = new NtlmAuthenticateMessage (negotiate, challenge, "User", "Password", null, "COMPUTER") {
				ClientChallenge = nonce,
				Timestamp = timestamp
			};
			authenticate.ComputeNtlmV2 (null, false, null);

			//var actualTargetInfo = GetNtChallengeResponseTargetInfo (authenticate.NtChallengeResponse);
			var actual = authenticate.Encode ();

			//var initializer = ToCSharpByteArrayInitializer ("ExampleNtlmV2AuthenticateMessage", actual);

			Assert.AreEqual (ExampleNtlmV2AuthenticateMessage.Length, actual.Length, "Raw message lengths differ.");

			// Note: The EncryptedRandomSessionKey is random and is the last 16 bytes of the message.
			for (int i = 0; i < ExampleNtlmV2AuthenticateMessage.Length - 16; i++)
				Assert.AreEqual (ExampleNtlmV2AuthenticateMessage[i], actual[i], $"Messages differ at index [{i}]");
		}

		static readonly byte[] ExampleNtlmV2AuthenticateMessageWithChannelBinding = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x03, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00,
			0x6c, 0x00, 0x00, 0x00, 0x6c, 0x00, 0x6c, 0x00, 0x84, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00,
			0x48, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x00, 0x54, 0x00, 0x00, 0x00, 0x10, 0x00, 0x10, 0x00,
			0x5c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf0, 0x00, 0x00, 0x00, 0x05, 0x82, 0x08, 0xa2,
			0x0a, 0x00, 0x63, 0x4a, 0x00, 0x00, 0x00, 0x0f, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00,
			0x69, 0x00, 0x6e, 0x00, 0x55, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x43, 0x00, 0x4f, 0x00,
			0x4d, 0x00, 0x50, 0x00, 0x55, 0x00, 0x54, 0x00, 0x45, 0x00, 0x52, 0x00, 0x86, 0xc3, 0x50, 0x97,
			0xac, 0x9c, 0xec, 0x10, 0x25, 0x54, 0x76, 0x4a, 0x57, 0xcc, 0xcc, 0x19, 0xaa, 0xaa, 0xaa, 0xaa,
			0xaa, 0xaa, 0xaa, 0xaa, 0x67, 0xcf, 0xbb, 0x4d, 0x65, 0xaf, 0x3f, 0x67, 0x3e, 0x51, 0x16, 0x22,
			0x6c, 0x30, 0xd9, 0x84, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0x00, 0x00, 0x00, 0x00,
			0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00,
			0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00,
			0x09, 0x00, 0x00, 0x00, 0x0a, 0x00, 0x10, 0x00, 0xf5, 0x09, 0xe7, 0xac, 0xfd, 0xee, 0x20, 0x13,
			0x98, 0xbb, 0xbf, 0xa1, 0x48, 0x63, 0x2a, 0x8c, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
		};


		[Test]
		public void TestNtlmv2ExampleWithChannelBinding ()
		{
			// Note: Had to reverse engineer these values from the example. The nonce is the last 8 bytes of the lmChallengeResponse
			// and the timestamp was bytes 8-16 of the 'temp' buffer.
			var nonce = new byte[] { 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa };
			var timestamp = DateTime.FromFileTimeUtc (0).Ticks;
			var uri = new Uri ("imap://elwood.innosoft.com");
			var sasl = new SaslMechanismNtlm ("User", "Password") {
				ChannelBindingContext = new ChannelBindingContext (ChannelBindingKind.Endpoint, uri.ToString ()),
				OSVersion = new Version (10, 0, 19043, 0),
				IsUnverifiedServicePrincipalName = false,
				ServicePrincipalName = null,
				AllowChannelBinding = true,
				Workstation = "COMPUTER",
				Timestamp = timestamp,
				Nonce = nonce
			};
			string response;

			// initial challenge
			sasl.Challenge (null);

			var challenge = new NtlmChallengeMessage (ExampleNtlmV2ChallengeMessage, 0, ExampleNtlmV2ChallengeMessage.Length);
			response = sasl.Challenge (Convert.ToBase64String (challenge.Encode ()));

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.IsTrue (sasl.NegotiatedChannelBinding, "NegotiatedChannelBinding");
			Assert.IsFalse (sasl.NegotiatedSecurityLayer, "NegotiatedSecurityLayer");

			var expectedAuthenticate = new NtlmAuthenticateMessage (ExampleNtlmV2AuthenticateMessage, 0, ExampleNtlmV2AuthenticateMessage.Length);
			var expectedTargetInfo = GetNtChallengeResponseTargetInfo (expectedAuthenticate.NtChallengeResponse);

			var actual = Convert.FromBase64String (response);
			var actualAuthenticate = DecodeAuthenticateMessage (response);
			var actualTargetInfo = GetNtChallengeResponseTargetInfo (actualAuthenticate.NtChallengeResponse);

			//var initializer = ToCSharpByteArrayInitializer ("ExampleNtlmV2AuthenticateMessage", actual);

			//var expected = DecodeAuthenticateMessage (Convert.ToBase64String (ExampleNtlmV2AuthenticateMessageWithChannelBinding));

			Assert.AreEqual (ExampleNtlmV2AuthenticateMessageWithChannelBinding.Length, actual.Length, "Raw message lengths differ.");

			// Note: The EncryptedRandomSessionKey is random and is the last 16 bytes of the message.
			for (int i = 0; i < ExampleNtlmV2AuthenticateMessageWithChannelBinding.Length - 16; i++)
				Assert.AreEqual (ExampleNtlmV2AuthenticateMessageWithChannelBinding[i], actual[i], $"Messages differ at index [{i}]");
		}

		[Test]
		public void TestSecurePassword ()
		{
			// Note: Had to reverse engineer these values from the example. The nonce is the last 8 bytes of the lmChallengeResponse
			// and the timestamp was bytes 8-16 of the 'temp' buffer.
			var nonce = new byte[] { 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa };
			var timestamp = DateTime.FromFileTimeUtc (0).Ticks;
			var uri = new Uri ("imap://elwood.innosoft.com");
			var password = new SecureString ();

			foreach (var c in "Password")
				password.AppendChar (c);
			password.MakeReadOnly ();

			var sasl = new SaslMechanismNtlm (new NetworkCredential ("User", password)) {
				ChannelBindingContext = new ChannelBindingContext (ChannelBindingKind.Endpoint, uri.ToString ()),
				OSVersion = new Version (10, 0, 19043, 0),
				IsUnverifiedServicePrincipalName = false,
				ServicePrincipalName = null,
				AllowChannelBinding = true,
				Workstation = "COMPUTER",
				Timestamp = timestamp,
				Nonce = nonce
			};
			string response;

			// initial challenge
			sasl.Challenge (null);

			var challenge = new NtlmChallengeMessage (ExampleNtlmV2ChallengeMessage, 0, ExampleNtlmV2ChallengeMessage.Length);
			response = sasl.Challenge (Convert.ToBase64String (challenge.Encode ()));

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.IsTrue (sasl.NegotiatedChannelBinding, "NegotiatedChannelBinding");
			Assert.IsFalse (sasl.NegotiatedSecurityLayer, "NegotiatedSecurityLayer");

			var expectedAuthenticate = new NtlmAuthenticateMessage (ExampleNtlmV2AuthenticateMessage, 0, ExampleNtlmV2AuthenticateMessage.Length);
			var expectedTargetInfo = GetNtChallengeResponseTargetInfo (expectedAuthenticate.NtChallengeResponse);

			var actual = Convert.FromBase64String (response);
			var actualAuthenticate = DecodeAuthenticateMessage (response);
			var actualTargetInfo = GetNtChallengeResponseTargetInfo (actualAuthenticate.NtChallengeResponse);

			//var initializer = ToCSharpByteArrayInitializer ("ExampleNtlmV2AuthenticateMessage", actual);

			Assert.AreEqual (ExampleNtlmV2AuthenticateMessageWithChannelBinding.Length, actual.Length, "Raw message lengths differ.");

			// Note: The EncryptedRandomSessionKey is random and is the last 16 bytes of the message.
			for (int i = 0; i < ExampleNtlmV2AuthenticateMessageWithChannelBinding.Length - 16; i++)
				Assert.AreEqual (ExampleNtlmV2AuthenticateMessageWithChannelBinding[i], actual[i], $"Messages differ at index [{i}]");
		}

		[Test]
		public void TestDefaultCredentials ()
		{
			var sasl = new SaslMechanismNtlm ();
			string response;

			// initial challenge
			sasl.Challenge (null);

			var challenge = new NtlmChallengeMessage (ExampleNtlmV2ChallengeMessage, 0, ExampleNtlmV2ChallengeMessage.Length);
			response = sasl.Challenge (Convert.ToBase64String (challenge.Encode ()));

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
			Assert.IsFalse (sasl.NegotiatedChannelBinding, "NegotiatedChannelBinding");
			Assert.IsFalse (sasl.NegotiatedSecurityLayer, "NegotiatedSecurityLayer");
		}

		[Test]
		public void TestSystemNetMailNtlmNegotiation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAO5CAAAADw==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADgAAAAFgomi18THmUUjMM4AAAAAAAAAAMoAygBEAAAABgOAJQAAAA9EAEUAVgBEAEkAVgACAAwARABFAFYARABJAFYAAQAQAEUAWABDAEgAQQBOAEcARQAEACgAZABlAHYAZABpAHYALgBtAGkAYwByAG8AcwBvAGYAdAAuAGMAbwBtAAMAOgBlAHgAYwBoAGEAbgBnAGUALgBkAGUAdgBkAGkAdgAuAG0AaQBjAHIAbwBzAG8AZgB0AC4AYwBvAG0ABQAoAGQAZQB2AGQAaQB2AC4AbQBpAGMAcgBvAHMAbwBmAHQALgBjAG8AbQAHAAgAS9aGGn7B1gEAAAAATlRMTVNTUAACAAAADAAMADgAAAAFgomi18THmUUjMM4AAAAAAAAAAMoAygBEAAAABgOAJQAAAA9EAEUAVgBEAEkAVgACAAwARABFAFYARABJAFYAAQAQAEUAWABDAEgAQQBOAEcARQAEACgAZABlAHYAZABpAHYALgBtAGkAYwByAG8AcwBvAGYAdAAuAGMAbwBtAAMAOgBlAHgAYwBoAGEAbgBnAGUALgBkAGUAdgBkAGkAdgAuAG0AaQBjAHIAbwBzAG8AZgB0AC4AYwBvAG0ABQAoAGQAZQB2AGQAaQB2AC4AbQBpAGMAcgBvAHMAbwBmAHQALgBjAG8AbQAHAAgAS9aGGn7B1gEAAAAA";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAIoAAABAAUABogAAAAwADABYAAAAEAAQAGQAAAAWABYAdAAAAAAAAADiAQAABYIIogoA7kIAAAAPDYFh2Vjzwk5e9YHnWRvYnUQARQBWAEQASQBWAHUAcwBlAHIAbgBhAG0AZQBXAG8AcgBrAHMAdABhAHQAaQBvAG4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAbqz8wqlkKSdjmeI+rX9+lwEBAAAAAAAAS9aGGn7B1gGZ26Mto5srdQAAAAACAAwARABFAFYARABJAFYAAQAQAEUAWABDAEgAQQBOAEcARQAEACgAZABlAHYAZABpAHYALgBtAGkAYwByAG8AcwBvAGYAdAAuAGMAbwBtAAMAOgBlAHgAYwBoAGEAbgBnAGUALgBkAGUAdgBkAGkAdgAuAG0AaQBjAHIAbwBzAG8AZgB0AC4AYwBvAG0ABQAoAGQAZQB2AGQAaQB2AC4AbQBpAGMAcgBvAHMAbwBmAHQALgBjAG8AbQAHAAgAS9aGGn7B1gEGAAQAAgAAAAkAJgBTAE0AVABQAFMAVgBDAC8AMQA5ADIALgAxADYAOAAuADEALgAxAAoAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
			var negotiate = DecodeNegotiateMessage (challenge1);
			//var challenge = DecodeChallengeMessage (challenge2);
			var authenticate = DecodeAuthenticateMessage (challenge3);

			// This is what System.Net.Mail sends as the initial challenge.
			Assert.AreEqual (NtlmNegotiateMessage.DefaultFlags | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateVersion | NtlmFlags.Negotiate56, negotiate.Flags, "System.Net.Mail Initial Flags");

			var ntlm = new SaslMechanismNtlm ("username", "password") {
				ServicePrincipalName = "SMTPSVC/192.168.1.1",
				OSVersion = new Version (10, 0, 17134),
				Workstation = "Workstation"
			};
			var response = ntlm.Challenge (null);

			Assert.AreEqual ("TlRMTVNTUAABAAAAB4IIogAAAAAoAAAAAAAAACgAAAAKAO5CAAAADw==", response, "MailKit Initial Challenge");

			response = ntlm.Challenge (challenge2);
			var auth = DecodeAuthenticateMessage (response);

			Assert.AreEqual (authenticate.Domain, auth.Domain, "Domain");
			Assert.AreEqual (authenticate.UserName, auth.UserName, "UserName");
			Assert.AreEqual (authenticate.Workstation, auth.Workstation, "Workstation");
			Assert.AreEqual (authenticate.OSVersion, auth.OSVersion, "OSVersion");

			Assert.AreEqual (authenticate.LmChallengeResponse.Length, auth.LmChallengeResponse.Length, "LmChallengeResponseLength");
			for (int i = 0; i < auth.LmChallengeResponse.Length; i++)
				Assert.AreEqual (0, auth.LmChallengeResponse[i], $"LmChallengeResponse[{i}]");
			Assert.NotNull (auth.Mic, "Mic");
			Assert.AreEqual (authenticate.Mic.Length, auth.Mic.Length, "Mic");

			var targetInfo = GetNtChallengeResponseTargetInfo (auth.NtChallengeResponse);
			var expected = GetNtChallengeResponseTargetInfo (authenticate.NtChallengeResponse);
			Assert.NotNull (targetInfo.ChannelBinding, "ChannelBinding");
			Assert.AreEqual (expected.ChannelBinding.Length, targetInfo.ChannelBinding.Length, "ChannelBinding");
			Assert.AreEqual (expected.ServerName, targetInfo.ServerName, "ServerName");
			Assert.AreEqual (expected.DomainName, targetInfo.DomainName, "DomainName");
			Assert.AreEqual (expected.DnsServerName, targetInfo.DnsServerName, "DnsServerName");
			Assert.AreEqual (expected.DnsDomainName, targetInfo.DnsDomainName, "DnsDomainName");
			Assert.AreEqual (expected.DnsTreeName, targetInfo.DnsTreeName, "DnsTreeName");
			Assert.AreEqual (expected.Flags, targetInfo.Flags, "Flags");
			Assert.AreEqual (expected.Timestamp, targetInfo.Timestamp, "Timestamp");

			Console.WriteLine ();
		}
	}
}
