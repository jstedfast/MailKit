//
// NtlmChallengeMessageTests.cs
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

using MailKit.Security.Ntlm;

namespace UnitTests.Security.Ntlm {
	[TestFixture]
	public class NtlmChallengeMessageTests
	{
		static readonly byte[] DavenportExampleNonce = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };
		static readonly byte[] JavaExampleNonce = { 0x53, 0x72, 0x76, 0x4e, 0x6f, 0x6e, 0x63, 0x65 };

		[Test]
		public void TestArgumentExceptions ()
		{
			byte[] badMessageData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x01, 0x00, 0x00, 0x00, 0x00 };
			var type2 = new NtlmChallengeMessage ();

			Assert.Throws<ArgumentNullException> (() => new NtlmChallengeMessage (null, 0, 16));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmChallengeMessage (new byte[8], 0, 8));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmChallengeMessage (new byte[8], -1, 8));
			Assert.Throws<ArgumentException> (() => new NtlmChallengeMessage (badMessageData, 0, badMessageData.Length));

			Assert.Throws<ArgumentNullException> (() => type2.ServerChallenge = null);
			Assert.Throws<ArgumentException> (() => type2.ServerChallenge = new byte[9]);
		}

		[Test]
		// Example from http://www.innovation.ch/java/ntlm.html
		public void TestEncodeJavaExample ()
		{
			var type2 = new NtlmChallengeMessage (NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign) { ServerChallenge = JavaExampleNonce };

			Assert.That (type2.Type, Is.EqualTo (2), "Type");
			Assert.That (type2.Flags, Is.EqualTo ((NtlmFlags) 0x8201), "Flags");
			Assert.That (Convert.ToBase64String (type2.Encode ()), Is.EqualTo ("TlRMTVNTUAACAAAAAAAAAAAAAAABggAAU3J2Tm9uY2UAAAAAAAAAAAAAAAAAAAAA"), "Encode");
		}

		[Test]
		// Example from http://www.innovation.ch/java/ntlm.html
		public void TestDecodeJavaExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x82, 0x00, 0x00, 0x53, 0x72, 0x76, 0x4e, 0x6f, 0x6e, 0x63, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
			var type2 = new NtlmChallengeMessage (rawData, 0, rawData.Length);

			Assert.That (type2.Type, Is.EqualTo (2), "Type");
			Assert.That (type2.Flags, Is.EqualTo ((NtlmFlags) 0x8201), "Flags");
			Assert.That (BitConverter.ToString (type2.ServerChallenge), Is.EqualTo (BitConverter.ToString (JavaExampleNonce)), "ServerChallenge");
		}

		[Test]
		// Example from http://davenport.sourceforge.net/ntlm.html#NtlmChallengeMessageExample
		public void TestEncodeDavenportExample ()
		{
			var type2 = new NtlmChallengeMessage (NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateNtlm | NtlmFlags.TargetTypeDomain | NtlmFlags.NegotiateTargetInfo) {
				TargetInfo = new NtlmTargetInfo () {
					DomainName = "DOMAIN",
					ServerName = "SERVER",
					DnsDomainName = "domain.com",
					DnsServerName = "server.domain.com"
				},
				ServerChallenge = DavenportExampleNonce,
				TargetName = "DOMAIN"
			};

			Assert.That (type2.Type, Is.EqualTo (2), "Type");
			Assert.That (type2.Flags, Is.EqualTo ((NtlmFlags) 0x00810201), "Flags");
			Assert.That (type2.TargetName, Is.EqualTo ("DOMAIN"), "TargetName");
			Assert.That (type2.TargetInfo.ServerName, Is.EqualTo ("SERVER"), "ServerName");
			Assert.That (type2.TargetInfo.DomainName, Is.EqualTo ("DOMAIN"), "DomainName");
			Assert.That (type2.TargetInfo.DnsServerName, Is.EqualTo ("server.domain.com"), "DnsServerName");
			Assert.That (type2.TargetInfo.DnsDomainName, Is.EqualTo ("domain.com"), "DnsDomainName");
			Assert.That (BitConverter.ToString (type2.ServerChallenge), Is.EqualTo ("01-23-45-67-89-AB-CD-EF"), "ServerChallenge");
			Assert.That (Convert.ToBase64String (type2.Encode ()), Is.EqualTo ("TlRMTVNTUAACAAAADAAMADgAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgBEAAAAAAAAAAAAAABEAE8ATQBBAEkATgACAAwARABPAE0AQQBJAE4AAQAMAFMARQBSAFYARQBSAAQAFABkAG8AbQBhAGkAbgAuAGMAbwBtAAMAIgBzAGUAcgB2AGUAcgAuAGQAbwBtAGEAaQBuAC4AYwBvAG0AAAAAAA=="), "Encode");
		}

		[Test]
		// Example from http://davenport.sourceforge.net/ntlm.html#NtlmChallengeMessageExample
		public void TestDecodeDavenportExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00, 0x30, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x00, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x62, 0x00, 0x62, 0x00, 0x3c, 0x00, 0x00, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x45, 0x00, 0x52, 0x00, 0x56, 0x00, 0x45, 0x00, 0x52, 0x00, 0x04, 0x00, 0x14, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x03, 0x00, 0x22, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x2e, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x00, 0x00, 0x00, 0x00 };
			var type2 = new NtlmChallengeMessage (rawData, 0, rawData.Length);

			Assert.That (type2.Type, Is.EqualTo (2), "Type");
			Assert.That (type2.Flags, Is.EqualTo ((NtlmFlags) 0x00810201), "Flags");
			Assert.That (type2.TargetName, Is.EqualTo ("DOMAIN"), "TargetName");
			Assert.That (type2.TargetInfo.ServerName, Is.EqualTo ("SERVER"), "ServerName");
			Assert.That (type2.TargetInfo.DomainName, Is.EqualTo ("DOMAIN"), "DomainName");
			Assert.That (type2.TargetInfo.DnsServerName, Is.EqualTo ("server.domain.com"), "DnsServerName");
			Assert.That (type2.TargetInfo.DnsDomainName, Is.EqualTo ("domain.com"), "DnsDomainName");
			Assert.That (BitConverter.ToString (type2.ServerChallenge), Is.EqualTo ("01-23-45-67-89-AB-CD-EF"), "ServerChallenge");
		}
	}
}
