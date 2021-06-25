//
// SmtpAuthenticationSecretDetectorTests.cs
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

using System.Text;
using System.Collections.Generic;

using NUnit.Framework;

using MailKit;
using MailKit.Net.Smtp;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpAuthenticationSecretDetectorTests
	{
		[Test]
		public void TestSaslIRAuthCommand ()
		{
			const string command = "AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n";
			var detector = new SmtpAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "# of secrets");
			Assert.AreEqual (11, secrets[0].StartIndex, "StartIndex");
			Assert.AreEqual (24, secrets[0].Length, "Length");
		}

		[Test]
		public void TestSaslIRAuthCommandBitByBit ()
		{
			const string command = "AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n";
			var detector = new SmtpAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			int secretIndex = "AUTH PLAIN ".Length;
			IList<AuthSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if (index >= secretIndex && command[index] != '\r' && command[index] != '\n') {
					Assert.AreEqual (1, secrets.Count, "# of secrets @ index {0}", index);
					Assert.AreEqual (index, secrets[0].StartIndex, "StartIndex");
					Assert.AreEqual (1, secrets[0].Length, "Length");
				} else {
					Assert.AreEqual (0, secrets.Count, "# of secrets @ index {0}", index);
				}
				index++;
			}
		}

		[Test]
		public void TestMultiLineSaslAuthCommand ()
		{
			var detector = new SmtpAuthenticationSecretDetector ();
			IList<AuthSecret> secrets;
			byte[] buffer;

			detector.IsAuthenticating = true;

			buffer = Encoding.ASCII.GetBytes ("AUTH PLAIN\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (0, secrets.Count, "initial # of secrets");

			buffer = Encoding.ASCII.GetBytes ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "# of secrets");
			Assert.AreEqual (0, secrets[0].StartIndex, "StartIndex");
			Assert.AreEqual (24, secrets[0].Length, "Length");
		}

		[Test]
		public void TestMultiLineSaslAuthCommandBitByBit ()
		{
			const string command = "AUTH PLAIN\r\nAHVzZXJuYW1lAHBhc3N3b3Jk\r\n";
			var detector = new SmtpAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			int secretIndex = "AUTH PLAIN\r\n".Length;
			IList<AuthSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if (index >= secretIndex && command[index] != '\r' && command[index] != '\n') {
					Assert.AreEqual (1, secrets.Count, "# of secrets @ index {0}", index);
					Assert.AreEqual (index, secrets[0].StartIndex, "StartIndex");
					Assert.AreEqual (1, secrets[0].Length, "Length");
				} else {
					Assert.AreEqual (0, secrets.Count, "# of secrets @ index {0}", index);
				}
				index++;
			}
		}
	}
}
