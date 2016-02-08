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
using System.Net.Sockets;
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
		public void TestArgumentExceptions ()
		{
			using (var client = new Pop3Client ()) {
				var credentials = new NetworkCredential ("username", "password");
				var socket = new Socket (SocketType.Stream, ProtocolType.Tcp);

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 110, false));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 110, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 110, false));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 110, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (async () => await client.ConnectAsync (null, 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				Assert.Throws<ArgumentNullException> (() => client.Connect (null, "host", 110, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (socket, "host", 110, SecureSocketOptions.None));

				// Authenticate
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ("username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ("username", null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, credentials));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, credentials));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, "username", null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, "username", null));
			}
		}

		[Test]
		public async void TestInvalidStateExceptions ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "comcast.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "comcast.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("USER username\r\n", "comcast.ok.txt"));
			commands.Add (new Pop3ReplayCommand ("PASS password\r\n", "comcast.err.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "comcast.quit.txt"));

			using (var client = new Pop3Client ()) {
				Assert.Throws<ServiceNotConnectedException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.AuthenticateAsync (new NetworkCredential ("username", "password")));

				Assert.Throws<ServiceNotConnectedException> (async () => await client.NoOpAsync ());

				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageSizesAsync ());
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageSizeAsync ("uid"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageUidsAsync ());
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageUidAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageAsync ("uid"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync ("uid"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.DeleteMessageAsync ("uid"));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.DeleteMessageAsync (0));

				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				Assert.Throws<AuthenticationException> (async () => await client.AuthenticateAsync ("username", "password"));
				Assert.IsTrue (client.IsConnected, "AuthenticationException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageSizesAsync ());
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageSizeAsync ("uid"));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageUidsAsync ());
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageUidAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageAsync ("uid"));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageHeadersAsync ("uid"));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetStreamAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.DeleteMessageAsync ("uid"));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.DeleteMessageAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestBasicPop3Client ()
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
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy, "ExpirePolicy");
				Assert.AreEqual (100000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				try {
					await client.AuthenticateAsync ("username", "password");
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
					var message = await client.GetMessageAsync (0);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestBasicPop3ClientUnixLineEndings ()
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
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				try {
					await client.AuthenticateAsync ("username", "password");
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
					var message = await client.GetMessageAsync (0);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestExchangePop3Client ()
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
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false));
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
					await client.AuthenticateAsync ("username", "password");
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
					var uids = await client.GetMessageUidsAsync ();
					Assert.AreEqual (7, uids.Count, "Expected 7 uids");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageUids: {0}", ex);
				}

				try {
					var message = await client.GetMessageAsync (0);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestGMailPop3Client ()
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
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false));
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
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailCapa2, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);

				Assert.AreEqual (3, client.Count, "Expected 3 messages");

				try {
					var message = await client.GetMessageAsync (0);

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
					var messages = await client.GetMessagesAsync (new [] { 0, 1, 2 });

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
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}
	}
}
