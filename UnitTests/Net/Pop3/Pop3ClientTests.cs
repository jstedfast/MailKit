//
// Pop3ClientTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
		readonly Pop3Capabilities LangCapa1 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
		    Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay | Pop3Capabilities.Top |
		    Pop3Capabilities.UIDL | Pop3Capabilities.Sasl | Pop3Capabilities.UTF8 |
		    Pop3Capabilities.UTF8User | Pop3Capabilities.Lang | Pop3Capabilities.Apop;
		readonly Pop3Capabilities LangCapa2 = Pop3Capabilities.User | Pop3Capabilities.ResponseCodes |
		    Pop3Capabilities.Pipelining | Pop3Capabilities.Expire | Pop3Capabilities.LoginDelay |
		    Pop3Capabilities.Top | Pop3Capabilities.UIDL | Pop3Capabilities.Lang | Pop3Capabilities.Apop;

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
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((SaslMechanism) null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ((SaslMechanism) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((ICredentials) null));
				Assert.Throws<ArgumentNullException> (async () => await client.AuthenticateAsync ((ICredentials) null));
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

				Assert.Throws<ArgumentNullException> (() => client.SetLanguage (null));
				Assert.Throws<ArgumentNullException> (async () => await client.SetLanguageAsync (null));
				Assert.Throws<ArgumentException> (() => client.SetLanguage (string.Empty));
				Assert.Throws<ArgumentException> (async () => await client.SetLanguageAsync (string.Empty));
			}
		}

		static Socket Connect (string host, int port)
		{
			var ipAddresses = Dns.GetHostAddresses (host);
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					socket.Connect (ipAddresses[i], port);
					break;
				} catch {
					socket.Dispose ();
					socket = null;
				}
			}

			return socket;
		}

		[Test]
		public void TestSslHandshakeExceptions ()
		{
			using (var client = new Pop3Client ()) {
				Socket socket;

				Assert.Throws<SslHandshakeException> (() => client.Connect ("www.gmail.com", 80, true));
				Assert.Throws<SslHandshakeException> (async () => await client.ConnectAsync ("www.gmail.com", 80, true));

				socket = Connect ("www.gmail.com", 80);
				Assert.Throws<SslHandshakeException> (() => client.Connect (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				socket = Connect ("www.gmail.com", 80);
				Assert.Throws<SslHandshakeException> (async () => await client.ConnectAsync (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));
			}
		}

		[Test]
		public void TestInvalidStateExceptions ()
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
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageUidsAsync ());
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageUidAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.DeleteMessageAsync (0));

				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				Assert.Throws<AuthenticationException> (() => client.Authenticate ("username", "password"));
				Assert.IsTrue (client.IsConnected, "AuthenticationException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageSizes ());
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageSize (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageUids ());
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageUid (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessage (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetMessageHeaders (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetStream (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetStreams (0, 1));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.GetStreams (new int[] { 0 }));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (() => client.DeleteMessage (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestInvalidStateExceptionsAsync ()
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
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageUidsAsync ());
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageUidAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamAsync (0));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
				Assert.Throws<ServiceNotConnectedException> (async () => await client.DeleteMessageAsync (0));

				try {
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, false));
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

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageSizeAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageUidsAsync ());
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageUidAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetMessageHeadersAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetStreamAsync (0));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetStreamsAsync (0, 1));
				Assert.IsTrue (client.IsConnected, "ServiceNotAuthenticatedException should not cause a disconnect.");

				Assert.Throws<ServiceNotAuthenticatedException> (async () => await client.GetStreamsAsync (new int[] { 0 }));
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
		public void TestConnect ()
		{
			using (var client = new Pop3Client ()) {
				client.Connect ("pop.gmail.com", 0, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");

				var socket = Connect ("pop.gmail.com", 995);
				client.Connect (socket, "smtp.gmail.com", 995, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");

				//var uri = new Uri ("pop3://pop.mail.yahoo.com/?starttls=always");
				//client.Connect (uri);
				//Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				//Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				//Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				//client.Disconnect (true);
				//Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				//Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
			}
		}

		[Test]
		public async void TestConnectAsync ()
		{
			using (var client = new Pop3Client ()) {
				await client.ConnectAsync ("pop.gmail.com", 0, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");

				var socket = Connect ("pop.gmail.com", 995);
				await client.ConnectAsync (socket, "pop.gmail.com", 995, SecureSocketOptions.SslOnConnect);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");

				//var uri = new Uri ("pop3://pop.gmail.com/?starttls=always");
				//await client.ConnectAsync (uri);
				//Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				//Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				//Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				//await client.DisconnectAsync (true);
				//Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				//Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
			}
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
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, false));
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
					client.Authenticate ("username", "password");
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
					var message = client.GetMessage (0);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestBasicPop3ClientAsync ()
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
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, false));
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
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ComcastCapa1, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);
				Assert.AreEqual (31, client.ExpirePolicy);

				try {
					client.Authenticate ("username", "password");
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
					var message = client.GetMessage (0);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestBasicPop3ClientUnixLineEndingsAsync ()
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
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, true));
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
		public void TestSaslAuthentication ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "exchange.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (new Uri ("pop://localhost"), credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (7, client.Count, "Expected 7 messages");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestSaslAuthenticationAsync ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "exchange.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (new Uri ("pop://localhost"), credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (7, client.Count, "Expected 7 messages");

				try {
					await client.DisconnectAsync (true);
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
			commands.Add (new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL\r\n", "exchange.uidl.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "exchange.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				// Note: remove these auth mechanisms to force LOGIN auth
				client.AuthenticationMechanisms.Remove ("GSSAPI");
				client.AuthenticationMechanisms.Remove ("NTLM");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (7, client.Count, "Expected 7 messages");

				try {
					var uids = client.GetMessageUids ();
					Assert.AreEqual (7, uids.Count, "Expected 7 uids");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageUids: {0}", ex);
				}

				try {
					var message = client.GetMessage (0);
					// TODO: assert that the message is byte-identical to what we expect
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessage: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestExchangePop3ClientAsync ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "exchange.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH LOGIN\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("dXNlcm5hbWU=\r\n", "exchange.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("cGFzc3dvcmQ=\r\n", "exchange.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "exchange.capa.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "exchange.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL\r\n", "exchange.uidl.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "exchange.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "exchange.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				// Note: remove these auth mechanisms to force LOGIN auth
				client.AuthenticationMechanisms.Remove ("GSSAPI");
				client.AuthenticationMechanisms.Remove ("NTLM");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (ExchangeCapa, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("GSSAPI"), "Expected SASL GSSAPI auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

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
		public void TestGMailPop3Client ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL 2\r\n", "gmail.uidl2.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL 3\r\n", "gmail.uidl3.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST\r\n", "gmail.list.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST 1\r\n", "gmail.list1.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST 2\r\n", "gmail.list2.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST 3\r\n", "gmail.list3.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.top.txt"));
			commands.Add (new Pop3ReplayCommand ("TOP 1 0\r\nTOP 2 0\r\nTOP 3 0\r\n", "gmail.top123.txt"));
			commands.Add (new Pop3ReplayCommand ("TOP 1 0\r\nTOP 2 0\r\nTOP 3 0\r\n", "gmail.top123.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("NOOP\r\n", "gmail.noop.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, false));
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
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailCapa2, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);

				Assert.AreEqual (3, client.Count, "Expected 3 messages");

				var uids = client.GetMessageUids ();
				Assert.AreEqual (3, uids.Count);
				Assert.AreEqual ("101", uids[0]);
				Assert.AreEqual ("102", uids[1]);
				Assert.AreEqual ("103", uids[2]);

				for (int i = 0; i < 3; i++) {
					var uid = client.GetMessageUid (i);

					Assert.AreEqual (uids[i], uid);
				}

				var sizes = client.GetMessageSizes ();
				Assert.AreEqual (3, sizes.Count);
				Assert.AreEqual (1024, sizes[0]);
				Assert.AreEqual (1025, sizes[1]);
				Assert.AreEqual (1026, sizes[2]);

				for (int i = 0; i < 3; i++) {
					var size = client.GetMessageSize (i);

					Assert.AreEqual (sizes[i], size);
				}

				try {
					var message = client.GetMessage (0);

					using (var jpeg = new MemoryStream ()) {
						var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

						attachment.Content.DecodeTo (jpeg);
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
					var messages = client.GetMessages (0, 3);

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
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
					var messages = client.GetMessages (new [] { 0, 1, 2 });

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
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
					var header = client.GetMessageHeaders (0);

					Assert.AreEqual ("Test inline image", header[HeaderId.Subject]);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageHeaders: {0}", ex);
				}

				try {
					var headers = client.GetMessageHeaders (0, 3);

					Assert.AreEqual (3, headers.Count);
					for (int i = 0; i < headers.Count; i++)
						Assert.AreEqual ("Test inline image", headers[i][HeaderId.Subject]);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageHeaders: {0}", ex);
				}

				try {
					var headers = client.GetMessageHeaders (new [] { 0, 1, 2 });

					Assert.AreEqual (3, headers.Count);
					for (int i = 0; i < headers.Count; i++)
						Assert.AreEqual ("Test inline image", headers[i][HeaderId.Subject]);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageHeaders: {0}", ex);
				}

				try {
					using (var stream = client.GetStream (0)) {
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetStream: {0}", ex);
				}

				try {
					var streams = client.GetStreams (0, 3);

					Assert.AreEqual (3, streams.Count);
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetStreams: {0}", ex);
				}

				try {
					var streams = client.GetStreams (new int[] { 0, 1, 2 });

					Assert.AreEqual (3, streams.Count);
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetStreams: {0}", ex);
				}

				try {
					client.NoOp ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in NoOp: {0}", ex);
				}

				try {
					client.DeleteMessage (0);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessage: {0}", ex);
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					client.DeleteMessages (new [] { 0, 1, 2 });
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessages: {0}", ex);
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					client.DeleteMessages (0, 3);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessages: {0}", ex);
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					client.DeleteAllMessages ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteAllMessages: {0}", ex);
				}

				try {
					client.Reset ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestGMailPop3ClientAsync ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("AUTH PLAIN\r\n", "gmail.plus.txt"));
			commands.Add (new Pop3ReplayCommand ("AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "gmail.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "gmail.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL\r\n", "gmail.uidl.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL 1\r\n", "gmail.uidl1.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL 2\r\n", "gmail.uidl2.txt"));
			commands.Add (new Pop3ReplayCommand ("UIDL 3\r\n", "gmail.uidl3.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST\r\n", "gmail.list.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST 1\r\n", "gmail.list1.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST 2\r\n", "gmail.list2.txt"));
			commands.Add (new Pop3ReplayCommand ("LIST 3\r\n", "gmail.list3.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("TOP 1 0\r\n", "gmail.top.txt"));
			commands.Add (new Pop3ReplayCommand ("TOP 1 0\r\nTOP 2 0\r\nTOP 3 0\r\n", "gmail.top123.txt"));
			commands.Add (new Pop3ReplayCommand ("TOP 1 0\r\nTOP 2 0\r\nTOP 3 0\r\n", "gmail.top123.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\n", "gmail.retr1.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("RETR 1\r\nRETR 2\r\nRETR 3\r\n", "gmail.retr123.txt"));
			commands.Add (new Pop3ReplayCommand ("NOOP\r\n", "gmail.noop.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\n", "gmail.dele.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("DELE 1\r\nDELE 2\r\nDELE 3\r\n", "gmail.dele123.txt"));
			commands.Add (new Pop3ReplayCommand ("RSET\r\n", "gmail.rset.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, false));
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

				var uids = await client.GetMessageUidsAsync ();
				Assert.AreEqual (3, uids.Count);
				Assert.AreEqual ("101", uids[0]);
				Assert.AreEqual ("102", uids[1]);
				Assert.AreEqual ("103", uids[2]);

				for (int i = 0; i < 3; i++) {
					var uid = await client.GetMessageUidAsync (i);

					Assert.AreEqual (uids[i], uid);
				}

				var sizes = await client.GetMessageSizesAsync ();
				Assert.AreEqual (3, sizes.Count);
				Assert.AreEqual (1024, sizes[0]);
				Assert.AreEqual (1025, sizes[1]);
				Assert.AreEqual (1026, sizes[2]);

				for (int i = 0; i < 3; i++) {
					var size = await client.GetMessageSizeAsync (i);

					Assert.AreEqual (sizes[i], size);
				}

				try {
					var message = await client.GetMessageAsync (0);

					using (var jpeg = new MemoryStream ()) {
						var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

						attachment.Content.DecodeTo (jpeg);
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
					var messages = await client.GetMessagesAsync (0, 3);

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
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
					var messages = await client.GetMessagesAsync (new [] { 0, 1, 2 });

					foreach (var message in messages) {
						using (var jpeg = new MemoryStream ()) {
							var attachment = message.Attachments.OfType<MimePart> ().FirstOrDefault ();

							attachment.Content.DecodeTo (jpeg);
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
					var header = await client.GetMessageHeadersAsync (0);

					Assert.AreEqual ("Test inline image", header[HeaderId.Subject]);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageHeaders: {0}", ex);
				}

				try {
					var headers = await client.GetMessageHeadersAsync (0, 3);

					Assert.AreEqual (3, headers.Count);
					for (int i = 0; i < headers.Count; i++)
						Assert.AreEqual ("Test inline image", headers[i][HeaderId.Subject]);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageHeaders: {0}", ex);
				}

				try {
					var headers = await client.GetMessageHeadersAsync (new [] { 0, 1, 2 });

					Assert.AreEqual (3, headers.Count);
					for (int i = 0; i < headers.Count; i++)
						Assert.AreEqual ("Test inline image", headers[i][HeaderId.Subject]);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetMessageHeaders: {0}", ex);
				}

				try {
					using (var stream = await client.GetStreamAsync (0)) {
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetStream: {0}", ex);
				}

				try {
					var streams = await client.GetStreamsAsync (0, 3);

					Assert.AreEqual (3, streams.Count);
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetStreams: {0}", ex);
				}

				try {
					var streams = await client.GetStreamsAsync (new int[] { 0, 1, 2 });

					Assert.AreEqual (3, streams.Count);
					for (int i = 0; i < 3; i++) {
						streams[i].Dispose ();
					}
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in GetStreams: {0}", ex);
				}

				try {
					await client.NoOpAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in NoOp: {0}", ex);
				}

				try {
					await client.DeleteMessageAsync (0);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessage: {0}", ex);
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					await client.DeleteMessagesAsync (new [] { 0, 1, 2 });
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessages: {0}", ex);
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					await client.DeleteMessagesAsync (0, 3);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteMessages: {0}", ex);
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
				}

				try {
					await client.DeleteAllMessagesAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in DeleteAllMessages: {0}", ex);
				}

				try {
					await client.ResetAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Reset: {0}", ex);
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
		public void TestLangExtension ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "lang.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"));
			commands.Add (new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "lang.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("LANG\r\n", "lang.getlang.txt"));
			commands.Add (new Pop3ReplayCommand ("LANG en\r\n", "lang.setlang.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					client.ReplayConnect ("localhost", new Pop3ReplayStream (commands, false, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (LangCapa1, client.Capabilities);
				Assert.AreEqual (2, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					client.EnableUTF8 ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in EnableUTF8: {0}", ex);
				}

				// Note: remove the XOAUTH2 auth mechanism to force PLAIN auth
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (LangCapa2, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);

				Assert.AreEqual (3, client.Count, "Expected 3 messages");

				var languages = client.GetLanguages ();
				Assert.AreEqual (6, languages.Count);
				Assert.AreEqual ("en", languages[0].Language);
				Assert.AreEqual ("English", languages[0].Description);
				Assert.AreEqual ("en-boont", languages[1].Language);
				Assert.AreEqual ("English Boontling dialect", languages[1].Description);
				Assert.AreEqual ("de", languages[2].Language);
				Assert.AreEqual ("Deutsch", languages[2].Description);
				Assert.AreEqual ("it", languages[3].Language);
				Assert.AreEqual ("Italiano", languages[3].Description);
				Assert.AreEqual ("es", languages[4].Language);
				Assert.AreEqual ("Espanol", languages[4].Description);
				Assert.AreEqual ("sv", languages[5].Language);
				Assert.AreEqual ("Svenska", languages[5].Description);

				client.SetLanguage ("en");

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Disconnect: {0}", ex);
				}

				Assert.IsFalse (client.IsConnected, "Failed to disconnect");
			}
		}

		[Test]
		public async void TestLangExtensionAsync ()
		{
			var commands = new List<Pop3ReplayCommand> ();
			commands.Add (new Pop3ReplayCommand ("", "lang.greeting.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "lang.capa1.txt"));
			commands.Add (new Pop3ReplayCommand ("UTF8\r\n", "lang.utf8.txt"));
			commands.Add (new Pop3ReplayCommand ("APOP username d99894e8445daf54c4ce781ef21331b7\r\n", "lang.auth.txt"));
			commands.Add (new Pop3ReplayCommand ("CAPA\r\n", "lang.capa2.txt"));
			commands.Add (new Pop3ReplayCommand ("STAT\r\n", "lang.stat.txt"));
			commands.Add (new Pop3ReplayCommand ("LANG\r\n", "lang.getlang.txt"));
			commands.Add (new Pop3ReplayCommand ("LANG en\r\n", "lang.setlang.txt"));
			commands.Add (new Pop3ReplayCommand ("QUIT\r\n", "gmail.quit.txt"));

			using (var client = new Pop3Client ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new Pop3ReplayStream (commands, true, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (LangCapa1, client.Capabilities);
				Assert.AreEqual (2, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				try {
					await client.EnableUTF8Async ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in EnableUTF8: {0}", ex);
				}

				// Note: remove the XOAUTH2 auth mechanism to force PLAIN auth
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (LangCapa2, client.Capabilities);
				Assert.AreEqual (0, client.AuthenticationMechanisms.Count);

				Assert.AreEqual (3, client.Count, "Expected 3 messages");

				var languages = await client.GetLanguagesAsync ();
				Assert.AreEqual (6, languages.Count);
				Assert.AreEqual ("en", languages[0].Language);
				Assert.AreEqual ("English", languages[0].Description);
				Assert.AreEqual ("en-boont", languages[1].Language);
				Assert.AreEqual ("English Boontling dialect", languages[1].Description);
				Assert.AreEqual ("de", languages[2].Language);
				Assert.AreEqual ("Deutsch", languages[2].Description);
				Assert.AreEqual ("it", languages[3].Language);
				Assert.AreEqual ("Italiano", languages[3].Description);
				Assert.AreEqual ("es", languages[4].Language);
				Assert.AreEqual ("Espanol", languages[4].Description);
				Assert.AreEqual ("sv", languages[5].Language);
				Assert.AreEqual ("Svenska", languages[5].Description);

				await client.SetLanguageAsync ("en");

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
