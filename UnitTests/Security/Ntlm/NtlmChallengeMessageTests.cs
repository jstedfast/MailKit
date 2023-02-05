//
// NtlmChallengeMessageTests.cs
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

using NUnit.Framework;

using MailKit.Security.Ntlm;

namespace UnitTests.Security.Ntlm {
	[TestFixture]
	public class NtlmChallengeMessageTests
	{
		static byte[] DavenportExampleNonce = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef };
		static byte[] JavaExampleNonce = { 0x53, 0x72, 0x76, 0x4e, 0x6f, 0x6e, 0x63, 0x65 };

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

			Assert.AreEqual (2, type2.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0x8201, type2.Flags, "Flags");
			Assert.AreEqual ("TlRMTVNTUAACAAAAAAAAAAAAAAABggAAU3J2Tm9uY2UAAAAAAAAAAAAAAAAAAAAA", Convert.ToBase64String (type2.Encode ()), "Encode");
		}

		[Test]
		// Example from http://www.innovation.ch/java/ntlm.html
		public void TestDecodeJavaExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x82, 0x00, 0x00, 0x53, 0x72, 0x76, 0x4e, 0x6f, 0x6e, 0x63, 0x65, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
			var type2 = new NtlmChallengeMessage (rawData, 0, rawData.Length);

			Assert.AreEqual (2, type2.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0x8201, type2.Flags, "Flags");
			Assert.AreEqual (BitConverter.ToString (JavaExampleNonce), BitConverter.ToString (type2.ServerChallenge), "ServerChallenge");
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

			Assert.AreEqual (2, type2.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0x00810201, type2.Flags, "Flags");
			Assert.AreEqual ("DOMAIN", type2.TargetName, "TargetName");
			Assert.AreEqual ("SERVER", type2.TargetInfo.ServerName, "ServerName");
			Assert.AreEqual ("DOMAIN", type2.TargetInfo.DomainName, "DomainName");
			Assert.AreEqual ("server.domain.com", type2.TargetInfo.DnsServerName, "DnsServerName");
			Assert.AreEqual ("domain.com", type2.TargetInfo.DnsDomainName, "DnsDomainName");
			Assert.AreEqual ("01-23-45-67-89-AB-CD-EF", BitConverter.ToString (type2.ServerChallenge), "ServerChallenge");
			Assert.AreEqual ("TlRMTVNTUAACAAAADAAMADgAAAABAoEAASNFZ4mrze8AAAAAAAAAAGIAYgBEAAAAAAAAAAAAAABEAE8ATQBBAEkATgACAAwARABPAE0AQQBJAE4AAQAMAFMARQBSAFYARQBSAAQAFABkAG8AbQBhAGkAbgAuAGMAbwBtAAMAIgBzAGUAcgB2AGUAcgAuAGQAbwBtAGEAaQBuAC4AYwBvAG0AAAAAAA==", Convert.ToBase64String (type2.Encode ()), "Encode");
		}

		[Test]
		// Example from http://davenport.sourceforge.net/ntlm.html#NtlmChallengeMessageExample
		public void TestDecodeDavenportExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x02, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00, 0x30, 0x00, 0x00, 0x00, 0x01, 0x02, 0x81, 0x00, 0x01, 0x23, 0x45, 0x67, 0x89, 0xab, 0xcd, 0xef, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x62, 0x00, 0x62, 0x00, 0x3c, 0x00, 0x00, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x02, 0x00, 0x0c, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x01, 0x00, 0x0c, 0x00, 0x53, 0x00, 0x45, 0x00, 0x52, 0x00, 0x56, 0x00, 0x45, 0x00, 0x52, 0x00, 0x04, 0x00, 0x14, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x03, 0x00, 0x22, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x76, 0x00, 0x65, 0x00, 0x72, 0x00, 0x2e, 0x00, 0x64, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x61, 0x00, 0x69, 0x00, 0x6e, 0x00, 0x2e, 0x00, 0x63, 0x00, 0x6f, 0x00, 0x6d, 0x00, 0x00, 0x00, 0x00, 0x00 };
			var type2 = new NtlmChallengeMessage (rawData, 0, rawData.Length);

			Assert.AreEqual (2, type2.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0x00810201, type2.Flags, "Flags");
			Assert.AreEqual ("DOMAIN", type2.TargetName, "TargetName");
			Assert.AreEqual ("SERVER", type2.TargetInfo.ServerName, "ServerName");
			Assert.AreEqual ("DOMAIN", type2.TargetInfo.DomainName, "DomainName");
			Assert.AreEqual ("server.domain.com", type2.TargetInfo.DnsServerName, "DnsServerName");
			Assert.AreEqual ("domain.com", type2.TargetInfo.DnsDomainName, "DnsDomainName");
			Assert.AreEqual ("01-23-45-67-89-AB-CD-EF", BitConverter.ToString (type2.ServerChallenge), "ServerChallenge");
		}
	}
}
