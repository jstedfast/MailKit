//
// ImapAuthenticationSecretDetectorTests.cs
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
using System.Text;
using System.Collections.Generic;

using NUnit.Framework;

using MailKit;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapAuthenticationSecretDetectorTests
	{
		[Test]
		public void TestEmptyCommand ()
		{
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = new byte[0];

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (0, secrets.Count, "# of secrets");
		}

		[Test]
		public void TestNonAuthCommand ()
		{
			string command = string.Format ("A00000000 APPEND INBOX (\\Seen) \"{0}\" {{4096}}\r\n", ImapUtils.FormatInternalDate (DateTimeOffset.Now));
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (0, secrets.Count, "# of secrets");
		}

		[Test]
		public void TestNotIsAuthenticating ()
		{
			const string command = "A00000000 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n";
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (0, secrets.Count, "# of secrets");
		}

		[Test]
		public void TestLoginCommand ()
		{
			const string command = "A00000000 LOGIN username password\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (2, secrets.Count, "# of secrets");
			Assert.AreEqual (userIndex, secrets[0].StartIndex, "UserName StartIndex");
			Assert.AreEqual (8, secrets[0].Length, "UserName Length");
			Assert.AreEqual (passwdIndex, secrets[1].StartIndex, "Password StartIndex");
			Assert.AreEqual (8, secrets[1].Length, "Password Length");
		}

		[Test]
		public void TestLoginCommandBitByBit ()
		{
			const string command = "A00000000 LOGIN username password\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if ((index >= userIndex && index < userIndex + 8) || (index >= passwdIndex && index < passwdIndex + 8)) {
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
		public void TestLoginCommandQStrings ()
		{
			const string command = "A00000000 LOGIN \"username\" \"password\"\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (2, secrets.Count, "# of secrets");
			Assert.AreEqual (userIndex, secrets[0].StartIndex, "UserName StartIndex");
			Assert.AreEqual (8, secrets[0].Length, "UserName Length");
			Assert.AreEqual (passwdIndex, secrets[1].StartIndex, "Password StartIndex");
			Assert.AreEqual (8, secrets[1].Length, "Password Length");
		}

		[Test]
		public void TestLoginCommandQStringsBitByBit ()
		{
			const string command = "A00000000 LOGIN \"username\" \"password\"\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if ((index >= userIndex && index < userIndex + 8) || (index >= passwdIndex && index < passwdIndex + 8)) {
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
		public void TestLoginCommandEscapedQStrings ()
		{
			const string command = "A00000000 LOGIN \"domain\\\\username\" \"pass\\\"word\"\r\n";
			var userIndex = command.IndexOf ("domain\\\\username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("pass\\\"word", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (2, secrets.Count, "# of secrets");
			Assert.AreEqual (userIndex, secrets[0].StartIndex, "UserName StartIndex");
			Assert.AreEqual (16, secrets[0].Length, "UserName Length");
			Assert.AreEqual (passwdIndex, secrets[1].StartIndex, "Password StartIndex");
			Assert.AreEqual (10, secrets[1].Length, "Password Length");
		}

		[Test]
		public void TestLoginCommandEscapedQStringsBitByBit ()
		{
			const string command = "A00000000 LOGIN \"domain\\\\username\" \"pass\\\"word\"\r\n";
			var userIndex = command.IndexOf ("domain\\\\username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("pass\\\"word", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if ((index >= userIndex && index < userIndex + 16) || (index >= passwdIndex && index < passwdIndex + 10)) {
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
		public void TestLoginCommandLiterals ()
		{
			var detector = new ImapAuthenticationSecretDetector ();
			IList<AuthenticationSecret> secrets;
			byte[] buffer;

			detector.IsAuthenticating = true;

			buffer = Encoding.ASCII.GetBytes ("A00000000 LOGIN {8}\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (0, secrets.Count, "LOGIN # of secrets");

			buffer = Encoding.ASCII.GetBytes ("username {8}\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "username # of secrets");
			Assert.AreEqual (0, secrets[0].StartIndex, "UserName StartIndex");
			Assert.AreEqual (8, secrets[0].Length, "UserName Length");

			buffer = Encoding.ASCII.GetBytes ("password\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "password # of secrets");
			Assert.AreEqual (0, secrets[0].StartIndex, "Password StartIndex");
			Assert.AreEqual (8, secrets[0].Length, "Password Length");
		}

		[Test]
		public void TestLoginCommandLiteralsBitByBit ()
		{
			const string command = "A00000000 LOGIN {8}\r\nusername {8}\r\npassword\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if ((index >= userIndex && index < userIndex + 8) || (index >= passwdIndex && index < passwdIndex + 8)) {
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
		public void TestLoginCommandLiteralPlus ()
		{
			const string command = "A00000000 LOGIN {8+}\r\nusername {8+}\r\npassword\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (2, secrets.Count, "# of secrets");
			Assert.AreEqual (userIndex, secrets[0].StartIndex, "UserName StartIndex");
			Assert.AreEqual (8, secrets[0].Length, "UserName Length");
			Assert.AreEqual (passwdIndex, secrets[1].StartIndex, "Password StartIndex");
			Assert.AreEqual (8, secrets[1].Length, "Password Length");
		}

		[Test]
		public void TestLoginCommandLiteralPlusBitByBit ()
		{
			const string command = "A00000000 LOGIN {8+}\r\nusername {8+}\r\npassword\r\n";
			var userIndex = command.IndexOf ("username", StringComparison.Ordinal);
			var passwdIndex = command.IndexOf ("password", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
			int index = 0;

			detector.IsAuthenticating = true;

			while (index < command.Length) {
				secrets = detector.DetectSecrets (buffer, index, 1);
				if ((index >= userIndex && index < userIndex + 8) || (index >= passwdIndex && index < passwdIndex + 8)) {
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
		public void TestSaslIRAuthCommand ()
		{
			const string command = "A00000000 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n";
			var secretIndex = command.IndexOf ("AHVzZXJuYW1lAHBhc3N3b3Jk", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);

			detector.IsAuthenticating = true;

			var secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "# of secrets");
			Assert.AreEqual (secretIndex, secrets[0].StartIndex, "StartIndex");
			Assert.AreEqual (24, secrets[0].Length, "Length");
		}

		[Test]
		public void TestSaslIRAuthCommandBitByBit ()
		{
			const string command = "A00000000 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n";
			var secretIndex = command.IndexOf ("AHVzZXJuYW1lAHBhc3N3b3Jk", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
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
			var detector = new ImapAuthenticationSecretDetector ();
			IList<AuthenticationSecret> secrets;
			byte[] buffer;

			detector.IsAuthenticating = true;

			buffer = Encoding.ASCII.GetBytes ("A00000000 AUTHENTICATE LOGIN\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (0, secrets.Count, "initial # of secrets");

			buffer = Encoding.ASCII.GetBytes ("dXNlcm5hbWU=\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "# of secrets");
			Assert.AreEqual (0, secrets[0].StartIndex, "StartIndex");
			Assert.AreEqual (12, secrets[0].Length, "Length");

			buffer = Encoding.ASCII.GetBytes ("cGFzc3dvcmQ=\r\n");
			secrets = detector.DetectSecrets (buffer, 0, buffer.Length);
			Assert.AreEqual (1, secrets.Count, "# of secrets");
			Assert.AreEqual (0, secrets[0].StartIndex, "StartIndex");
			Assert.AreEqual (12, secrets[0].Length, "Length");
		}

		[Test]
		public void TestMultiLineSaslAuthCommandBitByBit ()
		{
			const string command = "A00000000 AUTHENTICATE LOGIN\r\ndXNlcm5hbWU=\r\ncGFzc3dvcmQ=\r\n";
			var secretIndex = command.IndexOf ("dXNlcm5hbWU=", StringComparison.Ordinal);
			var detector = new ImapAuthenticationSecretDetector ();
			var buffer = Encoding.ASCII.GetBytes (command);
			IList<AuthenticationSecret> secrets;
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
