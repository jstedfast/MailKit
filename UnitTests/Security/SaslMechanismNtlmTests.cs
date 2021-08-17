//
// SaslMechanismNtlmTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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
			0x07, 0xb2, 0x08, 0x20, 0x06, 0x00, 0x06, 0x00, 0x2b, 0x00, 0x00, 0x00,
			0x0b, 0x00, 0x0b, 0x00, 0x20, 0x00, 0x00, 0x00, 0x57, 0x4f, 0x52, 0x4b,
			0x53, 0x54, 0x41, 0x54, 0x49, 0x4f, 0x4e, 0x44, 0x4f, 0x4d, 0x41, 0x49,
			0x4e
		};

		static readonly byte[] NtlmType1EncodedMessageWithVersion = {
			0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00,
			0x07, 0x82, 0x08, 0x22, 0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00,
			0x00, 0x00, 0x00, 0x00, 0x28, 0x00, 0x00, 0x00, 0x05, 0x00, 0x93, 0x08,
			0x00, 0x00, 0x00, 0x0f,
		};

		[Test]
		public void TestNtlmType1MessageEncode ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;
			var type1 = new Type1Message (flags, "Domain", "Workstation");
			var encoded = type1.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType1EncodedMessage);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded Type1Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType1MessageEncodeWithVersion ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;
			var type1 = new Type1Message (flags, "Domain", "Workstation", new Version (5, 0, 2195));
			var encoded = type1.Encode ();
			string actual, expected;

			expected = HexEncode (NtlmType1EncodedMessageWithVersion);
			actual = HexEncode (encoded);

			Assert.AreEqual (expected, actual, "The encoded Type1Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType1MessageDecode ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | 
				NtlmFlags.NegotiateDomainSupplied | NtlmFlags.NegotiateWorkstationSupplied | NtlmFlags.NegotiateAlwaysSign |
				NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.Negotiate128;
			var type1 = new Type1Message (NtlmType1EncodedMessage, 0, NtlmType1EncodedMessage.Length);

			Assert.AreEqual (flags, type1.Flags, "The expected flags do not match.");
			Assert.AreEqual ("WORKSTATION", type1.Workstation, "The expected workstation name does not match.");
			Assert.AreEqual ("DOMAIN", type1.Domain, "The expected domain does not match.");
			Assert.AreEqual (null, type1.OSVersion, "The expected OS Version does not match.");
		}

		[Test]
		public void TestNtlmType1MessageDecodeWithVersion ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm |
				NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateExtendedSessionSecurity | NtlmFlags.NegotiateVersion |
				NtlmFlags.Negotiate128;
			var type1 = new Type1Message (NtlmType1EncodedMessageWithVersion, 0, NtlmType1EncodedMessageWithVersion.Length);
			var osVersion = new Version (5, 0, 2195);

			Assert.AreEqual (flags, type1.Flags, "The expected flags do not match.");
			Assert.AreEqual (string.Empty, type1.Workstation, "The expected workstation name does not match.");
			Assert.AreEqual (string.Empty, type1.Domain, "The expected domain does not match.");
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
			var targetInfo = new NtlmTargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com"
			};

			var type2 = new Type2Message (NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo) {
				ServerChallenge = HexDecode ("0123456789abcdef"),
				TargetInfo = targetInfo,
				TargetName = "DOMAIN",
			};

			Assert.Throws<ArgumentNullException> (() => type2.ServerChallenge = null);
			Assert.Throws<ArgumentException> (() => type2.ServerChallenge = new byte[0]);

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

			var nonce = HexEncode (type2.ServerChallenge);
			Assert.AreEqual ("0123456789abcdef", nonce, "The expected nonce does not match.");

			var targetInfo = HexEncode (type2.GetEncodedTargetInfo ());
			Assert.AreEqual (expectedTargetInfo, targetInfo, "The expected TargetInfo does not match.");

			Assert.AreEqual ("DOMAIN", type2.TargetInfo.DomainName, "The expected TargetInfo domain name does not match.");
			Assert.AreEqual ("SERVER", type2.TargetInfo.ServerName, "The expected TargetInfo server name does not match.");
			Assert.AreEqual ("domain.com", type2.TargetInfo.DnsDomainName, "The expected TargetInfo DNS domain name does not match.");
			Assert.AreEqual ("server.domain.com", type2.TargetInfo.DnsServerName, "The expected TargetInfo DNS server name does not match.");

			targetInfo = HexEncode (type2.TargetInfo.Encode (true));
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
		public void TestNtlmType2MessageEncodeWithOSVersion ()
		{
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo | NtlmFlags.NegotiateVersion;

			var targetInfo = new NtlmTargetInfo {
				DomainName = "DOMAIN",
				ServerName = "SERVER",
				DnsDomainName = "domain.com",
				DnsServerName = "server.domain.com",
				Timestamp = 1234567890
			};

			var type2 = new Type2Message (flags, new Version (6, 3, 9600)) {
				ServerChallenge = HexDecode ("0123456789abcdef"),
				TargetInfo = targetInfo,
				TargetName = "DOMAIN",
			};

			Assert.Throws<ArgumentNullException> (() => type2.ServerChallenge = null);
			Assert.Throws<ArgumentException> (() => type2.ServerChallenge = new byte[0]);

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

			var nonce = HexEncode (type2.ServerChallenge);
			Assert.AreEqual ("0123456789abcdef", nonce, "The expected nonce does not match.");

			var targetInfo = HexEncode (type2.GetEncodedTargetInfo ());
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
			const string expected = "TlRMTVNTUAADAAAAGAAYAGoAAACqAKoAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAAAsAQAAAQKBAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAAFVaqKdtK6RUUd6vhq2MnkBAgMEBQUGByItsEaz8xYhhLclKBEweI0BAQAAAAAAAACQVAPpkdcBAQIDBAUFBgcAAAAAAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAkAAAAKABAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var flags = NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo;
			var timestamp = new DateTime (2021, 08, 15, 15, 20, 00, DateTimeKind.Utc).Ticks;
			var nonce = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x05, 0x06, 0x07 };
			var type1 = new Type1Message (flags, null, null, new Version (10, 0, 19043));
			var type2 = DecodeType2Message (challenge2);
			var type3 = new Type3Message (type1, type2, "user", "password", "WORKSTATION") {
				ClientChallenge = nonce,
				Timestamp = timestamp
			};

			type3.ComputeNtlmV2 (null, false, null);
			var actual = Convert.ToBase64String (type3.Encode ());

			//var expectedType3 = DecodeType3Message (expected);

			Assert.AreEqual (expected, actual, "The encoded Type3Message did not match the expected result.");
		}

		[Test]
		public void TestNtlmType3MessageDecode ()
		{
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAGoAAAAYABgAggAAAAwADABAAAAACAAIAEwAAAAWABYAVAAAAAAAAACaAAAAAQIAAEQATwBNAEEASQBOAHUAcwBlAHIAVwBPAFIASwBTAFQAQQBUAEkATwBOAJje97h/iKpdr+Lfd5aIoXLe8Rx9XM3vE91UKLAehvTfyr6sOUlG29Q+6I95TdYyVQ==";
			var flags = NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateUnicode;
			var type3 = DecodeType3Message (challenge3);

			Assert.AreEqual (flags, type3.Flags, "The expected flags do not match.");
			Assert.AreEqual ("DOMAIN", type3.Domain, "The expected Domain does not match.");
			Assert.AreEqual ("WORKSTATION", type3.Workstation, "The expected Workstation does not match.");
			Assert.AreEqual ("user", type3.UserName, "The expected Username does not match.");

			var nt = HexEncode (type3.NtChallengeResponse);
			Assert.AreEqual ("dd5428b01e86f4dfcabeac394946dbd43ee88f794dd63255", nt, "The NT payload does not match.");

			var lm = HexEncode (type3.LmChallengeResponse);
			Assert.AreEqual ("98def7b87f88aa5dafe2df779688a172def11c7d5ccdef13", lm, "The LM payload does not match.");
		}

		static void AssertNtlmAuthNoDomain (SaslMechanismNtlm sasl, string prefix)
		{
			string challenge;

			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);

			var type1 = DecodeType1Message (challenge);

			Assert.AreEqual (Type1Message.DefaultFlags, type1.Flags, "{0}: Expected initial NTLM client challenge flags do not match.", prefix);
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
			var initialFlags = Type1Message.DefaultFlags | NtlmFlags.NegotiateDomainSupplied;
			string challenge;

			Assert.IsTrue (sasl.SupportsInitialResponse, "{0}: SupportsInitialResponse", prefix);

			challenge = sasl.Challenge (string.Empty);

			var type1 = DecodeType1Message (challenge);

			Assert.AreEqual (initialFlags, type1.Flags, "{0}: Expected initial NTLM client challenge flags do not match.", prefix);
			Assert.AreEqual ("DOMAIN", type1.Domain, "{0}: Expected initial NTLM client challenge domain does not match.", prefix);
			Assert.AreEqual (string.Empty, type1.Workstation, "{0}: Expected initial NTLM client challenge workstation does not match.", prefix);
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

		static void AssertNtlmv2 (SaslMechanismNtlm sasl, string challenge1, string challenge2)
		{
			var challenge = sasl.Challenge (string.Empty);
			var timestamp = DateTime.UtcNow.Ticks;
			var nonce = NtlmUtils.NONCE (8);

			sasl.Timestamp = timestamp;
			sasl.Nonce = nonce;

			Assert.AreEqual (challenge1, challenge, "Initial challenge");
			Assert.IsFalse (sasl.IsAuthenticated, "IsAuthenticated");

			challenge = sasl.Challenge (challenge2);

			var type1 = DecodeType1Message (challenge1);
			var type2 = DecodeType2Message (challenge2);
			var type3 = new Type3Message (type1, type2, sasl.Credentials.UserName, sasl.Credentials.Password, sasl.Workstation) {
				ClientChallenge = nonce,
				Timestamp = timestamp
			};
			type3.ComputeNtlmV2 (null, false, null);

			var actual = Convert.FromBase64String (challenge);
			var expected = type3.Encode ();

			Assert.AreEqual (expected.Length, actual.Length, "Final challenge differs in length: {0} vs {1}", expected.Length, actual.Length);

			for (int i = 0; i < expected.Length; i++)
				Assert.AreEqual (expected[i], actual[i], "Final challenge differs at index {0}", i);

			Assert.IsTrue (sasl.IsAuthenticated, "IsAuthenticated");
		}

		[Test]
		public void TestAuthenticationNtlmv2 ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB4IIoAAAAAAgAAAAAAAAACAAAAA=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmv2WithDomain ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB5IIoAYABgAgAAAAAAAAACAAAABET01BSU4=";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADAAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgA8AAAARABPAE0AQQBJAE4AAgAMAEQATwBNAEEASQBOAAEADABTAEUAUgBWAEUAUgAEABQAZABvAG0AYQBpAG4ALgBjAG8AbQADACIAcwBlAHIAdgBlAHIALgBkAG8AbQBhAGkAbgAuAGMAbwBtAAAAAAA=";
			var credentials = new NetworkCredential ("user", "password", "DOMAIN");
			var sasl = new SaslMechanismNtlm (credentials) { OSVersion = null, Workstation = null };

			AssertNtlmv2 (sasl, challenge1, challenge2);
		}

		[Test]
		public void TestAuthenticationNtlmv2WithDomainAndWorkstation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB7IIoAYABgArAAAACwALACAAAABXT1JLU1RBVElPTkRPTUFJTg==";
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
			var type1 = new Type1Message (flags, "", "", new Version (5, 1, 2600));

			var type2 = new Type2Message (ExampleNtlmV2ChallengeMessage, 0, ExampleNtlmV2ChallengeMessage.Length);
			Assert.AreEqual ("Server", type2.TargetName, "TargetName");
			Assert.AreEqual ("Server", type2.TargetInfo.ServerName, "ServerName");
			Assert.AreEqual ("Domain", type2.TargetInfo.DomainName, "DomainName");

			// Note: Had to reverse engineer these values from the example. The nonce is the last 8 bytes of the lmChallengeResponse
			// and the timestamp was bytes 8-16 of the 'temp' buffer.
			var nonce = new byte[] { 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa, 0xaa };
			var timestamp = DateTime.FromFileTimeUtc (0).Ticks;

			var expectedType3 = new Type3Message (ExampleNtlmV2AuthenticateMessage, 0, ExampleNtlmV2AuthenticateMessage.Length);
			var expectedTargetInfo = GetNtChallengeResponseTargetInfo (expectedType3.NtChallengeResponse);
			var type3 = new Type3Message (type1, type2, "User", "Password", "COMPUTER") {
				ClientChallenge = nonce,
				Timestamp = timestamp
			};
			type3.ComputeNtlmV2 (null, false, null);

			var actualTargetInfo = GetNtChallengeResponseTargetInfo (type3.NtChallengeResponse);
			var actual = type3.Encode ();

			//var initializer = ToCSharpByteArrayInitializer ("ExampleNtlmV2AuthenticateMessage", actual);

			Assert.AreEqual (ExampleNtlmV2AuthenticateMessage.Length, actual.Length, "Raw message lengths differ.");

			/// Note: The EncryptedRandomSessionKey is random and is the last 16 bytes of the message.
			for (int i = 0; i < ExampleNtlmV2AuthenticateMessage.Length - 16; i++)
				Assert.AreEqual (ExampleNtlmV2AuthenticateMessage[i], actual[i], $"Messages differ at index [{i}]");
		}

		[Test]
		public void TestSystemNetMailNtlmNegotiation ()
		{
			const string challenge1 = "TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAO5CAAAADw==";
			const string challenge2 = "TlRMTVNTUAACAAAADAAMADgAAAAFgomi18THmUUjMM4AAAAAAAAAAMoAygBEAAAABgOAJQAAAA9EAEUAVgBEAEkAVgACAAwARABFAFYARABJAFYAAQAQAEUAWABDAEgAQQBOAEcARQAEACgAZABlAHYAZABpAHYALgBtAGkAYwByAG8AcwBvAGYAdAAuAGMAbwBtAAMAOgBlAHgAYwBoAGEAbgBnAGUALgBkAGUAdgBkAGkAdgAuAG0AaQBjAHIAbwBzAG8AZgB0AC4AYwBvAG0ABQAoAGQAZQB2AGQAaQB2AC4AbQBpAGMAcgBvAHMAbwBmAHQALgBjAG8AbQAHAAgAS9aGGn7B1gEAAAAATlRMTVNTUAACAAAADAAMADgAAAAFgomi18THmUUjMM4AAAAAAAAAAMoAygBEAAAABgOAJQAAAA9EAEUAVgBEAEkAVgACAAwARABFAFYARABJAFYAAQAQAEUAWABDAEgAQQBOAEcARQAEACgAZABlAHYAZABpAHYALgBtAGkAYwByAG8AcwBvAGYAdAAuAGMAbwBtAAMAOgBlAHgAYwBoAGEAbgBnAGUALgBkAGUAdgBkAGkAdgAuAG0AaQBjAHIAbwBzAG8AZgB0AC4AYwBvAG0ABQAoAGQAZQB2AGQAaQB2AC4AbQBpAGMAcgBvAHMAbwBmAHQALgBjAG8AbQAHAAgAS9aGGn7B1gEAAAAA";
			const string challenge3 = "TlRMTVNTUAADAAAAGAAYAIoAAABAAUABogAAAAwADABYAAAAEAAQAGQAAAAWABYAdAAAAAAAAADiAQAABYIIogoA7kIAAAAPDYFh2Vjzwk5e9YHnWRvYnUQARQBWAEQASQBWAHUAcwBlAHIAbgBhAG0AZQBXAG8AcgBrAHMAdABhAHQAaQBvAG4AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAbqz8wqlkKSdjmeI+rX9+lwEBAAAAAAAAS9aGGn7B1gGZ26Mto5srdQAAAAACAAwARABFAFYARABJAFYAAQAQAEUAWABDAEgAQQBOAEcARQAEACgAZABlAHYAZABpAHYALgBtAGkAYwByAG8AcwBvAGYAdAAuAGMAbwBtAAMAOgBlAHgAYwBoAGEAbgBnAGUALgBkAGUAdgBkAGkAdgAuAG0AaQBjAHIAbwBzAG8AZgB0AC4AYwBvAG0ABQAoAGQAZQB2AGQAaQB2AC4AbQBpAGMAcgBvAHMAbwBmAHQALgBjAG8AbQAHAAgAS9aGGn7B1gEGAAQAAgAAAAkAJgBTAE0AVABQAFMAVgBDAC8AMQA5ADIALgAxADYAOAAuADEALgAxAAoAEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
			var type1 = DecodeType1Message (challenge1);
			//var type2 = DecodeType2Message (challenge2);
			var type3 = DecodeType3Message (challenge3);

			// This is what System.Net.Mail sends as the initial challenge.
			Assert.AreEqual (Type1Message.DefaultFlags | NtlmFlags.NegotiateAlwaysSign | NtlmFlags.NegotiateVersion | NtlmFlags.Negotiate56, type1.Flags, "System.Net.Mail Initial Flags");

			var ntlm = new SaslMechanismNtlm ("username", "password") {
				ServicePrincipalName = "SMTPSVC/192.168.1.1",
				OSVersion = new Version (10, 0, 17134),
				Workstation = "Workstation"
			};
			var challenge = ntlm.Challenge (null);

			Assert.AreEqual ("TlRMTVNTUAABAAAAB4IIogAAAAAoAAAAAAAAACgAAAAKAO5CAAAADw==", challenge, "MailKit Initial Challenge");

			challenge = ntlm.Challenge (challenge2);
			var auth = DecodeType3Message (challenge);

			//Assert.AreEqual (type3.Domain, auth.Domain, "Domain");
			Assert.AreEqual (type3.UserName, auth.UserName, "UserName");
			Assert.AreEqual (type3.Workstation, auth.Workstation, "Workstation");
			Assert.AreEqual (type3.OSVersion, auth.OSVersion, "OSVersion");

			Assert.AreEqual (type3.LmChallengeResponse.Length, auth.LmChallengeResponse.Length, "LmChallengeResponseLength");
			for (int i = 0; i < auth.LmChallengeResponse.Length; i++)
				Assert.AreEqual (0, auth.LmChallengeResponse[i], $"LmChallengeResponse[{i}]");
			Assert.NotNull (auth.Mic, "Mic");
			Assert.AreEqual (type3.Mic.Length, auth.Mic.Length, "Mic");

			var targetInfo = GetNtChallengeResponseTargetInfo (auth.NtChallengeResponse);
			var expected = GetNtChallengeResponseTargetInfo (type3.NtChallengeResponse);
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
