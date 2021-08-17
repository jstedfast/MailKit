//
// Type1MessageTests.cs
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

using NUnit.Framework;

using MailKit.Security.Ntlm;

namespace UnitTests.Security.Ntlm {
	[TestFixture]
	public class Type1MessageTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			byte[] badMessageData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x01, 0x00, 0x00, 0x00, 0x00 };

			Assert.Throws<ArgumentNullException> (() => new Type1Message (null, 0, 16));
			Assert.Throws<ArgumentOutOfRangeException> (() => new Type1Message (new byte[8], 0, 8));
			Assert.Throws<ArgumentOutOfRangeException> (() => new Type1Message (new byte[8], -1, 8));
			Assert.Throws<ArgumentException> (() => new Type1Message (badMessageData, 0, badMessageData.Length));
		}

		[Test]
		// Example from http://www.innovation.ch/java/ntlm.html
		public void TestEncodeJavaExample ()
		{
			var type1 = new Type1Message (NtlmFlags.NegotiateUnicode | NtlmFlags.NegotiateOem | NtlmFlags.RequestTarget | NtlmFlags.NegotiateNtlm | NtlmFlags.NegotiateAlwaysSign, "Ursa-Minor", "LightCity");

			Assert.AreEqual (1, type1.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0xb207, type1.Flags, "Flags");
			Assert.AreEqual ("4E-54-4C-4D-53-53-50-00-01-00-00-00-07-B2-00-00-0A-00-0A-00-29-00-00-00-09-00-09-00-20-00-00-00-4C-49-47-48-54-43-49-54-59-55-52-53-41-2D-4D-49-4E-4F-52", BitConverter.ToString (type1.Encode ()), "Encode");
		}

		[Test]
		// Example from http://www.innovation.ch/java/ntlm.html
		public void TestDecodeJavaExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00, 0x03, 0xb2, 0x00, 0x00, 0x0a, 0x00, 0x0a, 0x00, 0x29, 0x00, 0x00, 0x00, 0x09, 0x00, 0x09, 0x00, 0x20, 0x00, 0x00, 0x00, 0x4c, 0x49, 0x47, 0x48, 0x54, 0x43, 0x49, 0x54, 0x59, 0x55, 0x52, 0x53, 0x41, 0x2d, 0x4d, 0x49, 0x4e, 0x4f, 0x52 };
			var type1 = new Type1Message (rawData, 0, rawData.Length);

			Assert.AreEqual (1, type1.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0xb203, type1.Flags, "Flags");
			Assert.AreEqual ("URSA-MINOR", type1.Domain, "Domain");
			Assert.AreEqual ("LIGHTCITY", type1.Workstation, "Workstation");
		}

		[Test]
		// Example from http://davenport.sourceforge.net/ntlm.html#type1MessageExample
		public void TestDecodeDavenportExample ()
		{
			byte[] rawData = { 0x4e, 0x54, 0x4c, 0x4d, 0x53, 0x53, 0x50, 0x00, 0x01, 0x00, 0x00, 0x00, 0x07, 0x32, 0x00, 0x00, 0x06, 0x00, 0x06, 0x00, 0x2b, 0x00, 0x00, 0x00, 0x0b, 0x00, 0x0b, 0x00, 0x20, 0x00, 0x00, 0x00, 0x57, 0x4f, 0x52, 0x4b, 0x53, 0x54, 0x41, 0x54, 0x49, 0x4f, 0x4e, 0x44, 0x4f, 0x4d, 0x41, 0x49, 0x4e };
			var type1 = new Type1Message (rawData, 0, rawData.Length);

			Assert.AreEqual (1, type1.Type, "Type");
			Assert.AreEqual ((NtlmFlags) 0x3207, type1.Flags, "Flags");
			Assert.AreEqual ("DOMAIN", type1.Domain, "Domain");
			Assert.AreEqual ("WORKSTATION", type1.Workstation, "Workstation");
		}
	}
}
