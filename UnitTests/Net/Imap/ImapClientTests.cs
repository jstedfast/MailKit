//
// ImapClientTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;

using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;
using MailKit.Net.Proxy;

using UnitTests.Net.Proxy;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapClientTests
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		static readonly ImapCapabilities GreetingCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Namespace | ImapCapabilities.Unselect;
		static readonly ImapCapabilities DovecotInitialCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.LiteralPlus | ImapCapabilities.SaslIR | ImapCapabilities.LoginReferrals | ImapCapabilities.Id |
			ImapCapabilities.Enable | ImapCapabilities.Idle;
		static readonly ImapCapabilities DovecotAuthenticatedCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.LiteralPlus | ImapCapabilities.SaslIR | ImapCapabilities.LoginReferrals | ImapCapabilities.Id |
			ImapCapabilities.Enable | ImapCapabilities.Idle | ImapCapabilities.Sort | ImapCapabilities.SortDisplay |
			ImapCapabilities.Thread | ImapCapabilities.MultiAppend | ImapCapabilities.Catenate | ImapCapabilities.Unselect |
			ImapCapabilities.Children | ImapCapabilities.Namespace | ImapCapabilities.UidPlus | ImapCapabilities.ListExtended |
			ImapCapabilities.I18NLevel | ImapCapabilities.CondStore | ImapCapabilities.QuickResync | ImapCapabilities.ESearch |
			ImapCapabilities.ESort | ImapCapabilities.SearchResults | ImapCapabilities.Within | ImapCapabilities.Context |
			ImapCapabilities.ListStatus | ImapCapabilities.Binary | ImapCapabilities.Move | ImapCapabilities.SpecialUse;
		static readonly ImapCapabilities GMailInitialCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Quota | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Id |
			ImapCapabilities.Children | ImapCapabilities.Unselect | ImapCapabilities.SaslIR | ImapCapabilities.XList |
			ImapCapabilities.GMailExt1;
		static readonly ImapCapabilities GMailAuthenticatedCapabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status |
			ImapCapabilities.Quota | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.Id |
			ImapCapabilities.Children | ImapCapabilities.Unselect | ImapCapabilities.UidPlus | ImapCapabilities.CondStore |
			ImapCapabilities.ESearch | ImapCapabilities.Compress | ImapCapabilities.Enable | ImapCapabilities.ListExtended |
			ImapCapabilities.ListStatus | ImapCapabilities.Move | ImapCapabilities.UTF8Accept | ImapCapabilities.XList |
			ImapCapabilities.GMailExt1 | ImapCapabilities.LiteralMinus | ImapCapabilities.AppendLimit;
		static readonly ImapCapabilities AclInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities AclAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities MetadataInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Metadata;
		static readonly ImapCapabilities MetadataAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Metadata;

		static FolderAttributes GetSpecialFolderAttribute (SpecialFolder special)
		{
			switch (special) {
			case SpecialFolder.All:       return FolderAttributes.All;
			case SpecialFolder.Archive:   return FolderAttributes.Archive;
			case SpecialFolder.Drafts:    return FolderAttributes.Drafts;
			case SpecialFolder.Flagged:   return FolderAttributes.Flagged;
			case SpecialFolder.Important: return FolderAttributes.Important;
			case SpecialFolder.Junk:      return FolderAttributes.Junk;
			case SpecialFolder.Sent:      return FolderAttributes.Sent;
			case SpecialFolder.Trash:     return FolderAttributes.Trash;
			default: throw new ArgumentOutOfRangeException ();
			}
		}

		Stream GetResourceStream (string name)
		{
			return GetType ().Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
		}

		static void GetStreamsCallback (ImapFolder folder, int index, UniqueId uid, Stream stream)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = reader.ReadToEnd ();

				Assert.AreEqual (expected, text);
			}
		}

		static async Task GetStreamsAsyncCallback (ImapFolder folder, int index, UniqueId uid, Stream stream, CancellationToken cancellationToken)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = await reader.ReadToEndAsync ();

				Assert.AreEqual (expected, text);
			}
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));

			using (var client = new ImapClient ()) {
				var credentials = new NetworkCredential ("username", "password");

				Assert.IsInstanceOf<ImapEngine> (client.SyncRoot, "SyncRoot");

				// ReplayConnect
				Assert.Throws<ArgumentNullException> (() => client.ReplayConnect (null, Stream.Null));
				Assert.Throws<ArgumentNullException> (() => client.ReplayConnect ("host", null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ReplayConnectAsync (null, Stream.Null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ReplayConnectAsync ("host", null));

				// Connect
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Uri) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Uri) null));
				Assert.Throws<ArgumentException> (() => client.Connect (new Uri ("path", UriKind.Relative)));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (new Uri ("path", UriKind.Relative)));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 143, false));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (null, 143, false));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 143, false));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 143, false));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, false));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, false));
				Assert.Throws<ArgumentNullException> (() => client.Connect (null, 143, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (null, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentException> (() => client.Connect (string.Empty, 143, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (string.Empty, 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect ("host", -1, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync ("host", -1, SecureSocketOptions.None));

				Assert.Throws<ArgumentNullException> (() => client.Connect ((Socket) null, "host", 143, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Socket) null, "host", 143, SecureSocketOptions.None));
				Assert.Throws<ArgumentNullException> (() => client.Connect ((Stream) null, "host", 143, SecureSocketOptions.None));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync ((Stream) null, "host", 143, SecureSocketOptions.None));

				using (var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
					Assert.Throws<ArgumentException> (() => client.Connect (socket, "host", 143, SecureSocketOptions.None));
					Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "host", 143, SecureSocketOptions.None));
				}

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Authenticate
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((SaslMechanism) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync ((SaslMechanism) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ((ICredentials) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync ((ICredentials) null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "password"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate ("username", null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync ("username", null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, credentials));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (null, credentials));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (null, "username", "password"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (null, "username", "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, null, "password"));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, null, "password"));
				Assert.Throws<ArgumentNullException> (() => client.Authenticate (Encoding.UTF8, "username", null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.AuthenticateAsync (Encoding.UTF8, "username", null));

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate (credentials);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				// Notify
				Assert.Throws<ArgumentNullException> (() => client.Notify (true, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.NotifyAsync (true, null));
				Assert.Throws<ArgumentException> (() => client.Notify (true, new ImapEventGroup[0]));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.NotifyAsync (true, new ImapEventGroup[0]));
				Assert.Throws<ArgumentNullException> (() => new ImapEventGroup (null, new List<ImapEvent> ()));
				Assert.Throws<ArgumentNullException> (() => new ImapEventGroup (ImapMailboxFilter.Selected, null));
				Assert.Throws<ArgumentNullException> (() => new ImapMailboxFilter.Subtree (null));
				Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Subtree (new List<IMailFolder> ()));
				Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Subtree (client.Inbox, null));
				Assert.Throws<ArgumentNullException> (() => new ImapMailboxFilter.Mailboxes (null));
				Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Mailboxes (new List<IMailFolder> ()));
				Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Mailboxes (client.Inbox, null));

				Assert.Throws<ArgumentNullException> (() => client.GetFolder ((string) null));
				Assert.Throws<ArgumentNullException> (() => client.GetFolder ((FolderNamespace) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetFolderAsync ((string) null));
				Assert.Throws<ArgumentNullException> (() => client.GetFolders (null));
				Assert.Throws<ArgumentNullException> (() => client.GetFolders (null, false));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetFoldersAsync (null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetFoldersAsync (null, false));

				Assert.AreEqual (1, client.PersonalNamespaces.Count, "Personal");
				Assert.AreEqual (0, client.SharedNamespaces.Count, "Shared");
				Assert.AreEqual (0, client.OtherNamespaces.Count, "Other");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);

				client.Disconnect (false);
			}
		}

		[Test]
		public void TestEscapeUserName ()
		{
			var escaped = ImapClient.EscapeUserName ("user:/?@&=+$%,;name");

			Assert.AreEqual ("user%3A%2F%3F%40%26%3D%2B%24%25%2C%3Bname", escaped);
		}

		[Test]
		public void TestUnescapeUserName ()
		{
			var unescaped = ImapClient.UnescapeUserName ("user%3A%2F%3F%40%26%3D%2B%24%25%2C%3Bname");

			Assert.AreEqual ("user:/?@&=+$%,;name", unescaped);

			unescaped = ImapClient.UnescapeUserName ("user%3a%2f%3f%40%26%3d%2b%24%25%2c%3bname");

			Assert.AreEqual ("user:/?@&=+$%,;name", unescaped);
		}

		static void AssertDefaultValues (string host, int port, SecureSocketOptions options, Uri expected)
		{
			ImapClient.ComputeDefaultValues (host, ref port, ref options, out Uri uri, out bool starttls);

			if (expected.PathAndQuery == "/?starttls=when-available") {
				Assert.AreEqual (SecureSocketOptions.StartTlsWhenAvailable, options, "{0}", expected);
				Assert.IsTrue (starttls, "{0}", expected);
			} else if (expected.PathAndQuery == "/?starttls=always") {
				Assert.AreEqual (SecureSocketOptions.StartTls, options, "{0}", expected);
				Assert.IsTrue (starttls, "{0}", expected);
			} else if (expected.Scheme == "imaps") {
				Assert.AreEqual (SecureSocketOptions.SslOnConnect, options, "{0}", expected);
				Assert.IsFalse (starttls, "{0}", expected);
			} else {
				Assert.AreEqual (SecureSocketOptions.None, options, "{0}", expected);
				Assert.IsFalse (starttls, "{0}", expected);
			}

			Assert.AreEqual (expected.ToString (), uri.ToString ());
			Assert.AreEqual (expected.Port, port, "{0}", expected);
		}

		[Test]
		public void TestComputeDefaultValues ()
		{
			const string host = "imap.skyfall.net";

			AssertDefaultValues (host, 0, SecureSocketOptions.None, new Uri ($"imap://{host}:143"));
			AssertDefaultValues (host, 143, SecureSocketOptions.None, new Uri ($"imap://{host}:143"));
			AssertDefaultValues (host, 993, SecureSocketOptions.None, new Uri ($"imap://{host}:993"));

			AssertDefaultValues (host, 0, SecureSocketOptions.SslOnConnect, new Uri ($"imaps://{host}:993"));
			AssertDefaultValues (host, 143, SecureSocketOptions.SslOnConnect, new Uri ($"imaps://{host}:143"));
			AssertDefaultValues (host, 993, SecureSocketOptions.SslOnConnect, new Uri ($"imaps://{host}:993"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTls, new Uri ($"imap://{host}:143/?starttls=always"));
			AssertDefaultValues (host, 143, SecureSocketOptions.StartTls, new Uri ($"imap://{host}:143/?starttls=always"));
			AssertDefaultValues (host, 993, SecureSocketOptions.StartTls, new Uri ($"imap://{host}:993/?starttls=always"));

			AssertDefaultValues (host, 0, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"imap://{host}:143/?starttls=when-available"));
			AssertDefaultValues (host, 143, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"imap://{host}:143/?starttls=when-available"));
			AssertDefaultValues (host, 993, SecureSocketOptions.StartTlsWhenAvailable, new Uri ($"imap://{host}:993/?starttls=when-available"));

			AssertDefaultValues (host, 0, SecureSocketOptions.Auto, new Uri ($"imap://{host}:143/?starttls=when-available"));
			AssertDefaultValues (host, 143, SecureSocketOptions.Auto, new Uri ($"imap://{host}:143/?starttls=when-available"));
			AssertDefaultValues (host, 993, SecureSocketOptions.Auto, new Uri ($"imaps://{host}:993"));
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
			using (var client = new ImapClient ()) {
				Socket socket;

				Assert.Throws<SslHandshakeException> (() => client.Connect ("www.gmail.com", 80, true));
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync ("www.gmail.com", 80, true));

				socket = Connect ("www.gmail.com", 80);
				Assert.Throws<SslHandshakeException> (() => client.Connect (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				socket = Connect ("www.gmail.com", 80);
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));
			}
		}

		[Test]
		public void TestConnectGMail ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "imap.gmail.com";
			int port = 993;

			using (var client = new ImapClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				client.Connect (host, 0, options);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public async Task TestConnectGMailAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "imap.gmail.com";
			int port = 993;

			using (var client = new ImapClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				await client.ConnectAsync (host, 0, options);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public void TestConnectGMailViaProxy ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "imap.gmail.com";
			int port = 993;

			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				using (var client = new ImapClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					client.ProxyClient = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
					client.ServerCertificateValidationCallback = (s, c, h, e) => true;
					client.ClientCertificates = null;
					client.LocalEndPoint = null;
					client.Timeout = 20000;

					try {
						client.Connect (host, 0, options);
					} catch (TimeoutException) {
						Assert.Inconclusive ("Timed out.");
						return;
					} catch (Exception ex) {
						Assert.Fail (ex.Message);
					}
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public async Task TestConnectGMailViaProxyAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "imap.gmail.com";
			int port = 993;

			using (var proxy = new Socks5ProxyListener ()) {
				proxy.Start (IPAddress.Loopback, 0);

				using (var client = new ImapClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};


					client.ProxyClient = new Socks5Client (proxy.IPAddress.ToString (), proxy.Port);
					client.ServerCertificateValidationCallback = (s, c, h, e) => true;
					client.ClientCertificates = null;
					client.LocalEndPoint = null;
					client.Timeout = 20000;

					try {
						await client.ConnectAsync (host, 0, options);
					} catch (TimeoutException) {
						Assert.Inconclusive ("Timed out.");
						return;
					} catch (Exception ex) {
						Assert.Fail (ex.Message);
					}
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestConnectGMailSocket ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "imap.gmail.com";
			int port = 993;

			using (var client = new ImapClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.Throws<ArgumentNullException> (() => client.Connect (socket, null, port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentException> (() => client.Connect (socket, "", port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect (socket, host, -1, SecureSocketOptions.Auto));

				client.Connect (socket, host, port, SecureSocketOptions.Auto);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (socket, host, port, SecureSocketOptions.Auto));

				client.Disconnect (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public async Task TestConnectGMailSocketAsync ()
		{
			var options = SecureSocketOptions.SslOnConnect;
			var host = "imap.gmail.com";
			int port = 993;

			using (var client = new ImapClient ()) {
				int connected = 0, disconnected = 0;

				client.Connected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
					Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
					Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
					Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (socket, null, port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "", port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync (socket, host, -1, SecureSocketOptions.Auto));

				await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto);
				Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
				Assert.IsTrue (client.IsSecure, "Expected a secure connection");
				Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
				Assert.AreEqual (1, connected, "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto));

				await client.DisconnectAsync (true);
				Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
				Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
				Assert.AreEqual (1, disconnected, "DisconnectedEvent");
			}
		}

		[Test]
		public void TestConnectGmxDe ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "imap.gmx.de";
			int port = 143;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new ImapClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"imap://{host}/?starttls=always");
					client.Connect (uri, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public async Task TestConnectGmxDeAsync ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "imap.gmx.de";
			int port = 143;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new ImapClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"imap://{host}/?starttls=always");
					await client.ConnectAsync (uri, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestConnectGmxDeSocket ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "imap.gmx.de";
			int port = 143;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new ImapClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					client.Connect (socket, host, port, options, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					client.Disconnect (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public async Task TestConnectGmxDeSocketAsync ()
		{
			var options = SecureSocketOptions.StartTls;
			var host = "imap.gmx.de";
			int port = 143;

			using (var cancel = new CancellationTokenSource (30 * 1000)) {
				using (var client = new ImapClient ()) {
					int connected = 0, disconnected = 0;

					client.Connected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "ConnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "ConnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.AreEqual (host, e.Host, "DisconnectedEventArgs.Host");
						Assert.AreEqual (port, e.Port, "DisconnectedEventArgs.Port");
						Assert.AreEqual (options, e.Options, "DisconnectedEventArgs.Options");
						Assert.IsTrue (e.IsRequested, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					await client.ConnectAsync (socket, host, port, options, cancel.Token);
					Assert.IsTrue (client.IsConnected, "Expected the client to be connected");
					Assert.IsTrue (client.IsSecure, "Expected a secure connection");
					Assert.IsFalse (client.IsAuthenticated, "Expected the client to not be authenticated");
					Assert.AreEqual (1, connected, "ConnectedEvent");

					await client.DisconnectAsync (true);
					Assert.IsFalse (client.IsConnected, "Expected the client to be disconnected");
					Assert.IsFalse (client.IsSecure, "Expected IsSecure to be false after disconnecting");
					Assert.AreEqual (1, disconnected, "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestGreetingCapabilities ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GreetingCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
			}
		}

		[Test]
		public async Task TestGreetingCapabilitiesAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GreetingCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
			}
		}

		[Test]
		public void TestByeGreeting ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("The IMAP server unexpectedly refused the connection.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestByeGreetingAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("The IMAP server unexpectedly refused the connection.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestByeGreetingWithAlert ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE [ALERT] Too many connections.\r\n")));

			using (var client = new ImapClient ()) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.AreEqual ("Too many connections.", e.Message);
					alerts++;
				};

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("Too many connections.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				Assert.AreEqual (1, alerts, "Expected 1 alert");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestByeGreetingWithAlertAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE [ALERT] Too many connections.\r\n")));

			using (var client = new ImapClient ()) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.AreEqual ("Too many connections.", e.Message);
					alerts++;
				};

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("Too many connections.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				Assert.AreEqual (1, alerts, "Expected 1 alert");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestByeGreetingWithRespText ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE Too many connections.\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("Too many connections.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestByeGreetingWithRespTextAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE Too many connections.\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("Too many connections.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestUnexpectedBye ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", Encoding.ASCII.GetBytes ("* BYE System going down for a reboot.\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Inbox.Open (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("System going down for a reboot.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Open: {0}", ex);
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnexpectedByeAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", Encoding.ASCII.GetBytes ("* BYE System going down for a reboot.\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.Inbox.OpenAsync (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("System going down for a reboot.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Open: {0}", ex);
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestUnexpectedByeAfterCapability ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* OK Yandex IMAP4rev1 at sas8-bccc92f57f23.qloud-c.yandex.net:993 ready to talk with, 2019-Oct-18 07:41:00, 0fHtH613ZiE1\r\n")));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", Encoding.ASCII.GetBytes ("* BYE Autologout; idle for too long (1)\r\n* BYE Autologout; idle for too long (2)\r\n* BYE Autologout; idle for too long (3)\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
					Assert.Fail ("Did not expect to connect");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("Autologout; idle for too long (1)", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnexpectedByeAfterCapabilityAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* OK Yandex IMAP4rev1 at sas8-bccc92f57f23.qloud-c.yandex.net:993 ready to talk with, 2019-Oct-18 07:41:00, 0fHtH613ZiE1\r\n")));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", Encoding.ASCII.GetBytes ("* BYE Autologout; idle for too long (1)\r\n* BYE Autologout; idle for too long (2)\r\n* BYE Autologout; idle for too long (3)\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
					Assert.Fail ("Did not expect to connect");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("Autologout; idle for too long (1)", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestUnexpectedByeWithAlert ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", Encoding.ASCII.GetBytes ("* BYE [ALERT] System going down for a reboot.\r\n")));

			using (var client = new ImapClient ()) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.AreEqual ("System going down for a reboot.", e.Message);
					alerts++;
				};

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					client.Inbox.Open (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("System going down for a reboot.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Open: {0}", ex);
				}

				Assert.AreEqual (1, alerts, "Expected 1 alert");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnexpectedByeWithAlertAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", Encoding.ASCII.GetBytes ("* BYE [ALERT] System going down for a reboot.\r\n")));

			using (var client = new ImapClient ()) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.AreEqual ("System going down for a reboot.", e.Message);
					alerts++;
				};

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Connect: {0}", ex);
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				try {
					await client.Inbox.OpenAsync (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.AreEqual ("System going down for a reboot.", ex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect this exception in Open: {0}", ex);
				}

				Assert.AreEqual (1, alerts, "Expected 1 alert");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestUnexpectedByeInSaslAuthenticate ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", Encoding.ASCII.GetBytes ("* BYE disconnecting\r\nA00000001 NO you are not allowed to act as a proxy server\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Expected failure");
				} catch (ImapProtocolException pex) {
					Assert.AreEqual ("you are not allowed to act as a proxy server", pex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Expected ImapProtocolException, but got: {0}", ex);
				}
			}
		}

		[Test]
		public async Task TestUnexpectedByeInSaslAuthenticateAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", Encoding.ASCII.GetBytes ("* BYE disconnecting\r\nA00000001 NO you are not allowed to act as a proxy server\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Expected failure");
				} catch (ImapProtocolException pex) {
					Assert.AreEqual ("you are not allowed to act as a proxy server", pex.Message);
				} catch (Exception ex) {
					Assert.Fail ("Expected ImapProtocolException, but got: {0}", ex);
				}
			}
		}

		[Test]
		public void TestPreAuthGreeting ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.preauth-greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "common.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LIST \"\" \"\"\r\n", "common.list-namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\"\r\n", "common.list-inbox.txt"));

			using (var client = new ImapClient ()) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsTrue (client.IsAuthenticated, "Client should be authenticated.");
				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (capabilities, client.Capabilities, "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox, inbox.Attributes, "Expected Inbox attributes to be empty.");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestPreAuthGreetingAsync ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.preauth-greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "common.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LIST \"\" \"\"\r\n", "common.list-namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\"\r\n", "common.list-inbox.txt"));

			using (var client = new ImapClient ()) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsTrue (client.IsAuthenticated, "Client should be authenticated.");
				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (capabilities, client.Capabilities, "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox, inbox.Attributes, "Expected Inbox attributes to be empty.");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestPreAuthCapabilityGreeting ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.preauth-capability-greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LIST \"\" \"\"\r\n", "common.list-namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LIST \"\" \"INBOX\"\r\n", "common.list-inbox.txt"));

			using (var client = new ImapClient ()) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsTrue (client.IsAuthenticated, "Client should be authenticated.");
				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (capabilities, client.Capabilities, "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox, inbox.Attributes, "Expected Inbox attributes to be empty.");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestPreAuthCapabilityGreetingAsync ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.preauth-capability-greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LIST \"\" \"\"\r\n", "common.list-namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LIST \"\" \"INBOX\"\r\n", "common.list-inbox.txt"));

			using (var client = new ImapClient ()) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsTrue (client.IsAuthenticated, "Client should be authenticated.");
				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (capabilities, client.Capabilities, "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox, inbox.Attributes, "Expected Inbox attributes to be empty.");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestUnicodeRespText ()
		{
			const string respText = "╟ы╩╣╙├╩┌╚и┬ы╡╟┬╝бг╧ъ╟щ╟ы┐┤";
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN username password\r\n", Encoding.UTF8.GetBytes ("A00000001 NO " + respText + "\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.AreEqual (respText, ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnicodeRespTextAsync ()
		{
			const string respText = "╟ы╩╣╙├╩┌╚и┬ы╡╟┬╝бг╧ъ╟щ╟ы┐┤";
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN username password\r\n", Encoding.UTF8.GetBytes ("A00000001 NO " + respText + "\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.AreEqual (respText, ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestInvalidUntaggedBadResponse ()
		{
			const string alertText = "Please enable IMAP access in your account settings first.";
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", Encoding.UTF8.GetBytes ("A00000000 OK [ALERT] " + alertText + "\r\n")));
			commands.Add (new ImapReplayCommand ("A00000001 CAPABILITY\r\n", Encoding.UTF8.GetBytes ("A00000001 NO [ALERT] " + alertText + "\r\n")));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", Encoding.UTF8.GetBytes ("A00000002 NO [ALERT] " + alertText + "\r\n")));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", Encoding.UTF8.GetBytes ("* BAD [ALERT] " + alertText + "\r\nA00000003 NO [ALERT] " + alertText + "\r\n")));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.AreEqual (alertText, e.Message);
					alerts++;
				};

				try {
					client.Authenticate ("username", "password");
				} catch (AuthenticationException ax) {
					Assert.AreEqual (alertText, ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (5, alerts, "Unexpected number of alerts: {0}", alerts);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestInvalidUntaggedBadResponseAsync ()
		{
			const string alertText = "Please enable IMAP access in your account settings first.";
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "common.capability-greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", Encoding.UTF8.GetBytes ("A00000000 OK [ALERT] " + alertText + "\r\n")));
			commands.Add (new ImapReplayCommand ("A00000001 CAPABILITY\r\n", Encoding.UTF8.GetBytes ("A00000001 NO [ALERT] " + alertText + "\r\n")));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", Encoding.UTF8.GetBytes ("A00000002 NO [ALERT] " + alertText + "\r\n")));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", Encoding.UTF8.GetBytes ("* BAD [ALERT] " + alertText + "\r\nA00000003 NO [ALERT] " + alertText + "\r\n")));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.AreEqual (alertText, e.Message);
					alerts++;
				};

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (AuthenticationException ax) {
					Assert.AreEqual (alertText, ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (5, alerts, "Unexpected number of alerts: {0}", alerts);

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestLogin ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN \"Indiana \\\"Han Solo\\\" Jones\" \"p@ss\\\\word\"\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try to use any SASL mechanisms
				client.AuthenticationMechanisms.Clear ();

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.Authenticate (new NetworkCredential ("Indiana \"Han Solo\" Jones", "p@ss\\word"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestLoginAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN \"Indiana \\\"Han Solo\\\" Jones\" \"p@ss\\\\word\"\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try to use any SASL mechanisms
				client.AuthenticationMechanisms.Clear ();

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.AuthenticateAsync (new NetworkCredential ("Indiana \"Han Solo\" Jones", "p@ss\\word"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestLoginSpecialCharacter ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN username \"pass%word\"\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try to use any SASL mechanisms
				client.AuthenticationMechanisms.Clear ();

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.Authenticate ("username", "pass%word");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestLoginSpecialCharacterAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LOGIN username \"pass%word\"\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try to use any SASL mechanisms
				client.AuthenticationMechanisms.Clear ();

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.AuthenticateAsync ("username", "pass%word");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestLoginDisabled ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+logindisabled.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", ImapReplayCommandResponse.NO));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities | ImapCapabilities.LoginDisabled, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.AreEqual ("AUTHENTICATE failed", ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.AreEqual ("The LOGIN command is disabled.", ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestLoginDisabledAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+logindisabled.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", ImapReplayCommandResponse.NO));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.AreEqual (GMailInitialCapabilities | ImapCapabilities.LoginDisabled, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.AreEqual ("AUTHENTICATE failed", ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.AreEqual ("The LOGIN command is disabled.", ax.Message);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestAdvancedFeatures ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" {35+}\r\n1 Memorial Dr.\r\nCambridge, MA 02142)\r\n", "common.id.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 GETQUOTAROOT INBOX\r\n", "common.getquota.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETQUOTA \"\" (MESSAGE 1000000 STORAGE 5242880)\r\n", "common.setquota.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (6, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try XOAUTH2 or PLAIN
				client.AuthenticationMechanisms.Remove ("XOAUTH2");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.Authenticate (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				client.EnableUTF8 ();

				var implementation = new ImapImplementation {
					Name = "MailKit", Version = "1.0", Vendor = "Xamarin Inc.", Address = "1 Memorial Dr.\r\nCambridge, MA 02142"
				};

				implementation = client.Identify (implementation);
				Assert.IsNotNull (implementation, "Expected a non-null ID response.");
				Assert.AreEqual ("GImap", implementation.Name);
				Assert.AreEqual ("Google, Inc.", implementation.Vendor);
				Assert.AreEqual ("http://support.google.com/mail", implementation.SupportUrl);
				Assert.AreEqual ("gmail_imap_150623.03_p1", implementation.Version);
				Assert.AreEqual ("127.0.0.1", implementation.Properties["remote-host"]);

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = inbox.GetQuota ();
				Assert.IsNotNull (quota, "Expected a non-null GETQUOTAROOT response.");
				Assert.AreEqual (personal.FullName, quota.QuotaRoot.FullName);
				Assert.AreEqual (personal, quota.QuotaRoot);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (15728640, quota.StorageLimit.Value);
				Assert.IsFalse (quota.CurrentMessageCount.HasValue);
				Assert.IsFalse (quota.MessageLimit.HasValue);

				quota = personal.SetQuota (1000000, 5242880);
				Assert.IsNotNull (quota, "Expected non-null SETQUOTA response.");
				Assert.AreEqual (1107, quota.CurrentMessageCount.Value);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (1000000, quota.MessageLimit.Value);
				Assert.AreEqual (5242880, quota.StorageLimit.Value);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestAdvancedFeaturesAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" {35+}\r\n1 Memorial Dr.\r\nCambridge, MA 02142)\r\n", "common.id.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 GETQUOTAROOT INBOX\r\n", "common.getquota.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETQUOTA \"\" (MESSAGE 1000000 STORAGE 5242880)\r\n", "common.setquota.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (6, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				// Note: Do not try XOAUTH2 or PLAIN
				client.AuthenticationMechanisms.Remove ("XOAUTH2");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				await client.EnableUTF8Async ();

				var implementation = new ImapImplementation {
					Name = "MailKit", Version = "1.0", Vendor = "Xamarin Inc.", Address = "1 Memorial Dr.\r\nCambridge, MA 02142"
				};

				implementation = await client.IdentifyAsync (implementation);
				Assert.IsNotNull (implementation, "Expected a non-null ID response.");
				Assert.AreEqual ("GImap", implementation.Name);
				Assert.AreEqual ("Google, Inc.", implementation.Vendor);
				Assert.AreEqual ("http://support.google.com/mail", implementation.SupportUrl);
				Assert.AreEqual ("gmail_imap_150623.03_p1", implementation.Version);
				Assert.AreEqual ("127.0.0.1", implementation.Properties["remote-host"]);

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = await inbox.GetQuotaAsync ();
				Assert.IsNotNull (quota, "Expected a non-null GETQUOTAROOT response.");
				Assert.AreEqual (personal.FullName, quota.QuotaRoot.FullName);
				Assert.AreEqual (personal, quota.QuotaRoot);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (15728640, quota.StorageLimit.Value);
				Assert.IsFalse (quota.CurrentMessageCount.HasValue);
				Assert.IsFalse (quota.MessageLimit.HasValue);

				quota = await personal.SetQuotaAsync (1000000, 5242880);
				Assert.IsNotNull (quota, "Expected non-null SETQUOTA response.");
				Assert.AreEqual (1107, quota.CurrentMessageCount.Value);
				Assert.AreEqual (3783, quota.CurrentStorageSize.Value);
				Assert.AreEqual (1000000, quota.MessageLimit.Value);
				Assert.AreEqual (5242880, quota.StorageLimit.Value);

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestSendingStringsAsLiterals ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" {35}\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000006", "1 Memorial Dr.\r\nCambridge, MA 02142)\r\n", "common.id.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2 or PLAIN
				client.AuthenticationMechanisms.Remove ("XOAUTH2");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				client.EnableUTF8 ();

				var implementation = new ImapImplementation {
					Name = "MailKit", Version = "1.0", Vendor = "Xamarin Inc.", Address = "1 Memorial Dr.\r\nCambridge, MA 02142"
				};

				// Disable LITERAL+ and LITERAL- extensions
				client.Capabilities &= ~ImapCapabilities.LiteralPlus;
				client.Capabilities &= ~ImapCapabilities.LiteralMinus;

				implementation = client.Identify (implementation);

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestSendingStringsAsLiteralsAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" {35}\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000006", "1 Memorial Dr.\r\nCambridge, MA 02142)\r\n", "common.id.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				// Note: Do not try XOAUTH2 or PLAIN
				client.AuthenticationMechanisms.Remove ("XOAUTH2");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				await client.EnableUTF8Async ();

				var implementation = new ImapImplementation {
					Name = "MailKit", Version = "1.0", Vendor = "Xamarin Inc.", Address = "1 Memorial Dr.\r\nCambridge, MA 02142"
				};

				// Disable LITERAL+ and LITERAL- extensions
				client.Capabilities &= ~ImapCapabilities.LiteralPlus;
				client.Capabilities &= ~ImapCapabilities.LiteralMinus;

				implementation = await client.IdentifyAsync (implementation);

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestSaslAuthentication ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (6, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSaslAuthenticationAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus));
			commands.Add (new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");
				Assert.IsFalse (client.IsSecure, "IsSecure should be false.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (6, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.AreEqual (120000, client.Timeout, "Timeout");
				client.Timeout *= 2;

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismLogin (credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, authenticated, "Authenticated event was not emitted the expected number of times");
				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.SupportsQuotas, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		static void AssertFolder (IMailFolder folder, string fullName, string id, FolderAttributes attributes, bool subscribed, ulong highestmodseq, int count, int recent, uint uidnext, uint validity, int unread, ulong size)
		{
			if (subscribed)
				attributes |= FolderAttributes.Subscribed;

			Assert.AreEqual (fullName, folder.FullName, "FullName");
			Assert.AreEqual (attributes, folder.Attributes, "Attributes");
			Assert.AreEqual (subscribed, folder.IsSubscribed, "IsSubscribed");
			Assert.AreEqual (highestmodseq, folder.HighestModSeq, "HighestModSeq");
			Assert.AreEqual (count, folder.Count, "Count");
			Assert.AreEqual (recent, folder.Recent, "Recent");
			Assert.AreEqual (unread, folder.Unread, "Unread");
			Assert.AreEqual (uidnext, folder.UidNext.HasValue ? folder.UidNext.Value.Id : (uint) 0, "UidNext");
			Assert.AreEqual (validity, folder.UidValidity, "UidValidity");
			Assert.AreEqual (size, folder.Size.HasValue ? folder.Size.Value : (ulong) 0, "Size");
			Assert.AreEqual (id, folder.Id, "MailboxId");
		}

		List<ImapReplayCommand> CreateGetFoldersCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST (SUBSCRIBED) \"\" \"*\" RETURN (CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID))\r\n", "gmail.list-all.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-all-no-status.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 STATUS INBOX (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 STATUS \"[Gmail]/All Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-all-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 STATUS \"[Gmail]/Drafts\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-drafts.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 STATUS \"[Gmail]/Important\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-important.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 STATUS \"[Gmail]/Sent Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-sent-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000012 STATUS \"[Gmail]/Spam\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-spam.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 STATUS \"[Gmail]/Starred\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-starred.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 STATUS \"[Gmail]/Trash\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-trash.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 LSUB \"\" \"*\"\r\n", "gmail.lsub-all.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 STATUS INBOX (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 STATUS \"[Gmail]/All Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-all-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 STATUS \"[Gmail]/Drafts\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-drafts.txt"));
			commands.Add (new ImapReplayCommand ("A00000019 STATUS \"[Gmail]/Important\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-important.txt"));
			commands.Add (new ImapReplayCommand ("A00000020 STATUS \"[Gmail]/Sent Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-sent-mail.txt"));
			commands.Add (new ImapReplayCommand ("A00000021 STATUS \"[Gmail]/Spam\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-spam.txt"));
			commands.Add (new ImapReplayCommand ("A00000022 STATUS \"[Gmail]/Starred\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-starred.txt"));
			commands.Add (new ImapReplayCommand ("A00000023 STATUS \"[Gmail]/Trash\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-trash.txt"));
			commands.Add (new ImapReplayCommand ("A00000024 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestGetFolders ()
		{
			var commands = CreateGetFoldersCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities | ImapCapabilities.StatusSize | ImapCapabilities.ObjectID, client.Capabilities);

				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread | StatusItems.Size | StatusItems.MailboxId;
				var folders = client.GetFolders (client.PersonalNamespaces[0], all, true);
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (folders[1], "[Gmail]", null, FolderAttributes.HasChildren | FolderAttributes.NonExistent, true, 0, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (folders[3], "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (folders[4], "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (folders[6], "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (folders[7], "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (folders[8], "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				AssertFolder (client.Inbox, "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				// Now make the same query but disable LIST-STATUS
				client.Capabilities &= ~ImapCapabilities.ListStatus;
				folders = client.GetFolders (client.PersonalNamespaces[0], all, false);
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (folders[1], "[Gmail]", null, FolderAttributes.HasChildren | FolderAttributes.NonExistent, true, 0, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (folders[3], "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (folders[4], "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (folders[6], "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (folders[7], "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (folders[8], "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				AssertFolder (client.Inbox, "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				// Now make the same query but disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;
				folders = client.GetFolders (client.PersonalNamespaces[0], all, true);
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (folders[1], "[Gmail]", null, FolderAttributes.HasChildren | FolderAttributes.NonExistent, true, 0, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (folders[3], "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (folders[4], "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (folders[6], "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (folders[7], "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (folders[8], "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				AssertFolder (client.Inbox, "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestGetFoldersAsync ()
		{
			var commands = CreateGetFoldersCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities | ImapCapabilities.StatusSize | ImapCapabilities.ObjectID, client.Capabilities);

				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread | StatusItems.Size | StatusItems.MailboxId;
				var folders = await client.GetFoldersAsync (client.PersonalNamespaces[0], all, true);
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (folders[1], "[Gmail]", null, FolderAttributes.HasChildren | FolderAttributes.NonExistent, true, 0, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (folders[3], "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (folders[4], "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (folders[6], "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (folders[7], "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (folders[8], "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				AssertFolder (client.Inbox, "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				// Now make the same query but disable LIST-STATUS
				client.Capabilities &= ~ImapCapabilities.ListStatus;
				folders = await client.GetFoldersAsync (client.PersonalNamespaces[0], all, false);
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (folders[1], "[Gmail]", null, FolderAttributes.HasChildren | FolderAttributes.NonExistent, true, 0, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (folders[3], "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (folders[4], "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (folders[6], "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (folders[7], "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (folders[8], "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				AssertFolder (client.Inbox, "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				// Now make the same query but disable LIST-STATUS
				client.Capabilities &= ~ImapCapabilities.ListExtended;
				folders = await client.GetFoldersAsync (client.PersonalNamespaces[0], all, true);
				Assert.AreEqual (9, folders.Count, "Unexpected folder count.");

				AssertFolder (folders[0], "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (folders[1], "[Gmail]", null, FolderAttributes.HasChildren | FolderAttributes.NonExistent, true, 0, 0, 0, 0, 0, 0, 0);
				AssertFolder (folders[2], "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (folders[3], "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (folders[4], "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (folders[5], "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (folders[6], "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (folders[7], "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (folders[8], "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				AssertFolder (client.Inbox, "INBOX", "d0f3b017-d3ec-40aa-9bb9-66c1aeccbb24", FolderAttributes.HasNoChildren | FolderAttributes.Inbox, true, 41234, 60, 0, 410, 1, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.All), "[Gmail]/All Mail", "f668b57d-9f42-453b-b315-a18cd3eb0f85", FolderAttributes.HasNoChildren | FolderAttributes.All, true, 41234, 67, 0, 1210, 11, 3, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Drafts), "[Gmail]/Drafts", "fdacc3c7-4e20-4ca0-a0d7-4f7267187e48", FolderAttributes.HasNoChildren | FolderAttributes.Drafts, true, 41234, 0, 0, 1, 6, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Important), "[Gmail]/Important", "2a0410e1-252a-4ee8-b48d-30111cda734a", FolderAttributes.HasNoChildren | FolderAttributes.Important, true, 41234, 58, 0, 307, 9, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Sent), "[Gmail]/Sent Mail", "79da5ecd-afe4-440e-81ce-64ace69c9fbd", FolderAttributes.HasNoChildren | FolderAttributes.Sent, true, 41234, 4, 0, 7, 5, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Junk), "[Gmail]/Spam", "f5df5af8-5e11-49a5-891d-c3e05591265e", FolderAttributes.HasNoChildren | FolderAttributes.Junk, true, 41234, 0, 0, 1, 3, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Flagged), "[Gmail]/Starred", "93ad849a-2127-4c8e-ac41-594cd0a346a4", FolderAttributes.HasNoChildren | FolderAttributes.Flagged, true, 41234, 1, 0, 7, 4, 0, 1024);
				AssertFolder (client.GetFolder (SpecialFolder.Trash), "[Gmail]/Trash", "a663f6ce-4f36-434e-9f0c-7f757046a6d4", FolderAttributes.HasNoChildren | FolderAttributes.Trash, true, 41234, 0, 0, 1143, 2, 0, 1024);

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestGetQuotaNonexistantQuotaRoot ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETQUOTAROOT INBOX\r\n", "common.getquota-no-root.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" storage=0 RETURN (SUBSCRIBED CHILDREN)\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = inbox.GetQuota ();
				Assert.IsNotNull (quota, "Expected a non-null GETQUOTAROOT response.");
				Assert.IsFalse (quota.QuotaRoot.Exists);
				Assert.AreEqual ("storage=0", quota.QuotaRoot.FullName);
				Assert.AreEqual (28257, quota.CurrentStorageSize.Value);
				Assert.AreEqual (256000, quota.StorageLimit.Value);
				Assert.IsFalse (quota.CurrentMessageCount.HasValue);
				Assert.IsFalse (quota.MessageLimit.HasValue);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestGetQuotaNonexistantQuotaRootAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETQUOTAROOT INBOX\r\n", "common.getquota-no-root.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" storage=0 RETURN (SUBSCRIBED CHILDREN)\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;

				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = await inbox.GetQuotaAsync ();
				Assert.IsNotNull (quota, "Expected a non-null GETQUOTAROOT response.");
				Assert.IsFalse (quota.QuotaRoot.Exists);
				Assert.AreEqual ("storage=0", quota.QuotaRoot.FullName);
				Assert.AreEqual (28257, quota.CurrentStorageSize.Value);
				Assert.AreEqual (256000, quota.StorageLimit.Value);
				Assert.IsFalse (quota.CurrentMessageCount.HasValue);
				Assert.IsFalse (quota.MessageLimit.HasValue);

				await client.DisconnectAsync (false);
			}
		}

		static MimeMessage CreateThreadableMessage (string subject, string msgid, string references, DateTimeOffset date)
		{
			var message = new MimeMessage ();
			message.From.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.To.Add (new MailboxAddress ("Unit Tests", "unit-tests@mimekit.net"));
			message.MessageId = msgid;
			message.Subject = subject;
			message.Date = date;

			if (references != null) {
				foreach (var reference in references.Split (' '))
					message.References.Add (reference);
			}

			message.Body = new TextPart ("plain") { Text = "This is the message body.\r\n" };

			return message;
		}

		List<ImapReplayCommand> CreateDovecotCommands (out List<DateTimeOffset> internalDates, out List<MimeMessage> messages, out List<MessageFlags> flags)
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 ENABLE QRESYNC CONDSTORE\r\n", "dovecot.enable-qresync.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\" RETURN (SUBSCRIBED CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ))\r\n", "dovecot.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 CREATE UnitTests.\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "dovecot.list-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 CREATE UnitTests.Messages\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000009 LIST \"\" UnitTests.Messages\r\n", "dovecot.list-unittests-messages.txt"));

			var command = new StringBuilder ("A00000010 APPEND UnitTests.Messages");
			var now = DateTimeOffset.Now;

			internalDates = new List<DateTimeOffset> ();
			messages = new List<MimeMessage> ();
			flags = new List<MessageFlags> ();

			messages.Add (CreateThreadableMessage ("A", "<a@mimekit.net>", null, now.AddMinutes (-7)));
			messages.Add (CreateThreadableMessage ("B", "<b@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-6)));
			messages.Add (CreateThreadableMessage ("C", "<c@mimekit.net>", "<a@mimekit.net> <b@mimekit.net>", now.AddMinutes (-5)));
			messages.Add (CreateThreadableMessage ("D", "<d@mimekit.net>", "<a@mimekit.net>", now.AddMinutes (-4)));
			messages.Add (CreateThreadableMessage ("E", "<e@mimekit.net>", "<c@mimekit.net> <x@mimekit.net> <y@mimekit.net> <z@mimekit.net>", now.AddMinutes (-3)));
			messages.Add (CreateThreadableMessage ("F", "<f@mimekit.net>", "<b@mimekit.net>", now.AddMinutes (-2)));
			messages.Add (CreateThreadableMessage ("G", "<g@mimekit.net>", null, now.AddMinutes (-1)));
			messages.Add (CreateThreadableMessage ("H", "<h@mimekit.net>", null, now));

			for (int i = 0; i < messages.Count; i++) {
				var message = messages[i];
				string latin1;
				long length;

				internalDates.Add (messages[i].Date);
				flags.Add (MessageFlags.Draft);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				command.AppendFormat (" (\\Draft) \"{0}\" ", ImapUtils.FormatInternalDate (message.Date));
				command.Append ('{');
				command.AppendFormat ("{0}+", length);
				command.Append ("}\r\n");
				command.Append (latin1);
			}
			command.Append ("\r\n");
			commands.Add (new ImapReplayCommand (command.ToString (), "dovecot.multiappend.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 SELECT UnitTests.Messages (CONDSTORE)\r\n", "dovecot.select-unittests-messages.txt"));
			commands.Add (new ImapReplayCommand ("A00000012 UID STORE 1:8 +FLAGS.SILENT (\\Seen)\r\n", "dovecot.store-seen.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 UID STORE 1:3 +FLAGS.SILENT (\\Answered)\r\n", "dovecot.store-answered.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 UID STORE 8 +FLAGS.SILENT (\\Deleted)\r\n", "dovecot.store-deleted.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 UID EXPUNGE 8\r\n", "dovecot.uid-expunge.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 UID THREAD REFERENCES US-ASCII ALL\r\n", "dovecot.thread-references.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 UID THREAD ORDEREDSUBJECT US-ASCII UID 1:* ALL\r\n", "dovecot.thread-orderedsubject.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 UNSELECT\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000019 SELECT UnitTests.Messages (QRESYNC (1436832084 2 1:8))\r\n", "dovecot.select-unittests-messages-qresync.txt"));
			commands.Add (new ImapReplayCommand ("A00000020 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) MODSEQ 2\r\n", "dovecot.search-changed-since.txt"));
			commands.Add (new ImapReplayCommand ("A00000021 UID FETCH 1:7 (UID FLAGS MODSEQ)\r\n", "dovecot.fetch1.txt"));
			commands.Add (new ImapReplayCommand ("A00000022 UID FETCH 1:* (UID FLAGS MODSEQ) (CHANGEDSINCE 2 VANISHED)\r\n", "dovecot.fetch2.txt"));
			commands.Add (new ImapReplayCommand ("A00000023 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (REVERSE ARRIVAL) US-ASCII ALL\r\n", "dovecot.sort-reverse-arrival.txt"));
			commands.Add (new ImapReplayCommand ("A00000024 UID SEARCH RETURN () UNDELETED SEEN\r\n", "dovecot.optimized-search.txt"));
			commands.Add (new ImapReplayCommand ("A00000025 CREATE UnitTests.Destination\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000026 LIST \"\" UnitTests.Destination\r\n", "dovecot.list-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000027 UID COPY 1:7 UnitTests.Destination\r\n", "dovecot.copy.txt"));
			commands.Add (new ImapReplayCommand ("A00000028 UID MOVE 1:7 UnitTests.Destination\r\n", "dovecot.move.txt"));
			commands.Add (new ImapReplayCommand ("A00000029 STATUS UnitTests.Destination (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "dovecot.status-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000030 SELECT UnitTests.Destination (CONDSTORE)\r\n", "dovecot.select-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000031 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1 VANISHED)\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000032 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch4.txt"));
			commands.Add (new ImapReplayCommand ("A00000033 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch5.txt"));
			commands.Add (new ImapReplayCommand ("A00000034 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch6.txt"));
			commands.Add (new ImapReplayCommand ("A00000035 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch7.txt"));
			commands.Add (new ImapReplayCommand ("A00000036 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch8.txt"));
			commands.Add (new ImapReplayCommand ("A00000037 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch9.txt"));
			commands.Add (new ImapReplayCommand ("A00000038 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch10.txt"));
			commands.Add (new ImapReplayCommand ("A00000039 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)])\r\n", "dovecot.fetch11.txt"));
			commands.Add (new ImapReplayCommand ("A00000040 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)])\r\n", "dovecot.fetch12.txt"));
			commands.Add (new ImapReplayCommand ("A00000041 UID FETCH 1 (BODY.PEEK[HEADER] BODY.PEEK[TEXT])\r\n", "dovecot.getbodypart.txt"));
			commands.Add (new ImapReplayCommand ("A00000042 FETCH 1 (BODY.PEEK[HEADER] BODY.PEEK[TEXT])\r\n", "dovecot.getbodypart2.txt"));
			commands.Add (new ImapReplayCommand ("A00000043 UID FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getmessageheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000044 FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getmessageheaders2.txt"));
			commands.Add (new ImapReplayCommand ("A00000045 UID FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getbodypartheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000046 FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getbodypartheaders2.txt"));
			commands.Add (new ImapReplayCommand ("A00000047 UID FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream.txt"));
			commands.Add (new ImapReplayCommand ("A00000048 UID FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream2.txt"));
			commands.Add (new ImapReplayCommand ("A00000049 FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream3.txt"));
			commands.Add (new ImapReplayCommand ("A00000050 FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream4.txt"));
			commands.Add (new ImapReplayCommand ("A00000051 UID FETCH 1 (BODY.PEEK[HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)])\r\n", "dovecot.getstream-section.txt"));
			commands.Add (new ImapReplayCommand ("A00000052 FETCH 1 (BODY.PEEK[HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)])\r\n", "dovecot.getstream-section2.txt"));
			commands.Add (new ImapReplayCommand ("A00000053 UID STORE 1:14 (UNCHANGEDSINCE 3) +FLAGS.SILENT (\\Deleted $MailKit)\r\n", "dovecot.store-deleted-custom.txt"));
			commands.Add (new ImapReplayCommand ("A00000054 STORE 1:7 (UNCHANGEDSINCE 5) FLAGS.SILENT (\\Deleted \\Seen $MailKit)\r\n", "dovecot.setflags-unchangedsince.txt"));
			commands.Add (new ImapReplayCommand ("A00000055 UID SEARCH RETURN () UID 1:14 OR NEW OR OLD OR ANSWERED OR DELETED OR DRAFT OR FLAGGED OR RECENT OR UNANSWERED OR UNDELETED OR UNDRAFT OR UNFLAGGED OR UNSEEN OR KEYWORD $MailKit UNKEYWORD $MailKit\r\n", "dovecot.search-uids.txt"));
			commands.Add (new ImapReplayCommand ("A00000056 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) UID 1:14 LARGER 256 SMALLER 512\r\n", "dovecot.search-uids-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000057 UID SORT RETURN () (REVERSE DATE SUBJECT DISPLAYFROM SIZE) US-ASCII OR OR (SENTBEFORE 12-Oct-2016 SENTSINCE 10-Oct-2016) NOT SENTON 11-Oct-2016 OR (BEFORE 12-Oct-2016 SINCE 10-Oct-2016) NOT ON 11-Oct-2016\r\n", "dovecot.sort-by-date.txt"));
			commands.Add (new ImapReplayCommand ("A00000058 UID SORT RETURN () (FROM TO CC) US-ASCII UID 1:14 OR BCC xyz OR CC xyz OR FROM xyz OR TO xyz OR SUBJECT xyz OR HEADER Message-Id mimekit.net OR BODY \"This is the message body.\" TEXT message\r\n", "dovecot.sort-by-strings.txt"));
			commands.Add (new ImapReplayCommand ("A00000059 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (DISPLAYTO) US-ASCII UID 1:14 OLDER 1 YOUNGER 3600\r\n", "dovecot.sort-uids-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000060 UID SEARCH ALL\r\n", "dovecot.search-raw.txt"));
			commands.Add (new ImapReplayCommand ("A00000061 UID SORT (REVERSE ARRIVAL) US-ASCII ALL\r\n", "dovecot.sort-raw.txt"));
			commands.Add (new ImapReplayCommand ("A00000062 UID FETCH 1:* (BODY.PEEK[])\r\n", "dovecot.getstreams1.txt"));
			commands.Add (new ImapReplayCommand ("A00000063 FETCH 1:3 (UID BODY.PEEK[])\r\n", "dovecot.getstreams2.txt"));
			commands.Add (new ImapReplayCommand ("A00000064 FETCH 1:* (UID BODY.PEEK[])\r\n", "dovecot.getstreams3.txt"));
			commands.Add (new ImapReplayCommand ("A00000065 EXPUNGE\r\n", "dovecot.expunge.txt"));
			commands.Add (new ImapReplayCommand ("A00000066 CLOSE\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000067 NOOP\r\n", "dovecot.noop+alert.txt"));
			commands.Add (new ImapReplayCommand ("A00000068 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestDovecot ()
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			List<DateTimeOffset> internalDates;
			List<MimeMessage> messages;
			List<MessageFlags> flags;

			var commands = CreateDovecotCommands (out internalDates, out messages, out flags);

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (DovecotInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (DovecotAuthenticatedCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.InternationalizationLevel, "Expected I18NLEVEL=1");
				Assert.IsTrue (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.IsTrue (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");
				// TODO: verify CONTEXT=SEARCH

				var personal = client.GetFolder (client.PersonalNamespaces[0]);

				Assert.IsFalse (client.Inbox.Supports (FolderFeature.AccessRights));
				Assert.IsFalse (client.Inbox.Supports (FolderFeature.Annotations));
				Assert.IsFalse (client.Inbox.Supports (FolderFeature.Metadata));
				Assert.IsFalse (client.Inbox.Supports (FolderFeature.ModSequences)); // not supported until opened
				Assert.IsFalse (client.Inbox.Supports (FolderFeature.QuickResync)); // not supported until it is enabled
				Assert.IsFalse (client.Inbox.Supports (FolderFeature.Quotas));
				Assert.IsTrue (client.Inbox.Supports (FolderFeature.Sorting));
				Assert.IsTrue (client.Inbox.Supports (FolderFeature.Threading));
				Assert.IsFalse (client.Inbox.Supports (FolderFeature.UTF8));

				// Make sure these all throw NotSupportedException
				Assert.Throws<NotSupportedException> (() => client.EnableUTF8 ());
				Assert.Throws<NotSupportedException> (() => client.Inbox.GetAccessRights ("smith"));
				Assert.Throws<NotSupportedException> (() => client.Inbox.GetMyAccessRights ());
				var rights = new AccessRights ("lrswida");
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddAccessRights ("smith", rights));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveAccessRights ("smith", rights));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetAccessRights ("smith", rights));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveAccess ("smith"));
				Assert.Throws<NotSupportedException> (() => client.Inbox.GetQuota ());
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetQuota (null, null));
				Assert.Throws<NotSupportedException> (() => client.GetMetadata (MetadataTag.PrivateComment));
				Assert.Throws<NotSupportedException> (() => client.GetMetadata (new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.Throws<NotSupportedException> (() => client.SetMetadata (new MetadataCollection ()));
				var labels = new string[] { "Label1", "Label2" };
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (UniqueId.MinValue, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (UniqueIdRange.All, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (UniqueIdRange.All, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (0, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (new int[] { 0 }, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.AddLabels (new int[] { 0 }, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (UniqueId.MinValue, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (UniqueIdRange.All, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (UniqueIdRange.All, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (0, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (new int[] { 0 }, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.RemoveLabels (new int[] { 0 }, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (UniqueId.MinValue, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (UniqueIdRange.All, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (UniqueIdRange.All, 1, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (0, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (new int[] { 0 }, labels, true));
				Assert.Throws<NotSupportedException> (() => client.Inbox.SetLabels (new int[] { 0 }, 1, labels, true));

				try {
					client.EnableQuickResync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception when enabling QRESYNC: {0}", ex);
				}

				Assert.IsTrue (client.Inbox.Supports (FolderFeature.QuickResync));

				// take advantage of LIST-STATUS to get top-level personal folders...
				var statusItems = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;

				var folders = personal.GetSubfolders (statusItems, false).ToArray ();
				Assert.AreEqual (7, folders.Length, "Expected 7 folders");

				var expectedFolderNames = new [] { "Archives", "Drafts", "Junk", "Sent Messages", "Trash", "INBOX", "NIL" };
				var expectedUidValidities = new [] { 1436832059, 1436832060, 1436832061, 1436832062, 1436832063, 1436832057, 1436832057 };
				var expectedHighestModSeq = new [] { 1, 1, 1, 1, 1, 15, 1 };
				var expectedMessages = new [] { 0, 0, 0, 0, 0, 4, 0 };
				var expectedUidNext = new [] { 1, 1, 1, 1, 1, 5, 1 };
				var expectedRecent = new [] { 0, 0, 0, 0, 0, 0, 0 };
				var expectedUnseen = new [] { 0, 0, 0, 0, 0, 0, 0 };

				for (int i = 0; i < folders.Length; i++) {
					Assert.AreEqual (expectedFolderNames[i], folders[i].FullName, "FullName did not match");
					Assert.AreEqual (expectedFolderNames[i], folders[i].Name, "Name did not match");
					Assert.AreEqual (expectedUidValidities[i], folders[i].UidValidity, "UidValidity did not match");
					Assert.AreEqual (expectedHighestModSeq[i], folders[i].HighestModSeq, "HighestModSeq did not match");
					Assert.AreEqual (expectedMessages[i], folders[i].Count, "Count did not match");
					Assert.AreEqual (expectedRecent[i], folders[i].Recent, "Recent did not match");
					Assert.AreEqual (expectedUnseen[i], folders[i].Unread, "Unread did not match");
				}

				var unitTests = personal.Create ("UnitTests", false);
				Assert.AreEqual (FolderAttributes.HasNoChildren, unitTests.Attributes, "Unexpected UnitTests folder attributes");

				var folder = unitTests.Create ("Messages", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, folder.Attributes, "Unexpected UnitTests.Messages folder attributes");
				//Assert.AreEqual (FolderAttributes.HasChildren, unitTests.Attributes, "Expected UnitTests Attributes to be updated");

				// Use MULTIAPPEND to append some test messages
				var appended = folder.Append (messages, flags, internalDates);
				Assert.AreEqual (8, appended.Count, "Unexpected number of messages appended");

				// SELECT the folder so that we can test some stuff
				var access = folder.Open (FolderAccess.ReadWrite);
				Assert.IsTrue (folder.Supports (FolderFeature.ModSequences));
				Assert.AreEqual (expectedPermanentFlags, folder.PermanentFlags, "UnitTests.Messages PERMANENTFLAGS");
				Assert.AreEqual (expectedFlags, folder.AcceptedFlags, "UnitTests.Messages FLAGS");
				Assert.AreEqual (8, folder.Count, "UnitTests.Messages EXISTS");
				Assert.AreEqual (8, folder.Recent, "UnitTests.Messages RECENT");
				Assert.AreEqual (0, folder.FirstUnread, "UnitTests.Messages UNSEEN");
				Assert.AreEqual (1436832084U, folder.UidValidity, "UnitTests.Messages UIDVALIDITY");
				Assert.AreEqual (9, folder.UidNext.Value.Id, "UnitTests.Messages UIDNEXT");
				Assert.AreEqual (2UL, folder.HighestModSeq, "UnitTests.Messages HIGHESTMODSEQ");
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Messages to be opened in READ-WRITE mode");

				// Keep track of various folder events
				var flagsChanged = new List<MessageFlagsChangedEventArgs> ();
				var modSeqChanged = new List<ModSeqChangedEventArgs> ();
				var vanished = new List<MessagesVanishedEventArgs> ();
				bool recentChanged = false;

				folder.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				folder.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				folder.MessagesVanished += (sender, e) => {
					vanished.Add (e);
				};

				folder.RecentChanged += (sender, e) => {
					recentChanged = true;
				};

				// Keep track of UIDVALIDITY and HIGHESTMODSEQ values for our QRESYNC test later
				var highestModSeq = folder.HighestModSeq;
				var uidValidity = folder.UidValidity;

				// Make some FLAGS changes to our messages so we can test QRESYNC
				folder.AddFlags (appended, MessageFlags.Seen, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (8, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected modSeqChanged[{0}].Index", i);
					Assert.AreEqual (i + 1, modSeqChanged[i].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", i);
					Assert.AreEqual (3, modSeqChanged[i].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", i);
				}
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				var answered = new UniqueIdSet (SortOrder.Ascending);
				answered.Add (appended[0]); // A
				answered.Add (appended[1]); // B
				answered.Add (appended[2]); // C
				folder.AddFlags (answered, MessageFlags.Answered, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (3, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected modSeqChanged[{0}].Index", i);
					Assert.AreEqual (i + 1, modSeqChanged[i].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", i);
					Assert.AreEqual (4, modSeqChanged[i].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", i);
				}
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				// Delete some messages so we can test that QRESYNC emits some MessageVanished events
				// both now *and* when we use QRESYNC to re-open the folder
				var deleted = new UniqueIdSet (SortOrder.Ascending);
				deleted.Add (appended[7]); // H
				folder.AddFlags (deleted, MessageFlags.Deleted, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (1, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (7, modSeqChanged[0].Index, "Unexpected modSeqChanged[{0}].Index", 0);
				Assert.AreEqual (8, modSeqChanged[0].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", 0);
				Assert.AreEqual (5, modSeqChanged[0].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", 0);
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				folder.Expunge (deleted);
				Assert.AreEqual (1, vanished.Count, "Expected MessagesVanished event");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				Assert.IsFalse (vanished[0].Earlier, "Expected EARLIER to be false");
				Assert.IsTrue (recentChanged, "Expected RecentChanged event");
				recentChanged = false;
				vanished.Clear ();

				Assert.IsTrue (folder.Supports (FolderFeature.Threading), "Supports threading");
				Assert.IsTrue (folder.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References), "Supports threading by References");
				Assert.IsTrue (folder.ThreadingAlgorithms.Contains (ThreadingAlgorithm.OrderedSubject), "Supports threading by OrderedSubject");

				// Verify that THREAD works correctly
				var threaded = folder.Thread (ThreadingAlgorithm.References, SearchQuery.All);
				Assert.AreEqual (2, threaded.Count, "Unexpected number of root nodes in threaded results");

				threaded = folder.Thread (UniqueIdRange.All, ThreadingAlgorithm.OrderedSubject, SearchQuery.All);
				Assert.AreEqual (7, threaded.Count, "Unexpected number of root nodes in threaded results");

				// UNSELECT the folder so we can re-open it using QRESYNC
				folder.Close ();

				// Use QRESYNC to get the changes since last time we opened the folder
				Assert.IsTrue (folder.Supports (FolderFeature.QuickResync), "Supports QRESYNC");
				access = folder.Open (FolderAccess.ReadWrite, uidValidity, highestModSeq, appended);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Messages to be opened in READ-WRITE mode");
				Assert.AreEqual (7, flagsChanged.Count, "Unexpected number of MessageFlagsChanged events");
				Assert.AreEqual (7, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < flagsChanged.Count; i++) {
					var messageFlags = MessageFlags.Seen | MessageFlags.Draft;

					if (i < 3)
						messageFlags |= MessageFlags.Answered;

					Assert.AreEqual (i, flagsChanged[i].Index, "Unexpected value for flagsChanged[{0}].Index", i);
					Assert.AreEqual ((uint) (i + 1), flagsChanged[i].UniqueId.Value.Id, "Unexpected value for flagsChanged[{0}].UniqueId", i);
					Assert.AreEqual (messageFlags, flagsChanged[i].Flags, "Unexpected value for flagsChanged[{0}].Flags", i);

					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					if (i < 3)
						Assert.AreEqual (4, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
					else
						Assert.AreEqual (3, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				Assert.AreEqual (1, vanished.Count, "Unexpected number of MessagesVanished events");
				Assert.IsTrue (vanished[0].Earlier, "Expected VANISHED EARLIER");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				vanished.Clear ();

				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.GMailMessageId (1)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.GMailThreadId (1)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.HasGMailLabel ("Custom Label")));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.GMailRawSearch ("has:attachment in:unread")));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.Fuzzy (SearchQuery.SubjectContains ("some fuzzy text"))));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.Filter (new MetadataTag ("/private/filters/values/saved-search"))));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.Filter ("saved-search")));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SaveDateSupported));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SavedBefore (DateTime.Now)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SavedOn (DateTime.Now)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SavedSince (DateTime.Now)));

				// Use SEARCH and FETCH to get the same info
				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var changed = folder.Search (searchOptions, SearchQuery.ChangedSince (highestModSeq));
				Assert.AreEqual (7, changed.UniqueIds.Count, "Unexpected number of UIDs");
				Assert.AreEqual (changed.Count, changed.Relevancy.Count, "Unexpected number of relevancy scores");
				Assert.IsTrue (changed.ModSeq.HasValue, "Expected the ModSeq property to be set");
				Assert.AreEqual (4, changed.ModSeq.Value, "Unexpected ModSeq value");
				Assert.AreEqual (1, changed.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (7, changed.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (7, changed.Count, "Unexpected Count");

				var fetched = folder.Fetch (changed.UniqueIds, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.AreEqual (7, fetched.Count, "Unexpected number of messages fetched");
				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");
				}

				// or... we could just use a single UID FETCH command like so:
				fetched = folder.Fetch (UniqueIdRange.All, highestModSeq, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");
				}
				Assert.AreEqual (7, fetched.Count, "Unexpected number of messages fetched");
				Assert.AreEqual (1, vanished.Count, "Unexpected number of MessagesVanished events");
				Assert.IsTrue (vanished[0].Earlier, "Expected VANISHED EARLIER");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				vanished.Clear ();

				// Use SORT to order by reverse arrival order
				var orderBy = new OrderBy[] { new OrderBy (OrderByType.Arrival, SortOrder.Descending) };
				var sorted = folder.Sort (searchOptions, SearchQuery.All, orderBy);
				Assert.AreEqual (7, sorted.UniqueIds.Count, "Unexpected number of UIDs");
				Assert.AreEqual (sorted.Count, sorted.Relevancy.Count, "Unexpected number of relevancy scores");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.AreEqual (7 - i, sorted.UniqueIds[i].Id, "Unexpected value for UniqueId[{0}]", i);
				Assert.IsFalse (sorted.ModSeq.HasValue, "Expected the ModSeq property to be null");
				Assert.AreEqual (7, sorted.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (1, sorted.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (7, sorted.Count, "Unexpected Count");

				// Verify that optimizing NOT queries works correctly
				var uids = folder.Search (SearchQuery.Not (SearchQuery.Deleted).And (SearchQuery.Not (SearchQuery.NotSeen)));
				Assert.AreEqual (7, uids.Count, "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected value for uids[{0}]", i);

				// Create a Destination folder to use for copying/moving messages to
				var destination = (ImapFolder) unitTests.Create ("Destination", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, destination.Attributes, "Unexpected UnitTests.Destination folder attributes");

				// COPY messages to the Destination folder
				var copied = folder.CopyTo (uids, destination);
				Assert.AreEqual (uids.Count, copied.Source.Count, "Unexpetced Source.Count");
				Assert.AreEqual (uids.Count, copied.Destination.Count, "Unexpetced Destination.Count");

				// MOVE messages to the Destination folder
				var moved = folder.MoveTo (uids, destination);
				Assert.AreEqual (uids.Count, copied.Source.Count, "Unexpetced Source.Count");
				Assert.AreEqual (uids.Count, copied.Destination.Count, "Unexpetced Destination.Count");
				Assert.AreEqual (1, vanished.Count, "Expected VANISHED event");
				vanished.Clear ();

				destination.Status (statusItems);
				Assert.AreEqual (moved.Destination[0].Validity, destination.UidValidity, "Unexpected UIDVALIDITY");

				destination.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				destination.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				destination.MessagesVanished += (sender, e) => {
					vanished.Add (e);
				};

				destination.RecentChanged += (sender, e) => {
					recentChanged = true;
				};

				destination.Open (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Destination to be opened in READ-WRITE mode");

				var fetchHeaders = new HashSet<HeaderId> ();
				fetchHeaders.Add (HeaderId.References);
				fetchHeaders.Add (HeaderId.XMailer);

				var indexes = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

				// Fetch + modseq
				fetched = destination.Fetch (UniqueIdRange.All, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				// Fetch
				fetched = destination.Fetch (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				uids = new UniqueIdSet (SortOrder.Ascending);

				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");

					uids.Add (fetched[i].UniqueId);
				}

				var entity = destination.GetBodyPart (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.IsInstanceOf<TextPart> (entity);

				entity = destination.GetBodyPart (fetched[0].Index, fetched[0].TextBody);
				Assert.IsInstanceOf<TextPart> (entity);

				var headers =  destination.GetHeaders (fetched[0].UniqueId);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(UniqueId) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[0].Index);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(int) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(UniqueId, BodyPart) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[0].Index, fetched[0].TextBody);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(int, BodyPart) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				using (var stream = destination.GetStream (fetched[0].UniqueId, 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = destination.GetStream (fetched[0].UniqueId, "", 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = destination.GetStream (fetched[0].Index, 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = destination.GetStream (fetched[0].Index, "", 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = destination.GetStream (fetched[0].UniqueId, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.AreEqual (62, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n", text);
				}

				using (var stream = destination.GetStream (fetched[0].Index, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.AreEqual (62, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n", text);
				}

				var custom = new HashSet<string> { "$MailKit" };

				var unchanged1 = destination.AddFlags (uids, destination.HighestModSeq, MessageFlags.Deleted, custom, true);
				Assert.AreEqual (14, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (5, destination.HighestModSeq);
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					Assert.AreEqual (5, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				Assert.AreEqual (2, unchanged1.Count, "[MODIFIED uid-set]");
				Assert.AreEqual (7, unchanged1[0].Id, "unchanged uids[0]");
				Assert.AreEqual (9, unchanged1[1].Id, "unchanged uids[1]");
				modSeqChanged.Clear ();

				var unchanged2 = destination.SetFlags (new int[] { 0, 1, 2, 3, 4, 5, 6 }, destination.HighestModSeq, MessageFlags.Seen | MessageFlags.Deleted, custom, true);
				Assert.AreEqual (7, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (6, destination.HighestModSeq);
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					Assert.AreEqual (6, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				Assert.AreEqual (2, unchanged2.Count, "[MODIFIED seq-set]");
				Assert.AreEqual (6, unchanged2[0], "unchanged indexes[0]");
				Assert.AreEqual (8, unchanged2[1], "unchanged indexes[1]");
				modSeqChanged.Clear ();

				var results = destination.Search (uids, SearchQuery.New.Or (SearchQuery.Old.Or (SearchQuery.Answered.Or (SearchQuery.Deleted.Or (SearchQuery.Draft.Or (SearchQuery.Flagged.Or (SearchQuery.Recent.Or (SearchQuery.NotAnswered.Or (SearchQuery.NotDeleted.Or (SearchQuery.NotDraft.Or (SearchQuery.NotFlagged.Or (SearchQuery.NotSeen.Or (SearchQuery.HasCustomFlag ("$MailKit").Or (SearchQuery.NotKeyword ("$MailKit")))))))))))))));
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");

				var matches = destination.Search (searchOptions, uids, SearchQuery.LargerThan (256).And (SearchQuery.SmallerThan (512)));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.AreEqual (10, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (13, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (2, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (10, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (expectedMatchedUids[i], matches.UniqueIds[i].Id);
				Assert.AreEqual (matches.Count, matches.Relevancy.Count, "Unexpected number of relevancy scores");

				orderBy = new OrderBy[] { OrderBy.ReverseDate, OrderBy.Subject, OrderBy.DisplayFrom, OrderBy.Size };
				var sentDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.SentBefore (new DateTime (2016, 10, 12)), SearchQuery.SentAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.SentOn (new DateTime (2016, 10, 11))));
				var deliveredDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.DeliveredBefore (new DateTime (2016, 10, 12)), SearchQuery.DeliveredAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.DeliveredOn (new DateTime (2016, 10, 11))));
				results = destination.Sort (sentDateQuery.Or (deliveredDateQuery), orderBy);
				var expectedSortByDateResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.AreEqual (expectedSortByDateResults[i], results[i].Id);

				var stringQuery = SearchQuery.BccContains ("xyz").Or (SearchQuery.CcContains ("xyz").Or (SearchQuery.FromContains ("xyz").Or (SearchQuery.ToContains ("xyz").Or (SearchQuery.SubjectContains ("xyz").Or (SearchQuery.HeaderContains ("Message-Id", "mimekit.net").Or (SearchQuery.BodyContains ("This is the message body.").Or (SearchQuery.MessageContains ("message"))))))));
				orderBy = new OrderBy[] { OrderBy.From, OrderBy.To, OrderBy.Cc };
				results = destination.Sort (uids, stringQuery, orderBy);
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.AreEqual (i + 1, results[i].Id);

				orderBy = new OrderBy[] { OrderBy.DisplayTo };
				matches = destination.Sort (searchOptions, uids, SearchQuery.OlderThan (1).And (SearchQuery.YoungerThan (3600)), orderBy);
				Assert.AreEqual (14, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (14, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (1, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (14, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);
				Assert.AreEqual (matches.Count, matches.Relevancy.Count, "Unexpected number of relevancy scores");

				client.Capabilities &= ~ImapCapabilities.ESearch;
				matches = ((ImapFolder) destination).Search ("ALL");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);

				client.Capabilities &= ~ImapCapabilities.ESort;
				matches = ((ImapFolder) destination).Sort ("(REVERSE ARRIVAL) US-ASCII ALL");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				var expectedSortByReverseArrivalResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (expectedSortByReverseArrivalResults[i], matches.UniqueIds[i].Id);

				destination.GetStreams (UniqueIdRange.All, GetStreamsCallback);
				destination.GetStreams (new int[] { 0, 1, 2 }, GetStreamsCallback);
				destination.GetStreams (0, -1, GetStreamsCallback);

				destination.Expunge ();
				Assert.AreEqual (7, destination.HighestModSeq);
				Assert.AreEqual (1, vanished.Count, "Unexpected number of Vanished events");
				Assert.AreEqual (14, vanished[0].UniqueIds.Count, "Unexpected number of UIDs in Vanished event");
				for (int i = 0; i < vanished[0].UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, vanished[0].UniqueIds[i].Id);
				Assert.IsFalse (vanished[0].Earlier, "Unexpected value for Earlier");
				vanished.Clear ();

				destination.Close (true);

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.AreEqual ("System shutdown in 10 minutes", e.Message);
					alerts++;
				};
				client.NoOp ();
				Assert.AreEqual (1, alerts, "Alert event failed to fire.");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestDovecotAsync ()
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			List<DateTimeOffset> internalDates;
			List<MimeMessage> messages;
			List<MessageFlags> flags;

			var commands = CreateDovecotCommands (out internalDates, out messages, out flags);

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (DovecotInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (DovecotAuthenticatedCapabilities, client.Capabilities);
				Assert.AreEqual (1, client.InternationalizationLevel, "Expected I18NLEVEL=1");
				Assert.IsTrue (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.IsTrue (client.ThreadingAlgorithms.Contains (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");
				// TODO: verify CONTEXT=SEARCH

				var personal = client.GetFolder (client.PersonalNamespaces[0]);

				// Make sure these all throw NotSupportedException
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.EnableUTF8Async ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.GetAccessRightsAsync ("smith"));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.GetMyAccessRightsAsync ());
				var rights = new AccessRights ("lrswida");
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddAccessRightsAsync ("smith", rights));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveAccessRightsAsync ("smith", rights));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetAccessRightsAsync ("smith", rights));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveAccessAsync ("smith"));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.GetQuotaAsync ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetQuotaAsync (null, null));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.GetMetadataAsync (MetadataTag.PrivateComment));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.GetMetadataAsync (new MetadataTag[] { MetadataTag.PrivateComment }));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.SetMetadataAsync (new MetadataCollection ()));
				var labels = new string[] { "Label1", "Label2" };
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddLabelsAsync (UniqueId.MinValue, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddLabelsAsync (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddLabelsAsync (UniqueIdRange.All, 1, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddLabelsAsync (0, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddLabelsAsync (new int[] { 0 }, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.AddLabelsAsync (new int[] { 0 }, 1, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveLabelsAsync (UniqueId.MinValue, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveLabelsAsync (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveLabelsAsync (UniqueIdRange.All, 1, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveLabelsAsync (0, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveLabelsAsync (new int[] { 0 }, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.RemoveLabelsAsync (new int[] { 0 }, 1, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetLabelsAsync (UniqueId.MinValue, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetLabelsAsync (UniqueIdRange.All, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetLabelsAsync (UniqueIdRange.All, 1, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetLabelsAsync (0, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetLabelsAsync (new int[] { 0 }, labels, true));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.Inbox.SetLabelsAsync (new int[] { 0 }, 1, labels, true));

				try {
					await client.EnableQuickResyncAsync ();
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception when enabling QRESYNC: {0}", ex);
				}

				// take advantage of LIST-STATUS to get top-level personal folders...
				var statusItems = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;

				var folders = (await personal.GetSubfoldersAsync (statusItems, false)).ToArray ();
				Assert.AreEqual (7, folders.Length, "Expected 7 folders");

				var expectedFolderNames = new [] { "Archives", "Drafts", "Junk", "Sent Messages", "Trash", "INBOX", "NIL" };
				var expectedUidValidities = new [] { 1436832059, 1436832060, 1436832061, 1436832062, 1436832063, 1436832057, 1436832057 };
				var expectedHighestModSeq = new [] { 1, 1, 1, 1, 1, 15, 1 };
				var expectedMessages = new [] { 0, 0, 0, 0, 0, 4, 0 };
				var expectedUidNext = new [] { 1, 1, 1, 1, 1, 5, 1 };
				var expectedRecent = new [] { 0, 0, 0, 0, 0, 0, 0 };
				var expectedUnseen = new [] { 0, 0, 0, 0, 0, 0, 0 };

				for (int i = 0; i < folders.Length; i++) {
					Assert.AreEqual (expectedFolderNames[i], folders[i].FullName, "FullName did not match");
					Assert.AreEqual (expectedFolderNames[i], folders[i].Name, "Name did not match");
					Assert.AreEqual (expectedUidValidities[i], folders[i].UidValidity, "UidValidity did not match");
					Assert.AreEqual (expectedHighestModSeq[i], folders[i].HighestModSeq, "HighestModSeq did not match");
					Assert.AreEqual (expectedMessages[i], folders[i].Count, "Count did not match");
					Assert.AreEqual (expectedRecent[i], folders[i].Recent, "Recent did not match");
					Assert.AreEqual (expectedUnseen[i], folders[i].Unread, "Unread did not match");
				}

				var unitTests = await personal.CreateAsync ("UnitTests", false);
				Assert.AreEqual (FolderAttributes.HasNoChildren, unitTests.Attributes, "Unexpected UnitTests folder attributes");

				var folder = await unitTests.CreateAsync ("Messages", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, folder.Attributes, "Unexpected UnitTests.Messages folder attributes");
				//Assert.AreEqual (FolderAttributes.HasChildren, unitTests.Attributes, "Expected UnitTests Attributes to be updated");

				// Use MULTIAPPEND to append some test messages
				var appended = await folder.AppendAsync (messages, flags, internalDates);
				Assert.AreEqual (8, appended.Count, "Unexpected number of messages appended");

				// SELECT the folder so that we can test some stuff
				var access = await folder.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (expectedPermanentFlags, folder.PermanentFlags, "UnitTests.Messages PERMANENTFLAGS");
				Assert.AreEqual (expectedFlags, folder.AcceptedFlags, "UnitTests.Messages FLAGS");
				Assert.AreEqual (8, folder.Count, "UnitTests.Messages EXISTS");
				Assert.AreEqual (8, folder.Recent, "UnitTests.Messages RECENT");
				Assert.AreEqual (0, folder.FirstUnread, "UnitTests.Messages UNSEEN");
				Assert.AreEqual (1436832084U, folder.UidValidity, "UnitTests.Messages UIDVALIDITY");
				Assert.AreEqual (9, folder.UidNext.Value.Id, "UnitTests.Messages UIDNEXT");
				Assert.AreEqual (2UL, folder.HighestModSeq, "UnitTests.Messages HIGHESTMODSEQ");
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Messages to be opened in READ-WRITE mode");

				// Keep track of various folder events
				var flagsChanged = new List<MessageFlagsChangedEventArgs> ();
				var modSeqChanged = new List<ModSeqChangedEventArgs> ();
				var vanished = new List<MessagesVanishedEventArgs> ();
				bool recentChanged = false;

				folder.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				folder.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				folder.MessagesVanished += (sender, e) => {
					vanished.Add (e);
				};

				folder.RecentChanged += (sender, e) => {
					recentChanged = true;
				};

				// Keep track of UIDVALIDITY and HIGHESTMODSEQ values for our QRESYNC test later
				var highestModSeq = folder.HighestModSeq;
				var uidValidity = folder.UidValidity;

				// Make some FLAGS changes to our messages so we can test QRESYNC
				await folder.AddFlagsAsync (appended, MessageFlags.Seen, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (8, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected modSeqChanged[{0}].Index", i);
					Assert.AreEqual (i + 1, modSeqChanged[i].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", i);
					Assert.AreEqual (3, modSeqChanged[i].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", i);
				}
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				var answered = new UniqueIdSet (SortOrder.Ascending);
				answered.Add (appended[0]); // A
				answered.Add (appended[1]); // B
				answered.Add (appended[2]); // C
				await folder.AddFlagsAsync (answered, MessageFlags.Answered, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (3, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected modSeqChanged[{0}].Index", i);
					Assert.AreEqual (i + 1, modSeqChanged[i].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", i);
					Assert.AreEqual (4, modSeqChanged[i].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", i);
				}
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				// Delete some messages so we can test that QRESYNC emits some MessageVanished events
				// both now *and* when we use QRESYNC to re-open the folder
				var deleted = new UniqueIdSet (SortOrder.Ascending);
				deleted.Add (appended[7]); // H
				await folder.AddFlagsAsync (deleted, MessageFlags.Deleted, true);
				Assert.AreEqual (0, flagsChanged.Count, "Unexpected number of FlagsChanged events");
				Assert.AreEqual (1, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (7, modSeqChanged[0].Index, "Unexpected modSeqChanged[{0}].Index", 0);
				Assert.AreEqual (8, modSeqChanged[0].UniqueId.Value.Id, "Unexpected modSeqChanged[{0}].UniqueId", 0);
				Assert.AreEqual (5, modSeqChanged[0].ModSeq, "Unexpected modSeqChanged[{0}].ModSeq", 0);
				Assert.IsFalse (recentChanged, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				await folder.ExpungeAsync (deleted);
				Assert.AreEqual (1, vanished.Count, "Expected MessagesVanished event");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				Assert.IsFalse (vanished[0].Earlier, "Expected EARLIER to be false");
				Assert.IsTrue (recentChanged, "Expected RecentChanged event");
				recentChanged = false;
				vanished.Clear ();

				// Verify that THREAD works correctly
				var threaded = await folder.ThreadAsync (ThreadingAlgorithm.References, SearchQuery.All);
				Assert.AreEqual (2, threaded.Count, "Unexpected number of root nodes in threaded results");

				threaded = await folder.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.OrderedSubject, SearchQuery.All);
				Assert.AreEqual (7, threaded.Count, "Unexpected number of root nodes in threaded results");

				// UNSELECT the folder so we can re-open it using QRESYNC
				await folder.CloseAsync ();

				// Use QRESYNC to get the changes since last time we opened the folder
				access = await folder.OpenAsync (FolderAccess.ReadWrite, uidValidity, highestModSeq, appended);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Messages to be opened in READ-WRITE mode");
				Assert.AreEqual (7, flagsChanged.Count, "Unexpected number of MessageFlagsChanged events");
				Assert.AreEqual (7, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < flagsChanged.Count; i++) {
					var messageFlags = MessageFlags.Seen | MessageFlags.Draft;

					if (i < 3)
						messageFlags |= MessageFlags.Answered;

					Assert.AreEqual (i, flagsChanged[i].Index, "Unexpected value for flagsChanged[{0}].Index", i);
					Assert.AreEqual ((uint) (i + 1), flagsChanged[i].UniqueId.Value.Id, "Unexpected value for flagsChanged[{0}].UniqueId", i);
					Assert.AreEqual (messageFlags, flagsChanged[i].Flags, "Unexpected value for flagsChanged[{0}].Flags", i);

					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					if (i < 3)
						Assert.AreEqual (4, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
					else
						Assert.AreEqual (3, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				Assert.AreEqual (1, vanished.Count, "Unexpected number of MessagesVanished events");
				Assert.IsTrue (vanished[0].Earlier, "Expected VANISHED EARLIER");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				vanished.Clear ();

				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.GMailMessageId (1)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.GMailThreadId (1)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.HasGMailLabel ("Custom Label")));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.GMailRawSearch ("has:attachment in:unread")));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.Fuzzy (SearchQuery.SubjectContains ("some fuzzy text"))));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.Filter (new MetadataTag ("/private/filters/values/saved-search"))));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.Filter ("saved-search")));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SaveDateSupported));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SavedBefore (DateTime.Now)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SavedOn (DateTime.Now)));
				Assert.Throws<NotSupportedException> (() => folder.Search (SearchQuery.SavedSince (DateTime.Now)));

				// Use SEARCH and FETCH to get the same info
				var searchOptions = SearchOptions.All | SearchOptions.Count | SearchOptions.Min | SearchOptions.Max | SearchOptions.Relevancy;
				var changed = await folder.SearchAsync (searchOptions, SearchQuery.ChangedSince (highestModSeq));
				Assert.AreEqual (7, changed.UniqueIds.Count, "Unexpected number of UIDs");
				Assert.AreEqual (changed.Count, changed.Relevancy.Count, "Unexpected number of relevancy scores");
				Assert.IsTrue (changed.ModSeq.HasValue, "Expected the ModSeq property to be set");
				Assert.AreEqual (4, changed.ModSeq.Value, "Unexpected ModSeq value");
				Assert.AreEqual (1, changed.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (7, changed.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (7, changed.Count, "Unexpected Count");

				var fetched = await folder.FetchAsync (changed.UniqueIds, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.AreEqual (7, fetched.Count, "Unexpected number of messages fetched");
				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");
				}

				// or... we could just use a single UID FETCH command like so:
				fetched = await folder.FetchAsync (UniqueIdRange.All, highestModSeq, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");
				}
				Assert.AreEqual (7, fetched.Count, "Unexpected number of messages fetched");
				Assert.AreEqual (1, vanished.Count, "Unexpected number of MessagesVanished events");
				Assert.IsTrue (vanished[0].Earlier, "Expected VANISHED EARLIER");
				Assert.AreEqual (1, vanished[0].UniqueIds.Count, "Unexpected number of messages vanished");
				Assert.AreEqual (8, vanished[0].UniqueIds[0].Id, "Unexpected UID for vanished message");
				vanished.Clear ();

				// Use SORT to order by reverse arrival order
				var orderBy = new OrderBy[] { new OrderBy (OrderByType.Arrival, SortOrder.Descending) };
				var sorted = await folder.SortAsync (searchOptions, SearchQuery.All, orderBy);
				Assert.AreEqual (7, sorted.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.AreEqual (7 - i, sorted.UniqueIds[i].Id, "Unexpected value for UniqueId[{0}]", i);
				Assert.AreEqual (sorted.Count, sorted.Relevancy.Count, "Unexpected number of relevancy scores");
				Assert.IsFalse (sorted.ModSeq.HasValue, "Expected the ModSeq property to be null");
				Assert.AreEqual (7, sorted.Min.Value.Id, "Unexpected Min");
				Assert.AreEqual (1, sorted.Max.Value.Id, "Unexpected Max");
				Assert.AreEqual (7, sorted.Count, "Unexpected Count");

				// Verify that optimizing NOT queries works correctly
				var uids = await folder.SearchAsync (SearchQuery.Not (SearchQuery.Deleted).And (SearchQuery.Not (SearchQuery.NotSeen)));
				Assert.AreEqual (7, uids.Count, "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.AreEqual (i + 1, uids[i].Id, "Unexpected value for uids[{0}]", i);

				// Create a Destination folder to use for copying/moving messages to
				var destination = (ImapFolder) await unitTests.CreateAsync ("Destination", true);
				Assert.AreEqual (FolderAttributes.HasNoChildren, destination.Attributes, "Unexpected UnitTests.Destination folder attributes");

				// COPY messages to the Destination folder
				var copied = await folder.CopyToAsync (uids, destination);
				Assert.AreEqual (uids.Count, copied.Source.Count, "Unexpetced Source.Count");
				Assert.AreEqual (uids.Count, copied.Destination.Count, "Unexpetced Destination.Count");

				// MOVE messages to the Destination folder
				var moved = await folder.MoveToAsync (uids, destination);
				Assert.AreEqual (uids.Count, copied.Source.Count, "Unexpetced Source.Count");
				Assert.AreEqual (uids.Count, copied.Destination.Count, "Unexpetced Destination.Count");
				Assert.AreEqual (1, vanished.Count, "Expected VANISHED event");
				vanished.Clear ();

				await destination.StatusAsync (statusItems);
				Assert.AreEqual (moved.Destination[0].Validity, destination.UidValidity, "Unexpected UIDVALIDITY");

				destination.MessageFlagsChanged += (sender, e) => {
					flagsChanged.Add (e);
				};

				destination.ModSeqChanged += (sender, e) => {
					modSeqChanged.Add (e);
				};

				destination.MessagesVanished += (sender, e) => {
					vanished.Add (e);
				};

				destination.RecentChanged += (sender, e) => {
					recentChanged = true;
				};

				await destination.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "Expected UnitTests.Destination to be opened in READ-WRITE mode");

				var fetchHeaders = new HashSet<HeaderId> ();
				fetchHeaders.Add (HeaderId.References);
				fetchHeaders.Add (HeaderId.XMailer);

				var indexes = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

				// Fetch + modseq
				fetched = await destination.FetchAsync (UniqueIdRange.All, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				// Fetch
				fetched = await destination.FetchAsync (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.AreEqual (14, fetched.Count, "Unexpected number of messages fetched");

				uids = new UniqueIdSet (SortOrder.Ascending);

				for (int i = 0; i < fetched.Count; i++) {
					Assert.AreEqual (i, fetched[i].Index, "Unexpected Index");
					Assert.AreEqual (i + 1, fetched[i].UniqueId.Id, "Unexpected UniqueId");

					uids.Add (fetched[i].UniqueId);
				}

				var entity = await destination.GetBodyPartAsync (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.IsInstanceOf<TextPart> (entity);

				entity = await destination.GetBodyPartAsync (fetched[0].Index, fetched[0].TextBody);
				Assert.IsInstanceOf<TextPart> (entity);

				var headers = await destination.GetHeadersAsync (fetched[0].UniqueId);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(UniqueId) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[0].Index);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(int) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(UniqueId, BodyPart) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[0].Index, fetched[0].TextBody);
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.From], "GetHeaders(int, BodyPart) failed to match From header");
				Assert.AreEqual ("Sun, 02 Oct 2016 17:56:45 -0400", headers[HeaderId.Date], "GetHeaders(UniqueId) failed to match Date header");
				Assert.AreEqual ("A", headers[HeaderId.Subject], "GetHeaders(UniqueId) failed to match Subject header");
				Assert.AreEqual ("<a@mimekit.net>", headers[HeaderId.MessageId], "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.AreEqual ("Unit Tests <unit-tests@mimekit.net>", headers[HeaderId.To], "GetHeaders(UniqueId) failed to match To header");
				Assert.AreEqual ("1.0", headers[HeaderId.MimeVersion], "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.AreEqual ("text/plain; charset=utf-8", headers[HeaderId.ContentType], "GetHeaders(UniqueId) failed to match Content-Type header");

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, "", 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, "", 128, 64)) {
					Assert.AreEqual (64, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.AreEqual (62, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n", text);
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.AreEqual (62, stream.Length, "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.AreEqual ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n", text);
				}

				var custom = new HashSet<string> ();
				custom.Add ("$MailKit");

				var unchanged1 = await destination.AddFlagsAsync (uids, destination.HighestModSeq, MessageFlags.Deleted, custom, true);
				Assert.AreEqual (14, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (5, destination.HighestModSeq);
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					Assert.AreEqual (5, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				Assert.AreEqual (2, unchanged1.Count, "[MODIFIED uid-set]");
				Assert.AreEqual (7, unchanged1[0].Id, "unchanged uids[0]");
				Assert.AreEqual (9, unchanged1[1].Id, "unchanged uids[1]");
				modSeqChanged.Clear ();

				var unchanged2 = await destination.SetFlagsAsync (new int[] { 0, 1, 2, 3, 4, 5, 6 }, destination.HighestModSeq, MessageFlags.Seen | MessageFlags.Deleted, custom, true);
				Assert.AreEqual (7, modSeqChanged.Count, "Unexpected number of ModSeqChanged events");
				Assert.AreEqual (6, destination.HighestModSeq);
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.AreEqual (i, modSeqChanged[i].Index, "Unexpected value for modSeqChanged[{0}].Index", i);
					Assert.AreEqual (6, modSeqChanged[i].ModSeq, "Unexpected value for modSeqChanged[{0}].ModSeq", i);
				}
				Assert.AreEqual (2, unchanged2.Count, "[MODIFIED seq-set]");
				Assert.AreEqual (6, unchanged2[0], "unchanged indexes[0]");
				Assert.AreEqual (8, unchanged2[1], "unchanged indexes[1]");
				modSeqChanged.Clear ();

				var results = await destination.SearchAsync (uids, SearchQuery.New.Or (SearchQuery.Old.Or (SearchQuery.Answered.Or (SearchQuery.Deleted.Or (SearchQuery.Draft.Or (SearchQuery.Flagged.Or (SearchQuery.Recent.Or (SearchQuery.NotAnswered.Or (SearchQuery.NotDeleted.Or (SearchQuery.NotDraft.Or (SearchQuery.NotFlagged.Or (SearchQuery.NotSeen.Or (SearchQuery.HasCustomFlag ("$MailKit").Or (SearchQuery.NotKeyword ("$MailKit")))))))))))))));
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");

				var matches = await destination.SearchAsync (searchOptions, uids, SearchQuery.LargerThan (256).And (SearchQuery.SmallerThan (512)));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.AreEqual (10, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (13, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (2, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (10, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (expectedMatchedUids[i], matches.UniqueIds[i].Id);
				Assert.AreEqual (matches.Count, matches.Relevancy.Count, "Unexpected number of relevancy scores");

				orderBy = new OrderBy[] { OrderBy.ReverseDate, OrderBy.Subject, OrderBy.DisplayFrom, OrderBy.Size };
				var sentDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.SentBefore (new DateTime (2016, 10, 12)), SearchQuery.SentAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.SentOn (new DateTime (2016, 10, 11))));
				var deliveredDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.DeliveredBefore (new DateTime (2016, 10, 12)), SearchQuery.DeliveredAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.DeliveredOn (new DateTime (2016, 10, 11))));
				results = await destination.SortAsync (sentDateQuery.Or (deliveredDateQuery), orderBy);
				var expectedSortByDateResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.AreEqual (expectedSortByDateResults[i], results[i].Id);

				var stringQuery = SearchQuery.BccContains ("xyz").Or (SearchQuery.CcContains ("xyz").Or (SearchQuery.FromContains ("xyz").Or (SearchQuery.ToContains ("xyz").Or (SearchQuery.SubjectContains ("xyz").Or (SearchQuery.HeaderContains ("Message-Id", "mimekit.net").Or (SearchQuery.BodyContains ("This is the message body.").Or (SearchQuery.MessageContains ("message"))))))));
				orderBy = new OrderBy[] { OrderBy.From, OrderBy.To, OrderBy.Cc };
				results = await destination.SortAsync (uids, stringQuery, orderBy);
				Assert.AreEqual (14, results.Count, "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.AreEqual (i + 1, results[i].Id);

				orderBy = new OrderBy[] { OrderBy.DisplayTo };
				matches = await destination.SortAsync (searchOptions, uids, SearchQuery.OlderThan (1).And (SearchQuery.YoungerThan (3600)), orderBy);
				Assert.AreEqual (14, matches.Count, "Unexpected COUNT");
				Assert.AreEqual (14, matches.Max.Value.Id, "Unexpected MAX");
				Assert.AreEqual (1, matches.Min.Value.Id, "Unexpected MIN");
				Assert.AreEqual (14, matches.UniqueIds.Count, "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);
				Assert.AreEqual (matches.Count, matches.Relevancy.Count, "Unexpected number of relevancy scores");

				client.Capabilities &= ~ImapCapabilities.ESearch;
				matches = await ((ImapFolder) destination).SearchAsync ("ALL");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, matches.UniqueIds[i].Id);

				client.Capabilities &= ~ImapCapabilities.ESort;
				matches = await ((ImapFolder) destination).SortAsync ("(REVERSE ARRIVAL) US-ASCII ALL");
				Assert.IsFalse (matches.Max.HasValue, "MAX should not be set");
				Assert.IsFalse (matches.Min.HasValue, "MIN should not be set");
				Assert.AreEqual (0, matches.Count, "COUNT should not be set");
				Assert.AreEqual (14, matches.UniqueIds.Count);
				var expectedSortByReverseArrivalResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.AreEqual (expectedSortByReverseArrivalResults[i], matches.UniqueIds[i].Id);

				await destination.GetStreamsAsync (UniqueIdRange.All, GetStreamsAsyncCallback);
				await destination.GetStreamsAsync (new int[] { 0, 1, 2 }, GetStreamsAsyncCallback);
				await destination.GetStreamsAsync (0, -1, GetStreamsAsyncCallback);

				await destination.ExpungeAsync ();
				Assert.AreEqual (7, destination.HighestModSeq);
				Assert.AreEqual (1, vanished.Count, "Unexpected number of Vanished events");
				Assert.AreEqual (14, vanished[0].UniqueIds.Count, "Unexpected number of UIDs in Vanished event");
				for (int i = 0; i < vanished[0].UniqueIds.Count; i++)
					Assert.AreEqual (i + 1, vanished[0].UniqueIds[i].Id);
				Assert.IsFalse (vanished[0].Earlier, "Unexpected value for Earlier");
				vanished.Clear ();

				await destination.CloseAsync (true);

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.AreEqual ("System shutdown in 10 minutes", e.Message);
					alerts++;
				};
				await client.NoOpAsync ();
				Assert.AreEqual (1, alerts, "Alert event failed to fire.");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateGMailCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 CREATE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "gmail.list-unittests.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SELECT UnitTests (CONDSTORE)\r\n", "gmail.select-unittests.txt"));

			for (int i = 0; i < 50; i++) {
				MimeMessage message;
				string latin1;
				long length;

				using (var resource = GetResourceStream (string.Format ("common.message.{0}.msg", i)))
					message = MimeMessage.Load (resource);

				using (var stream = new MemoryStream ()) {
					var options = FormatOptions.Default.Clone ();
					options.NewLineFormat = NewLineFormat.Dos;
					options.EnsureNewLine = true;

					message.WriteTo (options, stream);
					length = stream.Length;
					stream.Position = 0;

					using (var reader = new StreamReader (stream, Latin1))
						latin1 = reader.ReadToEnd ();
				}

				var tag = string.Format ("A{0:D8}", i + 9);
				var command = string.Format ("{0} APPEND UnitTests (\\Seen) ", tag);

				if (length > 4096) {
					command += "{" + length + "}\r\n";
					commands.Add (new ImapReplayCommand (command, "gmail.go-ahead.txt"));
					commands.Add (new ImapReplayCommand (tag, latin1 + "\r\n", string.Format ("gmail.append.{0}.txt", i + 1)));
				} else {
					command += "{" + length + "+}\r\n" + latin1 + "\r\n";
					commands.Add (new ImapReplayCommand (command, string.Format ("gmail.append.{0}.txt", i + 1)));
				}
			}
			commands.Add (new ImapReplayCommand ("A00000059 UID SEARCH RETURN () OR X-GM-MSGID 1 OR X-GM-THRID 5 OR X-GM-LABELS \"Custom Label\" X-GM-RAW \"has:attachment in:unread\"\r\n", "gmail.search.txt"));
			commands.Add (new ImapReplayCommand ("A00000060 UID FETCH 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODY X-GM-MSGID X-GM-THRID X-GM-LABELS)\r\n", "gmail.search-summary.txt"));
			commands.Add (new ImapReplayCommand ("A00000061 UID FETCH 1 (BODY.PEEK[])\r\n", "gmail.fetch.1.txt"));
			commands.Add (new ImapReplayCommand ("A00000062 UID FETCH 2 (BODY.PEEK[])\r\n", "gmail.fetch.2.txt"));
			commands.Add (new ImapReplayCommand ("A00000063 UID FETCH 3 (BODY.PEEK[])\r\n", "gmail.fetch.3.txt"));
			commands.Add (new ImapReplayCommand ("A00000064 UID FETCH 5 (BODY.PEEK[])\r\n", "gmail.fetch.5.txt"));
			commands.Add (new ImapReplayCommand ("A00000065 UID FETCH 7 (BODY.PEEK[])\r\n", "gmail.fetch.7.txt"));
			commands.Add (new ImapReplayCommand ("A00000066 UID FETCH 8 (BODY.PEEK[])\r\n", "gmail.fetch.8.txt"));
			commands.Add (new ImapReplayCommand ("A00000067 UID FETCH 9 (BODY.PEEK[])\r\n", "gmail.fetch.9.txt"));
			commands.Add (new ImapReplayCommand ("A00000068 UID FETCH 11 (BODY.PEEK[])\r\n", "gmail.fetch.11.txt"));
			commands.Add (new ImapReplayCommand ("A00000069 UID FETCH 12 (BODY.PEEK[])\r\n", "gmail.fetch.12.txt"));
			commands.Add (new ImapReplayCommand ("A00000070 UID FETCH 13 (BODY.PEEK[])\r\n", "gmail.fetch.13.txt"));
			commands.Add (new ImapReplayCommand ("A00000071 UID FETCH 14 (BODY.PEEK[])\r\n", "gmail.fetch.14.txt"));
			commands.Add (new ImapReplayCommand ("A00000072 UID FETCH 26 (BODY.PEEK[])\r\n", "gmail.fetch.26.txt"));
			commands.Add (new ImapReplayCommand ("A00000073 UID FETCH 27 (BODY.PEEK[])\r\n", "gmail.fetch.27.txt"));
			commands.Add (new ImapReplayCommand ("A00000074 UID FETCH 28 (BODY.PEEK[])\r\n", "gmail.fetch.28.txt"));
			commands.Add (new ImapReplayCommand ("A00000075 UID FETCH 29 (BODY.PEEK[])\r\n", "gmail.fetch.29.txt"));
			commands.Add (new ImapReplayCommand ("A00000076 UID FETCH 31 (BODY.PEEK[])\r\n", "gmail.fetch.31.txt"));
			commands.Add (new ImapReplayCommand ("A00000077 UID FETCH 34 (BODY.PEEK[])\r\n", "gmail.fetch.34.txt"));
			commands.Add (new ImapReplayCommand ("A00000078 UID FETCH 41 (BODY.PEEK[])\r\n", "gmail.fetch.41.txt"));
			commands.Add (new ImapReplayCommand ("A00000079 UID FETCH 42 (BODY.PEEK[])\r\n", "gmail.fetch.42.txt"));
			commands.Add (new ImapReplayCommand ("A00000080 UID FETCH 43 (BODY.PEEK[])\r\n", "gmail.fetch.43.txt"));
			commands.Add (new ImapReplayCommand ("A00000081 UID FETCH 50 (BODY.PEEK[])\r\n", "gmail.fetch.50.txt"));
			commands.Add (new ImapReplayCommand ("A00000082 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.set-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000083 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -X-GM-LABELS.SILENT (\\Important \"Custom Label\" NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000084 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.add-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000085 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.set-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000086 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) -X-GM-LABELS.SILENT (\\Important \"Custom Label\" NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000087 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) +X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.add-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000088 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 FLAGS (\\Answered \\Seen)\r\n", "gmail.set-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000089 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -FLAGS.SILENT (\\Answered)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000090 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", "gmail.add-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000091 CHECK\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000092 UNSELECT\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000093 SUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000094 LSUB \"\" \"%\"\r\n", "gmail.lsub-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000095 UNSUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000096 CREATE UnitTests/Dummy\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000097 LIST \"\" UnitTests/Dummy\r\n", "gmail.list-unittests-dummy.txt"));
			commands.Add (new ImapReplayCommand ("A00000098 RENAME UnitTests RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000099 DELETE RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000100 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestGMail ()
		{
			var commands = CreateGMailCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.AppendLimit.HasValue, "Expected AppendLimit to have a value");
				Assert.AreEqual (35651584, client.AppendLimit.Value, "Expected AppendLimit value to match");

				Assert.Throws<NotSupportedException> (() => client.EnableQuickResync ());
				Assert.Throws<NotSupportedException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Inboxes, new List<ImapEvent> {
						ImapEvent.FlagChange
					})
				}));
				Assert.Throws<NotSupportedException> (() => client.DisableNotify ());

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = personal.Create ("UnitTests", true);
				Assert.IsNotNull (created, "Expected a non-null created folder.");
				Assert.AreEqual (FolderAttributes.HasNoChildren, created.Attributes);

				Assert.IsNotNull (created.ParentFolder, "The ParentFolder property should not be null.");

				const MessageFlags ExpectedPermanentFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.UserDefined;
				const MessageFlags ExpectedAcceptedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen;
				var access = created.Open (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "The UnitTests folder was not opened with the expected access mode.");
				Assert.AreEqual (ExpectedPermanentFlags, created.PermanentFlags, "The PermanentFlags do not match the expected value.");
				Assert.AreEqual (ExpectedAcceptedFlags, created.AcceptedFlags, "The AcceptedFlags do not match the expected value.");

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						var message = MimeMessage.Load (stream);

						var uid = created.Append (message, MessageFlags.Seen);
						Assert.IsTrue (uid.HasValue, "Expected a UID to be returned from folder.Append().");
						Assert.AreEqual ((uint) (i + 1), uid.Value.Id, "The UID returned from the APPEND command does not match the expected UID.");
					}
				}

				var query = SearchQuery.GMailMessageId (1).Or (SearchQuery.GMailThreadId (5).Or (SearchQuery.HasGMailLabel ("Custom Label").Or (SearchQuery.GMailRawSearch ("has:attachment in:unread"))));
				var matches = created.Search (query);
				Assert.AreEqual (21, matches.Count);

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId | MessageSummaryItems.GMailLabels | MessageSummaryItems.GMailMessageId | MessageSummaryItems.GMailThreadId;
				var summaries = created.Fetch (matches, items);
				var indexes = new List<int> ();

				foreach (var summary in summaries) {
					Assert.AreEqual (1592225494819146100 + summary.UniqueId.Id, summary.GMailMessageId.Value, "GMailMessageId");
					Assert.AreEqual (1592225494819146100 + summary.UniqueId.Id, summary.GMailThreadId.Value, "GMailThreadId");
					Assert.AreEqual (2, summary.GMailLabels.Count, "GMailLabels.Count");
					Assert.AreEqual ("Test Messages", summary.GMailLabels[0]);
					Assert.AreEqual ("\\Important", summary.GMailLabels[1]);
					Assert.IsTrue (summary.UniqueId.IsValid, "UniqueId.IsValid");

					created.GetMessage (summary.UniqueId);
					indexes.Add (summary.Index);
				}

				var labels = new [] { "\\Important", "Custom Label", null };
				created.SetLabels (matches, labels, false);
				created.RemoveLabels (matches, labels, true);
				created.AddLabels (matches, labels, false);

				created.SetLabels (indexes, 5, labels, false);
				created.RemoveLabels (indexes, 5, labels, true);
				created.AddLabels (indexes, 5, labels, false);

				created.SetFlags (matches, MessageFlags.Seen | MessageFlags.Answered, false);
				created.RemoveFlags (matches, MessageFlags.Answered, true);
				created.AddFlags (matches, MessageFlags.Deleted, true);

				created.Check ();

				created.Close ();
				Assert.IsFalse (created.IsOpen, "Expected the UnitTests folder to be closed.");

				created.Subscribe ();
				Assert.IsTrue (created.IsSubscribed, "Expected IsSubscribed to be true after subscribing to the folder.");

				var subscribed = personal.GetSubfolders (true);
				Assert.IsTrue (subscribed.Contains (created), "Expected the list of subscribed folders to contain the UnitTests folder.");

				created.Unsubscribe ();
				Assert.IsFalse (created.IsSubscribed, "Expected IsSubscribed to be false after unsubscribing from the folder.");

				var dummy = created.Create ("Dummy", true);
				bool dummyRenamed = false;
				bool renamed = false;
				bool deleted = false;

				dummy.Renamed += (sender, e) => { dummyRenamed = true; };
				created.Renamed += (sender, e) => { renamed = true; };

				created.Rename (created.ParentFolder, "RenamedUnitTests");
				Assert.AreEqual ("RenamedUnitTests", created.Name);
				Assert.AreEqual ("RenamedUnitTests", created.FullName);
				Assert.IsTrue (renamed, "Expected the Rename event to be emitted for the UnitTests folder.");

				Assert.AreEqual ("RenamedUnitTests/Dummy", dummy.FullName);
				Assert.IsTrue (dummyRenamed, "Expected the Rename event to be emitted for the UnitTests/Dummy folder.");

				created.Deleted += (sender, e) => { deleted = true; };

				created.Delete ();
				Assert.IsTrue (deleted, "Expected the Deleted event to be emitted for the UnitTests folder.");
				Assert.IsFalse (created.Exists, "Expected Exists to be false after deleting the folder.");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestGMailAsync ()
		{
			var commands = CreateGMailCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (GMailInitialCapabilities, client.Capabilities);
				Assert.AreEqual (5, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (GMailAuthenticatedCapabilities, client.Capabilities);
				Assert.IsTrue (client.AppendLimit.HasValue, "Expected AppendLimit to have a value");
				Assert.AreEqual (35651584, client.AppendLimit.Value, "Expected AppendLimit value to match");

				Assert.ThrowsAsync<NotSupportedException> (async () => await client.EnableQuickResyncAsync ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Inboxes, new List<ImapEvent> {
						ImapEvent.FlagChange
					})
				}));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.DisableNotifyAsync ());

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.AreEqual (client.Inbox, folders[0], "Expected the first folder to be the Inbox.");
				Assert.AreEqual ("[Gmail]", folders[1].FullName, "Expected the second folder to be [Gmail].");
				Assert.AreEqual (FolderAttributes.NoSelect | FolderAttributes.HasChildren, folders[1].Attributes, "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = await personal.CreateAsync ("UnitTests", true);
				Assert.IsNotNull (created, "Expected a non-null created folder.");
				Assert.AreEqual (FolderAttributes.HasNoChildren, created.Attributes);

				Assert.IsNotNull (created.ParentFolder, "The ParentFolder property should not be null.");

				const MessageFlags ExpectedPermanentFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.UserDefined;
				const MessageFlags ExpectedAcceptedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen;
				var access = await created.OpenAsync (FolderAccess.ReadWrite);
				Assert.AreEqual (FolderAccess.ReadWrite, access, "The UnitTests folder was not opened with the expected access mode.");
				Assert.AreEqual (ExpectedPermanentFlags, created.PermanentFlags, "The PermanentFlags do not match the expected value.");
				Assert.AreEqual (ExpectedAcceptedFlags, created.AcceptedFlags, "The AcceptedFlags do not match the expected value.");

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						var message = MimeMessage.Load (stream);

						var uid = await created.AppendAsync (message, MessageFlags.Seen);
						Assert.IsTrue (uid.HasValue, "Expected a UID to be returned from folder.Append().");
						Assert.AreEqual ((uint) (i + 1), uid.Value.Id, "The UID returned from the APPEND command does not match the expected UID.");
					}
				}

				var query = SearchQuery.GMailMessageId (1).Or (SearchQuery.GMailThreadId (5).Or (SearchQuery.HasGMailLabel ("Custom Label").Or (SearchQuery.GMailRawSearch ("has:attachment in:unread"))));
				var matches = await created.SearchAsync (query);
				Assert.AreEqual (21, matches.Count);

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId | MessageSummaryItems.GMailLabels | MessageSummaryItems.GMailMessageId | MessageSummaryItems.GMailThreadId;
				var summaries = await created.FetchAsync (matches, items);
				var indexes = new List<int> ();

				foreach (var summary in summaries) {
					Assert.AreEqual (1592225494819146100 + summary.UniqueId.Id, summary.GMailMessageId.Value, "GMailMessageId");
					Assert.AreEqual (1592225494819146100 + summary.UniqueId.Id, summary.GMailThreadId.Value, "GMailThreadId");
					Assert.AreEqual (2, summary.GMailLabels.Count, "GMailLabels.Count");
					Assert.AreEqual ("Test Messages", summary.GMailLabels[0]);
					Assert.AreEqual ("\\Important", summary.GMailLabels[1]);
					Assert.IsTrue (summary.UniqueId.IsValid, "UniqueId.IsValid");

					await created.GetMessageAsync (summary.UniqueId);
					indexes.Add (summary.Index);
				}

				var labels = new [] { "\\Important", "Custom Label", null };
				await created.SetLabelsAsync (matches, labels, false);
				await created.RemoveLabelsAsync (matches, labels, true);
				await created.AddLabelsAsync (matches, labels, false);

				await created.SetLabelsAsync (indexes, 5, labels, false);
				await created.RemoveLabelsAsync (indexes, 5, labels, true);
				await created.AddLabelsAsync (indexes, 5, labels, false);

				await created.SetFlagsAsync (matches, MessageFlags.Seen | MessageFlags.Answered, false);
				await created.RemoveFlagsAsync (matches, MessageFlags.Answered, true);
				await created.AddFlagsAsync (matches, MessageFlags.Deleted, true);

				await created.CheckAsync ();

				await created.CloseAsync ();
				Assert.IsFalse (created.IsOpen, "Expected the UnitTests folder to be closed.");

				await created.SubscribeAsync ();
				Assert.IsTrue (created.IsSubscribed, "Expected IsSubscribed to be true after subscribing to the folder.");

				var subscribed = await personal.GetSubfoldersAsync (true);
				Assert.IsTrue (subscribed.Contains (created), "Expected the list of subscribed folders to contain the UnitTests folder.");

				await created.UnsubscribeAsync ();
				Assert.IsFalse (created.IsSubscribed, "Expected IsSubscribed to be false after unsubscribing from the folder.");

				var dummy = await created.CreateAsync ("Dummy", true);
				bool dummyRenamed = false;
				bool renamed = false;
				bool deleted = false;

				dummy.Renamed += (sender, e) => { dummyRenamed = true; };
				created.Renamed += (sender, e) => { renamed = true; };

				await created.RenameAsync (created.ParentFolder, "RenamedUnitTests");
				Assert.AreEqual ("RenamedUnitTests", created.Name);
				Assert.AreEqual ("RenamedUnitTests", created.FullName);
				Assert.IsTrue (renamed, "Expected the Rename event to be emitted for the UnitTests folder.");

				Assert.AreEqual ("RenamedUnitTests/Dummy", dummy.FullName);
				Assert.IsTrue (dummyRenamed, "Expected the Rename event to be emitted for the UnitTests/Dummy folder.");

				created.Deleted += (sender, e) => { deleted = true; };

				await created.DeleteAsync ();
				Assert.IsTrue (deleted, "Expected the Deleted event to be emitted for the UnitTests folder.");
				Assert.IsFalse (created.Exists, "Expected Exists to be false after deleting the folder.");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateGetFolderCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LIST \"\" Level1/Level2/Level3 RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-level3.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LIST \"\" Level1/Level2 RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-level2.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LIST \"\" Level1 RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-level1.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestGetFolder ()
		{
			var commands = CreateGetFolderCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var level3 = client.GetFolder ("Level1/Level2/Level3");
				Assert.AreEqual ("Level1/Level2/Level3", level3.FullName);
				Assert.AreEqual ("Level3", level3.Name);
				Assert.AreEqual ('/', level3.DirectorySeparator);
				Assert.AreEqual (FolderAttributes.HasNoChildren, level3.Attributes);

				var level2 = level3.ParentFolder;
				Assert.AreEqual ("Level1/Level2", level2.FullName);
				Assert.AreEqual ("Level2", level2.Name);
				Assert.AreEqual ('/', level2.DirectorySeparator);
				Assert.AreEqual (FolderAttributes.HasChildren, level2.Attributes);

				var level1 = level2.ParentFolder;
				Assert.AreEqual ("Level1", level1.FullName);
				Assert.AreEqual ("Level1", level1.Name);
				Assert.AreEqual ('/', level1.DirectorySeparator);
				Assert.AreEqual (FolderAttributes.HasChildren, level1.Attributes);

				var personal = level1.ParentFolder;
				Assert.AreEqual (string.Empty, personal.FullName);
				Assert.AreEqual (string.Empty, personal.Name);
				Assert.IsTrue (personal.IsNamespace, "IsNamespace");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestGetFolderAsync ()
		{
			var commands = CreateGetFolderCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var level3 = await client.GetFolderAsync ("Level1/Level2/Level3");
				Assert.AreEqual ("Level1/Level2/Level3", level3.FullName);
				Assert.AreEqual ("Level3", level3.Name);
				Assert.AreEqual ('/', level3.DirectorySeparator);
				Assert.AreEqual (FolderAttributes.HasNoChildren, level3.Attributes);

				var level2 = level3.ParentFolder;
				Assert.AreEqual ("Level1/Level2", level2.FullName);
				Assert.AreEqual ("Level2", level2.Name);
				Assert.AreEqual ('/', level2.DirectorySeparator);
				Assert.AreEqual (FolderAttributes.HasChildren, level2.Attributes);

				var level1 = level2.ParentFolder;
				Assert.AreEqual ("Level1", level1.FullName);
				Assert.AreEqual ("Level1", level1.Name);
				Assert.AreEqual ('/', level1.DirectorySeparator);
				Assert.AreEqual (FolderAttributes.HasChildren, level1.Attributes);

				var personal = level1.ParentFolder;
				Assert.AreEqual (string.Empty, personal.FullName);
				Assert.AreEqual (string.Empty, personal.Name);
				Assert.IsTrue (personal.IsNamespace, "IsNamespace");

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateIdleCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 IDLE\r\n", "gmail.idle.txt"));
			commands.Add (new ImapReplayCommand ("A00000006", "DONE\r\n", "gmail.idle-done.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestIdle ()
		{
			var commands = CreateIdleCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);

				using (var done = new CancellationTokenSource ()) {
					int count = 0, expunged = 0, flags = 0;
					bool droppedToZero = false;

					inbox.MessageExpunged += (o, e) => {
						expunged++;
						Assert.AreEqual (0, e.Index, "Expunged Index");
					};
					inbox.MessageFlagsChanged += (o, e) => {
						flags++;
						Assert.AreEqual (MessageFlags.Answered | MessageFlags.Deleted | MessageFlags.Seen, e.Flags, "Flags");
					};
					inbox.CountChanged += (o, e) => {
						count++;

						if (inbox.Count == 0)
							droppedToZero = true;
						else if (droppedToZero && inbox.Count == 1)
							done.Cancel ();
					};

					client.Idle (done.Token);

					Assert.AreEqual (21, expunged, "Unexpected number of Expunged events");
					Assert.AreEqual (23, count, "Unexpected number of CountChanged events");
					Assert.AreEqual (21, flags, "Unexpected number of FlagsChanged events");
					Assert.AreEqual (1, inbox.Count, "Count");
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestIdleAsync ()
		{
			var commands = CreateIdleCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);

				using (var done = new CancellationTokenSource ()) {
					int count = 0, expunged = 0, flags = 0;
					bool droppedToZero = false;

					inbox.MessageExpunged += (o, e) => {
						expunged++;
						Assert.AreEqual (0, e.Index, "Expunged Index");
					};
					inbox.MessageFlagsChanged += (o, e) => {
						flags++;
						Assert.AreEqual (MessageFlags.Answered | MessageFlags.Deleted | MessageFlags.Seen, e.Flags, "Flags");
					};
					inbox.CountChanged += (o, e) => {
						count++;

						if (inbox.Count == 0)
							droppedToZero = true;
						else if (droppedToZero && inbox.Count == 1)
							done.Cancel ();
					};

					await client.IdleAsync (done.Token);

					Assert.AreEqual (21, expunged, "Unexpected number of Expunged events");
					Assert.AreEqual (23, count, "Unexpected number of CountChanged events");
					Assert.AreEqual (21, flags, "Unexpected number of FlagsChanged events");
					Assert.AreEqual (1, inbox.Count, "Count");
				}

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateNotifyCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "dovecot.greeting-preauth.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 NAMESPACE\r\n", "dovecot.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"%\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.notify-list-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 EXAMINE Folder (CONDSTORE)\r\n", "dovecot.examine-folder.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 NOTIFY SET STATUS (PERSONAL (MailboxName SubscriptionChange)) (SELECTED (MessageNew (UID FLAGS ENVELOPE BODYSTRUCTURE MODSEQ) MessageExpunge FlagChange)) (SUBTREE (INBOX Folder) (MessageNew MessageExpunge MailboxMetadataChange ServerMetadataChange))\r\n", "dovecot.notify.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 IDLE\r\n", "dovecot.notify-idle.txt"));
			commands.Add (new ImapReplayCommand ("A00000006", "DONE\r\n", "dovecot.notify-idle-done.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 NOTIFY NONE\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000008 LOGOUT\r\n", "gmail.logout.txt"));

			// TODO: test MessageNew w/ headers

			return commands;
		}

		[Test]
		public void TestNotify ()
		{
			const MessageSummaryItems items = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq;
			var commands = CreateNotifyCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				var inbox = client.Inbox;

				var folder = folders.FirstOrDefault (x => x.Name == "Folder");
				var deleteMe = folders.FirstOrDefault (x => x.Name == "DeleteMe");
				var renameMe = folders.FirstOrDefault (x => x.Name == "RenameMe");
				var subscribeMe = folders.FirstOrDefault (x => x.Name == "SubscribeMe");
				var unsubscribeMe = folders.FirstOrDefault (x => x.Name == "UnsubscribeMe");

				folder.Open (FolderAccess.ReadOnly);

				// Test some InvalidOperationExceptions

				// This fires due to non-message events for a Selected mailbox filter
				Assert.Throws<InvalidOperationException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						ImapEvent.MailboxName,
					})
				}));

				// This fires due to having MessageNew, but not MessageExpunged
				Assert.Throws<InvalidOperationException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items, new HashSet<HeaderId> ())
					})
				}));

				// This fires due to having MessageNew, but not MessageExpunged
				Assert.Throws<InvalidOperationException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items, new HashSet<string> ())
					})
				}));

				// This fires due to having FlagsChanged but not MessageNew and MessageExpunged
				Assert.Throws<InvalidOperationException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						ImapEvent.AnnotationChange, ImapEvent.FlagChange
					})
				}));

				// This fires due to MessageNew being aded to a non-Selected folder
				Assert.Throws<InvalidOperationException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Personal, new List<ImapEvent> {
						new ImapEvent.MessageNew (items, new HashSet<string> ())
					})
				}));

				client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Personal, new List<ImapEvent> {
						ImapEvent.MailboxName,
						ImapEvent.SubscriptionChange
					}),
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items),
						ImapEvent.MessageExpunge,
						ImapEvent.FlagChange
					}),
					new ImapEventGroup (new ImapMailboxFilter.Subtree (inbox, folder), new List<ImapEvent> {
						new ImapEvent.MessageNew (),
						ImapEvent.MessageExpunge,
						ImapEvent.MailboxMetadataChange,
						ImapEvent.ServerMetadataChange
					}),
				});

				// Passing true to notify will update Count
				Assert.AreEqual (1, inbox.Count, "Messages in INBOX");
				Assert.AreEqual (0, folder.Count, "Messages in Folder");

				IMessageSummary fetched = null;
				var folderMessageSummaryFetched = 0;
				var folderCountChanged = 0;
				var folderFlagsChanged = 0;

				var inboxHighestModSeqChanged = 0;
				var inboxMetadataChanged = 0;
				var inboxCountChanged = 0;
				var metadataChanged = 0;
				var unsubscribed = 0;
				var subscribed = 0;
				var created = 0;
				var deleted = 0;
				var renamed = 0;

				client.FolderCreated += (sender, e) => {
					Assert.AreEqual ("NewFolder", e.Folder.FullName, "e.Folder.FullName");
					Assert.AreEqual (FolderAttributes.HasNoChildren, e.Folder.Attributes, "e.Folder.Attributes");
					created++;
				};

				client.MetadataChanged += (sender, e) => {
					Assert.AreEqual ("/private/comment", e.Metadata.Tag.Id, "Metadata.Tag");
					Assert.AreEqual ("this is a comment", e.Metadata.Value, "Metadata.Value");
					metadataChanged++;
				};

				inbox.MetadataChanged += (sender, e) => {
					Assert.AreEqual ("/private/comment", e.Metadata.Tag.Id, "Metadata.Tag");
					Assert.AreEqual ("this is a comment", e.Metadata.Value, "Metadata.Value");
					inboxMetadataChanged++;
				};

				deleteMe.Deleted += (sender, e) => {
					deleted++;
				};

				renameMe.Renamed += (sender, e) => {
					Assert.AreEqual ("RenamedFolder", renameMe.FullName, "renameMe.FullName");
					renamed++;
				};

				subscribeMe.Subscribed += (sender, e) => {
					subscribed++;
				};

				unsubscribeMe.Unsubscribed += (sender, e) => {
					unsubscribed++;
				};

				inbox.HighestModSeqChanged += (sender, e) => {
					inboxHighestModSeqChanged++;
				};

				inbox.CountChanged += (sender, e) => {
					inboxCountChanged++;
				};

				folder.MessageSummaryFetched += (sender, e) => {
					folderMessageSummaryFetched++;
					fetched = e.Message;
				};

				folder.MessageFlagsChanged += (sender, e) => {
					folderFlagsChanged++;
				};

				folder.CountChanged += (sender, e) => {
					folderCountChanged++;
				};

				using (var done = new CancellationTokenSource ()) {
					folder.CountChanged += (o, e) => {
						done.Cancel ();
					};

					client.Idle (done.Token);
				}

				Assert.AreEqual (3, inbox.Count, "Inbox.Count");
				Assert.AreEqual (3, inbox.Unread, "Inbox.Unread");
				Assert.AreEqual (4, inbox.UidNext.Value.Id, "Inbox.UidNext");
				Assert.AreEqual (3, inbox.HighestModSeq, "Inbox.HighestModSeq");

				Assert.AreEqual (1, inboxHighestModSeqChanged, "Inbox.HighestModSeqChanged");
				Assert.AreEqual (1, inboxMetadataChanged, "Inbox.MetadataChanged");
				Assert.AreEqual (1, inboxCountChanged, "Inbox.CountChanged");

				Assert.AreEqual (1, created, "FolderCreated");
				Assert.AreEqual (1, deleted, "deleteMe.Deleted");
				Assert.AreEqual (1, renamed, "renameMe.Renamed");
				Assert.AreEqual (1, subscribed, "subscribeMe.Deleted");
				Assert.AreEqual (1, unsubscribed, "unsubscribeMe.Renamed");
				Assert.AreEqual (1, metadataChanged, "metadataChanged");

				Assert.AreEqual (1, folder.Count, "Folder.Count");
				Assert.AreEqual (1, folderCountChanged, "Folder.CountChanged");
				Assert.AreEqual (1, folderFlagsChanged, "Folder.MessageFlagsChanged");
				Assert.AreEqual (1, folderMessageSummaryFetched, "Folder.MessageSummaryFetched");

				Assert.AreEqual (1, fetched.UniqueId.Id, "fetched.UniqueId");
				Assert.AreEqual (MessageFlags.Recent, fetched.Flags.Value, "fetched.Flags");
				Assert.AreEqual ("IMAP4rev1 WG mtg summary and minutes", fetched.Envelope.Subject, "fetched.Envelope.Subject");
				var body = fetched.Body as BodyPartBasic;
				Assert.IsNotNull (fetched.Body, "fetched.Body");
				Assert.AreEqual (3028, body.Octets, "fetched.Body.Octets");
				Assert.AreEqual (1, fetched.ModSeq.Value, "fetched.ModSeq");

				client.DisableNotify ();

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestNotifyAsync ()
		{
			const MessageSummaryItems items = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq;
			var commands = CreateNotifyCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				var inbox = client.Inbox;

				var folder = folders.FirstOrDefault (x => x.Name == "Folder");
				var deleteMe = folders.FirstOrDefault (x => x.Name == "DeleteMe");
				var renameMe = folders.FirstOrDefault (x => x.Name == "RenameMe");
				var subscribeMe = folders.FirstOrDefault (x => x.Name == "SubscribeMe");
				var unsubscribeMe = folders.FirstOrDefault (x => x.Name == "UnsubscribeMe");

				await folder.OpenAsync (FolderAccess.ReadOnly);

				// Test some InvalidOperationExceptions

				// This fires due to non-message events for a Selected mailbox filter
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						ImapEvent.MailboxName,
					})
				}));

				// This fires due to having MessageNew, but not MessageExpunged
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items, new HashSet<HeaderId> ())
					})
				}));

				// This fires due to having MessageNew, but not MessageExpunged
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items, new HashSet<string> ())
					})
				}));

				// This fires due to having FlagsChanged but not MessageNew and MessageExpunged
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						ImapEvent.AnnotationChange, ImapEvent.FlagChange
					})
				}));

				// This fires due to MessageNew being aded to a non-Selected folder
				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Personal, new List<ImapEvent> {
						new ImapEvent.MessageNew (items, new HashSet<string> ())
					})
				}));

				await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Personal, new List<ImapEvent> {
						ImapEvent.MailboxName,
						ImapEvent.SubscriptionChange
					}),
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items),
						ImapEvent.MessageExpunge,
						ImapEvent.FlagChange
					}),
					new ImapEventGroup (new ImapMailboxFilter.Subtree (inbox, folder), new List<ImapEvent> {
						new ImapEvent.MessageNew (),
						ImapEvent.MessageExpunge,
						ImapEvent.MailboxMetadataChange,
						ImapEvent.ServerMetadataChange
					}),
				});

				// Passing true to notify will update Count
				Assert.AreEqual (1, inbox.Count, "Messages in INBOX");
				Assert.AreEqual (0, folder.Count, "Messages in Folder");

				IMessageSummary fetched = null;
				var folderMessageSummaryFetched = 0;
				var folderCountChanged = 0;
				var folderFlagsChanged = 0;

				var inboxHighestModSeqChanged = 0;
				var inboxMetadataChanged = 0;
				var inboxCountChanged = 0;
				var metadataChanged = 0;
				var unsubscribed = 0;
				var subscribed = 0;
				var created = 0;
				var deleted = 0;
				var renamed = 0;

				client.FolderCreated += (sender, e) => {
					Assert.AreEqual ("NewFolder", e.Folder.FullName, "e.Folder.FullName");
					Assert.AreEqual (FolderAttributes.HasNoChildren, e.Folder.Attributes, "e.Folder.Attributes");
					created++;
				};

				client.MetadataChanged += (sender, e) => {
					Assert.AreEqual ("/private/comment", e.Metadata.Tag.Id, "Metadata.Tag");
					Assert.AreEqual ("this is a comment", e.Metadata.Value, "Metadata.Value");
					metadataChanged++;
				};

				inbox.MetadataChanged += (sender, e) => {
					Assert.AreEqual ("/private/comment", e.Metadata.Tag.Id, "Metadata.Tag");
					Assert.AreEqual ("this is a comment", e.Metadata.Value, "Metadata.Value");
					inboxMetadataChanged++;
				};

				deleteMe.Deleted += (sender, e) => {
					deleted++;
				};

				renameMe.Renamed += (sender, e) => {
					Assert.AreEqual ("RenamedFolder", renameMe.FullName, "renameMe.FullName");
					renamed++;
				};

				subscribeMe.Subscribed += (sender, e) => {
					subscribed++;
				};

				unsubscribeMe.Unsubscribed += (sender, e) => {
					unsubscribed++;
				};

				inbox.HighestModSeqChanged += (sender, e) => {
					inboxHighestModSeqChanged++;
				};

				inbox.CountChanged += (sender, e) => {
					inboxCountChanged++;
				};

				folder.MessageSummaryFetched += (sender, e) => {
					folderMessageSummaryFetched++;
					fetched = e.Message;
				};

				folder.MessageFlagsChanged += (sender, e) => {
					folderFlagsChanged++;
				};

				folder.CountChanged += (sender, e) => {
					folderCountChanged++;
				};

				using (var done = new CancellationTokenSource ()) {
					folder.CountChanged += (o, e) => {
						done.Cancel ();
					};

					await client.IdleAsync (done.Token);
				}

				Assert.AreEqual (3, inbox.Count, "Inbox.Count");
				Assert.AreEqual (3, inbox.Unread, "Inbox.Unread");
				Assert.AreEqual (4, inbox.UidNext.Value.Id, "Inbox.UidNext");
				Assert.AreEqual (3, inbox.HighestModSeq, "Inbox.HighestModSeq");

				Assert.AreEqual (1, inboxHighestModSeqChanged, "Inbox.HighestModSeqChanged");
				Assert.AreEqual (1, inboxMetadataChanged, "Inbbox.MetadataChanged");
				Assert.AreEqual (1, inboxCountChanged, "Inbox.CountChanged");

				Assert.AreEqual (1, created, "FolderCreated");
				Assert.AreEqual (1, deleted, "deleteMe.Deleted");
				Assert.AreEqual (1, renamed, "renameMe.Renamed");
				Assert.AreEqual (1, subscribed, "subscribeMe.Deleted");
				Assert.AreEqual (1, unsubscribed, "unsubscribeMe.Renamed");
				Assert.AreEqual (1, metadataChanged, "metadataChanged");

				Assert.AreEqual (1, folder.Count, "Folder.Count");
				Assert.AreEqual (1, folderCountChanged, "Folder.CountChanged");
				Assert.AreEqual (1, folderFlagsChanged, "Folder.MessageFlagsChanged");
				Assert.AreEqual (1, folderMessageSummaryFetched, "Folder.MessageSummaryFetched");

				Assert.AreEqual (1, fetched.UniqueId.Id, "fetched.UniqueId");
				Assert.AreEqual (MessageFlags.Recent, fetched.Flags.Value, "fetched.Flags");
				Assert.AreEqual ("IMAP4rev1 WG mtg summary and minutes", fetched.Envelope.Subject, "fetched.Envelope.Subject");
				var body = fetched.Body as BodyPartBasic;
				Assert.IsNotNull (fetched.Body, "fetched.Body");
				Assert.AreEqual (3028, body.Octets, "fetched.Body.Octets");
				Assert.AreEqual (1, fetched.ModSeq.Value, "fetched.ModSeq");

				await client.DisableNotifyAsync ();

				await client.DisconnectAsync (true);
			}
		}

		List<ImapReplayCommand> CreateCompressCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 COMPRESS DEFLATE\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000006 COMPRESS DEFLATE\r\n", ImapReplayCommandResponse.NO, true));
			commands.Add (new ImapReplayCommand ("A00000007 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt", true));
			commands.Add (new ImapReplayCommand ("A00000008 UID SEARCH RETURN () ALL\r\n", "gmail.search.txt", true));
			commands.Add (new ImapReplayCommand ("A00000009 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK, true));
			commands.Add (new ImapReplayCommand ("A00000010 UID EXPUNGE 1:3\r\n", "gmail.expunge.txt", true));
			commands.Add (new ImapReplayCommand ("A00000011 LOGOUT\r\n", "gmail.logout.txt", true));

			return commands;
		}

		[Test]
		public void TestCompress ()
		{
			var commands = CreateCompressCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				client.Compress ();

				Assert.Throws<ImapCommandException> (() => client.Compress ());

				int changed = 0, expunged = 0;
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.AreEqual (0, e.Index, "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				var uids = inbox.Search (SearchQuery.All);
				inbox.AddFlags (uids, MessageFlags.Deleted, true);

				uids = new UniqueIdRange (0, 1, 3);
				inbox.Expunge (uids);

				Assert.AreEqual (3, expunged, "Unexpected number of Expunged events");
				Assert.AreEqual (4, changed, "Unexpected number of CountChanged events");
				Assert.AreEqual (18, inbox.Count, "Count");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCompressAsync ()
		{
			var commands = CreateCompressCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				await client.CompressAsync ();

				Assert.ThrowsAsync<ImapCommandException> (async () => await client.CompressAsync ());

				int changed = 0, expunged = 0;
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.AreEqual (0, e.Index, "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				var uids = await inbox.SearchAsync (SearchQuery.All);
				await inbox.AddFlagsAsync (uids, MessageFlags.Deleted, true);

				uids = new UniqueIdRange (0, 1, 3);
				await inbox.ExpungeAsync (uids);

				Assert.AreEqual (3, expunged, "Unexpected number of Expunged events");
				Assert.AreEqual (4, changed, "Unexpected number of CountChanged events");
				Assert.AreEqual (18, inbox.Count, "Count");

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestAccessControlLists ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "acl.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "acl.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETACL INBOX\r\n", "acl.getacl.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LISTRIGHTS INBOX smith\r\n", "acl.listrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 MYRIGHTS INBOX\r\n", "acl.myrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETACL INBOX smith +lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000009 SETACL INBOX smith -lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000010 SETACL INBOX smith lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000011 DELETEACL INBOX smith\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (AclInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.AreEqual ("texk", client.Rights.ToString (), "Rights");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (AclAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// GETACL INBOX
				var acl = client.Inbox.GetAccessControlList ();
				Assert.AreEqual (2, acl.Count, "The number of access controls does not match.");
				Assert.AreEqual ("Fred", acl[0].Name, "The identifier for the first access control does not match.");
				Assert.AreEqual ("rwipslxetad", acl[0].Rights.ToString (), "The access rights for the first access control does not match.");
				Assert.AreEqual ("Chris", acl[1].Name, "The identifier for the second access control does not match.");
				Assert.AreEqual ("lrswi", acl[1].Rights.ToString (), "The access rights for the second access control does not match.");

				// LISTRIGHTS INBOX smith
				Assert.Throws<ArgumentNullException> (() => client.Inbox.GetAccessRights (null));
				//Assert.Throws<ArgumentException> (() => client.Inbox.GetAccessRights (string.Empty));
				var rights = client.Inbox.GetAccessRights ("smith");
				Assert.AreEqual ("lrswipkxtecda0123456789", rights.ToString (), "The access rights do not match for user smith.");

				// MYRIGHTS INBOX
				rights = client.Inbox.GetMyAccessRights ();
				Assert.AreEqual ("rwiptsldaex", rights.ToString (), "My access rights do not match.");

				// SETACL INBOX smith +lrswida
				var empty = new AccessRights (string.Empty);
				rights = new AccessRights ("lrswida");
				Assert.Throws<ArgumentNullException> (() => client.Inbox.AddAccessRights (null, rights));
				//Assert.Throws<ArgumentException> (() => client.Inbox.AddAccessRights (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (() => client.Inbox.AddAccessRights ("smith", null));
				Assert.Throws<ArgumentException> (() => client.Inbox.AddAccessRights ("smith", empty));
				client.Inbox.AddAccessRights ("smith", rights);

				// SETACL INBOX smith -lrswida
				Assert.Throws<ArgumentNullException> (() => client.Inbox.RemoveAccessRights (null, rights));
				//Assert.Throws<ArgumentException> (() => client.Inbox.RemoveAccessRights (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (() => client.Inbox.RemoveAccessRights ("smith", null));
				Assert.Throws<ArgumentException> (() => client.Inbox.RemoveAccessRights ("smith", empty));
				client.Inbox.RemoveAccessRights ("smith", rights);

				// SETACL INBOX smith lrswida
				Assert.Throws<ArgumentNullException> (() => client.Inbox.SetAccessRights (null, rights));
				//Assert.Throws<ArgumentException> (() => client.Inbox.SetAccessRights (string.Empty, rights));
				Assert.Throws<ArgumentNullException> (() => client.Inbox.SetAccessRights ("smith", null));
				client.Inbox.SetAccessRights ("smith", rights);

				// DELETEACL INBOX smith
				Assert.Throws<ArgumentNullException> (() => client.Inbox.RemoveAccess (null));
				//Assert.Throws<ArgumentException> (() => client.Inbox.RemoveAccess (string.Empty));
				client.Inbox.RemoveAccess ("smith");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestAccessControlListsAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "acl.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "acl.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETACL INBOX\r\n", "acl.getacl.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 LISTRIGHTS INBOX smith\r\n", "acl.listrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 MYRIGHTS INBOX\r\n", "acl.myrights.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETACL INBOX smith +lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000009 SETACL INBOX smith -lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000010 SETACL INBOX smith lrswida\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000011 DELETEACL INBOX smith\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (AclInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.AreEqual ("texk", client.Rights.ToString (), "Rights");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (AclAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// GETACL INBOX
				var acl = await client.Inbox.GetAccessControlListAsync ();
				Assert.AreEqual (2, acl.Count, "The number of access controls does not match.");
				Assert.AreEqual ("Fred", acl[0].Name, "The identifier for the first access control does not match.");
				Assert.AreEqual ("rwipslxetad", acl[0].Rights.ToString (), "The access rights for the first access control does not match.");
				Assert.AreEqual ("Chris", acl[1].Name, "The identifier for the second access control does not match.");
				Assert.AreEqual ("lrswi", acl[1].Rights.ToString (), "The access rights for the second access control does not match.");

				// LISTRIGHTS INBOX smith
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.GetAccessRightsAsync (null));
				//Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.GetAccessRightsAsync (string.Empty));
				var rights = await client.Inbox.GetAccessRightsAsync ("smith");
				Assert.AreEqual ("lrswipkxtecda0123456789", rights.ToString (), "The access rights do not match for user smith.");

				// MYRIGHTS INBOX
				rights = await client.Inbox.GetMyAccessRightsAsync ();
				Assert.AreEqual ("rwiptsldaex", rights.ToString (), "My access rights do not match.");

				// SETACL INBOX smith +lrswida
				var empty = new AccessRights (string.Empty);
				rights = new AccessRights ("lrswida");
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.AddAccessRightsAsync (null, rights));
				//Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.AddAccessRightsAsync (string.Empty, rights));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.AddAccessRightsAsync ("smith", null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.AddAccessRightsAsync ("smith", empty));
				await client.Inbox.AddAccessRightsAsync ("smith", rights);

				// SETACL INBOX smith -lrswida
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.RemoveAccessRightsAsync (null, rights));
				//Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.RemoveAccessRightsAsync (string.Empty, rights));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.RemoveAccessRightsAsync ("smith", null));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.RemoveAccessRightsAsync ("smith", empty));
				await client.Inbox.RemoveAccessRightsAsync ("smith", rights);

				// SETACL INBOX smith lrswida
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.SetAccessRightsAsync (null, rights));
				//Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.SetAccessRightsAsync (string.Empty, rights));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.SetAccessRightsAsync ("smith", null));
				await client.Inbox.SetAccessRightsAsync ("smith", rights);

				// DELETEACL INBOX smith
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.RemoveAccessAsync (null));
				//Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.RemoveAccessAsync (string.Empty));
				await client.Inbox.RemoveAccessAsync ("smith");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestMetadata ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "metadata.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "metadata.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETMETADATA \"\" /private/comment\r\n", "metadata.getmetadata.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 GETMETADATA \"\" (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.getmetadata-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 GETMETADATA \"\" /private/comment /shared/comment\r\n", "metadata.getmetadata-multi.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETMETADATA \"\" (/private/comment \"this is a comment\")\r\n", "metadata.setmetadata-noprivate.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 SETMETADATA \"\" (/private/comment \"this comment is too long!\")\r\n", "metadata.setmetadata-maxsize.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 SETMETADATA \"\" (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.setmetadata-toomany.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 SETMETADATA \"\" (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000012 GETMETADATA INBOX /private/comment\r\n", "metadata.inbox-getmetadata.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 GETMETADATA INBOX (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.inbox-getmetadata-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 GETMETADATA INBOX /private/comment /shared/comment\r\n", "metadata.inbox-getmetadata-multi.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 SETMETADATA INBOX (/private/comment \"this is a comment\")\r\n", "metadata.inbox-setmetadata-noprivate.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 SETMETADATA INBOX (/private/comment \"this comment is too long!\")\r\n", "metadata.inbox-setmetadata-maxsize.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 SETMETADATA INBOX (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.inbox-setmetadata-toomany.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 SETMETADATA INBOX (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				MetadataCollection metadata;
				MetadataOptions options;

				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (MetadataInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (MetadataAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// GETMETADATA
				Assert.AreEqual ("this is a comment", client.GetMetadata (MetadataTag.PrivateComment), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = client.GetMetadata (options, new [] { new MetadataTag ("/private") });
				Assert.AreEqual (1, metadata.Count, "Expected 1 metadata value.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "Metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "Metadata value did not match.");
				Assert.AreEqual (2199, options.LongEntries, "LongEntries does not match.");

				metadata = client.GetMetadata (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.AreEqual (2, metadata.Count, "Expected 2 metadata values.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "First metadata tag did not match.");
				Assert.AreEqual (MetadataTag.SharedComment.Id, metadata[1].Tag.Id, "Second metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "First metadata value did not match.");
				Assert.AreEqual ("this is a shared comment", metadata[1].Value, "Second metadata value did not match.");

				// SETMETADATA
				Assert.Throws<ImapCommandException> (() => client.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a comment")
				})), "Expected NOPRIVATE RESP-CODE.");
				Assert.Throws<ImapCommandException> (() => client.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this comment is too long!")
				})), "Expected MAXSIZE RESP-CODE.");
				Assert.Throws<ImapCommandException> (() => client.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a private comment"),
					new Metadata (MetadataTag.SharedComment, "this is a shared comment"),
				})), "Expected TOOMANY RESP-CODE.");
				client.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				// GETMETADATA folder
				Assert.AreEqual ("this is a comment", inbox.GetMetadata (MetadataTag.PrivateComment), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = inbox.GetMetadata (options, new [] { new MetadataTag ("/private") });
				Assert.AreEqual (1, metadata.Count, "Expected 1 metadata value.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "Metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "Metadata value did not match.");
				Assert.AreEqual (2199, options.LongEntries, "LongEntries does not match.");

				metadata = inbox.GetMetadata (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.AreEqual (2, metadata.Count, "Expected 2 metadata values.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "First metadata tag did not match.");
				Assert.AreEqual (MetadataTag.SharedComment.Id, metadata[1].Tag.Id, "Second metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "First metadata value did not match.");
				Assert.AreEqual ("this is a shared comment", metadata[1].Value, "Second metadata value did not match.");

				// SETMETADATA folder
				Assert.Throws<ImapCommandException> (() => inbox.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a comment")
				})), "Expected NOPRIVATE RESP-CODE.");
				Assert.Throws<ImapCommandException> (() => inbox.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this comment is too long!")
				})), "Expected MAXSIZE RESP-CODE.");
				Assert.Throws<ImapCommandException> (() => inbox.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a private comment"),
					new Metadata (MetadataTag.SharedComment, "this is a shared comment"),
				})), "Expected TOOMANY RESP-CODE.");
				inbox.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestMetadataAsync ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "metadata.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "metadata.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 GETMETADATA \"\" /private/comment\r\n", "metadata.getmetadata.txt"));
			commands.Add (new ImapReplayCommand ("A00000006 GETMETADATA \"\" (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.getmetadata-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000007 GETMETADATA \"\" /private/comment /shared/comment\r\n", "metadata.getmetadata-multi.txt"));
			commands.Add (new ImapReplayCommand ("A00000008 SETMETADATA \"\" (/private/comment \"this is a comment\")\r\n", "metadata.setmetadata-noprivate.txt"));
			commands.Add (new ImapReplayCommand ("A00000009 SETMETADATA \"\" (/private/comment \"this comment is too long!\")\r\n", "metadata.setmetadata-maxsize.txt"));
			commands.Add (new ImapReplayCommand ("A00000010 SETMETADATA \"\" (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.setmetadata-toomany.txt"));
			commands.Add (new ImapReplayCommand ("A00000011 SETMETADATA \"\" (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000012 GETMETADATA INBOX /private/comment\r\n", "metadata.inbox-getmetadata.txt"));
			commands.Add (new ImapReplayCommand ("A00000013 GETMETADATA INBOX (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.inbox-getmetadata-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000014 GETMETADATA INBOX /private/comment /shared/comment\r\n", "metadata.inbox-getmetadata-multi.txt"));
			commands.Add (new ImapReplayCommand ("A00000015 SETMETADATA INBOX (/private/comment \"this is a comment\")\r\n", "metadata.inbox-setmetadata-noprivate.txt"));
			commands.Add (new ImapReplayCommand ("A00000016 SETMETADATA INBOX (/private/comment \"this comment is too long!\")\r\n", "metadata.inbox-setmetadata-maxsize.txt"));
			commands.Add (new ImapReplayCommand ("A00000017 SETMETADATA INBOX (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.inbox-setmetadata-toomany.txt"));
			commands.Add (new ImapReplayCommand ("A00000018 SETMETADATA INBOX (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK));

			using (var client = new ImapClient ()) {
				MetadataCollection metadata;
				MetadataOptions options;

				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				Assert.AreEqual (MetadataInitialCapabilities, client.Capabilities);
				Assert.AreEqual (4, client.AuthenticationMechanisms.Count);
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.IsTrue (client.AuthenticationMechanisms.Contains ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (MetadataAuthenticatedCapabilities, client.Capabilities);

				var inbox = client.Inbox;
				Assert.IsNotNull (inbox, "Expected non-null Inbox folder.");
				Assert.AreEqual (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed, inbox.Attributes, "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.IsNotNull (folder, "Expected non-null {0} folder.", special);
						Assert.AreEqual (expected, folder.Attributes, "Expected {0} attributes to be \\HasNoChildren.", special);
					} else {
						Assert.IsNull (folder, "Expected null {0} folder.", special);
					}
				}

				// GETMETADATA
				Assert.AreEqual ("this is a comment", await client.GetMetadataAsync (MetadataTag.PrivateComment), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = await client.GetMetadataAsync (options, new [] { new MetadataTag ("/private") });
				Assert.AreEqual (1, metadata.Count, "Expected 1 metadata value.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "Metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "Metadata value did not match.");
				Assert.AreEqual (2199, options.LongEntries, "LongEntries does not match.");

				metadata = await client.GetMetadataAsync (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.AreEqual (2, metadata.Count, "Expected 2 metadata values.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "First metadata tag did not match.");
				Assert.AreEqual (MetadataTag.SharedComment.Id, metadata[1].Tag.Id, "Second metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "First metadata value did not match.");
				Assert.AreEqual ("this is a shared comment", metadata[1].Value, "Second metadata value did not match.");

				// SETMETADATA
				Assert.ThrowsAsync<ImapCommandException> (async () => await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a comment")
				})), "Expected NOPRIVATE RESP-CODE.");
				Assert.ThrowsAsync<ImapCommandException> (async () => await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this comment is too long!")
				})), "Expected MAXSIZE RESP-CODE.");
				Assert.ThrowsAsync<ImapCommandException> (async () => await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a private comment"),
					new Metadata (MetadataTag.SharedComment, "this is a shared comment"),
				})), "Expected TOOMANY RESP-CODE.");
				await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				// GETMETADATA folder
				Assert.AreEqual ("this is a comment", await inbox.GetMetadataAsync (MetadataTag.PrivateComment), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = await inbox.GetMetadataAsync (options, new [] { new MetadataTag ("/private") });
				Assert.AreEqual (1, metadata.Count, "Expected 1 metadata value.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "Metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "Metadata value did not match.");
				Assert.AreEqual (2199, options.LongEntries, "LongEntries does not match.");

				metadata = await inbox.GetMetadataAsync (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.AreEqual (2, metadata.Count, "Expected 2 metadata values.");
				Assert.AreEqual (MetadataTag.PrivateComment.Id, metadata[0].Tag.Id, "First metadata tag did not match.");
				Assert.AreEqual (MetadataTag.SharedComment.Id, metadata[1].Tag.Id, "Second metadata tag did not match.");
				Assert.AreEqual ("this is a private comment", metadata[0].Value, "First metadata value did not match.");
				Assert.AreEqual ("this is a shared comment", metadata[1].Value, "Second metadata value did not match.");

				// SETMETADATA folder
				Assert.ThrowsAsync<ImapCommandException> (async () => await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a comment")
				})), "Expected NOPRIVATE RESP-CODE.");
				Assert.ThrowsAsync<ImapCommandException> (async () => await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this comment is too long!")
				})), "Expected MAXSIZE RESP-CODE.");
				Assert.ThrowsAsync<ImapCommandException> (async () => await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, "this is a private comment"),
					new Metadata (MetadataTag.SharedComment, "this is a shared comment"),
				})), "Expected TOOMANY RESP-CODE.");
				await inbox.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				await client.DisconnectAsync (false);
			}
		}

		List<ImapReplayCommand> CreateNamespaceExtensionCommands ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "gmail.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "common.namespace.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"));
			commands.Add (new ImapReplayCommand ("A00000005 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestNamespaceExtensions ()
		{
			var commands = CreateNamespaceExtensionCommands ();

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, client.PersonalNamespaces.Count, "PersonalNamespaces.Count");
				Assert.AreEqual (string.Empty, client.PersonalNamespaces[0].Path, "PersonalNamespaces[0].Path");
				Assert.AreEqual ('/', client.PersonalNamespaces[0].DirectorySeparator, "PersonalNamespaces[0].DirectorySeparator");

				Assert.AreEqual (1, client.OtherNamespaces.Count, "OtherNamespaces.Count");
				Assert.AreEqual ("Other Users", client.OtherNamespaces[0].Path, "OtherNamespaces[0].Path");
				Assert.AreEqual ('/', client.OtherNamespaces[0].DirectorySeparator, "OtherNamespaces[0].DirectorySeparator");

				Assert.AreEqual (1, client.SharedNamespaces.Count, "SharedNamespaces.Count");
				Assert.AreEqual ("Public Folders", client.SharedNamespaces[0].Path, "SharedNamespaces[0].Path");
				Assert.AreEqual ('/', client.SharedNamespaces[0].DirectorySeparator, "SharedNamespaces[0].DirectorySeparator");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestNamespaceExtensionsAsync ()
		{
			var commands = CreateNamespaceExtensionCommands ();

			using (var client = new ImapClient ()) {
				try {
					await client.ReplayConnectAsync ("localhost", new ImapReplayStream (commands, true));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, client.PersonalNamespaces.Count, "PersonalNamespaces.Count");
				Assert.AreEqual (string.Empty, client.PersonalNamespaces[0].Path, "PersonalNamespaces[0].Path");
				Assert.AreEqual ('/', client.PersonalNamespaces[0].DirectorySeparator, "PersonalNamespaces[0].DirectorySeparator");

				Assert.AreEqual (1, client.OtherNamespaces.Count, "OtherNamespaces.Count");
				Assert.AreEqual ("Other Users", client.OtherNamespaces[0].Path, "OtherNamespaces[0].Path");
				Assert.AreEqual ('/', client.OtherNamespaces[0].DirectorySeparator, "OtherNamespaces[0].DirectorySeparator");

				Assert.AreEqual (1, client.SharedNamespaces.Count, "SharedNamespaces.Count");
				Assert.AreEqual ("Public Folders", client.SharedNamespaces[0].Path, "SharedNamespaces[0].Path");
				Assert.AreEqual ('/', client.SharedNamespaces[0].DirectorySeparator, "SharedNamespaces[0].DirectorySeparator");

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestLowercaseImapResponses ()
		{
			var commands = new List<ImapReplayCommand> ();
			commands.Add (new ImapReplayCommand ("", "lowercase.greeting.txt"));
			commands.Add (new ImapReplayCommand ("A00000000 LOGIN username password\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000001 CAPABILITY\r\n", "lowercase.capability.txt"));
			commands.Add (new ImapReplayCommand ("A00000002 LIST \"\" \"\"\r\n", "lowercase.list.txt"));
			commands.Add (new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "lowercase.list.txt"));
			commands.Add (new ImapReplayCommand ("A00000004 LIST (SPECIAL-USE) \"\" \"*\"\r\n", "lowercase.list.txt"));

			using (var client = new ImapClient ()) {
				try {
					client.ReplayConnect ("localhost", new ImapReplayStream (commands, false));
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Connect: {0}", ex);
				}

				Assert.IsTrue (client.IsConnected, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ("Did not expect an exception in Authenticate: {0}", ex);
				}

				Assert.AreEqual (1, client.PersonalNamespaces.Count, "PersonalNamespaces.Count");
				Assert.AreEqual (string.Empty, client.PersonalNamespaces[0].Path, "PersonalNamespaces[0].Path");
				Assert.AreEqual ('/', client.PersonalNamespaces[0].DirectorySeparator, "PersonalNamespaces[0].DirectorySeparator");
				Assert.AreEqual (0, client.OtherNamespaces.Count, "OtherNamespaces.Count");
				Assert.AreEqual (0, client.SharedNamespaces.Count, "SharedNamespaces.Count");

				Assert.IsNotNull (client.Inbox, "Inbox");
			}
		}
	}
}
