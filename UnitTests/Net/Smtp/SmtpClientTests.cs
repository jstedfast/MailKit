//
// SmtpClientTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.Threading;
using System.Collections.Generic;

using NUnit.Framework;

using MailKit.Net.Smtp;
using MailKit;
using MimeKit;

namespace UnitTests.Net.Smtp {
	[TestFixture]
	public class SmtpClientTests
	{
		MimeMessage CreateSimpleMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body."
			};

			return message;
		}

		MimeMessage CreateEightBitMessage ()
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Sender Name", "sender@example.com"));
			message.To.Add (new MailboxAddress ("Recipient Name", "recipient@example.com"));
			message.Subject = "This is a test...";

			message.Body = new TextPart ("plain") {
				Text = "This is the message body with some unicode unicode: ☮ ☯"
			};

			return message;
		}

		[Test]
		public void TestBasicFunctionality ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage (), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestEightBitMime ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "comcast-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateEightBitMessage (), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestPipelining ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo+pipelining.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com> BODY=8BITMIME\r\nRCPT TO:<recipient@example.com>\r\n", "pipelined-mail-from-rcpt-to.txt"));
			commands.Add (new SmtpReplayCommand ("DATA\r\n", "comcast-data.txt"));
			commands.Add (new SmtpReplayCommand (".\r\n", "comcast-data-done.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateEightBitMessage (), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Send: {0}", ex);
				}

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestMailFromMailboxUnavailable ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage (), CancellationToken.None);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.SenderNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestRcptToMailboxUnavailable ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("AUTH PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "comcast-auth-plain.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "comcast-mail-from.txt"));
			commands.Add (new SmtpReplayCommand ("RCPT TO:<recipient@example.com>\r\n", "mailbox-unavailable.txt"));
			commands.Add (new SmtpReplayCommand ("RSET\r\n", "comcast-rset.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Send (CreateSimpleMessage (), CancellationToken.None);
					Assert.Fail ("Expected an SmtpException");
				} catch (SmtpCommandException sex) {
					Assert.AreEqual (sex.ErrorCode, SmtpErrorCode.RecipientNotAccepted, "Unexpected SmtpErrorCode");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestUnauthorizedAccessException ()
		{
			var commands = new List<SmtpReplayCommand> ();
			commands.Add (new SmtpReplayCommand ("", "comcast-greeting.txt"));
			commands.Add (new SmtpReplayCommand ("EHLO [127.0.0.1]\r\n", "comcast-ehlo.txt"));
			commands.Add (new SmtpReplayCommand ("MAIL FROM:<sender@example.com>\r\n", "auth-required.txt"));
			commands.Add (new SmtpReplayCommand ("QUIT\r\n", "comcast-quit.txt"));

			using (var client = new SmtpClient ()) {
				try {
					client.ReplayConnect ("localhost", new SmtpReplayStream (commands), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Authentication), "Failed to detect AUTH extension");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Failed to detect the LOGIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Failed to detect the PLAIN auth mechanism");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EightBitMime), "Failed to detect 8BITMIME extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.EnhancedStatusCodes), "Failed to detect ENHANCEDSTATUSCODES extension");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.Size), "Failed to detect SIZE extension");
				Assert.AreEqual (36700160, client.MaxSize, "Failed to parse SIZE correctly");

				Assert.IsTrue (client.Capabilities.HasFlag (SmtpCapabilities.StartTLS), "Failed to detect STARTTLS extension");

				try {
					client.Send (CreateSimpleMessage (), CancellationToken.None);
					Assert.Fail ("Expected an UnauthorizedAccessException");
				} catch (UnauthorizedAccessException) {
					// this is the expected exception
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Send: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Expected the client to still be connected");

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}
	}
}

