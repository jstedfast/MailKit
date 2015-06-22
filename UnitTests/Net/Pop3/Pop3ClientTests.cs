//
// Pop3ClientTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;

using NUnit.Framework;

using MimeKit;
using MailKit;
using MailKit.Net.Pop3;
using MailKit.Security;

namespace UnitTests.Net.Pop3 {
	[TestFixture]
	public class Pop3ClientTests
	{
		readonly Pop3Capabilities ComcastCapa1 = Pop3Capabilities.Expire | Pop3Capabilities.StartTLS |
			Pop3Capabilities.Top | Pop3Capabilities.UIDL | Pop3Capabilities.User;
		readonly Pop3Capabilities ComcastCapa2 = Pop3Capabilities.Expire | Pop3Capabilities.StartTLS |
			Pop3Capabilities.Sasl | Pop3Capabilities.Top | Pop3Capabilities.UIDL | Pop3Capabilities.User;
		readonly Pop3Capabilities ExchangeCapa = Pop3Capabilities.Sasl | Pop3Capabilities.Top |
			Pop3Capabilities.UIDL | Pop3Capabilities.User;
		readonly Pop3Capabilities GMailCapa1 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
			Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay | Pop3Capabilities.Top |
			Pop3Capabilities.UIDL | Pop3Capabilities.Sasl;
		readonly Pop3Capabilities GMailCapa2 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
			Pop3Capabilities.Pipelining | Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay |
			Pop3Capabilities.Top | Pop3Capabilities.UIDL;

		static string HexEncode (byte[] digest)
		{
			var hex = new StringBuilder ();

			for (int i = 0; i < digest.Length; i++)
				hex.Append (digest[i].ToString ("x2"));

			return hex.ToString ();
		}

		[Test]
		public void TestBasicPop3Client ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "comcast.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"));
			commands.Add (new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "comcast.stat1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "comcast.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ComcastCapa2, client.Capabilities);
				Assert.AreEqual ("ZimbraInc", client.Implementation);
				Assert.AreEqual (2, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.AreEqual (-1, client.ExpirePolicy);

				Assert.AreEqual (1, client.Count, "Expected 1 message");

				try {
					var message = client.GetMessage (0, CancellationToken.None);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
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
		public void TestBasicPop3ClientUnixLineEndings ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "comcast.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"));
			commands.Add (new Pop3ReplayCommand ("PASS password\r\n", "comcast.ok.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "comcast.stat1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "comcast.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, true), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ComcastCapa2, client.Capabilities);
				Assert.AreEqual ("ZimbraInc", client.Implementation);
				Assert.AreEqual (2, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("X-ZIMBRA"), "Expected SASL X-ZIMBRA auth mechanism");
				Assert.AreEqual (-1, client.ExpirePolicy);

				Assert.AreEqual (1, client.Count, "Expected 1 message");

				try {
					var message = client.GetMessage (0, CancellationToken.None);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
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
		public void TestAuthenticationExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "comcast.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"));
			commands.Add (new Pop3ReplayCommand ("PASS password\r\n", "comcast.err.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
					Assert.Fail ("Expected AuthenticationException");
				} catch (AuthenticationException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "AuthenticationException should not cause a disconnect.");

				try {
					var sizes = client.GetMessageSizes (CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Count: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var sizes = client.GetMessageSizes (CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageSizes: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var size = client.GetMessageSize ("uid", CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageSize(uid): {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var size = client.GetMessageSize (0, CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageSize(int): {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var uids = client.GetMessageUids (CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageUids: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var uid = client.GetMessageUid (0, CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageUid: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var message = client.GetMessage ("uid", CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage(uid): {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					var message = client.GetMessage (0, CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage(int): {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					client.DeleteMessage ("uid", CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessage(uid): {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					client.DeleteMessage (0, CancellationToken.None);
					Assert.Fail ("Expected ServiceNotAuthenticatedException");
				} catch (ServiceNotAuthenticatedException) {
					// we expect this exception...
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessage(int): {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					client.Disconnect (true, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public void TestExchangePop3Client ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "exchange.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH PLAIN\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "exchange.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL\r\n", "exchange.uidl.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "exchange.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (3, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				// Note: remove these auth mechanisms to force PLAIN auth
				client.AuthenticationMechanisms.Remove ("GSSAPI");
				client.AuthenticationMechanisms.Remove ("NTLM");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (3, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				Assert.AreEqual (7, client.Count, "Expected 7 messages");

				try {
					var uids = client.GetMessageUids (CancellationToken.None);
					Assert.AreEqual (7, uids.Count, "Expected 7 uids");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageUids: {0}", ex);
				}

				try {
					var message = client.GetMessage (0, CancellationToken.None);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
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
		public void TestGMailPop3Client ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false), CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailCapa1, client.Capabilities);
				Assert.AreEqual (2, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				// Note: remove the XOAUTH2 auth mechanism to force PLAIN auth
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					var credentials = new NetworkCredential ("username", "password");
					client.Authenticate (credentials, CancellationToken.None);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailCapa2, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);

				Assert.AreEqual (3, client.Count, "Expected 3 messages");

				try {
					var message = client.GetMessage (0, CancellationToken.None);

					using (var jpeg = new MemoryStream ()) {
						var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

						attachment.ContentObject.DecodeTo (jpeg);
						jpeg.Position = 0;

						using (var md5 = new MD5CryptoServiceProvider ()) {
							var md5sum = HexEncode (md5.ComputeHash (jpeg));

							Assert.AreEqual ("5b1b8b2c9300c9cd01099f44e1155e2b", md5sum, "MD5 checksums do not match.");
						}
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					var messages = client.GetMessages (new [] { 0, 1, 2 }, CancellationToken.None);

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.ContentObject.DecodeTo (jpeg);
							jpeg.Position = 0;

							using (var md5 = new MD5CryptoServiceProvider ()) {
								var md5sum = HexEncode (md5.ComputeHash (jpeg));

								Assert.AreEqual ("5b1b8b2c9300c9cd01099f44e1155e2b", md5sum, "MD5 checksums do not match.");
							}
						}
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessages: {0}", ex);
				}

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
