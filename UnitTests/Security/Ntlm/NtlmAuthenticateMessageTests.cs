//
// NtlmAuthenticateMessageTests.cs
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
	public class NtlmAuthenticateMessageTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			byte[] badMessageData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x01, 0x00, 0x00, 0x00, 0x00 };
			var NtlmNegotiate = new NtlmNegotiateMessage ();
			var NtlmChallenge = new NtlmChallengeMessage ();
			var NtlmAuthenticate = new NtlmAuthenticateMessage (NtlmNegotiate, NtlmChallenge, "username", "password", "domain", "workstation");

			Assert.Throws<ArgumentNullException> (() => new NtlmAuthenticateMessage (null, NtlmChallenge, "username", "password", "domain", "workstation"));
			Assert.Throws<ArgumentNullException> (() => new NtlmAuthenticateMessage (NtlmNegotiate, null, "username", "password", "domain", "workstation"));
			Assert.Throws<ArgumentNullException> (() => new NtlmAuthenticateMessage (NtlmNegotiate, NtlmChallenge, null, "password", "domain", "workstation"));
			Assert.Throws<ArgumentNullException> (() => new NtlmAuthenticateMessage (NtlmNegotiate, NtlmChallenge, "username", null, "domain", "workstation"));

			Assert.Throws<ArgumentNullException> (() => new NtlmAuthenticateMessage (null, 0, 16));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmAuthenticateMessage (new byte[8], 0, 8));
			Assert.Throws<ArgumentOutOfRangeException> (() => new NtlmAuthenticateMessage (new byte[8], -1, 8));
			Assert.Throws<ArgumentException> (() => new NtlmAuthenticateMessage (badMessageData, 0, badMessageData.Length));

			Assert.DoesNotThrow (() => NtlmAuthenticate.ClientChallenge = null);
			Assert.Throws<ArgumentException> (() => NtlmAuthenticate.ClientChallenge = new byte[9]);
		}

		[Test]
		// Example from http://www.innovation.ch/java/ntlm.html
		public void TestDecodeJavaExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x03, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00, 0x72, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00, 0x8a, 0x00, 0x00, 0x00, 0x14, 0x00, 0x14, 0x00, 0x40, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00, 0x54, 0x00, 0x00, 0x00, 0x12, 0x00, 0x12, 0x00, 0x60, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xa2, 0x00, 0x00, 0x00, 0x01, 0x82, 0x00, 0x00, 0x55, 0x00, 0x52, 0x00, 0x53, 0x00, 0x41, 0x00, 0x2d, 0x00, 0x4d, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x4f, 0x00, 0x52, 0x00, 0x5a, 0x00, 0x61, 0x00, 0x70, 0x00, 0x68, 0x00, 0x6f, 0x00, 0x64, 0x00, 0x4c, 0x00, 0x49, 0x00, 0x47, 0x00, 0x48, 0x00, 0x54, 0x00, 0x43, 0x00, 0x49, 0x00, 0x54, 0x00, 0x59, 0x00, 0xad, 0x87, 0xca, 0x6d, 0xef, 0xe3, 0x46, 0x85, 0xb9, 0xc4, 0x3c, 0x47, 0x7a, 0x8c, 0x42, 0xd6, 0x00, 0x66, 0x7d, 0x68, 0x92, 0xe7, 0xe8, 0x97, 0xe0, 0xe0, 0x0d, 0xe3, 0x10, 0x4a, 0x1b, 0xf2, 0x05, 0x3f, 0x07, 0xc7, 0xdd, 0xa8, 0x2d, 0x3c, 0x48, 0x9a, 0xe9, 0x89, 0xe1, 0xb0, 0x00, 0xd3 };
			var NtlmAuthenticate = new NtlmAuthenticateMessage (rawData, 0, rawData.Length);

			Assert.That (NtlmAuthenticate.Type, Is.EqualTo (3), "Type");
			Assert.That (NtlmAuthenticate.Flags, Is.EqualTo ((NtlmFlags) 0x8201), "Flags");
			Assert.That (NtlmAuthenticate.Domain, Is.EqualTo ("URSA-MINOR"), "Domain");
			Assert.That (NtlmAuthenticate.Workstation, Is.EqualTo ("LIGHTCITY"), "Workstation");
			Assert.That (NtlmAuthenticate.UserName, Is.EqualTo ("Zaphod"), "UserName");
			Assert.That (NtlmAuthenticate.Password, Is.Null, "Password");

			Assert.That (BitConverter.ToString (NtlmAuthenticate.LmChallengeResponse), Is.EqualTo ("AD-87-CA-6D-EF-E3-46-85-B9-C4-3C-47-7A-8C-42-D6-00-66-7D-68-92-E7-E8-97"), "LmChallengeResponse");
			Assert.That (BitConverter.ToString (NtlmAuthenticate.NtChallengeResponse), Is.EqualTo ("E0-E0-0D-E3-10-4A-1B-F2-05-3F-07-C7-DD-A8-2D-3C-48-9A-E9-89-E1-B0-00-D3"), "NtChallengeResponse");
		}

		[Test]
		// Example from http://davenport.sourceforge.net/ntlm.html#NtlmAuthenticateMessageExample
		public void TestDecodeDavenportExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x03, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00, 0x6a, 0x00, 0x00, 0x00, 0x18, 0x00, 0x18, 0x00, 0x82, 0x00, 0x00, 0x00, 0x0c, 0x00, 0x0c, 0x00, 0x40, 0x00, 0x00, 0x00, 0x08, 0x00, 0x08, 0x00, 0x4c, 0x00, 0x00, 0x00, 0x16, 0x00, 0x16, 0x00, 0x54, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x9a, 0x00, 0x00, 0x00, 0x01, 0x02, 0x00, 0x00, 0x44, 0x00, 0x4f, 0x00, 0x4d, 0x00, 0x41, 0x00, 0x49, 0x00, 0x4e, 0x00, 0x75, 0x00, 0x73, 0x00, 0x65, 0x00, 0x72, 0x00, 0x57, 0x00, 0x4f, 0x00, 0x52, 0x00, 0x4b, 0x00, 0x53, 0x00, 0x54, 0x00, 0x41, 0x00, 0x54, 0x00, 0x49, 0x00, 0x4f, 0x00, 0x4e, 0x00, 0xc3, 0x37, 0xcd, 0x5c, 0xbd, 0x44, 0xfc, 0x97, 0x82, 0xa6, 0x67, 0xaf, 0x6d, 0x42, 0x7c, 0x6d, 0xe6, 0x7c, 0x20, 0xc2, 0xd3, 0xe7, 0x7c, 0x56, 0x25, 0xa9, 0x8c, 0x1c, 0x31, 0xe8, 0x18, 0x47, 0x46, 0x6b, 0x29, 0xb2, 0xdf, 0x46, 0x80, 0xf3, 0x99, 0x58, 0xfb, 0x8c, 0x21, 0x3a, 0x9c, 0xc6 };
			NtlmAuthenticateMessage NtlmAuthenticate = new NtlmAuthenticateMessage (rawData, 0, rawData.Length);

			Assert.That (NtlmAuthenticate.Type, Is.EqualTo (3), "Type");
			Assert.That (NtlmAuthenticate.Flags, Is.EqualTo ((NtlmFlags) 0x201), "Flags");
			Assert.That (NtlmAuthenticate.Domain, Is.EqualTo ("DOMAIN"), "Domain");
			Assert.That (NtlmAuthenticate.Workstation, Is.EqualTo ("WORKSTATION"), "Workstation");
			Assert.That (NtlmAuthenticate.UserName, Is.EqualTo ("user"), "UserName");
			Assert.That (NtlmAuthenticate.Password, Is.Null, "Password");

			Assert.That (BitConverter.ToString (NtlmAuthenticate.LmChallengeResponse), Is.EqualTo ("C3-37-CD-5C-BD-44-FC-97-82-A6-67-AF-6D-42-7C-6D-E6-7C-20-C2-D3-E7-7C-56"), "LmChallengeResponse");
			Assert.That (BitConverter.ToString (NtlmAuthenticate.NtChallengeResponse), Is.EqualTo ("25-A9-8C-1C-31-E8-18-47-46-6B-29-B2-DF-46-80-F3-99-58-FB-8C-21-3A-9C-C6"), "NtChallengeResponse");
		}
	}
}
