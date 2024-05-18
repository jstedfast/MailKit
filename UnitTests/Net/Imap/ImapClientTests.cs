//
// ImapClientTests.cs
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

using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;

using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;
using MailKit.Net.Proxy;

using UnitTests.Security;
using UnitTests.Net.Proxy;

using AuthenticationException = MailKit.Security.AuthenticationException;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapClientTests
	{
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
		static readonly ImapCapabilities IMAP4rev2CoreCapabilities = ImapCapabilities.IMAP4rev2 | ImapCapabilities.Status |
			ImapCapabilities.Namespace | ImapCapabilities.Unselect | ImapCapabilities.UidPlus | ImapCapabilities.ESearch |
			ImapCapabilities.SearchResults | ImapCapabilities.Enable | ImapCapabilities.Idle | ImapCapabilities.SaslIR | ImapCapabilities.ListExtended |
			ImapCapabilities.ListStatus | ImapCapabilities.Move | ImapCapabilities.LiteralMinus | ImapCapabilities.SpecialUse;
		static readonly ImapCapabilities AclInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities AclAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Acl;
		static readonly ImapCapabilities MetadataInitialCapabilities = GMailInitialCapabilities | ImapCapabilities.Metadata;
		static readonly ImapCapabilities MetadataAuthenticatedCapabilities = GMailAuthenticatedCapabilities | ImapCapabilities.Metadata;
		const CipherAlgorithmType GmxDeCipherAlgorithm = CipherAlgorithmType.Aes256;
		const int GmxDeCipherStrength = 256;
#if !MONO
		const HashAlgorithmType GmxDeHashAlgorithm = HashAlgorithmType.Sha384;
#else
		const HashAlgorithmType GmxDeHashAlgorithm = HashAlgorithmType.None;
#endif
		const ExchangeAlgorithmType EcdhEphemeral = (ExchangeAlgorithmType) 44550;

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
			default: throw new ArgumentOutOfRangeException (nameof (special));
			}
		}

		static Stream GetResourceStream (string name)
		{
			return typeof (ImapClientTests).Assembly.GetManifestResourceStream ("UnitTests.Net.Imap.Resources." + name);
		}

		static void GetStreamsCallback (ImapFolder folder, int index, UniqueId uid, Stream stream)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
				var text = reader.ReadToEnd ();

				Assert.That (text, Is.EqualTo (expected));
			}
		}

		static async Task GetStreamsAsyncCallback (ImapFolder folder, int index, UniqueId uid, Stream stream, CancellationToken cancellationToken)
		{
			using (var reader = new StreamReader (stream)) {
				const string expected = "This is some dummy text just to make sure this is working correctly.";
#if NET8_0_OR_GREATER
				var text = await reader.ReadToEndAsync (cancellationToken);
#else
				var text = await reader.ReadToEndAsync ();
#endif

				Assert.That (text, Is.EqualTo (expected));
			}
		}

		[Test]
		public void TestArgumentExceptions ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate+gmail-capabilities.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				var credentials = new NetworkCredential ("username", "password");

				Assert.That (client.SyncRoot, Is.InstanceOf<ImapEngine> (), "SyncRoot");

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
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				// Notify
				Assert.Throws<ArgumentNullException> (() => client.Notify (true, null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.NotifyAsync (true, null));
				Assert.Throws<ArgumentException> (() => client.Notify (true, Array.Empty<ImapEventGroup> ()));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.NotifyAsync (true, Array.Empty<ImapEventGroup> ()));

				Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Subtree (client.Inbox, null));
				Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Mailboxes (client.Inbox, null));

				Assert.Throws<ArgumentNullException> (() => client.GetFolder ((string) null));
				Assert.Throws<ArgumentNullException> (() => client.GetFolder ((FolderNamespace) null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetFolderAsync ((string) null));
				Assert.Throws<ArgumentNullException> (() => client.GetFolders (null));
				Assert.Throws<ArgumentNullException> (() => client.GetFolders (null, false));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetFoldersAsync (null));
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.GetFoldersAsync (null, false));

				Assert.That (client.PersonalNamespaces, Has.Count.EqualTo (1), "Personal");
				Assert.That (client.SharedNamespaces, Is.Empty, "Shared");
				Assert.That (client.OtherNamespaces, Is.Empty, "Other");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);

				client.Disconnect (false);
			}
		}

		static IList<ImapReplayCommand> CreateIMAP4rev2Commands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* OK [CAPABILITY STARTTLS AUTH=SCRAM-SHA-256 LOGINDISABLED IMAP4rev2] IMAP4rev2 Service Ready\r\n")),
			};
		}

		[Test]
		public void TestIMAP4rev2 ()
		{
			var commands = CreateIMAP4rev2Commands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (IMAP4rev2CoreCapabilities | ImapCapabilities.StartTLS | ImapCapabilities.LoginDisabled), "Capabilities");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("SCRAM-SHA-256"), "AUTH=SCRAM-SHA-256");
			}
		}

		[Test]
		public async Task TestIMAP4rev2Async ()
		{
			var commands = CreateIMAP4rev2Commands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (IMAP4rev2CoreCapabilities | ImapCapabilities.StartTLS | ImapCapabilities.LoginDisabled), "Capabilities");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("SCRAM-SHA-256"), "AUTH=SCRAM-SHA-256");
			}
		}

		[Test]
		public void TestEscapeUserName ()
		{
			var builder = new StringBuilder ();
			ImapClient.EscapeUserName (builder, "user:/?@&=+$%,;name");
			var escaped = builder.ToString ();

			Assert.That (escaped, Is.EqualTo ("user%3A%2F%3F%40%26%3D%2B%24%25%2C%3Bname"));
		}

		[Test]
		public void TestUnescapeUserName ()
		{
			var unescaped = ImapClient.UnescapeUserName ("user%3A%2F%3F%40%26%3D%2B%24%25%2C%3Bname");

			Assert.That (unescaped, Is.EqualTo ("user:/?@&=+$%,;name"));

			unescaped = ImapClient.UnescapeUserName ("user%3a%2f%3f%40%26%3d%2b%24%25%2c%3bname");

			Assert.That (unescaped, Is.EqualTo ("user:/?@&=+$%,;name"));
		}

		static void AssertDefaultValues (string host, int port, SecureSocketOptions options, Uri expected)
		{
			ImapClient.ComputeDefaultValues (host, ref port, ref options, out Uri uri, out bool starttls);

			if (expected.PathAndQuery == "/?starttls=when-available") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.StartTlsWhenAvailable), $"{expected}");
				Assert.That (starttls, Is.True, $"{expected}");
			} else if (expected.PathAndQuery == "/?starttls=always") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.StartTls), $"{expected}");
				Assert.That (starttls, Is.True, $"{expected}");
			} else if (expected.Scheme == "imaps") {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.SslOnConnect), $"{expected}");
				Assert.That (starttls, Is.False, $"{expected}");
			} else {
				Assert.That (options, Is.EqualTo (SecureSocketOptions.None), $"{expected}");
				Assert.That (starttls, Is.False, $"{expected}");
			}

			Assert.That (uri.ToString (), Is.EqualTo (expected.ToString ()));
			Assert.That (port, Is.EqualTo (expected.Port), $"{expected}");
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

				// 1. Test connecting to a non-SSL port fails with an SslHandshakeException.
				Assert.Throws<SslHandshakeException> (() => client.Connect ("www.gmail.com", 80, true));

				socket = Connect ("www.gmail.com", 80);
				Assert.Throws<SslHandshakeException> (() => client.Connect (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				// 2. Test connecting to a server with a bad SSL certificate fails with an SslHandshakeException.
				try {
					client.Connect ("untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}

				try {
					socket = Connect ("untrusted-root.badssl.com", 443);
					client.Connect (socket, "untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public async Task TestSslHandshakeExceptionsAsync ()
		{
			using (var client = new ImapClient ()) {
				Socket socket;

				// 1. Test connecting to a non-SSL port fails with an SslHandshakeException.
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync ("www.gmail.com", 80, true));

				socket = Connect ("www.gmail.com", 80);
				Assert.ThrowsAsync<SslHandshakeException> (async () => await client.ConnectAsync (socket, "www.gmail.com", 80, SecureSocketOptions.SslOnConnect));

				// 2. Test connecting to a server with a bad SSL certificate fails with an SslHandshakeException.
				try {
					await client.ConnectAsync ("untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}

				try {
					socket = Connect ("untrusted-root.badssl.com", 443);
					await client.ConnectAsync (socket, "untrusted-root.badssl.com", 443, SecureSocketOptions.SslOnConnect);
					Assert.Fail ("SSL handshake should have failed with untrusted-root.badssl.com.");
				} catch (SslHandshakeException ex) {
					Assert.That (ex.ServerCertificate, Is.Not.Null, "ServerCertificate");
					SslHandshakeExceptionTests.AssertBadSslUntrustedRootServerCertificate ((X509Certificate2) ex.ServerCertificate);

					// Note: This is null on Mono because Mono provides an empty chain.
					if (ex.RootCertificateAuthority is X509Certificate2 root)
						SslHandshakeExceptionTests.AssertBadSslUntrustedRootCACertificate (root);
				} catch (Exception ex) {
					Assert.Ignore ($"SSL handshake failure inconclusive: {ex}");
				}
			}
		}

		[Test]
		public void TestStartTlsNotSupported ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "common.basic-greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "common.capability.txt"),
			};

			using (var client = new ImapClient () { TagPrefix = 'A' })
				Assert.Throws<NotSupportedException> (() => client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.StartTls), "STARTTLS");

			using (var client = new ImapClient () { TagPrefix = 'A' })
				Assert.ThrowsAsync<NotSupportedException> (() => client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.StartTls), "STARTTLS Async");
		}

		[Test]
		public void TestProtocolLoggerExceptions ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
			};

			using (var client = new ImapClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogConnect)) { TagPrefix = 'A' })
				Assert.Throws<NotImplementedException> (() => client.Connect (Stream.Null, "imap.gmail.com", 143, SecureSocketOptions.None), "LogConnect");

			using (var client = new ImapClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogConnect)) { TagPrefix = 'A' })
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (Stream.Null, "imap.gmail.com", 143, SecureSocketOptions.None), "LogConnect Async");

			using (var client = new ImapClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogServer)) { TagPrefix = 'A' })
				Assert.Throws<NotImplementedException> (() => client.Connect (new ImapReplayStream (commands, false), "imap.gmail.com", 143, SecureSocketOptions.None), "LogServer");

			using (var client = new ImapClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogServer)) { TagPrefix = 'A' })
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (new ImapReplayStream (commands, true), "imap.gmail.com", 143, SecureSocketOptions.None), "LogServer Async");

			using (var client = new ImapClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogClient)) { TagPrefix = 'A' })
				Assert.Throws<NotImplementedException> (() => client.Connect (new ImapReplayStream (commands, false), "imap.gmail.com", 143, SecureSocketOptions.None), "LogClient");

			using (var client = new ImapClient (new ExceptionalProtocolLogger (ExceptionalProtocolLoggerMode.ThrowOnLogClient)) { TagPrefix = 'A' })
				Assert.ThrowsAsync<NotImplementedException> (() => client.ConnectAsync (new ImapReplayStream (commands, true), "imap.gmail.com", 143, SecureSocketOptions.None), "LogClient Async");
		}

		static void AssertGMailIsConnected (IMailService client)
		{
			Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
			Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
			Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
			Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
			Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
			Assert.That (client.SslCipherAlgorithm == CipherAlgorithmType.Aes128 || client.SslCipherAlgorithm == CipherAlgorithmType.Aes256, Is.True, $"Unexpected SslCipherAlgorithm: {client.SslCipherAlgorithm}");
			Assert.That (client.SslCipherStrength == 128 || client.SslCipherStrength == 256, Is.True, $"Unexpected SslCipherStrength: {client.SslCipherStrength}");
#if !MONO
			Assert.That (client.SslCipherSuite == TlsCipherSuite.TLS_AES_128_GCM_SHA256 || client.SslCipherSuite == TlsCipherSuite.TLS_AES_256_GCM_SHA384, Is.True, $"Unexpected SslCipherSuite: {client.SslCipherSuite}");
			Assert.That (client.SslHashAlgorithm == HashAlgorithmType.Sha256 || client.SslHashAlgorithm == HashAlgorithmType.Sha384, Is.True, $"Unexpected SslHashAlgorithm: {client.SslHashAlgorithm}");
#else
			Assert.That (client.SslHashAlgorithm == HashAlgorithmType.None, Is.True, $"Unexpected SslHashAlgorithm: {client.SslHashAlgorithm}");
#endif

			Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
			Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
			Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
			Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
		}

		static void AssertClientIsDisconnected (IMailService client)
		{
			Assert.That (client.IsConnected, Is.False, "Expected the client to be disconnected");
			Assert.That (client.IsSecure, Is.False, "Expected IsSecure to be false after disconnecting");
			Assert.That (client.IsEncrypted, Is.False, "Expected IsEncrypted to be false after disconnecting");
			Assert.That (client.IsSigned, Is.False, "Expected IsSigned to be false after disconnecting");
			Assert.That (client.SslProtocol, Is.EqualTo (SslProtocols.None), "Expected SslProtocol to be None after disconnecting");
			Assert.That (client.SslCipherAlgorithm, Is.Null, "Expected SslCipherAlgorithm to be null after disconnecting");
			Assert.That (client.SslCipherStrength, Is.Null, "Expected SslCipherStrength to be null after disconnecting");
			Assert.That (client.SslCipherSuite, Is.Null, "Expected SslCipherSuite to be null after disconnecting");
			Assert.That (client.SslHashAlgorithm, Is.Null, "Expected SslHashAlgorithm to be null after disconnecting");
			Assert.That (client.SslHashStrength, Is.Null, "Expected SslHashStrength to be null after disconnecting");
			Assert.That (client.SslKeyExchangeAlgorithm, Is.Null, "Expected SslKeyExchangeAlgorithm to be null after disconnecting");
			Assert.That (client.SslKeyExchangeStrength, Is.Null, "Expected SslKeyExchangeStrength to be null after disconnecting");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				client.Connect (host, 0, options);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

				client.Disconnect (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				await client.ConnectAsync (host, 0, options);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

				await client.DisconnectAsync (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
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
					AssertGMailIsConnected (client);
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					Assert.Throws<InvalidOperationException> (() => client.Connect (host, 0, options));

					client.Disconnect (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
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
					AssertGMailIsConnected (client);
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (host, 0, options));

					await client.DisconnectAsync (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.Throws<ArgumentNullException> (() => client.Connect (socket, null, port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentException> (() => client.Connect (socket, "", port, SecureSocketOptions.Auto));
				Assert.Throws<ArgumentOutOfRangeException> (() => client.Connect (socket, host, -1, SecureSocketOptions.Auto));

				client.Connect (socket, host, port, SecureSocketOptions.Auto);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.Throws<InvalidOperationException> (() => client.Connect (socket, host, port, SecureSocketOptions.Auto));

				client.Disconnect (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
					Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
					connected++;
				};

				client.Disconnected += (sender, e) => {
					Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
					Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
					Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
					Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
					disconnected++;
				};

				var socket = Connect (host, port);

				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.ConnectAsync (socket, null, port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentException> (async () => await client.ConnectAsync (socket, "", port, SecureSocketOptions.Auto));
				Assert.ThrowsAsync<ArgumentOutOfRangeException> (async () => await client.ConnectAsync (socket, host, -1, SecureSocketOptions.Auto));

				await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto);
				AssertGMailIsConnected (client);
				Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

				Assert.ThrowsAsync<InvalidOperationException> (async () => await client.ConnectAsync (socket, host, port, SecureSocketOptions.Auto));

				await client.DisconnectAsync (true);
				AssertClientIsDisconnected (client);
				Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"imap://{host}/?starttls=always");
					client.Connect (uri, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					client.Disconnect (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var uri = new Uri ($"imap://{host}/?starttls=always");
					await client.ConnectAsync (uri, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					await client.DisconnectAsync (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					client.Connect (socket, host, port, options, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					client.Disconnect (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
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
						Assert.That (e.Host, Is.EqualTo (host), "ConnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "ConnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "ConnectedEventArgs.Options");
						connected++;
					};

					client.Disconnected += (sender, e) => {
						Assert.That (e.Host, Is.EqualTo (host), "DisconnectedEventArgs.Host");
						Assert.That (e.Port, Is.EqualTo (port), "DisconnectedEventArgs.Port");
						Assert.That (e.Options, Is.EqualTo (options), "DisconnectedEventArgs.Options");
						Assert.That (e.IsRequested, Is.True, "DisconnectedEventArgs.IsRequested");
						disconnected++;
					};

					var socket = Connect (host, port);
					await client.ConnectAsync (socket, host, port, options, cancel.Token);
					Assert.That (client.IsConnected, Is.True, "Expected the client to be connected");
					Assert.That (client.IsSecure, Is.True, "Expected a secure connection");
					Assert.That (client.IsEncrypted, Is.True, "Expected an encrypted connection");
					Assert.That (client.IsSigned, Is.True, "Expected a signed connection");
					Assert.That (client.SslProtocol == SslProtocols.Tls12 || client.SslProtocol == SslProtocols.Tls13, Is.True, "Expected a TLS v1.2 or TLS v1.3 connection");
					Assert.That (client.SslCipherAlgorithm, Is.EqualTo (GmxDeCipherAlgorithm));
					Assert.That (client.SslCipherStrength, Is.EqualTo (GmxDeCipherStrength));
					Assert.That (client.SslHashAlgorithm, Is.EqualTo (GmxDeHashAlgorithm));
					Assert.That (client.SslHashStrength, Is.EqualTo (0), $"Unexpected SslHashStrength: {client.SslHashStrength}");
					Assert.That (client.SslKeyExchangeAlgorithm == ExchangeAlgorithmType.None || client.SslKeyExchangeAlgorithm == EcdhEphemeral, Is.True, $"Unexpected SslKeyExchangeAlgorithm: {client.SslKeyExchangeAlgorithm}");
					Assert.That (client.SslKeyExchangeStrength == 0 || client.SslKeyExchangeStrength == 255, Is.True, $"Unexpected SslKeyExchangeStrength: {client.SslKeyExchangeStrength}");
					Assert.That (client.IsAuthenticated, Is.False, "Expected the client to not be authenticated");
					Assert.That (connected, Is.EqualTo (1), "ConnectedEvent");

					await client.DisconnectAsync (true);
					AssertClientIsDisconnected (client);
					Assert.That (disconnected, Is.EqualTo (1), "DisconnectedEvent");
				}
			}
		}

		[Test]
		public void TestUnexpectedGreeting ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* INVALID\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' })
				Assert.Throws<ImapProtocolException> (() => client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None), "Connect");

			using (var client = new ImapClient () { TagPrefix = 'A' })
				Assert.ThrowsAsync<ImapProtocolException> (() => client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None), "ConnectAsync");
		}

		[Test]
		public void TestGreetingCapabilities ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "common.capability-greeting.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GreetingCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (1));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
			}
		}

		[Test]
		public async Task TestGreetingCapabilitiesAsync ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "common.capability-greeting.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GreetingCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (1));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
			}
		}

		[Test]
		public void TestByeGreeting ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("The IMAP server unexpectedly refused the connection."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestByeGreetingAsync ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("The IMAP server unexpectedly refused the connection."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestByeGreetingWithAlert ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE [ALERT] Too many connections.\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo ("Too many connections."));
					alerts++;
				};

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("Too many connections."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (1), "Expected 1 alert");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestByeGreetingWithAlertAsync ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE [ALERT] Too many connections.\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo ("Too many connections."));
					alerts++;
				};

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("Too many connections."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (1), "Expected 1 alert");

				await client.DisconnectAsync (false);
			}
		}

		[Test]
		public void TestByeGreetingWithRespText ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE Too many connections.\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("Too many connections."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestByeGreetingWithRespTextAsync ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* BYE Too many connections.\r\n"))
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to be connected.");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("Too many connections."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateUnexpectedByeCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", Encoding.ASCII.GetBytes ("* BYE System going down for a reboot.\r\n"))
			};
		}

		[Test]
		public void TestUnexpectedBye ()
		{
			var commands = CreateUnexpectedByeCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Inbox.Open (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("System going down for a reboot."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Open: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnexpectedByeAsync ()
		{
			var commands = CreateUnexpectedByeCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.Inbox.OpenAsync (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("System going down for a reboot."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Open: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateUnexpectedByeAfterCapabilityCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", Encoding.ASCII.GetBytes ("* OK Yandex IMAP4rev1 at sas8-bccc92f57f23.qloud-c.yandex.net:993 ready to talk with, 2019-Oct-18 07:41:00, 0fHtH613ZiE1\r\n")),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", Encoding.ASCII.GetBytes ("* BYE Autologout; idle for too long (1)\r\n* BYE Autologout; idle for too long (2)\r\n* BYE Autologout; idle for too long (3)\r\n"))
			};
		}

		[Test]
		public void TestUnexpectedByeAfterCapability ()
		{
			var commands = CreateUnexpectedByeAfterCapabilityCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to connect");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("Autologout; idle for too long (1)"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnexpectedByeAfterCapabilityAsync ()
		{
			var commands = CreateUnexpectedByeAfterCapabilityCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					Assert.Fail ("Did not expect to connect");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("Autologout; idle for too long (1)"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateUnexpectedByeWithAlertCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", Encoding.ASCII.GetBytes ("* BYE [ALERT] System going down for a reboot.\r\n"))
			};
		}

		[Test]
		public void TestUnexpectedByeWithAlert ()
		{
			var commands = CreateUnexpectedByeWithAlertCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo ("System going down for a reboot."));
					alerts++;
				};

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Inbox.Open (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("System going down for a reboot."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Open: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (1), "Expected 1 alert");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnexpectedByeWithAlertAsync ()
		{
			var commands = CreateUnexpectedByeWithAlertCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo ("System going down for a reboot."));
					alerts++;
				};

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.Inbox.OpenAsync (FolderAccess.ReadWrite);
					Assert.Fail ("Did not expect to open the Inbox");
				} catch (ImapProtocolException ex) {
					Assert.That (ex.Message, Is.EqualTo ("System going down for a reboot."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Open: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (1), "Expected 1 alert");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateUnexpectedByeInSaslAuthenticateCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", Encoding.ASCII.GetBytes ("* BYE disconnecting\r\nA00000001 NO you are not allowed to act as a proxy server\r\n"))
			};
		}

		[Test]
		public void TestUnexpectedByeInSaslAuthenticate ()
		{
			var commands = CreateUnexpectedByeInSaslAuthenticateCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Expected failure");
				} catch (ImapProtocolException pex) {
					Assert.That (pex.Message, Is.EqualTo ("you are not allowed to act as a proxy server"));
				} catch (Exception ex) {
					Assert.Fail ($"Expected ImapProtocolException, but got: {ex}");
				}
			}
		}

		[Test]
		public async Task TestUnexpectedByeInSaslAuthenticateAsync ()
		{
			var commands = CreateUnexpectedByeInSaslAuthenticateCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Expected failure");
				} catch (ImapProtocolException pex) {
					Assert.That (pex.Message, Is.EqualTo ("you are not allowed to act as a proxy server"));
				} catch (Exception ex) {
					Assert.Fail ($"Expected ImapProtocolException, but got: {ex}");
				}
			}
		}

		static List<ImapReplayCommand> CreateInvalidTaggedByeDuringLogoutCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LOGOUT\r\n", Encoding.ASCII.GetBytes ("A00000005 BYE IMAP4rev1 Server logging out\r\n"))
			};
		}

		[Test]
		public void TestInvalidTaggedByeDuringLogout ()
		{
			var commands = CreateInvalidTaggedByeDuringLogoutCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					client.Disconnect (true);
				} catch (Exception ex) {
					Assert.Fail ($"Exceptions should be swallowed in Disconnect: {ex}");
				}
			}
		}

		[Test]
		public async Task TestInvalidTaggedByeDuringLogoutAsync ()
		{
			var commands = CreateInvalidTaggedByeDuringLogoutCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect this exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				try {
					await client.DisconnectAsync (true);
				} catch (Exception ex) {
					Assert.Fail ($"Exceptions should be swallowed in Disconnect: {ex}");
				}
			}
		}

		static List<ImapReplayCommand> CreatePreAuthGreetingCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "common.preauth-greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "common.capability.txt"),
				new ImapReplayCommand ("A00000001 LIST \"\" \"\"\r\n", "common.list-namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\"\r\n", "common.list-inbox.txt")
			};
		}

		[Test]
		public void TestPreAuthGreeting ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = CreatePreAuthGreetingCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsAuthenticated, Is.True, "Client should be authenticated.");
				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (capabilities), "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox), "Expected Inbox attributes to be empty.");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestPreAuthGreetingAsync ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = CreatePreAuthGreetingCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsAuthenticated, Is.True, "Client should be authenticated.");
				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (capabilities), "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox), "Expected Inbox attributes to be empty.");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreatePreAuthCapabilityGreetingCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "common.preauth-capability-greeting.txt"),
				new ImapReplayCommand ("A00000000 LIST \"\" \"\"\r\n", "common.list-namespace.txt"),
				new ImapReplayCommand ("A00000001 LIST \"\" \"INBOX\"\r\n", "common.list-inbox.txt")
			};
		}

		[Test]
		public void TestPreAuthCapabilityGreeting ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = CreatePreAuthCapabilityGreetingCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsAuthenticated, Is.True, "Client should be authenticated.");
				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (capabilities), "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox), "Expected Inbox attributes to be empty.");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestPreAuthCapabilityGreetingAsync ()
		{
			var capabilities = ImapCapabilities.IMAP4rev1 | ImapCapabilities.Status;
			var commands = CreatePreAuthCapabilityGreetingCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsAuthenticated, Is.True, "Client should be authenticated.");
				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (capabilities), "Capabilities");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox), "Expected Inbox attributes to be empty.");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateGMailWebAlertCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 LOGIN username password\r\n", "gmail.authenticate+webalert.txt")
			};
		}

		[Test]
		public void TestGMailWebAlert ()
		{
			const string webUri = "https://accounts.google.com/signin/continue?sarp=1&scc=1&plt=AKgnsbsNd6RU3LIlgDfhmL9Y7ywYhtagFig_xfuSJCUHD9Eg3XqN8DKlDk3G8jmj2w5viIm5PDC3BS4SVy7iFMB6g1244cnQt1E60EdOTSEpnqDzL6FH2L-ReOAyZ3qkSXZQZs2pIfL2";
			const string alert = "Please log in via your web browser: https://support.google.com/mail/accounts/answer/78754 (Failure)";

			var commands = CreateGMailWebAlertCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int webalerts = 0;
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo (alert));
					alerts++;
				};

				client.WebAlert += (sender, e) => {
					Assert.That (e.WebUri.AbsoluteUri, Is.EqualTo (webUri));
					Assert.That (e.Message, Is.EqualTo ("Web login required."));
					webalerts++;
				};

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo (alert));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (1), "Expected 1 alert");
				Assert.That (webalerts, Is.EqualTo (1), "Expected 1 web alert");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestGMailWebAlertAsync ()
		{
			const string webUri = "https://accounts.google.com/signin/continue?sarp=1&scc=1&plt=AKgnsbsNd6RU3LIlgDfhmL9Y7ywYhtagFig_xfuSJCUHD9Eg3XqN8DKlDk3G8jmj2w5viIm5PDC3BS4SVy7iFMB6g1244cnQt1E60EdOTSEpnqDzL6FH2L-ReOAyZ3qkSXZQZs2pIfL2";
			const string alert = "Please log in via your web browser: https://support.google.com/mail/accounts/answer/78754 (Failure)";

			var commands = CreateGMailWebAlertCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				int webalerts = 0;
				int alerts = 0;

				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo (alert));
					alerts++;
				};

				client.WebAlert += (sender, e) => {
					Assert.That (e.WebUri.AbsoluteUri, Is.EqualTo (webUri));
					Assert.That (e.Message, Is.EqualTo ("Web login required."));
					webalerts++;
				};

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo (alert));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (1), "Expected 1 alert");
				Assert.That (webalerts, Is.EqualTo (1), "Expected 1 web alert");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateUnicodeRespTextCommands (out string respText)
		{
			respText = "╟ы╩╣╙├╩┌╚и┬ы╡╟┬╝бг╧ъ╟щ╟ы┐┤";

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 LOGIN username password\r\n", Encoding.UTF8.GetBytes ("A00000001 NO " + respText + "\r\n"))
			};
		}

		[Test]
		public void TestUnicodeRespText ()
		{
			var commands = CreateUnicodeRespTextCommands (out var respText);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo (respText));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUnicodeRespTextAsync ()
		{
			var commands = CreateUnicodeRespTextCommands (out var respText);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo (respText));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateInvalidUntaggedResponseCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"Buggy Folder Listing\" RETURN (SUBSCRIBED CHILDREN)\r\n", Encoding.ASCII.GetBytes ("* {25}\r\nThis should be skipped...\r\n* LIST (\\NoSelect) \"/\" \"Buggy Folder Listing\"\r\nA00000005 OK LIST completed.\r\n")),
			};
		}

		[Test]
		public void TestInvalidUntaggedResponse ()
		{
			var commands = CreateInvalidUntaggedResponseCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var folder = client.GetFolder ("Buggy Folder Listing");
				Assert.That (folder.Name, Is.EqualTo ("Buggy Folder Listing"), "Name");
				Assert.That (folder.Attributes, Is.EqualTo (FolderAttributes.NoSelect), "Attributes");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestInvalidUntaggedResponseAsync ()
		{
			var commands = CreateInvalidUntaggedResponseCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var folder = await client.GetFolderAsync ("Buggy Folder Listing");
				Assert.That (folder.Name, Is.EqualTo ("Buggy Folder Listing"), "Name");
				Assert.That (folder.Attributes, Is.EqualTo (FolderAttributes.NoSelect), "Attributes");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateInvalidUntaggedBadResponseCommands (out string alertText)
		{
			alertText = "Please enable IMAP access in your account settings first.";

			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "common.capability-greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", Encoding.UTF8.GetBytes ("A00000000 OK [ALERT] " + alertText + "\r\n")),
				new ImapReplayCommand ("A00000001 CAPABILITY\r\n", Encoding.UTF8.GetBytes ("A00000001 NO [ALERT] " + alertText + "\r\n")),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", Encoding.UTF8.GetBytes ("A00000002 NO [ALERT] " + alertText + "\r\n")),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", Encoding.UTF8.GetBytes ("* BAD [ALERT] " + alertText + "\r\nA00000003 NO [ALERT] " + alertText + "\r\n"))
			};
		}

		[Test]
		public void TestInvalidUntaggedBadResponse ()
		{
			var commands = CreateInvalidUntaggedBadResponseCommands (out var alertText);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo (alertText));
					alerts++;
				};

				try {
					client.Authenticate ("username", "password");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo (alertText));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (5), $"Unexpected number of alerts: {alerts}");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestInvalidUntaggedBadResponseAsync ()
		{
			var commands = CreateInvalidUntaggedBadResponseCommands (out var alertText);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo (alertText));
					alerts++;
				};

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo (alertText));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (alerts, Is.EqualTo (5), $"Unexpected number of alerts: {alerts}");

				await client.DisconnectAsync (false);
			}
		}

		// Tests issue https://github.com/jstedfast/MailKit/issues/115#issuecomment-313684616
		static IList<ImapReplayCommand> CreateUntaggedRespCodeCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 UID MOVE 1 \"[Gmail]/Trash\"\r\n", Encoding.ASCII.GetBytes ("* [COPYUID 123456 1 2]\r\n* 1 EXPUNGE\r\nA00000006 OK Completed.\r\n"))
			};
		}

		[Test]
		public void TestUntaggedRespCode ()
		{
			var commands = CreateUntaggedRespCodeCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var trash = client.GetFolder (SpecialFolder.Trash);
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);
				var moved = inbox.MoveTo (UniqueId.MinValue, trash);
				Assert.That (moved.Value.Id, Is.EqualTo (2));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestUntaggedRespCodeAsync ()
		{
			var commands = CreateUntaggedRespCodeCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var trash = client.GetFolder (SpecialFolder.Trash);
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);
				var moved = await inbox.MoveToAsync (UniqueId.MinValue, trash);
				Assert.That (moved.Value.Id, Is.EqualTo (2));

				await client.DisconnectAsync (false);
			}
		}

		static IList<ImapReplayCommand> CreateSuperfluousUntaggedOkNoOrBadCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 UID MOVE 1 \"[Gmail]/Trash\"\r\n", Encoding.ASCII.GetBytes ("* OK The good,\r\n* BAD the bad,\r\n* NO and the ugly.\r\n* OK [COPYUID 123456 1 2]\r\n* 1 EXPUNGE\r\nA00000006 OK Completed.\r\n"))
			};
		}

		[Test]
		public void TesSuperfluousUntaggedOkNoOrBad ()
		{
			var commands = CreateSuperfluousUntaggedOkNoOrBadCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var trash = client.GetFolder (SpecialFolder.Trash);
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);
				var moved = inbox.MoveTo (UniqueId.MinValue, trash);
				Assert.That (moved.Value.Id, Is.EqualTo (2));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSuperfluousUntaggedOkNoOrBadAsync ()
		{
			var commands = CreateSuperfluousUntaggedOkNoOrBadCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var trash = client.GetFolder (SpecialFolder.Trash);
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);
				var moved = await inbox.MoveToAsync (UniqueId.MinValue, trash);
				Assert.That (moved.Value.Id, Is.EqualTo (2));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateLoginCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 LOGIN \"Indiana \\\"Han Solo\\\" Jones\" \"p@ss\\\\word\"\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestLogin ()
		{
			var commands = CreateLoginCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestLoginAsync ()
		{
			var commands = CreateLoginCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateLoginSpecialCharacterCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 LOGIN username \"pass%word\"\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestLoginSpecialCharacter ()
		{
			var commands = CreateLoginSpecialCharacterCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestLoginSpecialCharacterAsync ()
		{
			var commands = CreateLoginSpecialCharacterCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateLoginDisabledCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+logindisabled.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", ImapReplayCommandResponse.NO)
			};
		}

		[Test]
		public void TestLoginDisabled ()
		{
			var commands = CreateLoginDisabledCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities | ImapCapabilities.LoginDisabled));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo ("AUTHENTICATE failed"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo ("The LOGIN command is disabled."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestLoginDisabledAsync ()
		{
			var commands = CreateLoginDisabledCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities | ImapCapabilities.LoginDisabled));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo ("AUTHENTICATE failed"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (AuthenticationException ax) {
					Assert.That (ax.Message, Is.EqualTo ("The LOGIN command is disabled."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateExchangeUserIsAuthenticatedButNotConnectedCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "exchange.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "exchange.capability-preauth.txt"),
				new ImapReplayCommand ("A00000001 LOGIN \"user@domain.com\\\\mailbox\" password\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000002 CAPABILITY\r\n", "exchange.capability-postauth.txt"),
				new ImapReplayCommand ("A00000003 NAMESPACE\r\n", Encoding.ASCII.GetBytes ("A00000003 BAD User is authenticated but not connected.\r\n"))
			};
		}

		[Test]
		public void TestExchangeUserIsAuthenticatedButNotConnected ()
		{
			var commands = CreateExchangeUserIsAuthenticatedButNotConnectedCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ImapCapabilities.IMAP4 | ImapCapabilities.IMAP4rev1 | ImapCapabilities.SaslIR | ImapCapabilities.UidPlus | ImapCapabilities.Id |
					ImapCapabilities.Unselect | ImapCapabilities.Children | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.LiteralPlus |
					ImapCapabilities.Status));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("user@domain.com\\mailbox", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (ImapCommandException cx) {
					Assert.That (cx.Response, Is.EqualTo (ImapCommandResponse.Bad));
					Assert.That (cx.ResponseText, Is.EqualTo ("User is authenticated but not connected."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestExchangeUserIsAuthenticatedButNotConnectedAsync ()
		{
			var commands = CreateExchangeUserIsAuthenticatedButNotConnectedCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (ImapCapabilities.IMAP4 | ImapCapabilities.IMAP4rev1 | ImapCapabilities.SaslIR | ImapCapabilities.UidPlus | ImapCapabilities.Id |
					ImapCapabilities.Unselect | ImapCapabilities.Children | ImapCapabilities.Idle | ImapCapabilities.Namespace | ImapCapabilities.LiteralPlus |
					ImapCapabilities.Status));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (2));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");

				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("user@domain.com\\mailbox", "password");
					Assert.Fail ("Did not expect Authenticate to work.");
				} catch (ImapCommandException cx) {
					Assert.That (cx.Response, Is.EqualTo (ImapCommandResponse.Bad));
					Assert.That (cx.ResponseText, Is.EqualTo ("User is authenticated but not connected."));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateAdvancedFeaturesCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt"),
				new ImapReplayCommand ("A00000006 GETQUOTAROOT INBOX\r\n", "common.getquota.txt"),
				new ImapReplayCommand ("A00000007 SETQUOTA \"\" (MESSAGE 1000000 STORAGE 5242880)\r\n", "common.setquota.txt")
			};
		}

		[Test]
		public void TestAdvancedFeatures ()
		{
			var commands = CreateAdvancedFeaturesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (6));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				client.EnableUTF8 ();

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = inbox.GetQuota ();
				Assert.That (quota, Is.Not.Null, "Expected a non-null GETQUOTAROOT response.");
				Assert.That (quota.QuotaRoot.FullName, Is.EqualTo (personal.FullName));
				Assert.That (quota.QuotaRoot, Is.EqualTo (personal));
				Assert.That (quota.CurrentStorageSize.Value, Is.EqualTo (3783));
				Assert.That (quota.StorageLimit.Value, Is.EqualTo (15728640));
				Assert.That (quota.CurrentMessageCount.HasValue, Is.False);
				Assert.That (quota.MessageLimit.HasValue, Is.False);

				quota = personal.SetQuota (1000000, 5242880);
				Assert.That (quota, Is.Not.Null, "Expected non-null SETQUOTA response.");
				Assert.That (quota.CurrentMessageCount.Value, Is.EqualTo (1107));
				Assert.That (quota.CurrentStorageSize.Value, Is.EqualTo (3783));
				Assert.That (quota.MessageLimit.Value, Is.EqualTo (1000000));
				Assert.That (quota.StorageLimit.Value, Is.EqualTo (5242880));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestAdvancedFeaturesAsync ()
		{
			var commands = CreateAdvancedFeaturesCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (6));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				await client.EnableUTF8Async ();

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = await inbox.GetQuotaAsync ();
				Assert.That (quota, Is.Not.Null, "Expected a non-null GETQUOTAROOT response.");
				Assert.That (quota.QuotaRoot.FullName, Is.EqualTo (personal.FullName));
				Assert.That (quota.QuotaRoot, Is.EqualTo (personal));
				Assert.That (quota.CurrentStorageSize.Value, Is.EqualTo (3783));
				Assert.That (quota.StorageLimit.Value, Is.EqualTo (15728640));
				Assert.That (quota.CurrentMessageCount.HasValue, Is.False);
				Assert.That (quota.MessageLimit.HasValue, Is.False);

				quota = await personal.SetQuotaAsync (1000000, 5242880);
				Assert.That (quota, Is.Not.Null, "Expected non-null SETQUOTA response.");
				Assert.That (quota.CurrentMessageCount.Value, Is.EqualTo (1107));
				Assert.That (quota.CurrentStorageSize.Value, Is.EqualTo (3783));
				Assert.That (quota.MessageLimit.Value, Is.EqualTo (1000000));
				Assert.That (quota.StorageLimit.Value, Is.EqualTo (5242880));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSendingStringsAsLiteralsCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt"),
				new ImapReplayCommand ("A00000006 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" {35}\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("A00000006", "1 Memorial Dr.\r\nCambridge, MA 02142)\r\n", "common.id.txt"),
				new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestSendingStringsAsLiterals ()
		{
			var commands = CreateSendingStringsAsLiteralsCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2 or PLAIN
				client.AuthenticationMechanisms.Remove ("XOAUTH2");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					client.Authenticate (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
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
			var commands = CreateSendingStringsAsLiteralsCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				// Note: Do not try XOAUTH2 or PLAIN
				client.AuthenticationMechanisms.Remove ("XOAUTH2");
				client.AuthenticationMechanisms.Remove ("PLAIN");

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
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

		static List<ImapReplayCommand> CreateSaslAuthenticationCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestSaslAuthentication ()
		{
			var commands = CreateSaslAuthenticationCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (6));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSaslAuthenticationAsync ()
		{
			var commands = CreateSaslAuthenticationCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (6));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateSaslIRAuthenticationCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestSaslIRAuthentication ()
		{
			var commands = CreateSaslIRAuthenticationCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismPlain (credentials);

					client.Authenticate (sasl);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestSaslIRAuthenticationAsync ()
		{
			var commands = CreateSaslIRAuthenticationCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
				client.Timeout *= 2;

				int authenticated = 0;
				client.Authenticated += (sender, e) => {
					authenticated++;
				};

				try {
					var credentials = new NetworkCredential ("username", "password");
					var sasl = new SaslMechanismPlain (credentials);

					await client.AuthenticateAsync (sasl);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				await client.DisconnectAsync (false);
			}
		}

		static void AssertRedacted (MemoryStream stream, string commandPrefix, string nextCommandPrefix)
		{
			stream.Position = 0;

			using (var reader = new StreamReader (stream, Encoding.ASCII, false, 1024, true)) {
				string secrets;
				string line;

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith (commandPrefix, StringComparison.Ordinal))
						break;
				}

				Assert.That (line, Is.Not.Null, $"Authentication command not found: {commandPrefix}");

				if (line.Length > commandPrefix.Length) {
					secrets = line.Substring (commandPrefix.Length);

					var tokens = secrets.Split (' ');
					var expectedTokens = new string[tokens.Length];
					for (int i = 0; i < tokens.Length; i++) {
						if (tokens[i][0] == '"')
							expectedTokens[i] = "\"********\"";
						else
							expectedTokens[i] = "********";
					}

					var expected = string.Join (" ", expectedTokens);

					Assert.That (secrets, Is.EqualTo (expected), commandPrefix);
				}

				while ((line = reader.ReadLine ()) != null) {
					if (line.StartsWith (nextCommandPrefix, StringComparison.Ordinal))
						return;

					if (!line.StartsWith ("C: ", StringComparison.Ordinal))
						continue;

					secrets = line.Substring (3);

					Assert.That (secrets, Is.EqualTo ("********"), "SASL challenge");
				}

				Assert.Fail ("Did not find response.");
			}
		}

		static List<ImapReplayCommand> CreateRedactLoginCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 LOGIN username \"pass%word\"\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestRedactLogin ()
		{
			var commands = CreateRedactLoginCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new ImapClient (new ProtocolLogger (stream, true) { RedactSecrets = true }) { TagPrefix = 'A' }) {
					try {
						client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

					Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
					Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
					Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

					client.Disconnect (false);
				}

				AssertRedacted (stream, "C: A00000001 LOGIN ", "C: A00000002 NAMESPACE");
			}
		}

		[Test]
		public async Task TestRedactLoginAsync ()
		{
			var commands = CreateRedactLoginCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new ImapClient (new ProtocolLogger (stream, true) { RedactSecrets = true }) { TagPrefix = 'A' }) {
					try {
						await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

					Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
					Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
					Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

					await client.DisconnectAsync (false);
				}

				AssertRedacted (stream, "C: A00000001 LOGIN ", "C: A00000002 NAMESPACE");
			}
		}

		static List<ImapReplayCommand> CreateRedactAuthenticationCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestRedactAuthentication ()
		{
			var commands = CreateRedactAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new ImapClient (new ProtocolLogger (stream, true) { RedactSecrets = true }) { TagPrefix = 'A' }) {
					try {
						client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

					// Note: Do not try XOAUTH2
					client.AuthenticationMechanisms.Remove ("XOAUTH2");

					try {
						client.Authenticate ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));

					client.Disconnect (false);
				}

				AssertRedacted (stream, "C: A00000001 AUTHENTICATE PLAIN ", "C: A00000002 NAMESPACE");
			}
		}

		[Test]
		public async Task TestRedactAuthenticationAsync ()
		{
			var commands = CreateRedactAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new ImapClient (new ProtocolLogger (stream, true) { RedactSecrets = true }) { TagPrefix = 'A' }) {
					try {
						await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

					// Note: Do not try XOAUTH2
					client.AuthenticationMechanisms.Remove ("XOAUTH2");

					try {
						await client.AuthenticateAsync ("username", "password");
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));

					await client.DisconnectAsync (false);
				}

				AssertRedacted (stream, "C: A00000001 AUTHENTICATE PLAIN ", "C: A00000002 NAMESPACE");
			}
		}

		static List<ImapReplayCommand> CreateRedactSaslAuthenticationCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability+login.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE LOGIN\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("dXNlcm5hbWU=\r\n", ImapReplayCommandResponse.Plus),
				new ImapReplayCommand ("A00000001", "cGFzc3dvcmQ=\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt")
			};
		}

		[Test]
		public void TestRedactSaslAuthentication ()
		{
			var commands = CreateRedactSaslAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new ImapClient (new ProtocolLogger (stream, true) { RedactSecrets = true }) { TagPrefix = 'A' }) {
					try {
						client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (6));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
					Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
					Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

					client.Disconnect (false);
				}

				AssertRedacted (stream, "C: A00000001 AUTHENTICATE LOGIN", "C: A00000002 NAMESPACE");
			}
		}

		[Test]
		public async Task TestRedactSaslAuthenticationAsync ()
		{
			var commands = CreateRedactSaslAuthenticationCommands ();

			using (var stream = new MemoryStream ()) {
				using (var client = new ImapClient (new ProtocolLogger (stream, true) { RedactSecrets = true }) { TagPrefix = 'A' }) {
					try {
						await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
					} catch (Exception ex) {
						Assert.Fail ($"Did not expect an exception in Connect: {ex}");
					}

					Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
					Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

					Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
					Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (6));
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
					Assert.That (client.AuthenticationMechanisms, Does.Contain ("LOGIN"), "Expected SASL LOGIN auth mechanism");

					Assert.That (client.Timeout, Is.EqualTo (120000), "Timeout");
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
						Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
					}

					Assert.That (authenticated, Is.EqualTo (1), "Authenticated event was not emitted the expected number of times");
					Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
					Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

					await client.DisconnectAsync (false);
				}

				AssertRedacted (stream, "C: A00000001 AUTHENTICATE LOGIN", "C: A00000002 NAMESPACE");
			}
		}

		static List<ImapReplayCommand> CreateEnableUTF8Commands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 ENABLE UTF8=ACCEPT\r\n", "gmail.utf8accept.txt")
			};
		}

		[Test]
		public void TestEnableUTF8 ()
		{
			var commands = CreateEnableUTF8Commands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					client.Authenticate (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				client.EnableUTF8 ();

				// ENABLE UTF8 a second time should no-op.
				client.EnableUTF8 ();

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestEnableUTF8Async ()
		{
			var commands = CreateEnableUTF8Commands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.SupportsQuotas, Is.True, "SupportsQuotas");

				await client.EnableUTF8Async ();

				// ENABLE UTF8 a second time should no-op.
				await client.EnableUTF8Async ();

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateEnableQuickResyncCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 ENABLE QRESYNC CONDSTORE\r\n", "dovecot.enable-qresync.txt"),
			};
		}

		[Test]
		public void TestEnableQuickResync ()
		{
			var commands = CreateEnableQuickResyncCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (DovecotInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (DovecotAuthenticatedCapabilities));
				Assert.That (client.InternationalizationLevel, Is.EqualTo (1), "Expected I18NLEVEL=1");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");

				client.EnableQuickResync ();

				// ENABLE QRESYNC a second time should no-op.
				client.EnableQuickResync ();

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestEnableQuickResyncAsync ()
		{
			var commands = CreateEnableQuickResyncCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (DovecotInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (DovecotAuthenticatedCapabilities));
				Assert.That (client.InternationalizationLevel, Is.EqualTo (1), "Expected I18NLEVEL=1");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");

				await client.EnableQuickResyncAsync ();

				// ENABLE QRESYNC a second time should no-op.
				await client.EnableQuickResyncAsync ();

				await client.DisconnectAsync (false);
			}
		}

		static void AssertFolder (IMailFolder folder, string fullName, string id, FolderAttributes attributes, bool subscribed, ulong highestmodseq, int count, int recent, uint uidnext, uint validity, int unread, ulong size)
		{
			if (subscribed)
				attributes |= FolderAttributes.Subscribed;

			Assert.That (folder.FullName, Is.EqualTo (fullName), "FullName");
			Assert.That (folder.Attributes, Is.EqualTo (attributes), "Attributes");
			Assert.That (folder.IsSubscribed, Is.EqualTo (subscribed), "IsSubscribed");
			Assert.That (folder.HighestModSeq, Is.EqualTo (highestmodseq), "HighestModSeq");
			Assert.That (folder, Has.Count.EqualTo (count), "Count");
			Assert.That (folder.Recent, Is.EqualTo (recent), "Recent");
			Assert.That (folder.Unread, Is.EqualTo (unread), "Unread");
			Assert.That (folder.UidNext.HasValue ? folder.UidNext.Value.Id : (uint) 0, Is.EqualTo (uidnext), "UidNext");
			Assert.That (folder.UidValidity, Is.EqualTo (validity), "UidValidity");
			Assert.That (folder.Size ?? (ulong) 0, Is.EqualTo (size), "Size");
			Assert.That (folder.Id, Is.EqualTo (id), "MailboxId");
		}

		static List<ImapReplayCommand> CreateGetFoldersCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate+statussize+objectid.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST (SUBSCRIBED) \"\" \"*\" RETURN (CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID))\r\n", "gmail.list-all.txt"),
				new ImapReplayCommand ("A00000006 LIST \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-all-no-status.txt"),
				new ImapReplayCommand ("A00000007 STATUS INBOX (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-inbox.txt"),
				new ImapReplayCommand ("A00000008 STATUS \"[Gmail]/All Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-all-mail.txt"),
				new ImapReplayCommand ("A00000009 STATUS \"[Gmail]/Drafts\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-drafts.txt"),
				new ImapReplayCommand ("A00000010 STATUS \"[Gmail]/Important\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-important.txt"),
				new ImapReplayCommand ("A00000011 STATUS \"[Gmail]/Sent Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-sent-mail.txt"),
				new ImapReplayCommand ("A00000012 STATUS \"[Gmail]/Spam\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-spam.txt"),
				new ImapReplayCommand ("A00000013 STATUS \"[Gmail]/Starred\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-starred.txt"),
				new ImapReplayCommand ("A00000014 STATUS \"[Gmail]/Trash\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-trash.txt"),
				new ImapReplayCommand ("A00000015 LSUB \"\" \"*\"\r\n", "gmail.lsub-all.txt"),
				new ImapReplayCommand ("A00000016 STATUS INBOX (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-inbox.txt"),
				new ImapReplayCommand ("A00000017 STATUS \"[Gmail]/All Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-all-mail.txt"),
				new ImapReplayCommand ("A00000018 STATUS \"[Gmail]/Drafts\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-drafts.txt"),
				new ImapReplayCommand ("A00000019 STATUS \"[Gmail]/Important\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-important.txt"),
				new ImapReplayCommand ("A00000020 STATUS \"[Gmail]/Sent Mail\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-sent-mail.txt"),
				new ImapReplayCommand ("A00000021 STATUS \"[Gmail]/Spam\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-spam.txt"),
				new ImapReplayCommand ("A00000022 STATUS \"[Gmail]/Starred\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-starred.txt"),
				new ImapReplayCommand ("A00000023 STATUS \"[Gmail]/Trash\" (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ SIZE MAILBOXID)\r\n", "gmail.status-trash.txt"),
				new ImapReplayCommand ("A00000024 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestGetFolders ()
		{
			var commands = CreateGetFoldersCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities | ImapCapabilities.StatusSize | ImapCapabilities.ObjectID));

				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread | StatusItems.Size | StatusItems.MailboxId;
				var folders = client.GetFolders (client.PersonalNamespaces[0], all, true);
				Assert.That (folders, Has.Count.EqualTo (9), "Unexpected folder count.");

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
				Assert.That (folders, Has.Count.EqualTo (9), "Unexpected folder count.");

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
				Assert.That (folders, Has.Count.EqualTo (9), "Unexpected folder count.");

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

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities | ImapCapabilities.StatusSize | ImapCapabilities.ObjectID));

				var all = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread | StatusItems.Size | StatusItems.MailboxId;
				var folders = await client.GetFoldersAsync (client.PersonalNamespaces[0], all, true);
				Assert.That (folders, Has.Count.EqualTo (9), "Unexpected folder count.");

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
				Assert.That (folders, Has.Count.EqualTo (9), "Unexpected folder count.");

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
				Assert.That (folders, Has.Count.EqualTo (9), "Unexpected folder count.");

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

		static List<ImapReplayCommand> CreateGetQuotaNonexistantQuotaRootCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 GETQUOTAROOT INBOX\r\n", "common.getquota-no-root.txt"),
				new ImapReplayCommand ("A00000006 LIST \"\" storage=0 RETURN (SUBSCRIBED CHILDREN)\r\n", ImapReplayCommandResponse.OK)
			};
		}

		[Test]
		public void TestGetQuotaNonexistantQuotaRoot ()
		{
			var commands = CreateGetQuotaNonexistantQuotaRootCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));

				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = inbox.GetQuota ();
				Assert.That (quota, Is.Not.Null, "Expected a non-null GETQUOTAROOT response.");
				Assert.That (quota.QuotaRoot.Exists, Is.False);
				Assert.That (quota.QuotaRoot.FullName, Is.EqualTo ("storage=0"));
				Assert.That (quota.CurrentStorageSize.Value, Is.EqualTo (28257));
				Assert.That (quota.StorageLimit.Value, Is.EqualTo (256000));
				Assert.That (quota.CurrentMessageCount.HasValue, Is.False);
				Assert.That (quota.MessageLimit.HasValue, Is.False);

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestGetQuotaNonexistantQuotaRootAsync ()
		{
			var commands = CreateGetQuotaNonexistantQuotaRootCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));

				var inbox = client.Inbox;

				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				var quota = await inbox.GetQuotaAsync ();
				Assert.That (quota, Is.Not.Null, "Expected a non-null GETQUOTAROOT response.");
				Assert.That (quota.QuotaRoot.Exists, Is.False);
				Assert.That (quota.QuotaRoot.FullName, Is.EqualTo ("storage=0"));
				Assert.That (quota.CurrentStorageSize.Value, Is.EqualTo (28257));
				Assert.That (quota.StorageLimit.Value, Is.EqualTo (256000));
				Assert.That (quota.CurrentMessageCount.HasValue, Is.False);
				Assert.That (quota.MessageLimit.HasValue, Is.False);

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

		static List<ImapReplayCommand> CreateDovecotCommands (out List<DateTimeOffset> internalDates, out List<MimeMessage> messages, out List<MessageFlags> flags)
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", "dovecot.authenticate.txt"),
				new ImapReplayCommand ("A00000001 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000003 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000004 ENABLE QRESYNC CONDSTORE\r\n", "dovecot.enable-qresync.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\" RETURN (SUBSCRIBED CHILDREN STATUS (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ))\r\n", "dovecot.list-personal.txt"),
				new ImapReplayCommand ("A00000006 CREATE UnitTests.\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "dovecot.list-unittests.txt"),
				new ImapReplayCommand ("A00000008 CREATE UnitTests.Messages\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000009 LIST \"\" UnitTests.Messages\r\n", "dovecot.list-unittests-messages.txt")
			};

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

					using (var reader = new StreamReader (stream, TextEncodings.Latin1))
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
			commands.Add (new ImapReplayCommand ("A00000024 UID SEARCH RETURN (ALL) UNDELETED SEEN\r\n", "dovecot.optimized-search.txt"));
			commands.Add (new ImapReplayCommand ("A00000025 CREATE UnitTests.Destination\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000026 LIST \"\" UnitTests.Destination\r\n", "dovecot.list-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000027 UID COPY 1:7 UnitTests.Destination\r\n", "dovecot.copy.txt"));
			commands.Add (new ImapReplayCommand ("A00000028 UID MOVE 1:7 UnitTests.Destination\r\n", "dovecot.move.txt"));
			commands.Add (new ImapReplayCommand ("A00000029 STATUS UnitTests.Destination (MESSAGES RECENT UIDNEXT UIDVALIDITY UNSEEN HIGHESTMODSEQ)\r\n", "dovecot.status-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000030 SELECT UnitTests.Destination (CONDSTORE)\r\n", "dovecot.select-unittests-destination.txt"));
			commands.Add (new ImapReplayCommand ("A00000031 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1 VANISHED)\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000032 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000033 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000034 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch4.txt"));
			commands.Add (new ImapReplayCommand ("A00000035 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) (CHANGEDSINCE 1)\r\n", "dovecot.fetch4.txt"));
			commands.Add (new ImapReplayCommand ("A00000036 UID FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000037 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000038 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES X-MAILER)])\r\n", "dovecot.fetch3.txt"));
			commands.Add (new ImapReplayCommand ("A00000039 FETCH 1:* (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)])\r\n", "dovecot.fetch4.txt"));
			commands.Add (new ImapReplayCommand ("A00000040 FETCH 1:14 (UID FLAGS INTERNALDATE RFC822.SIZE ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)])\r\n", "dovecot.fetch4.txt"));
			commands.Add (new ImapReplayCommand ("A00000041 UID FETCH 1 (BODY.PEEK[])\r\n", "dovecot.getbodypart.txt"));
			commands.Add (new ImapReplayCommand ("A00000042 FETCH 1 (BODY.PEEK[])\r\n", "dovecot.getbodypart.txt"));
			commands.Add (new ImapReplayCommand ("A00000043 UID FETCH 2 (BODY.PEEK[1.MIME] BODY.PEEK[1])\r\n", "dovecot.getbodypart1.txt"));
			commands.Add (new ImapReplayCommand ("A00000044 FETCH 2 (BODY.PEEK[1.MIME] BODY.PEEK[1])\r\n", "dovecot.getbodypart1.txt"));
			commands.Add (new ImapReplayCommand ("A00000045 UID FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getmessageheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000046 FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getmessageheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000047 UID FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getmessageheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000048 FETCH 1 (BODY.PEEK[HEADER])\r\n", "dovecot.getmessageheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000049 UID FETCH 2 (BODY.PEEK[1.MIME])\r\n", "dovecot.getbodypartheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000050 FETCH 2 (BODY.PEEK[1.MIME])\r\n", "dovecot.getbodypartheaders.txt"));
			commands.Add (new ImapReplayCommand ("A00000051 UID FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream.txt"));
			commands.Add (new ImapReplayCommand ("A00000052 UID FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream2.txt"));
			commands.Add (new ImapReplayCommand ("A00000053 FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream.txt"));
			commands.Add (new ImapReplayCommand ("A00000054 FETCH 1 (BODY.PEEK[]<128.64>)\r\n", "dovecot.getstream2.txt"));
			commands.Add (new ImapReplayCommand ("A00000055 UID FETCH 1 (BODY.PEEK[HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)])\r\n", "dovecot.getstream-section.txt"));
			commands.Add (new ImapReplayCommand ("A00000056 FETCH 1 (BODY.PEEK[HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)])\r\n", "dovecot.getstream-section2.txt"));
			commands.Add (new ImapReplayCommand ("A00000057 UID STORE 1:14 (UNCHANGEDSINCE 3) +FLAGS.SILENT (\\Deleted $MailKit)\r\n", "dovecot.store-deleted-custom.txt"));
			commands.Add (new ImapReplayCommand ("A00000058 STORE 1:7 (UNCHANGEDSINCE 5) FLAGS.SILENT (\\Deleted \\Seen $MailKit)\r\n", "dovecot.setflags-unchangedsince.txt"));
			commands.Add (new ImapReplayCommand ("A00000059 UID SEARCH RETURN (ALL) UID 1:14 OR NEW OR OLD OR ANSWERED OR DELETED OR DRAFT OR FLAGGED OR RECENT OR UNANSWERED OR UNDELETED OR UNDRAFT OR UNFLAGGED OR UNSEEN OR KEYWORD $MailKit UNKEYWORD $MailKit\r\n", "dovecot.search-uids.txt"));
			commands.Add (new ImapReplayCommand ("A00000060 UID SEARCH RETURN (ALL RELEVANCY COUNT MIN MAX) UID 1:14 LARGER 256 SMALLER 512\r\n", "dovecot.search-uids-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000061 UID SORT RETURN (ALL) (REVERSE DATE SUBJECT DISPLAYFROM SIZE) US-ASCII OR OR (SENTBEFORE 12-Oct-2016 SENTSINCE 10-Oct-2016) NOT SENTON 11-Oct-2016 OR (BEFORE 12-Oct-2016 SINCE 10-Oct-2016) NOT ON 11-Oct-2016\r\n", "dovecot.sort-by-date.txt"));
			commands.Add (new ImapReplayCommand ("A00000062 UID SORT RETURN (ALL) (FROM TO CC) US-ASCII UID 1:14 OR BCC xyz OR CC xyz OR FROM xyz OR TO xyz OR SUBJECT xyz OR HEADER Message-Id mimekit.net OR BODY \"This is the message body.\" TEXT message\r\n", "dovecot.sort-by-strings.txt"));
			commands.Add (new ImapReplayCommand ("A00000063 UID SORT RETURN (ALL RELEVANCY COUNT MIN MAX) (DISPLAYTO) US-ASCII UID 1:14 OLDER 1 YOUNGER 3600\r\n", "dovecot.sort-uids-options.txt"));
			commands.Add (new ImapReplayCommand ("A00000064 UID SEARCH ALL\r\n", "dovecot.search-raw.txt"));
			commands.Add (new ImapReplayCommand ("A00000065 UID SORT (REVERSE ARRIVAL) US-ASCII ALL\r\n", "dovecot.sort-raw.txt"));
			commands.Add (new ImapReplayCommand ("A00000066 UID FETCH 1:* (BODY.PEEK[])\r\n", "dovecot.getstreams1.txt"));
			commands.Add (new ImapReplayCommand ("A00000067 FETCH 1:3 (UID BODY.PEEK[])\r\n", "dovecot.getstreams1.txt"));
			commands.Add (new ImapReplayCommand ("A00000068 FETCH 1:* (UID BODY.PEEK[])\r\n", "dovecot.getstreams2.txt"));
			commands.Add (new ImapReplayCommand ("A00000069 EXPUNGE\r\n", "dovecot.expunge.txt"));
			commands.Add (new ImapReplayCommand ("A00000070 CLOSE\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000071 NOOP\r\n", "dovecot.noop+alert.txt"));
			commands.Add (new ImapReplayCommand ("A00000072 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestDovecot ()
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			var commands = CreateDovecotCommands (out var internalDates, out var messages, out var flags);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (DovecotInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (DovecotAuthenticatedCapabilities));
				Assert.That (client.InternationalizationLevel, Is.EqualTo (1), "Expected I18NLEVEL=1");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");
				// TODO: verify CONTEXT=SEARCH

				var personal = client.GetFolder (client.PersonalNamespaces[0]);

				Assert.That (client.Inbox.Supports (FolderFeature.AccessRights), Is.False);
				Assert.That (client.Inbox.Supports (FolderFeature.Annotations), Is.False);
				Assert.That (client.Inbox.Supports (FolderFeature.Metadata), Is.False);
				Assert.That (client.Inbox.Supports (FolderFeature.ModSequences), Is.False); // not supported until opened
				Assert.That (client.Inbox.Supports (FolderFeature.QuickResync), Is.False); // not supported until it is enabled
				Assert.That (client.Inbox.Supports (FolderFeature.Quotas), Is.False);
				Assert.That (client.Inbox.Supports (FolderFeature.Sorting), Is.True);
				Assert.That (client.Inbox.Supports (FolderFeature.Threading), Is.True);
				Assert.That (client.Inbox.Supports (FolderFeature.UTF8), Is.False);

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
					Assert.Fail ($"Did not expect an exception when enabling QRESYNC: {ex}");
				}

				Assert.That (client.Inbox.Supports (FolderFeature.QuickResync), Is.True);

				// take advantage of LIST-STATUS to get top-level personal folders...
				var statusItems = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;

				var folders = personal.GetSubfolders (statusItems, false).ToArray ();
				Assert.That (folders, Has.Length.EqualTo (7), "Expected 7 folders");

				var expectedFolderNames = new [] { "Archives", "Drafts", "Junk", "Sent Messages", "Trash", "INBOX", "NIL" };
				var expectedUidValidities = new [] { 1436832059, 1436832060, 1436832061, 1436832062, 1436832063, 1436832057, 1436832057 };
				var expectedHighestModSeq = new [] { 1, 1, 1, 1, 1, 15, 1 };
				var expectedMessages = new [] { 0, 0, 0, 0, 0, 4, 0 };
				var expectedUidNext = new [] { 1, 1, 1, 1, 1, 5, 1 };
				var expectedRecent = new [] { 0, 0, 0, 0, 0, 0, 0 };
				var expectedUnseen = new [] { 0, 0, 0, 0, 0, 0, 0 };

				for (int i = 0; i < folders.Length; i++) {
					Assert.That (folders[i].FullName, Is.EqualTo (expectedFolderNames[i]), "FullName did not match");
					Assert.That (folders[i].Name, Is.EqualTo (expectedFolderNames[i]), "Name did not match");
					Assert.That (folders[i].UidValidity, Is.EqualTo (expectedUidValidities[i]), "UidValidity did not match");
					Assert.That (folders[i].HighestModSeq, Is.EqualTo (expectedHighestModSeq[i]), "HighestModSeq did not match");
					Assert.That (folders[i], Has.Count.EqualTo (expectedMessages[i]), "Count did not match");
					Assert.That (folders[i].Recent, Is.EqualTo (expectedRecent[i]), "Recent did not match");
					Assert.That (folders[i].Unread, Is.EqualTo (expectedUnseen[i]), "Unread did not match");
				}

				var unitTests = personal.Create ("UnitTests", false);
				Assert.That (unitTests.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "Unexpected UnitTests folder attributes");

				var folder = unitTests.Create ("Messages", true);
				Assert.That (folder.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "Unexpected UnitTests.Messages folder attributes");
				//Assert.That (unitTests.Attributes, Is.EqualTo (FolderAttributes.HasChildren), "Expected UnitTests Attributes to be updated");

				// Use MULTIAPPEND to append some test messages
				var appended = folder.Append (messages, flags, internalDates);
				Assert.That (appended, Has.Count.EqualTo (8), "Unexpected number of messages appended");
				foreach (var message in messages)
					message.Dispose ();

				// SELECT the folder so that we can test some stuff
				var access = folder.Open (FolderAccess.ReadWrite);
				Assert.That (folder.Supports (FolderFeature.ModSequences), Is.True);
				Assert.That (folder.PermanentFlags, Is.EqualTo (expectedPermanentFlags), "UnitTests.Messages PERMANENTFLAGS");
				Assert.That (folder.AcceptedFlags, Is.EqualTo (expectedFlags), "UnitTests.Messages FLAGS");
				Assert.That (folder, Has.Count.EqualTo (8), "UnitTests.Messages EXISTS");
				Assert.That (folder.Recent, Is.EqualTo (8), "UnitTests.Messages RECENT");
				Assert.That (folder.FirstUnread, Is.EqualTo (0), "UnitTests.Messages UNSEEN");
				Assert.That (folder.UidValidity, Is.EqualTo (1436832084U), "UnitTests.Messages UIDVALIDITY");
				Assert.That (folder.UidNext.Value.Id, Is.EqualTo (9), "UnitTests.Messages UIDNEXT");
				Assert.That (folder.HighestModSeq, Is.EqualTo (2UL), "UnitTests.Messages HIGHESTMODSEQ");
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "Expected UnitTests.Messages to be opened in READ-WRITE mode");

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
				Assert.That (flagsChanged, Is.Empty, "Unexpected number of FlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (8), "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].UniqueId.Value.Id, Is.EqualTo (i + 1), $"Unexpected modSeqChanged[{i}].UniqueId");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (3), $"Unexpected modSeqChanged[{i}].ModSeq");
				}
				Assert.That (recentChanged, Is.False, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				var answered = new UniqueIdSet (SortOrder.Ascending) {
					appended[0], // A
					appended[1], // B
					appended[2] // C
				};
				folder.AddFlags (answered, MessageFlags.Answered, true);
				Assert.That (flagsChanged, Is.Empty, "Unexpected number of FlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (3), "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].UniqueId.Value.Id, Is.EqualTo (i + 1), $"Unexpected modSeqChanged[{i}].UniqueId");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (4), $"Unexpected modSeqChanged[{i}].ModSeq");
				}
				Assert.That (recentChanged, Is.False, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				// Delete some messages so we can test that QRESYNC emits some MessageVanished events
				// both now *and* when we use QRESYNC to re-open the folder
				var deleted = new UniqueIdSet (SortOrder.Ascending) {
					appended[7] // H
				};
				folder.AddFlags (deleted, MessageFlags.Deleted, true);
				Assert.That (flagsChanged, Is.Empty, "Unexpected number of FlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (1), "Unexpected number of ModSeqChanged events");
				Assert.That (modSeqChanged[0].Index, Is.EqualTo (7), $"Unexpected modSeqChanged[{0}].Index");
				Assert.That (modSeqChanged[0].UniqueId.Value.Id, Is.EqualTo (8), $"Unexpected modSeqChanged[{0}].UniqueId");
				Assert.That (modSeqChanged[0].ModSeq, Is.EqualTo (5), $"Unexpected modSeqChanged[{0}].ModSeq");
				Assert.That (recentChanged, Is.False, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				folder.Expunge (deleted);
				Assert.That (vanished, Has.Count.EqualTo (1), "Expected MessagesVanished event");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (1), "Unexpected number of messages vanished");
				Assert.That (vanished[0].UniqueIds[0].Id, Is.EqualTo (8), "Unexpected UID for vanished message");
				Assert.That (vanished[0].Earlier, Is.False, "Expected EARLIER to be false");
				Assert.That (recentChanged, Is.True, "Expected RecentChanged event");
				recentChanged = false;
				vanished.Clear ();

				Assert.That (folder.Supports (FolderFeature.Threading), Is.True, "Supports threading");
				Assert.That (folder.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Supports threading by References");
				Assert.That (folder.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.OrderedSubject), "Supports threading by OrderedSubject");

				// Verify that THREAD works correctly
				var threaded = folder.Thread (ThreadingAlgorithm.References, SearchQuery.All);
				Assert.That (threaded, Has.Count.EqualTo (2), "Unexpected number of root nodes in threaded results");

				threaded = folder.Thread (UniqueIdRange.All, ThreadingAlgorithm.OrderedSubject, SearchQuery.All);
				Assert.That (threaded, Has.Count.EqualTo (7), "Unexpected number of root nodes in threaded results");

				// UNSELECT the folder so we can re-open it using QRESYNC
				folder.Close ();

				// Use QRESYNC to get the changes since last time we opened the folder
				Assert.That (folder.Supports (FolderFeature.QuickResync), Is.True, "Supports QRESYNC");
				access = folder.Open (FolderAccess.ReadWrite, uidValidity, highestModSeq, appended);
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "Expected UnitTests.Messages to be opened in READ-WRITE mode");
				Assert.That (flagsChanged, Has.Count.EqualTo (7), "Unexpected number of MessageFlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (7), "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < flagsChanged.Count; i++) {
					var messageFlags = MessageFlags.Seen | MessageFlags.Draft;

					if (i < 3)
						messageFlags |= MessageFlags.Answered;

					Assert.That (flagsChanged[i].Index, Is.EqualTo (i), $"Unexpected value for flagsChanged[{i}].Index");
					Assert.That (flagsChanged[i].UniqueId.Value.Id, Is.EqualTo ((uint) (i + 1)), $"Unexpected value for flagsChanged[{i}].UniqueId");
					Assert.That (flagsChanged[i].Flags, Is.EqualTo (messageFlags), $"Unexpected value for flagsChanged[{i}].Flags");

					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected value for modSeqChanged[{i}].Index");
					if (i < 3)
						Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (4), $"Unexpected value for modSeqChanged[{i}].ModSeq");
					else
						Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (3), $"Unexpected value for modSeqChanged[{i}].ModSeq");
				}
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				Assert.That (vanished, Has.Count.EqualTo (1), "Unexpected number of MessagesVanished events");
				Assert.That (vanished[0].Earlier, Is.True, "Expected VANISHED EARLIER");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (1), "Unexpected number of messages vanished");
				Assert.That (vanished[0].UniqueIds[0].Id, Is.EqualTo (8), "Unexpected UID for vanished message");
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
				Assert.That (changed.UniqueIds, Has.Count.EqualTo (7), "Unexpected number of UIDs");
				Assert.That (changed.Relevancy, Has.Count.EqualTo (changed.Count), "Unexpected number of relevancy scores");
				Assert.That (changed.ModSeq.HasValue, Is.True, "Expected the ModSeq property to be set");
				Assert.That (changed.ModSeq.Value, Is.EqualTo (4), "Unexpected ModSeq value");
				Assert.That (changed.Min.Value.Id, Is.EqualTo (1), "Unexpected Min");
				Assert.That (changed.Max.Value.Id, Is.EqualTo (7), "Unexpected Max");
				Assert.That (changed.Count, Is.EqualTo (7), "Unexpected Count");

				var fetched = folder.Fetch (changed.UniqueIds, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.That (fetched, Has.Count.EqualTo (7), "Unexpected number of messages fetched");
				for (int i = 0; i < fetched.Count; i++) {
					Assert.That (fetched[i].Index, Is.EqualTo (i), "Unexpected Index");
					Assert.That (fetched[i].UniqueId.Id, Is.EqualTo (i + 1), "Unexpected UniqueId");
				}

				// or... we could just use a single UID FETCH command like so:
				fetched = folder.Fetch (UniqueIdRange.All, highestModSeq, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				for (int i = 0; i < fetched.Count; i++) {
					Assert.That (fetched[i].Index, Is.EqualTo (i), "Unexpected Index");
					Assert.That (fetched[i].UniqueId.Id, Is.EqualTo (i + 1), "Unexpected UniqueId");
				}
				Assert.That (fetched, Has.Count.EqualTo (7), "Unexpected number of messages fetched");
				Assert.That (vanished, Has.Count.EqualTo (1), "Unexpected number of MessagesVanished events");
				Assert.That (vanished[0].Earlier, Is.True, "Expected VANISHED EARLIER");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (1), "Unexpected number of messages vanished");
				Assert.That (vanished[0].UniqueIds[0].Id, Is.EqualTo (8), "Unexpected UID for vanished message");
				vanished.Clear ();

				// Use SORT to order by reverse arrival order
				var orderBy = new OrderBy[] { new OrderBy (OrderByType.Arrival, SortOrder.Descending) };
				var sorted = folder.Sort (searchOptions, SearchQuery.All, orderBy);
				Assert.That (sorted.UniqueIds, Has.Count.EqualTo (7), "Unexpected number of UIDs");
				Assert.That (sorted.Relevancy, Has.Count.EqualTo (sorted.Count), "Unexpected number of relevancy scores");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.That (sorted.UniqueIds[i].Id, Is.EqualTo (7 - i), $"Unexpected value for UniqueId[{i}]");
				Assert.That (sorted.ModSeq.HasValue, Is.False, "Expected the ModSeq property to be null");
				Assert.That (sorted.Min.Value.Id, Is.EqualTo (7), "Unexpected Min");
				Assert.That (sorted.Max.Value.Id, Is.EqualTo (1), "Unexpected Max");
				Assert.That (sorted.Count, Is.EqualTo (7), "Unexpected Count");

				// Verify that optimizing NOT queries works correctly
				var uids = folder.Search (SearchQuery.Not (SearchQuery.Deleted).And (SearchQuery.Not (SearchQuery.NotSeen)));
				Assert.That (uids, Has.Count.EqualTo (7), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				// Create a Destination folder to use for copying/moving messages to
				var destination = (ImapFolder) unitTests.Create ("Destination", true);
				Assert.That (destination.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "Unexpected UnitTests.Destination folder attributes");

				// COPY messages to the Destination folder
				var copied = folder.CopyTo (uids, destination);
				Assert.That (copied.Source, Has.Count.EqualTo (uids.Count), "Unexpetced Source.Count");
				Assert.That (copied.Destination, Has.Count.EqualTo (uids.Count), "Unexpetced Destination.Count");

				// MOVE messages to the Destination folder
				var moved = folder.MoveTo (uids, destination);
				Assert.That (copied.Source, Has.Count.EqualTo (uids.Count), "Unexpetced Source.Count");
				Assert.That (copied.Destination, Has.Count.EqualTo (uids.Count), "Unexpetced Destination.Count");
				Assert.That (vanished, Has.Count.EqualTo (1), "Expected VANISHED event");
				vanished.Clear ();

				destination.Status (statusItems);
				Assert.That (destination.UidValidity, Is.EqualTo (moved.Destination[0].Validity), "Unexpected UIDVALIDITY");

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
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "Expected UnitTests.Destination to be opened in READ-WRITE mode");

				var fetchHeaders = new HashSet<HeaderId> {
					HeaderId.References,
					HeaderId.XMailer
				};

				var indexes = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

				// Fetch + modseq
				fetched = destination.Fetch (UniqueIdRange.All, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				// Fetch
				fetched = destination.Fetch (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = destination.Fetch (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                             MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                             MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				uids = new UniqueIdSet (SortOrder.Ascending);

				for (int i = 0; i < fetched.Count; i++) {
					Assert.That (fetched[i].Index, Is.EqualTo (i), "Unexpected Index");
					Assert.That (fetched[i].UniqueId.Id, Is.EqualTo (i + 1), "Unexpected UniqueId");

					uids.Add (fetched[i].UniqueId);
				}

				using (var entity = destination.GetBodyPart (fetched[0].UniqueId, fetched[0].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				using (var entity = destination.GetBodyPart (fetched[0].Index, fetched[0].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				using (var entity = destination.GetBodyPart (fetched[1].UniqueId, fetched[1].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				using (var entity = destination.GetBodyPart (fetched[1].Index, fetched[1].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				var headers =  destination.GetHeaders (fetched[0].UniqueId);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[0].Index);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(int) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId, BodyPart) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[0].Index, fetched[0].TextBody);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(int, BodyPart) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[1].UniqueId, fetched[1].TextBody);
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = destination.GetHeaders (fetched[1].Index, fetched[1].TextBody);
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				using (var stream = destination.GetStream (fetched[0].UniqueId, 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = destination.GetStream (fetched[0].UniqueId, "", 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = destination.GetStream (fetched[0].Index, 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = destination.GetStream (fetched[0].Index, "", 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = destination.GetStream (fetched[0].UniqueId, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.That (stream.Length, Is.EqualTo (62), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n"));
				}

				using (var stream = destination.GetStream (fetched[0].Index, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.That (stream.Length, Is.EqualTo (62), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n"));
				}

				var custom = new HashSet<string> { "$MailKit" };

				var unchanged1 = destination.AddFlags (uids, destination.HighestModSeq, MessageFlags.Deleted, custom, true);
				Assert.That (modSeqChanged, Has.Count.EqualTo (14), "Unexpected number of ModSeqChanged events");
				Assert.That (destination.HighestModSeq, Is.EqualTo (5));
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected value for modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (5), $"Unexpected value for modSeqChanged[{i}].ModSeq");
				}
				Assert.That (unchanged1, Has.Count.EqualTo (2), "[MODIFIED uid-set]");
				Assert.That (unchanged1[0].Id, Is.EqualTo (7), "unchanged uids[0]");
				Assert.That (unchanged1[1].Id, Is.EqualTo (9), "unchanged uids[1]");
				modSeqChanged.Clear ();

				var unchanged2 = destination.SetFlags (new int[] { 0, 1, 2, 3, 4, 5, 6 }, destination.HighestModSeq, MessageFlags.Seen | MessageFlags.Deleted, custom, true);
				Assert.That (modSeqChanged, Has.Count.EqualTo (7), "Unexpected number of ModSeqChanged events");
				Assert.That (destination.HighestModSeq, Is.EqualTo (6));
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected value for modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (6), $"Unexpected value for modSeqChanged[{i}].ModSeq");
				}
				Assert.That (unchanged2, Has.Count.EqualTo (2), "[MODIFIED seq-set]");
				Assert.That (unchanged2[0], Is.EqualTo (6), "unchanged indexes[0]");
				Assert.That (unchanged2[1], Is.EqualTo (8), "unchanged indexes[1]");
				modSeqChanged.Clear ();

				var results = destination.Search (uids, SearchQuery.New.Or (SearchQuery.Old.Or (SearchQuery.Answered.Or (SearchQuery.Deleted.Or (SearchQuery.Draft.Or (SearchQuery.Flagged.Or (SearchQuery.Recent.Or (SearchQuery.NotAnswered.Or (SearchQuery.NotDeleted.Or (SearchQuery.NotDraft.Or (SearchQuery.NotFlagged.Or (SearchQuery.NotSeen.Or (SearchQuery.HasKeyword ("$MailKit").Or (SearchQuery.NotKeyword ("$MailKit")))))))))))))));
				Assert.That (results, Has.Count.EqualTo (14), "Unexpected number of UIDs");

				var matches = destination.Search (searchOptions, uids, SearchQuery.LargerThan (256).And (SearchQuery.SmallerThan (512)));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.That (matches.Count, Is.EqualTo (10), "Unexpected COUNT");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (13), "Unexpected MAX");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (2), "Unexpected MIN");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (10), "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (expectedMatchedUids[i]));
				Assert.That (matches.Relevancy, Has.Count.EqualTo (matches.Count), "Unexpected number of relevancy scores");

				orderBy = new OrderBy[] { OrderBy.ReverseDate, OrderBy.Subject, OrderBy.DisplayFrom, OrderBy.Size };
				var sentDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.SentBefore (new DateTime (2016, 10, 12)), SearchQuery.SentSince (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.SentOn (new DateTime (2016, 10, 11))));
				var deliveredDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.DeliveredBefore (new DateTime (2016, 10, 12)), SearchQuery.DeliveredAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.DeliveredOn (new DateTime (2016, 10, 11))));
				results = destination.Sort (sentDateQuery.Or (deliveredDateQuery), orderBy);
				var expectedSortByDateResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				Assert.That (results, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.That (results[i].Id, Is.EqualTo (expectedSortByDateResults[i]));

				var stringQuery = SearchQuery.BccContains ("xyz").Or (SearchQuery.CcContains ("xyz").Or (SearchQuery.FromContains ("xyz").Or (SearchQuery.ToContains ("xyz").Or (SearchQuery.SubjectContains ("xyz").Or (SearchQuery.HeaderContains ("Message-Id", "mimekit.net").Or (SearchQuery.BodyContains ("This is the message body.").Or (SearchQuery.MessageContains ("message"))))))));
				orderBy = new OrderBy[] { OrderBy.From, OrderBy.To, OrderBy.Cc };
				results = destination.Sort (uids, stringQuery, orderBy);
				Assert.That (results, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.That (results[i].Id, Is.EqualTo (i + 1));

				orderBy = new OrderBy[] { OrderBy.DisplayTo };
				matches = destination.Sort (searchOptions, uids, SearchQuery.OlderThan (1).And (SearchQuery.YoungerThan (3600)), orderBy);
				Assert.That (matches.Count, Is.EqualTo (14), "Unexpected COUNT");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (i + 1));
				Assert.That (matches.Relevancy, Has.Count.EqualTo (matches.Count), "Unexpected number of relevancy scores");

				client.Capabilities &= ~ImapCapabilities.ESearch;
				matches = ((ImapFolder) destination).Search ("ALL");
				Assert.That (matches.Max.HasValue, Is.True, "MAX should always be set");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX value");
				Assert.That (matches.Min.HasValue, Is.True, "MIN should always be set");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN value");
				Assert.That (matches.Count, Is.EqualTo (14), "COUNT should always be set");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14));
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (i + 1));

				client.Capabilities &= ~ImapCapabilities.ESort;
				matches = ((ImapFolder) destination).Sort ("(REVERSE ARRIVAL) US-ASCII ALL");
				Assert.That (matches.Max.HasValue, Is.True, "MAX should always be set");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX value");
				Assert.That (matches.Min.HasValue, Is.True, "MIN should always be set");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN value");
				Assert.That (matches.Count, Is.EqualTo (14), "COUNT should always be set");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14));
				var expectedSortByReverseArrivalResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (expectedSortByReverseArrivalResults[i]));

				destination.GetStreams (UniqueIdRange.All, GetStreamsCallback);
				destination.GetStreams (new int[] { 0, 1, 2 }, GetStreamsCallback);
				destination.GetStreams (0, -1, GetStreamsCallback);

				destination.Expunge ();
				Assert.That (destination.HighestModSeq, Is.EqualTo (7));
				Assert.That (vanished, Has.Count.EqualTo (1), "Unexpected number of Vanished events");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (14), "Unexpected number of UIDs in Vanished event");
				for (int i = 0; i < vanished[0].UniqueIds.Count; i++)
					Assert.That (vanished[0].UniqueIds[i].Id, Is.EqualTo (i + 1));
				Assert.That (vanished[0].Earlier, Is.False, "Unexpected value for Earlier");
				vanished.Clear ();

				destination.Close (true);

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo ("System shutdown in 10 minutes"));
					alerts++;
				};
				client.NoOp ();
				Assert.That (alerts, Is.EqualTo (1), "Alert event failed to fire.");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestDovecotAsync ()
		{
			var expectedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.Draft;
			var expectedPermanentFlags = expectedFlags | MessageFlags.UserDefined;
			var commands = CreateDovecotCommands (out var internalDates, out var messages, out var flags);

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (DovecotInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("DIGEST-MD5"), "Expected SASL DIGEST-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("CRAM-MD5"), "Expected SASL CRAM-MD5 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("NTLM"), "Expected SASL NTLM auth mechanism");

				// Note: we do not want to use SASL at all...
				client.AuthenticationMechanisms.Clear ();

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (DovecotAuthenticatedCapabilities));
				Assert.That (client.InternationalizationLevel, Is.EqualTo (1), "Expected I18NLEVEL=1");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.OrderedSubject), "Expected THREAD=ORDEREDSUBJECT");
				Assert.That (client.ThreadingAlgorithms, Does.Contain (ThreadingAlgorithm.References), "Expected THREAD=REFERENCES");
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
					Assert.Fail ($"Did not expect an exception when enabling QRESYNC: {ex}");
				}

				// take advantage of LIST-STATUS to get top-level personal folders...
				var statusItems = StatusItems.Count | StatusItems.HighestModSeq | StatusItems.Recent | StatusItems.UidNext | StatusItems.UidValidity | StatusItems.Unread;

				var folders = (await personal.GetSubfoldersAsync (statusItems, false)).ToArray ();
				Assert.That (folders, Has.Length.EqualTo (7), "Expected 7 folders");

				var expectedFolderNames = new [] { "Archives", "Drafts", "Junk", "Sent Messages", "Trash", "INBOX", "NIL" };
				var expectedUidValidities = new [] { 1436832059, 1436832060, 1436832061, 1436832062, 1436832063, 1436832057, 1436832057 };
				var expectedHighestModSeq = new [] { 1, 1, 1, 1, 1, 15, 1 };
				var expectedMessages = new [] { 0, 0, 0, 0, 0, 4, 0 };
				var expectedUidNext = new [] { 1, 1, 1, 1, 1, 5, 1 };
				var expectedRecent = new [] { 0, 0, 0, 0, 0, 0, 0 };
				var expectedUnseen = new [] { 0, 0, 0, 0, 0, 0, 0 };

				for (int i = 0; i < folders.Length; i++) {
					Assert.That (folders[i].FullName, Is.EqualTo (expectedFolderNames[i]), "FullName did not match");
					Assert.That (folders[i].Name, Is.EqualTo (expectedFolderNames[i]), "Name did not match");
					Assert.That (folders[i].UidValidity, Is.EqualTo (expectedUidValidities[i]), "UidValidity did not match");
					Assert.That (folders[i].HighestModSeq, Is.EqualTo (expectedHighestModSeq[i]), "HighestModSeq did not match");
					Assert.That (folders[i], Has.Count.EqualTo (expectedMessages[i]), "Count did not match");
					Assert.That (folders[i].Recent, Is.EqualTo (expectedRecent[i]), "Recent did not match");
					Assert.That (folders[i].Unread, Is.EqualTo (expectedUnseen[i]), "Unread did not match");
				}

				var unitTests = await personal.CreateAsync ("UnitTests", false);
				Assert.That (unitTests.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "Unexpected UnitTests folder attributes");

				var folder = await unitTests.CreateAsync ("Messages", true);
				Assert.That (folder.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "Unexpected UnitTests.Messages folder attributes");
				//Assert.That (unitTests.Attributes, Is.EqualTo (FolderAttributes.HasChildren), "Expected UnitTests Attributes to be updated");

				// Use MULTIAPPEND to append some test messages
				var appended = await folder.AppendAsync (messages, flags, internalDates);
				Assert.That (appended, Has.Count.EqualTo (8), "Unexpected number of messages appended");
				foreach (var message in messages)
					message.Dispose ();

				// SELECT the folder so that we can test some stuff
				var access = await folder.OpenAsync (FolderAccess.ReadWrite);
				Assert.That (folder.PermanentFlags, Is.EqualTo (expectedPermanentFlags), "UnitTests.Messages PERMANENTFLAGS");
				Assert.That (folder.AcceptedFlags, Is.EqualTo (expectedFlags), "UnitTests.Messages FLAGS");
				Assert.That (folder, Has.Count.EqualTo (8), "UnitTests.Messages EXISTS");
				Assert.That (folder.Recent, Is.EqualTo (8), "UnitTests.Messages RECENT");
				Assert.That (folder.FirstUnread, Is.EqualTo (0), "UnitTests.Messages UNSEEN");
				Assert.That (folder.UidValidity, Is.EqualTo (1436832084U), "UnitTests.Messages UIDVALIDITY");
				Assert.That (folder.UidNext.Value.Id, Is.EqualTo (9), "UnitTests.Messages UIDNEXT");
				Assert.That (folder.HighestModSeq, Is.EqualTo (2UL), "UnitTests.Messages HIGHESTMODSEQ");
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "Expected UnitTests.Messages to be opened in READ-WRITE mode");

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
				Assert.That (flagsChanged, Is.Empty, "Unexpected number of FlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (8), "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].UniqueId.Value.Id, Is.EqualTo (i + 1), $"Unexpected modSeqChanged[{i}].UniqueId");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (3), $"Unexpected modSeqChanged[{i}].ModSeq");
				}
				Assert.That (recentChanged, Is.False, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				var answered = new UniqueIdSet (SortOrder.Ascending) {
					appended[0], // A
					appended[1], // B
					appended[2] // C
				};
				await folder.AddFlagsAsync (answered, MessageFlags.Answered, true);
				Assert.That (flagsChanged, Is.Empty, "Unexpected number of FlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (3), "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].UniqueId.Value.Id, Is.EqualTo (i + 1), $"Unexpected modSeqChanged[{i}].UniqueId");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (4), $"Unexpected modSeqChanged[{i}].ModSeq");
				}
				Assert.That (recentChanged, Is.False, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				// Delete some messages so we can test that QRESYNC emits some MessageVanished events
				// both now *and* when we use QRESYNC to re-open the folder
				var deleted = new UniqueIdSet (SortOrder.Ascending) {
					appended[7] // H
				};
				await folder.AddFlagsAsync (deleted, MessageFlags.Deleted, true);
				Assert.That (flagsChanged, Is.Empty, "Unexpected number of FlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (1), "Unexpected number of ModSeqChanged events");
				Assert.That (modSeqChanged[0].Index, Is.EqualTo (7), $"Unexpected modSeqChanged[{0}].Index");
				Assert.That (modSeqChanged[0].UniqueId.Value.Id, Is.EqualTo (8), $"Unexpected modSeqChanged[{0}].UniqueId");
				Assert.That (modSeqChanged[0].ModSeq, Is.EqualTo (5), $"Unexpected modSeqChanged[{0}].ModSeq");
				Assert.That (recentChanged, Is.False, "Unexpected RecentChanged event");
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				await folder.ExpungeAsync (deleted);
				Assert.That (vanished, Has.Count.EqualTo (1), "Expected MessagesVanished event");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (1), "Unexpected number of messages vanished");
				Assert.That (vanished[0].UniqueIds[0].Id, Is.EqualTo (8), "Unexpected UID for vanished message");
				Assert.That (vanished[0].Earlier, Is.False, "Expected EARLIER to be false");
				Assert.That (recentChanged, Is.True, "Expected RecentChanged event");
				recentChanged = false;
				vanished.Clear ();

				// Verify that THREAD works correctly
				var threaded = await folder.ThreadAsync (ThreadingAlgorithm.References, SearchQuery.All);
				Assert.That (threaded, Has.Count.EqualTo (2), "Unexpected number of root nodes in threaded results");

				threaded = await folder.ThreadAsync (UniqueIdRange.All, ThreadingAlgorithm.OrderedSubject, SearchQuery.All);
				Assert.That (threaded, Has.Count.EqualTo (7), "Unexpected number of root nodes in threaded results");

				// UNSELECT the folder so we can re-open it using QRESYNC
				await folder.CloseAsync ();

				// Use QRESYNC to get the changes since last time we opened the folder
				access = await folder.OpenAsync (FolderAccess.ReadWrite, uidValidity, highestModSeq, appended);
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "Expected UnitTests.Messages to be opened in READ-WRITE mode");
				Assert.That (flagsChanged, Has.Count.EqualTo (7), "Unexpected number of MessageFlagsChanged events");
				Assert.That (modSeqChanged, Has.Count.EqualTo (7), "Unexpected number of ModSeqChanged events");
				for (int i = 0; i < flagsChanged.Count; i++) {
					var messageFlags = MessageFlags.Seen | MessageFlags.Draft;

					if (i < 3)
						messageFlags |= MessageFlags.Answered;

					Assert.That (flagsChanged[i].Index, Is.EqualTo (i), $"Unexpected value for flagsChanged[{i}].Index");
					Assert.That (flagsChanged[i].UniqueId.Value.Id, Is.EqualTo ((uint) (i + 1)), $"Unexpected value for flagsChanged[{i}].UniqueId");
					Assert.That (flagsChanged[i].Flags, Is.EqualTo (messageFlags), $"Unexpected value for flagsChanged[{i}].Flags");

					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected value for modSeqChanged[{i}].Index");
					if (i < 3)
						Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (4), $"Unexpected value for modSeqChanged[{i}].ModSeq");
					else
						Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (3), $"Unexpected value for modSeqChanged[{i}].ModSeq");
				}
				modSeqChanged.Clear ();
				flagsChanged.Clear ();

				Assert.That (vanished, Has.Count.EqualTo (1), "Unexpected number of MessagesVanished events");
				Assert.That (vanished[0].Earlier, Is.True, "Expected VANISHED EARLIER");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (1), "Unexpected number of messages vanished");
				Assert.That (vanished[0].UniqueIds[0].Id, Is.EqualTo (8), "Unexpected UID for vanished message");
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
				Assert.That (changed.UniqueIds, Has.Count.EqualTo (7), "Unexpected number of UIDs");
				Assert.That (changed.Relevancy, Has.Count.EqualTo (changed.Count), "Unexpected number of relevancy scores");
				Assert.That (changed.ModSeq.HasValue, Is.True, "Expected the ModSeq property to be set");
				Assert.That (changed.ModSeq.Value, Is.EqualTo (4), "Unexpected ModSeq value");
				Assert.That (changed.Min.Value.Id, Is.EqualTo (1), "Unexpected Min");
				Assert.That (changed.Max.Value.Id, Is.EqualTo (7), "Unexpected Max");
				Assert.That (changed.Count, Is.EqualTo (7), "Unexpected Count");

				var fetched = await folder.FetchAsync (changed.UniqueIds, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				Assert.That (fetched, Has.Count.EqualTo (7), "Unexpected number of messages fetched");
				for (int i = 0; i < fetched.Count; i++) {
					Assert.That (fetched[i].Index, Is.EqualTo (i), "Unexpected Index");
					Assert.That (fetched[i].UniqueId.Id, Is.EqualTo (i + 1), "Unexpected UniqueId");
				}

				// or... we could just use a single UID FETCH command like so:
				fetched = await folder.FetchAsync (UniqueIdRange.All, highestModSeq, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq);
				for (int i = 0; i < fetched.Count; i++) {
					Assert.That (fetched[i].Index, Is.EqualTo (i), "Unexpected Index");
					Assert.That (fetched[i].UniqueId.Id, Is.EqualTo (i + 1), "Unexpected UniqueId");
				}
				Assert.That (fetched, Has.Count.EqualTo (7), "Unexpected number of messages fetched");
				Assert.That (vanished, Has.Count.EqualTo (1), "Unexpected number of MessagesVanished events");
				Assert.That (vanished[0].Earlier, Is.True, "Expected VANISHED EARLIER");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (1), "Unexpected number of messages vanished");
				Assert.That (vanished[0].UniqueIds[0].Id, Is.EqualTo (8), "Unexpected UID for vanished message");
				vanished.Clear ();

				// Use SORT to order by reverse arrival order
				var orderBy = new OrderBy[] { new OrderBy (OrderByType.Arrival, SortOrder.Descending) };
				var sorted = await folder.SortAsync (searchOptions, SearchQuery.All, orderBy);
				Assert.That (sorted.UniqueIds, Has.Count.EqualTo (7), "Unexpected number of UIDs");
				for (int i = 0; i < sorted.UniqueIds.Count; i++)
					Assert.That (sorted.UniqueIds[i].Id, Is.EqualTo (7 - i), $"Unexpected value for UniqueId[{i}]");
				Assert.That (sorted.Relevancy, Has.Count.EqualTo (sorted.Count), "Unexpected number of relevancy scores");
				Assert.That (sorted.ModSeq.HasValue, Is.False, "Expected the ModSeq property to be null");
				Assert.That (sorted.Min.Value.Id, Is.EqualTo (7), "Unexpected Min");
				Assert.That (sorted.Max.Value.Id, Is.EqualTo (1), "Unexpected Max");
				Assert.That (sorted.Count, Is.EqualTo (7), "Unexpected Count");

				// Verify that optimizing NOT queries works correctly
				var uids = await folder.SearchAsync (SearchQuery.Not (SearchQuery.Deleted).And (SearchQuery.Not (SearchQuery.NotSeen)));
				Assert.That (uids, Has.Count.EqualTo (7), "Unexpected number of UIDs");
				for (int i = 0; i < uids.Count; i++)
					Assert.That (uids[i].Id, Is.EqualTo (i + 1), $"Unexpected value for uids[{i}]");

				// Create a Destination folder to use for copying/moving messages to
				var destination = (ImapFolder) await unitTests.CreateAsync ("Destination", true);
				Assert.That (destination.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "Unexpected UnitTests.Destination folder attributes");

				// COPY messages to the Destination folder
				var copied = await folder.CopyToAsync (uids, destination);
				Assert.That (copied.Source, Has.Count.EqualTo (uids.Count), "Unexpetced Source.Count");
				Assert.That (copied.Destination, Has.Count.EqualTo (uids.Count), "Unexpetced Destination.Count");

				// MOVE messages to the Destination folder
				var moved = await folder.MoveToAsync (uids, destination);
				Assert.That (copied.Source, Has.Count.EqualTo (uids.Count), "Unexpetced Source.Count");
				Assert.That (copied.Destination, Has.Count.EqualTo (uids.Count), "Unexpetced Destination.Count");
				Assert.That (vanished, Has.Count.EqualTo (1), "Expected VANISHED event");
				vanished.Clear ();

				await destination.StatusAsync (statusItems);
				Assert.That (destination.UidValidity, Is.EqualTo (moved.Destination[0].Validity), "Unexpected UIDVALIDITY");

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
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "Expected UnitTests.Destination to be opened in READ-WRITE mode");

				var fetchHeaders = new HashSet<HeaderId> {
					HeaderId.References,
					HeaderId.XMailer
				};

				var indexes = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };

				// Fetch + modseq
				fetched = await destination.FetchAsync (UniqueIdRange.All, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, 1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				// Fetch
				fetched = await destination.FetchAsync (UniqueIdRange.All, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References, fetchHeaders);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				fetched = await destination.FetchAsync (indexes, MessageSummaryItems.Full | MessageSummaryItems.UniqueId | 
				                                        MessageSummaryItems.BodyStructure | MessageSummaryItems.ModSeq | 
				                                        MessageSummaryItems.References);
				Assert.That (fetched, Has.Count.EqualTo (14), "Unexpected number of messages fetched");

				uids = new UniqueIdSet (SortOrder.Ascending);

				for (int i = 0; i < fetched.Count; i++) {
					Assert.That (fetched[i].Index, Is.EqualTo (i), "Unexpected Index");
					Assert.That (fetched[i].UniqueId.Id, Is.EqualTo (i + 1), "Unexpected UniqueId");

					uids.Add (fetched[i].UniqueId);
				}

				using (var entity = await destination.GetBodyPartAsync (fetched[0].UniqueId, fetched[0].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				using (var entity = await destination.GetBodyPartAsync (fetched[0].Index, fetched[0].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				using (var entity = await destination.GetBodyPartAsync (fetched[1].UniqueId, fetched[1].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				using (var entity = await destination.GetBodyPartAsync (fetched[1].Index, fetched[1].TextBody))
					Assert.That (entity, Is.InstanceOf<TextPart> ());

				var headers = await destination.GetHeadersAsync (fetched[0].UniqueId);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[0].Index);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(int) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[0].UniqueId, fetched[0].TextBody);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId, BodyPart) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[0].Index, fetched[0].TextBody);
				Assert.That (headers[HeaderId.From], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(int, BodyPart) failed to match From header");
				Assert.That (headers[HeaderId.Date], Is.EqualTo ("Sun, 02 Oct 2016 17:56:45 -0400"), "GetHeaders(UniqueId) failed to match Date header");
				Assert.That (headers[HeaderId.Subject], Is.EqualTo ("A"), "GetHeaders(UniqueId) failed to match Subject header");
				Assert.That (headers[HeaderId.MessageId], Is.EqualTo ("<a@mimekit.net>"), "GetHeaders(UniqueId) failed to match Message-Id header");
				Assert.That (headers[HeaderId.To], Is.EqualTo ("Unit Tests <unit-tests@mimekit.net>"), "GetHeaders(UniqueId) failed to match To header");
				Assert.That (headers[HeaderId.MimeVersion], Is.EqualTo ("1.0"), "GetHeaders(UniqueId) failed to match MIME-Version header");
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[1].UniqueId, fetched[1].TextBody);
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				headers = await destination.GetHeadersAsync (fetched[1].Index, fetched[1].TextBody);
				Assert.That (headers[HeaderId.ContentType], Is.EqualTo ("text/plain; charset=utf-8"), "GetHeaders(UniqueId) failed to match Content-Type header");

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, "", 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, "", 128, 64)) {
					Assert.That (stream.Length, Is.EqualTo (64), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("nit Tests <unit-tests@mimekit.net>\r\nMIME-Version: 1.0\r\nContent-T"));
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].UniqueId, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.That (stream.Length, Is.EqualTo (62), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n"));
				}

				using (var stream = await destination.GetStreamAsync (fetched[0].Index, "HEADER.FIELDS (MIME-VERSION CONTENT-TYPE)")) {
					Assert.That (stream.Length, Is.EqualTo (62), "Unexpected stream length");

					string text;
					using (var reader = new StreamReader (stream))
						text = reader.ReadToEnd ();

					Assert.That (text, Is.EqualTo ("MIME-Version: 1.0\r\nContent-Type: text/plain; charset=utf-8\r\n\r\n"));
				}

				var custom = new HashSet<string> {
					"$MailKit"
				};

				var unchanged1 = await destination.AddFlagsAsync (uids, destination.HighestModSeq, MessageFlags.Deleted, custom, true);
				Assert.That (modSeqChanged, Has.Count.EqualTo (14), "Unexpected number of ModSeqChanged events");
				Assert.That (destination.HighestModSeq, Is.EqualTo (5));
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected value for modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (5), $"Unexpected value for modSeqChanged[{i}].ModSeq");
				}
				Assert.That (unchanged1, Has.Count.EqualTo (2), "[MODIFIED uid-set]");
				Assert.That (unchanged1[0].Id, Is.EqualTo (7), "unchanged uids[0]");
				Assert.That (unchanged1[1].Id, Is.EqualTo (9), "unchanged uids[1]");
				modSeqChanged.Clear ();

				var unchanged2 = await destination.SetFlagsAsync (new int[] { 0, 1, 2, 3, 4, 5, 6 }, destination.HighestModSeq, MessageFlags.Seen | MessageFlags.Deleted, custom, true);
				Assert.That (modSeqChanged, Has.Count.EqualTo (7), "Unexpected number of ModSeqChanged events");
				Assert.That (destination.HighestModSeq, Is.EqualTo (6));
				for (int i = 0; i < modSeqChanged.Count; i++) {
					Assert.That (modSeqChanged[i].Index, Is.EqualTo (i), $"Unexpected value for modSeqChanged[{i}].Index");
					Assert.That (modSeqChanged[i].ModSeq, Is.EqualTo (6), $"Unexpected value for modSeqChanged[{i}].ModSeq");
				}
				Assert.That (unchanged2, Has.Count.EqualTo (2), "[MODIFIED seq-set]");
				Assert.That (unchanged2[0], Is.EqualTo (6), "unchanged indexes[0]");
				Assert.That (unchanged2[1], Is.EqualTo (8), "unchanged indexes[1]");
				modSeqChanged.Clear ();

				var results = await destination.SearchAsync (uids, SearchQuery.New.Or (SearchQuery.Old.Or (SearchQuery.Answered.Or (SearchQuery.Deleted.Or (SearchQuery.Draft.Or (SearchQuery.Flagged.Or (SearchQuery.Recent.Or (SearchQuery.NotAnswered.Or (SearchQuery.NotDeleted.Or (SearchQuery.NotDraft.Or (SearchQuery.NotFlagged.Or (SearchQuery.NotSeen.Or (SearchQuery.HasKeyword ("$MailKit").Or (SearchQuery.NotKeyword ("$MailKit")))))))))))))));
				Assert.That (results, Has.Count.EqualTo (14), "Unexpected number of UIDs");

				var matches = await destination.SearchAsync (searchOptions, uids, SearchQuery.LargerThan (256).And (SearchQuery.SmallerThan (512)));
				var expectedMatchedUids = new uint[] { 2, 3, 4, 5, 6, 9, 10, 11, 12, 13 };
				Assert.That (matches.Count, Is.EqualTo (10), "Unexpected COUNT");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (13), "Unexpected MAX");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (2), "Unexpected MIN");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (10), "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (expectedMatchedUids[i]));
				Assert.That (matches.Relevancy, Has.Count.EqualTo (matches.Count), "Unexpected number of relevancy scores");

				orderBy = new OrderBy[] { OrderBy.ReverseDate, OrderBy.Subject, OrderBy.DisplayFrom, OrderBy.Size };
				var sentDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.SentBefore (new DateTime (2016, 10, 12)), SearchQuery.SentSince (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.SentOn (new DateTime (2016, 10, 11))));
				var deliveredDateQuery = SearchQuery.Or (SearchQuery.And (SearchQuery.DeliveredBefore (new DateTime (2016, 10, 12)), SearchQuery.DeliveredAfter (new DateTime (2016, 10, 10))), SearchQuery.Not (SearchQuery.DeliveredOn (new DateTime (2016, 10, 11))));
				results = await destination.SortAsync (sentDateQuery.Or (deliveredDateQuery), orderBy);
				var expectedSortByDateResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				Assert.That (results, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.That (results[i].Id, Is.EqualTo (expectedSortByDateResults[i]));

				var stringQuery = SearchQuery.BccContains ("xyz").Or (SearchQuery.CcContains ("xyz").Or (SearchQuery.FromContains ("xyz").Or (SearchQuery.ToContains ("xyz").Or (SearchQuery.SubjectContains ("xyz").Or (SearchQuery.HeaderContains ("Message-Id", "mimekit.net").Or (SearchQuery.BodyContains ("This is the message body.").Or (SearchQuery.MessageContains ("message"))))))));
				orderBy = new OrderBy[] { OrderBy.From, OrderBy.To, OrderBy.Cc };
				results = await destination.SortAsync (uids, stringQuery, orderBy);
				Assert.That (results, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < results.Count; i++)
					Assert.That (results[i].Id, Is.EqualTo (i + 1));

				orderBy = new OrderBy[] { OrderBy.DisplayTo };
				matches = await destination.SortAsync (searchOptions, uids, SearchQuery.OlderThan (1).And (SearchQuery.YoungerThan (3600)), orderBy);
				Assert.That (matches.Count, Is.EqualTo (14), "Unexpected COUNT");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14), "Unexpected number of UIDs");
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (i + 1));
				Assert.That (matches.Relevancy, Has.Count.EqualTo (matches.Count), "Unexpected number of relevancy scores");

				client.Capabilities &= ~ImapCapabilities.ESearch;
				matches = await ((ImapFolder) destination).SearchAsync ("ALL");
				Assert.That (matches.Max.HasValue, Is.True, "MAX should always be set");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX value");
				Assert.That (matches.Min.HasValue, Is.True, "MIN should always be set");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN value");
				Assert.That (matches.Count, Is.EqualTo (14), "COUNT should always be set");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14));
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (i + 1));

				client.Capabilities &= ~ImapCapabilities.ESort;
				matches = await ((ImapFolder) destination).SortAsync ("(REVERSE ARRIVAL) US-ASCII ALL");
				Assert.That (matches.Max.HasValue, Is.True, "MAX should always be set");
				Assert.That (matches.Max.Value.Id, Is.EqualTo (14), "Unexpected MAX value");
				Assert.That (matches.Min.HasValue, Is.True, "MIN should always be set");
				Assert.That (matches.Min.Value.Id, Is.EqualTo (1), "Unexpected MIN value");
				Assert.That (matches.Count, Is.EqualTo (14), "COUNT should always be set");
				Assert.That (matches.UniqueIds, Has.Count.EqualTo (14));
				var expectedSortByReverseArrivalResults = new uint[] { 7, 14, 6, 13, 5, 12, 4, 11, 3, 10, 2, 9, 1, 8 };
				for (int i = 0; i < matches.UniqueIds.Count; i++)
					Assert.That (matches.UniqueIds[i].Id, Is.EqualTo (expectedSortByReverseArrivalResults[i]));

				await destination.GetStreamsAsync (UniqueIdRange.All, GetStreamsAsyncCallback);
				await destination.GetStreamsAsync (new int[] { 0, 1, 2 }, GetStreamsAsyncCallback);
				await destination.GetStreamsAsync (0, -1, GetStreamsAsyncCallback);

				await destination.ExpungeAsync ();
				Assert.That (destination.HighestModSeq, Is.EqualTo (7));
				Assert.That (vanished, Has.Count.EqualTo (1), "Unexpected number of Vanished events");
				Assert.That (vanished[0].UniqueIds, Has.Count.EqualTo (14), "Unexpected number of UIDs in Vanished event");
				for (int i = 0; i < vanished[0].UniqueIds.Count; i++)
					Assert.That (vanished[0].UniqueIds[i].Id, Is.EqualTo (i + 1));
				Assert.That (vanished[0].Earlier, Is.False, "Unexpected value for Earlier");
				vanished.Clear ();

				await destination.CloseAsync (true);

				int alerts = 0;
				client.Alert += (sender, e) => {
					Assert.That (e.Message, Is.EqualTo ("System shutdown in 10 minutes"));
					alerts++;
				};
				await client.NoOpAsync ();
				Assert.That (alerts, Is.EqualTo (1), "Alert event failed to fire.");

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateGMailCommands ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" \"%\"\r\n", "gmail.list-personal.txt"),
				new ImapReplayCommand ("A00000006 CREATE UnitTests\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000007 LIST \"\" UnitTests\r\n", "gmail.list-unittests.txt"),
				new ImapReplayCommand ("A00000008 SELECT UnitTests (CONDSTORE)\r\n", "gmail.select-unittests.txt")
			};

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

					using (var reader = new StreamReader (stream, TextEncodings.Latin1))
						latin1 = reader.ReadToEnd ();
				}

				message.Dispose ();

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
			commands.Add (new ImapReplayCommand ("A00000059 UID SEARCH RETURN (ALL) OR X-GM-MSGID 1 OR X-GM-THRID 5 OR X-GM-LABELS \"Custom Label\" X-GM-RAW \"has:attachment in:unread\"\r\n", "gmail.search.txt"));
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
			commands.Add (new ImapReplayCommand ("A00000085 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.set-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000086 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) -X-GM-LABELS.SILENT (\\Important \"Custom Label\" NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000087 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) +X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.add-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000088 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.set-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000089 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -X-GM-LABELS.SILENT (\\Important \"Custom Label\" NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000090 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.add-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000091 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.set-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000092 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) -X-GM-LABELS.SILENT (\\Important \"Custom Label\" NIL)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000093 STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 (UNCHANGEDSINCE 5) +X-GM-LABELS (\\Important \"Custom Label\" NIL)\r\n", "gmail.add-labels.txt"));
			commands.Add (new ImapReplayCommand ("A00000094 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 FLAGS (\\Answered \\Seen)\r\n", "gmail.set-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000095 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 -FLAGS.SILENT (\\Answered)\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000096 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", "gmail.add-flags.txt"));
			commands.Add (new ImapReplayCommand ("A00000097 CHECK\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000098 UNSELECT\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000099 SUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000100 LSUB \"\" \"%\"\r\n", "gmail.lsub-personal.txt"));
			commands.Add (new ImapReplayCommand ("A00000101 UNSUBSCRIBE UnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000102 CREATE UnitTests/Dummy\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000103 LIST \"\" UnitTests/Dummy\r\n", "gmail.list-unittests-dummy.txt"));
			commands.Add (new ImapReplayCommand ("A00000104 RENAME UnitTests RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000105 DELETE RenamedUnitTests\r\n", ImapReplayCommandResponse.OK));
			commands.Add (new ImapReplayCommand ("A00000106 LOGOUT\r\n", "gmail.logout.txt"));

			return commands;
		}

		[Test]
		public void TestGMail ()
		{
			var commands = CreateGMailCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.AppendLimit.HasValue, Is.True, "Expected AppendLimit to have a value");
				Assert.That (client.AppendLimit.Value, Is.EqualTo (35651584), "Expected AppendLimit value to match");

				Assert.Throws<NotSupportedException> (() => client.EnableQuickResync ());
				Assert.Throws<NotSupportedException> (() => client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.FlagChange, new ImapEvent.MessageNew (), ImapEvent.MessageExpunge)
				}));
				Assert.Throws<NotSupportedException> (() => client.DisableNotify ());

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = personal.Create ("UnitTests", true);
				Assert.That (created, Is.Not.Null, "Expected a non-null created folder.");
				Assert.That (created.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren));

				Assert.That (created.ParentFolder, Is.Not.Null, "The ParentFolder property should not be null.");

				const MessageFlags ExpectedPermanentFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.UserDefined;
				const MessageFlags ExpectedAcceptedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen;
				var access = created.Open (FolderAccess.ReadWrite);
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "The UnitTests folder was not opened with the expected access mode.");
				Assert.That (created.PermanentFlags, Is.EqualTo (ExpectedPermanentFlags), "The PermanentFlags do not match the expected value.");
				Assert.That (created.AcceptedFlags, Is.EqualTo (ExpectedAcceptedFlags), "The AcceptedFlags do not match the expected value.");

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						using (var message = MimeMessage.Load (stream)) {
							var uid = created.Append (message, MessageFlags.Seen);
							Assert.That (uid.HasValue, Is.True, "Expected a UID to be returned from folder.Append().");
							Assert.That (uid.Value.Id, Is.EqualTo ((uint) (i + 1)), "The UID returned from the APPEND command does not match the expected UID.");
						}
					}
				}

				var query = SearchQuery.GMailMessageId (1).Or (SearchQuery.GMailThreadId (5).Or (SearchQuery.HasGMailLabel ("Custom Label").Or (SearchQuery.GMailRawSearch ("has:attachment in:unread"))));
				var matches = created.Search (query);
				Assert.That (matches, Has.Count.EqualTo (21));

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId | MessageSummaryItems.GMailLabels | MessageSummaryItems.GMailMessageId | MessageSummaryItems.GMailThreadId;
				var summaries = created.Fetch (matches, items);
				var indexes = new List<int> ();

				foreach (var summary in summaries) {
					Assert.That (summary.GMailMessageId.Value, Is.EqualTo (1592225494819146100 + summary.UniqueId.Id), "GMailMessageId");
					Assert.That (summary.GMailThreadId.Value, Is.EqualTo (1592225494819146100 + summary.UniqueId.Id), "GMailThreadId");
					Assert.That (summary.GMailLabels, Has.Count.EqualTo (2), "GMailLabels.Count");
					Assert.That (summary.GMailLabels[0], Is.EqualTo ("Test Messages"));
					Assert.That (summary.GMailLabels[1], Is.EqualTo ("\\Important"));
					Assert.That (summary.UniqueId.IsValid, Is.True, "UniqueId.IsValid");

					created.GetMessage (summary.UniqueId);
					indexes.Add (summary.Index);
				}

				var labels = new [] { "\\Important", "Custom Label", null };
				created.SetLabels (matches, labels, false);
				created.RemoveLabels (matches, labels, true);
				created.AddLabels (matches, labels, false);

				created.SetLabels (matches, 5, labels, false);
				created.RemoveLabels (matches, 5, labels, true);
				created.AddLabels (matches, 5, labels, false);

				created.SetLabels (indexes, labels, false);
				created.RemoveLabels (indexes, labels, true);
				created.AddLabels (indexes, labels, false);

				created.SetLabels (indexes, 5, labels, false);
				created.RemoveLabels (indexes, 5, labels, true);
				created.AddLabels (indexes, 5, labels, false);

				// Verify that Adding and/or removing an empty set of labels is a no-op
				labels = Array.Empty<string> ();

				created.RemoveLabels (matches, labels, true);
				created.AddLabels (matches, labels, false);

				created.RemoveLabels (matches, 5, labels, true);
				created.AddLabels (matches, 5, labels, false);

				created.RemoveLabels (indexes, labels, true);
				created.AddLabels (indexes, labels, false);

				created.RemoveLabels (indexes, 5, labels, true);
				created.AddLabels (indexes, 5, labels, false);

				created.SetFlags (matches, MessageFlags.Seen | MessageFlags.Answered, false);
				created.RemoveFlags (matches, MessageFlags.Answered, true);
				created.AddFlags (matches, MessageFlags.Deleted, true);

				// Verify that Adding and/or removing an empty set of flags is a no-op
				created.RemoveFlags (matches, MessageFlags.None, true);
				created.AddFlags (matches, MessageFlags.None, true);

				created.RemoveFlags (matches, 5, MessageFlags.None, true);
				created.AddFlags (matches, 5, MessageFlags.None, true);

				created.RemoveFlags (indexes, MessageFlags.None, true);
				created.AddFlags (indexes, MessageFlags.None, true);

				created.RemoveFlags (indexes, 5, MessageFlags.None, true);
				created.AddFlags (indexes, 5, MessageFlags.None, true);

				created.Check ();

				created.Close ();
				Assert.That (created.IsOpen, Is.False, "Expected the UnitTests folder to be closed.");

				created.Subscribe ();
				Assert.That (created.IsSubscribed, Is.True, "Expected IsSubscribed to be true after subscribing to the folder.");

				var subscribed = personal.GetSubfolders (true);
				Assert.That (subscribed.Contains (created), Is.True, "Expected the list of subscribed folders to contain the UnitTests folder.");

				created.Unsubscribe ();
				Assert.That (created.IsSubscribed, Is.False, "Expected IsSubscribed to be false after unsubscribing from the folder.");

				var dummy = created.Create ("Dummy", true);
				bool dummyRenamed = false;
				bool renamed = false;
				bool deleted = false;

				dummy.Renamed += (sender, e) => { dummyRenamed = true; };
				created.Renamed += (sender, e) => { renamed = true; };

				created.Rename (created.ParentFolder, "RenamedUnitTests");
				Assert.That (created.Name, Is.EqualTo ("RenamedUnitTests"));
				Assert.That (created.FullName, Is.EqualTo ("RenamedUnitTests"));
				Assert.That (renamed, Is.True, "Expected the Rename event to be emitted for the UnitTests folder.");

				Assert.That (dummy.FullName, Is.EqualTo ("RenamedUnitTests/Dummy"));
				Assert.That (dummyRenamed, Is.True, "Expected the Rename event to be emitted for the UnitTests/Dummy folder.");

				created.Deleted += (sender, e) => { deleted = true; };

				created.Delete ();
				Assert.That (deleted, Is.True, "Expected the Deleted event to be emitted for the UnitTests folder.");
				Assert.That (created.Exists, Is.False, "Expected Exists to be false after deleting the folder.");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestGMailAsync ()
		{
			var commands = CreateGMailCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));
				Assert.That (client.AppendLimit.HasValue, Is.True, "Expected AppendLimit to have a value");
				Assert.That (client.AppendLimit.Value, Is.EqualTo (35651584), "Expected AppendLimit value to match");

				Assert.ThrowsAsync<NotSupportedException> (async () => await client.EnableQuickResyncAsync ());
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.FlagChange, new ImapEvent.MessageNew (), ImapEvent.MessageExpunge)
				}));
				Assert.ThrowsAsync<NotSupportedException> (async () => await client.DisableNotifyAsync ());

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// disable LIST-EXTENDED
				client.Capabilities &= ~ImapCapabilities.ListExtended;

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				Assert.That (folders[0], Is.EqualTo (client.Inbox), "Expected the first folder to be the Inbox.");
				Assert.That (folders[1].FullName, Is.EqualTo ("[Gmail]"), "Expected the second folder to be [Gmail].");
				Assert.That (folders[1].Attributes, Is.EqualTo (FolderAttributes.NoSelect | FolderAttributes.HasChildren), "Expected [Gmail] folder to be \\Noselect \\HasChildren.");

				var created = await personal.CreateAsync ("UnitTests", true);
				Assert.That (created, Is.Not.Null, "Expected a non-null created folder.");
				Assert.That (created.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren));

				Assert.That (created.ParentFolder, Is.Not.Null, "The ParentFolder property should not be null.");

				const MessageFlags ExpectedPermanentFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen | MessageFlags.UserDefined;
				const MessageFlags ExpectedAcceptedFlags = MessageFlags.Answered | MessageFlags.Flagged | MessageFlags.Draft | MessageFlags.Deleted | MessageFlags.Seen;
				var access = await created.OpenAsync (FolderAccess.ReadWrite);
				Assert.That (access, Is.EqualTo (FolderAccess.ReadWrite), "The UnitTests folder was not opened with the expected access mode.");
				Assert.That (created.PermanentFlags, Is.EqualTo (ExpectedPermanentFlags), "The PermanentFlags do not match the expected value.");
				Assert.That (created.AcceptedFlags, Is.EqualTo (ExpectedAcceptedFlags), "The AcceptedFlags do not match the expected value.");

				for (int i = 0; i < 50; i++) {
					using (var stream = GetResourceStream (string.Format ("common.message.{0}.msg", i))) {
						using (var message = MimeMessage.Load (stream)) {
							var uid = await created.AppendAsync (message, MessageFlags.Seen);
							Assert.That (uid.HasValue, Is.True, "Expected a UID to be returned from folder.Append().");
							Assert.That (uid.Value.Id, Is.EqualTo ((uint) (i + 1)), "The UID returned from the APPEND command does not match the expected UID.");
						}
					}
				}

				var query = SearchQuery.GMailMessageId (1).Or (SearchQuery.GMailThreadId (5).Or (SearchQuery.HasGMailLabel ("Custom Label").Or (SearchQuery.GMailRawSearch ("has:attachment in:unread"))));
				var matches = await created.SearchAsync (query);
				Assert.That (matches, Has.Count.EqualTo (21));

				const MessageSummaryItems items = MessageSummaryItems.Full | MessageSummaryItems.UniqueId | MessageSummaryItems.GMailLabels | MessageSummaryItems.GMailMessageId | MessageSummaryItems.GMailThreadId;
				var summaries = await created.FetchAsync (matches, items);
				var indexes = new List<int> ();

				foreach (var summary in summaries) {
					Assert.That (summary.GMailMessageId.Value, Is.EqualTo (1592225494819146100 + summary.UniqueId.Id), "GMailMessageId");
					Assert.That (summary.GMailThreadId.Value, Is.EqualTo (1592225494819146100 + summary.UniqueId.Id), "GMailThreadId");
					Assert.That (summary.GMailLabels, Has.Count.EqualTo (2), "GMailLabels.Count");
					Assert.That (summary.GMailLabels[0], Is.EqualTo ("Test Messages"));
					Assert.That (summary.GMailLabels[1], Is.EqualTo ("\\Important"));
					Assert.That (summary.UniqueId.IsValid, Is.True, "UniqueId.IsValid");

					await created.GetMessageAsync (summary.UniqueId);
					indexes.Add (summary.Index);
				}

				var labels = new [] { "\\Important", "Custom Label", null };
				await created.SetLabelsAsync (matches, labels, false);
				await created.RemoveLabelsAsync (matches, labels, true);
				await created.AddLabelsAsync (matches, labels, false);

				await created.SetLabelsAsync (matches, 5, labels, false);
				await created.RemoveLabelsAsync (matches, 5, labels, true);
				await created.AddLabelsAsync (matches, 5, labels, false);

				await created.SetLabelsAsync (indexes, labels, false);
				await created.RemoveLabelsAsync (indexes, labels, true);
				await created.AddLabelsAsync (indexes, labels, false);

				await created.SetLabelsAsync (indexes, 5, labels, false);
				await created.RemoveLabelsAsync (indexes, 5, labels, true);
				await created.AddLabelsAsync (indexes, 5, labels, false);

				// Verify that Adding and/or removing an empty set of labels is a no-op
				labels = Array.Empty<string> ();

				await created.RemoveLabelsAsync (matches, labels, true);
				await created.AddLabelsAsync (matches, labels, false);

				await created.RemoveLabelsAsync (matches, 5, labels, true);
				await created.AddLabelsAsync (matches, 5, labels, false);

				await created.RemoveLabelsAsync (indexes, labels, true);
				await created.AddLabelsAsync (indexes, labels, false);

				await created.RemoveLabelsAsync (indexes, 5, labels, true);
				await created.AddLabelsAsync (indexes, 5, labels, false);

				await created.SetFlagsAsync (matches, MessageFlags.Seen | MessageFlags.Answered, false);
				await created.RemoveFlagsAsync (matches, MessageFlags.Answered, true);
				await created.AddFlagsAsync (matches, MessageFlags.Deleted, true);

				// Verify that Adding and/or removing an empty set of flags is a no-op
				await created.RemoveFlagsAsync (matches, MessageFlags.None, true);
				await created.AddFlagsAsync (matches, MessageFlags.None, true);

				await created.RemoveFlagsAsync (matches, 5, MessageFlags.None, true);
				await created.AddFlagsAsync (matches, 5, MessageFlags.None, true);

				await created.RemoveFlagsAsync (indexes, MessageFlags.None, true);
				await created.AddFlagsAsync (indexes, MessageFlags.None, true);

				await created.RemoveFlagsAsync (indexes, 5, MessageFlags.None, true);
				await created.AddFlagsAsync (indexes, 5, MessageFlags.None, true);

				await created.CheckAsync ();

				await created.CloseAsync ();
				Assert.That (created.IsOpen, Is.False, "Expected the UnitTests folder to be closed.");

				await created.SubscribeAsync ();
				Assert.That (created.IsSubscribed, Is.True, "Expected IsSubscribed to be true after subscribing to the folder.");

				var subscribed = await personal.GetSubfoldersAsync (true);
				Assert.That (subscribed.Contains (created), Is.True, "Expected the list of subscribed folders to contain the UnitTests folder.");

				await created.UnsubscribeAsync ();
				Assert.That (created.IsSubscribed, Is.False, "Expected IsSubscribed to be false after unsubscribing from the folder.");

				var dummy = await created.CreateAsync ("Dummy", true);
				bool dummyRenamed = false;
				bool renamed = false;
				bool deleted = false;

				dummy.Renamed += (sender, e) => { dummyRenamed = true; };
				created.Renamed += (sender, e) => { renamed = true; };

				await created.RenameAsync (created.ParentFolder, "RenamedUnitTests");
				Assert.That (created.Name, Is.EqualTo ("RenamedUnitTests"));
				Assert.That (created.FullName, Is.EqualTo ("RenamedUnitTests"));
				Assert.That (renamed, Is.True, "Expected the Rename event to be emitted for the UnitTests folder.");

				Assert.That (dummy.FullName, Is.EqualTo ("RenamedUnitTests/Dummy"));
				Assert.That (dummyRenamed, Is.True, "Expected the Rename event to be emitted for the UnitTests/Dummy folder.");

				created.Deleted += (sender, e) => { deleted = true; };

				await created.DeleteAsync ();
				Assert.That (deleted, Is.True, "Expected the Deleted event to be emitted for the UnitTests folder.");
				Assert.That (created.Exists, Is.False, "Expected Exists to be false after deleting the folder.");

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateGetFolderCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LIST \"\" Level1/Level2/Level3 RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-level3.txt"),
				new ImapReplayCommand ("A00000006 LIST \"\" Level1/Level2 RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-level2.txt"),
				new ImapReplayCommand ("A00000007 LIST \"\" Level1 RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-level1.txt"),
				new ImapReplayCommand ("A00000008 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestGetFolder ()
		{
			var commands = CreateGetFolderCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var level3 = client.GetFolder ("Level1/Level2/Level3");
				Assert.That (level3.FullName, Is.EqualTo ("Level1/Level2/Level3"));
				Assert.That (level3.Name, Is.EqualTo ("Level3"));
				Assert.That (level3.DirectorySeparator, Is.EqualTo ('/'));
				Assert.That (level3.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren));

				var level2 = level3.ParentFolder;
				Assert.That (level2.FullName, Is.EqualTo ("Level1/Level2"));
				Assert.That (level2.Name, Is.EqualTo ("Level2"));
				Assert.That (level2.DirectorySeparator, Is.EqualTo ('/'));
				Assert.That (level2.Attributes, Is.EqualTo (FolderAttributes.HasChildren));

				var level1 = level2.ParentFolder;
				Assert.That (level1.FullName, Is.EqualTo ("Level1"));
				Assert.That (level1.Name, Is.EqualTo ("Level1"));
				Assert.That (level1.DirectorySeparator, Is.EqualTo ('/'));
				Assert.That (level1.Attributes, Is.EqualTo (FolderAttributes.HasChildren));

				var personal = level1.ParentFolder;
				Assert.That (personal.FullName, Is.EqualTo (string.Empty));
				Assert.That (personal.Name, Is.EqualTo (string.Empty));
				Assert.That (personal.IsNamespace, Is.True, "IsNamespace");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestGetFolderAsync ()
		{
			var commands = CreateGetFolderCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var level3 = await client.GetFolderAsync ("Level1/Level2/Level3");
				Assert.That (level3.FullName, Is.EqualTo ("Level1/Level2/Level3"));
				Assert.That (level3.Name, Is.EqualTo ("Level3"));
				Assert.That (level3.DirectorySeparator, Is.EqualTo ('/'));
				Assert.That (level3.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren));

				var level2 = level3.ParentFolder;
				Assert.That (level2.FullName, Is.EqualTo ("Level1/Level2"));
				Assert.That (level2.Name, Is.EqualTo ("Level2"));
				Assert.That (level2.DirectorySeparator, Is.EqualTo ('/'));
				Assert.That (level2.Attributes, Is.EqualTo (FolderAttributes.HasChildren));

				var level1 = level2.ParentFolder;
				Assert.That (level1.FullName, Is.EqualTo ("Level1"));
				Assert.That (level1.Name, Is.EqualTo ("Level1"));
				Assert.That (level1.DirectorySeparator, Is.EqualTo ('/'));
				Assert.That (level1.Attributes, Is.EqualTo (FolderAttributes.HasChildren));

				var personal = level1.ParentFolder;
				Assert.That (personal.FullName, Is.EqualTo (string.Empty));
				Assert.That (personal.Name, Is.EqualTo (string.Empty));
				Assert.That (personal.IsNamespace, Is.True, "IsNamespace");

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateIdentifyCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 ID NIL\r\n", "common.id.txt"),
				new ImapReplayCommand ("A00000006 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" {35+}\r\n1 Memorial Dr.\r\nCambridge, MA 02142)\r\n", "common.id.txt"),
				new ImapReplayCommand ("A00000007 ID (\"name\" \"MailKit\" \"version\" \"1.0\" \"vendor\" \"Xamarin Inc.\" \"address\" NIL)\r\n", "common.id.txt"),
			};
		}

		[Test]
		public void TestIdentify ()
		{
			var commands = CreateIdentifyCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				ImapImplementation implementation;

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					client.Authenticate (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));

				implementation = client.Identify (null);
				Assert.That (implementation, Is.Not.Null, "Expected a non-null ID response.");
				Assert.That (implementation.Name, Is.EqualTo ("GImap"));
				Assert.That (implementation.Vendor, Is.EqualTo ("Google, Inc."));
				Assert.That (implementation.SupportUrl, Is.EqualTo ("http://support.google.com/mail"));
				Assert.That (implementation.Version, Is.EqualTo ("gmail_imap_150623.03_p1"));
				Assert.That (implementation.Properties["remote-host"], Is.EqualTo ("127.0.0.1"));

				implementation = new ImapImplementation {
					Name = "MailKit",
					Version = "1.0",
					Vendor = "Xamarin Inc.",
					Address = "1 Memorial Dr.\r\nCambridge, MA 02142"
				};

				implementation = client.Identify (implementation);
				Assert.That (implementation, Is.Not.Null, "Expected a non-null ID response.");
				Assert.That (implementation.Name, Is.EqualTo ("GImap"));
				Assert.That (implementation.Vendor, Is.EqualTo ("Google, Inc."));
				Assert.That (implementation.SupportUrl, Is.EqualTo ("http://support.google.com/mail"));
				Assert.That (implementation.Version, Is.EqualTo ("gmail_imap_150623.03_p1"));
				Assert.That (implementation.Properties["remote-host"], Is.EqualTo ("127.0.0.1"));

				implementation = new ImapImplementation {
					Name = "MailKit",
					Version = "1.0",
					Vendor = "Xamarin Inc.",
					Address = null
				};

				implementation = client.Identify (implementation);
				Assert.That (implementation, Is.Not.Null, "Expected a non-null ID response.");
				Assert.That (implementation.Name, Is.EqualTo ("GImap"));
				Assert.That (implementation.Vendor, Is.EqualTo ("Google, Inc."));
				Assert.That (implementation.SupportUrl, Is.EqualTo ("http://support.google.com/mail"));
				Assert.That (implementation.Version, Is.EqualTo ("gmail_imap_150623.03_p1"));
				Assert.That (implementation.Properties["remote-host"], Is.EqualTo ("127.0.0.1"));

				// disable ID support
				client.Capabilities &= ~ImapCapabilities.Id;
				Assert.Throws<NotSupportedException> (() => client.Identify (null));

				client.Disconnect (false);
			}
		}

		[Test]
		public async Task TestIdentifyAsync ()
		{
			var commands = CreateIdentifyCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				ImapImplementation implementation;

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");
				Assert.That (client.IsSecure, Is.False, "IsSecure should be false.");

				Assert.That (client.Capabilities, Is.EqualTo (GMailInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (5));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("OAUTHBEARER"), "Expected SASL OAUTHBEARER auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				try {
					await client.AuthenticateAsync (new NetworkCredential ("username", "password"));
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (GMailAuthenticatedCapabilities));

				implementation = await client.IdentifyAsync (null);
				Assert.That (implementation, Is.Not.Null, "Expected a non-null ID response.");
				Assert.That (implementation.Name, Is.EqualTo ("GImap"));
				Assert.That (implementation.Vendor, Is.EqualTo ("Google, Inc."));
				Assert.That (implementation.SupportUrl, Is.EqualTo ("http://support.google.com/mail"));
				Assert.That (implementation.Version, Is.EqualTo ("gmail_imap_150623.03_p1"));
				Assert.That (implementation.Properties["remote-host"], Is.EqualTo ("127.0.0.1"));

				implementation = new ImapImplementation {
					Name = "MailKit",
					Version = "1.0",
					Vendor = "Xamarin Inc.",
					Address = "1 Memorial Dr.\r\nCambridge, MA 02142"
				};

				implementation = await client.IdentifyAsync (implementation);
				Assert.That (implementation, Is.Not.Null, "Expected a non-null ID response.");
				Assert.That (implementation.Name, Is.EqualTo ("GImap"));
				Assert.That (implementation.Vendor, Is.EqualTo ("Google, Inc."));
				Assert.That (implementation.SupportUrl, Is.EqualTo ("http://support.google.com/mail"));
				Assert.That (implementation.Version, Is.EqualTo ("gmail_imap_150623.03_p1"));
				Assert.That (implementation.Properties["remote-host"], Is.EqualTo ("127.0.0.1"));

				implementation = new ImapImplementation {
					Name = "MailKit",
					Version = "1.0",
					Vendor = "Xamarin Inc.",
					Address = null
				};

				implementation = await client.IdentifyAsync (implementation);
				Assert.That (implementation, Is.Not.Null, "Expected a non-null ID response.");
				Assert.That (implementation.Name, Is.EqualTo ("GImap"));
				Assert.That (implementation.Vendor, Is.EqualTo ("Google, Inc."));
				Assert.That (implementation.SupportUrl, Is.EqualTo ("http://support.google.com/mail"));
				Assert.That (implementation.Version, Is.EqualTo ("gmail_imap_150623.03_p1"));
				Assert.That (implementation.Properties["remote-host"], Is.EqualTo ("127.0.0.1"));

				// disable ID support
				client.Capabilities &= ~ImapCapabilities.Id;
				Assert.ThrowsAsync<NotSupportedException> (() => client.IdentifyAsync (null));

				await client.DisconnectAsync (false);
			}
		}

		static List<ImapReplayCommand> CreateIdleCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 IDLE\r\n", "gmail.idle.txt"),
				new ImapReplayCommand ("A00000006", "DONE\r\n", "gmail.idle-done.txt"),
				new ImapReplayCommand ("A00000007 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestIdle ()
		{
			var commands = CreateIdleCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				using (var done = new CancellationTokenSource ()) {
					Assert.Throws<ArgumentException> (() => client.Idle (CancellationToken.None));

					// Should throw InvalidOperationException until a folder is selected.
					Assert.Throws<InvalidOperationException> (() => client.Idle (done.Token));

					var inbox = client.Inbox;

					inbox.Open (FolderAccess.ReadWrite);

					int count = 0, expunged = 0, flags = 0;
					bool droppedToZero = false;

					inbox.MessageExpunged += (o, e) => {
						expunged++;
						Assert.That (e.Index, Is.EqualTo (0), "Expunged Index");
					};
					inbox.MessageFlagsChanged += (o, e) => {
						flags++;
						Assert.That (e.Flags, Is.EqualTo (MessageFlags.Answered | MessageFlags.Deleted | MessageFlags.Seen), "Flags");
					};
					inbox.CountChanged += (o, e) => {
						count++;

						if (inbox.Count == 0)
							droppedToZero = true;
						else if (droppedToZero && inbox.Count == 1)
							done.Cancel ();
					};

					client.Idle (done.Token);

					Assert.That (expunged, Is.EqualTo (21), "Unexpected number of Expunged events");
					Assert.That (count, Is.EqualTo (2), "Unexpected number of CountChanged events");
					Assert.That (flags, Is.EqualTo (21), "Unexpected number of FlagsChanged events");
					Assert.That (inbox, Has.Count.EqualTo (1), "Count");
				}

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestIdleAsync ()
		{
			var commands = CreateIdleCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				using (var done = new CancellationTokenSource ()) {
					Assert.ThrowsAsync<ArgumentException> (() => client.IdleAsync (CancellationToken.None));

					// Should throw InvalidOperationException until a folder is selected.
					Assert.ThrowsAsync<InvalidOperationException> (() => client.IdleAsync (done.Token));

					var inbox = client.Inbox;

					await inbox.OpenAsync (FolderAccess.ReadWrite);

					int count = 0, expunged = 0, flags = 0;
					bool droppedToZero = false;

					inbox.MessageExpunged += (o, e) => {
						expunged++;
						Assert.That (e.Index, Is.EqualTo (0), "Expunged Index");
					};
					inbox.MessageFlagsChanged += (o, e) => {
						flags++;
						Assert.That (e.Flags, Is.EqualTo (MessageFlags.Answered | MessageFlags.Deleted | MessageFlags.Seen), "Flags");
					};
					inbox.CountChanged += (o, e) => {
						count++;

						if (inbox.Count == 0)
							droppedToZero = true;
						else if (droppedToZero && inbox.Count == 1)
							done.Cancel ();
					};

					await client.IdleAsync (done.Token);

					Assert.That (expunged, Is.EqualTo (21), "Unexpected number of Expunged events");
					Assert.That (count, Is.EqualTo (2), "Unexpected number of CountChanged events");
					Assert.That (flags, Is.EqualTo (21), "Unexpected number of FlagsChanged events");
					Assert.That (inbox, Has.Count.EqualTo (1), "Count");
				}

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateIdleNotSupportedCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt"),
				new ImapReplayCommand ("A00000006 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestIdleNotSupported ()
		{
			var commands = CreateIdleNotSupportedCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);

				// disable IDLE
				client.Capabilities &= ~ImapCapabilities.Idle;

				using (var done = new CancellationTokenSource ())
					Assert.Throws<NotSupportedException> (() => client.Idle (done.Token));

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestIdleNotSupportedAsync ()
		{
			var commands = CreateIdleNotSupportedCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);

				// disable IDLE
				client.Capabilities &= ~ImapCapabilities.Idle;

				using (var done = new CancellationTokenSource ())
					Assert.ThrowsAsync<NotSupportedException> (() => client.IdleAsync (done.Token));

				await client.DisconnectAsync (true);
			}
		}

		// TODO: test MessageNew w/ headers
		static List<ImapReplayCommand> CreateNotifyCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "dovecot.greeting-preauth.txt"),
				new ImapReplayCommand ("A00000000 NAMESPACE\r\n", "dovecot.namespace.txt"),
				new ImapReplayCommand ("A00000001 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-inbox.txt"),
				new ImapReplayCommand ("A00000002 LIST (SPECIAL-USE) \"\" \"*\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.list-special-use.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"%\" RETURN (SUBSCRIBED CHILDREN)\r\n", "dovecot.notify-list-personal.txt"),
				new ImapReplayCommand ("A00000004 EXAMINE Folder (CONDSTORE)\r\n", "dovecot.examine-folder.txt"),
				new ImapReplayCommand ("A00000005 NOTIFY SET STATUS (PERSONAL (MailboxName SubscriptionChange)) (SELECTED (MessageNew (UID FLAGS ENVELOPE BODYSTRUCTURE MODSEQ) MessageExpunge FlagChange)) (SUBTREE (INBOX Folder) (MessageNew MessageExpunge MailboxMetadataChange ServerMetadataChange))\r\n", "dovecot.notify.txt"),
				new ImapReplayCommand ("A00000006 IDLE\r\n", "dovecot.notify-idle.txt"),
				new ImapReplayCommand ("A00000006", "DONE\r\n", "dovecot.notify-idle-done.txt"),
				new ImapReplayCommand ("A00000007 NOTIFY NONE\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000008 NOTIFY SET STATUS (SELECTED (MessageNew (UID FLAGS ENVELOPE BODYSTRUCTURE MODSEQ BODY.PEEK[HEADER.FIELDS (REFERENCES)]) MessageExpunge FlagChange)) (MAILBOXES INBOX (MessageNew MessageExpunge MailboxMetadataChange ServerMetadataChange))\r\n", "dovecot.notify.txt"),
				new ImapReplayCommand ("A00000009 NOTIFY NONE\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000010 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestNotify ()
		{
			const MessageSummaryItems items = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq;
			var commands = CreateNotifyCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = personal.GetSubfolders ();
				var inbox = client.Inbox;

				var folder = folders.FirstOrDefault (x => x.Name == "Folder");
				var deleteMe = folders.FirstOrDefault (x => x.Name == "DeleteMe");
				var renameMe = folders.FirstOrDefault (x => x.Name == "RenameMe");
				var subscribeMe = folders.FirstOrDefault (x => x.Name == "SubscribeMe");
				var unsubscribeMe = folders.FirstOrDefault (x => x.Name == "UnsubscribeMe");

				folder.Open (FolderAccess.ReadOnly);

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
						new ImapEvent.MessageNew (new FetchRequest ()),
						ImapEvent.MessageExpunge,
						ImapEvent.MailboxMetadataChange,
						ImapEvent.ServerMetadataChange
					}),
				});

				// Passing true to notify will update Count
				Assert.That (inbox, Has.Count.EqualTo (1), "Messages in INBOX");
				Assert.That (folder, Has.Count.EqualTo (0), "Messages in Folder");

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
					Assert.That (e.Folder.FullName, Is.EqualTo ("NewFolder"), "e.Folder.FullName");
					Assert.That (e.Folder.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "e.Folder.Attributes");
					created++;
				};

				client.MetadataChanged += (sender, e) => {
					Assert.That (e.Metadata.Tag.Id, Is.EqualTo ("/private/comment"), "Metadata.Tag");
					Assert.That (e.Metadata.Value, Is.EqualTo ("this is a comment"), "Metadata.Value");
					metadataChanged++;
				};

				inbox.MetadataChanged += (sender, e) => {
					Assert.That (e.Metadata.Tag.Id, Is.EqualTo ("/private/comment"), "Metadata.Tag");
					Assert.That (e.Metadata.Value, Is.EqualTo ("this is a comment"), "Metadata.Value");
					inboxMetadataChanged++;
				};

				deleteMe.Deleted += (sender, e) => {
					deleted++;
				};

				renameMe.Renamed += (sender, e) => {
					Assert.That (renameMe.FullName, Is.EqualTo ("RenamedFolder"), "renameMe.FullName");
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

				Assert.That (inbox, Has.Count.EqualTo (3), "Inbox.Count");
				Assert.That (inbox.Unread, Is.EqualTo (3), "Inbox.Unread");
				Assert.That (inbox.UidNext.Value.Id, Is.EqualTo (4), "Inbox.UidNext");
				Assert.That (inbox.HighestModSeq, Is.EqualTo (3), "Inbox.HighestModSeq");

				Assert.That (inboxHighestModSeqChanged, Is.EqualTo (1), "Inbox.HighestModSeqChanged");
				Assert.That (inboxMetadataChanged, Is.EqualTo (1), "Inbox.MetadataChanged");
				Assert.That (inboxCountChanged, Is.EqualTo (1), "Inbox.CountChanged");

				Assert.That (created, Is.EqualTo (1), "FolderCreated");
				Assert.That (deleted, Is.EqualTo (1), "deleteMe.Deleted");
				Assert.That (renamed, Is.EqualTo (1), "renameMe.Renamed");
				Assert.That (subscribed, Is.EqualTo (1), "subscribeMe.Deleted");
				Assert.That (unsubscribed, Is.EqualTo (1), "unsubscribeMe.Renamed");
				Assert.That (metadataChanged, Is.EqualTo (1), "metadataChanged");

				Assert.That (folder, Has.Count.EqualTo (1), "Folder.Count");
				Assert.That (folderCountChanged, Is.EqualTo (1), "Folder.CountChanged");
				Assert.That (folderFlagsChanged, Is.EqualTo (1), "Folder.MessageFlagsChanged");
				Assert.That (folderMessageSummaryFetched, Is.EqualTo (1), "Folder.MessageSummaryFetched");

				Assert.That (fetched.UniqueId.Id, Is.EqualTo (1), "fetched.UniqueId");
				Assert.That (fetched.Flags.Value, Is.EqualTo (MessageFlags.Recent), "fetched.Flags");
				Assert.That (fetched.Envelope.Subject, Is.EqualTo ("IMAP4rev1 WG mtg summary and minutes"), "fetched.Envelope.Subject");
				var body = fetched.Body as BodyPartBasic;
				Assert.That (fetched.Body, Is.Not.Null, "fetched.Body");
				Assert.That (body.Octets, Is.EqualTo (3028), "fetched.Body.Octets");
				Assert.That (fetched.ModSeq.Value, Is.EqualTo (1), "fetched.ModSeq");

				client.DisableNotify ();

				client.Notify (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items | MessageSummaryItems.References),
						ImapEvent.MessageExpunge,
						ImapEvent.FlagChange
					}),
					new ImapEventGroup (new ImapMailboxFilter.Mailboxes (inbox), new List<ImapEvent> {
						new ImapEvent.MessageNew (),
						ImapEvent.MessageExpunge,
						ImapEvent.MailboxMetadataChange,
						ImapEvent.ServerMetadataChange
					}),
				});

				client.DisableNotify ();

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestNotifyAsync ()
		{
			const MessageSummaryItems items = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.BodyStructure | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq;
			var commands = CreateNotifyCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				var personal = client.GetFolder (client.PersonalNamespaces[0]);
				var folders = await personal.GetSubfoldersAsync ();
				var inbox = client.Inbox;

				var folder = folders.FirstOrDefault (x => x.Name == "Folder");
				var deleteMe = folders.FirstOrDefault (x => x.Name == "DeleteMe");
				var renameMe = folders.FirstOrDefault (x => x.Name == "RenameMe");
				var subscribeMe = folders.FirstOrDefault (x => x.Name == "SubscribeMe");
				var unsubscribeMe = folders.FirstOrDefault (x => x.Name == "UnsubscribeMe");

				await folder.OpenAsync (FolderAccess.ReadOnly);

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
						new ImapEvent.MessageNew (new FetchRequest ()),
						ImapEvent.MessageExpunge,
						ImapEvent.MailboxMetadataChange,
						ImapEvent.ServerMetadataChange
					}),
				});

				// Passing true to notify will update Count
				Assert.That (inbox, Has.Count.EqualTo (1), "Messages in INBOX");
				Assert.That (folder, Has.Count.EqualTo (0), "Messages in Folder");

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
					Assert.That (e.Folder.FullName, Is.EqualTo ("NewFolder"), "e.Folder.FullName");
					Assert.That (e.Folder.Attributes, Is.EqualTo (FolderAttributes.HasNoChildren), "e.Folder.Attributes");
					created++;
				};

				client.MetadataChanged += (sender, e) => {
					Assert.That (e.Metadata.Tag.Id, Is.EqualTo ("/private/comment"), "Metadata.Tag");
					Assert.That (e.Metadata.Value, Is.EqualTo ("this is a comment"), "Metadata.Value");
					metadataChanged++;
				};

				inbox.MetadataChanged += (sender, e) => {
					Assert.That (e.Metadata.Tag.Id, Is.EqualTo ("/private/comment"), "Metadata.Tag");
					Assert.That (e.Metadata.Value, Is.EqualTo ("this is a comment"), "Metadata.Value");
					inboxMetadataChanged++;
				};

				deleteMe.Deleted += (sender, e) => {
					deleted++;
				};

				renameMe.Renamed += (sender, e) => {
					Assert.That (renameMe.FullName, Is.EqualTo ("RenamedFolder"), "renameMe.FullName");
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

				Assert.That (inbox, Has.Count.EqualTo (3), "Inbox.Count");
				Assert.That (inbox.Unread, Is.EqualTo (3), "Inbox.Unread");
				Assert.That (inbox.UidNext.Value.Id, Is.EqualTo (4), "Inbox.UidNext");
				Assert.That (inbox.HighestModSeq, Is.EqualTo (3), "Inbox.HighestModSeq");

				Assert.That (inboxHighestModSeqChanged, Is.EqualTo (1), "Inbox.HighestModSeqChanged");
				Assert.That (inboxMetadataChanged, Is.EqualTo (1), "Inbbox.MetadataChanged");
				Assert.That (inboxCountChanged, Is.EqualTo (1), "Inbox.CountChanged");

				Assert.That (created, Is.EqualTo (1), "FolderCreated");
				Assert.That (deleted, Is.EqualTo (1), "deleteMe.Deleted");
				Assert.That (renamed, Is.EqualTo (1), "renameMe.Renamed");
				Assert.That (subscribed, Is.EqualTo (1), "subscribeMe.Deleted");
				Assert.That (unsubscribed, Is.EqualTo (1), "unsubscribeMe.Renamed");
				Assert.That (metadataChanged, Is.EqualTo (1), "metadataChanged");

				Assert.That (folder, Has.Count.EqualTo (1), "Folder.Count");
				Assert.That (folderCountChanged, Is.EqualTo (1), "Folder.CountChanged");
				Assert.That (folderFlagsChanged, Is.EqualTo (1), "Folder.MessageFlagsChanged");
				Assert.That (folderMessageSummaryFetched, Is.EqualTo (1), "Folder.MessageSummaryFetched");

				Assert.That (fetched.UniqueId.Id, Is.EqualTo (1), "fetched.UniqueId");
				Assert.That (fetched.Flags.Value, Is.EqualTo (MessageFlags.Recent), "fetched.Flags");
				Assert.That (fetched.Envelope.Subject, Is.EqualTo ("IMAP4rev1 WG mtg summary and minutes"), "fetched.Envelope.Subject");
				var body = fetched.Body as BodyPartBasic;
				Assert.That (fetched.Body, Is.Not.Null, "fetched.Body");
				Assert.That (body.Octets, Is.EqualTo (3028), "fetched.Body.Octets");
				Assert.That (fetched.ModSeq.Value, Is.EqualTo (1), "fetched.ModSeq");

				await client.DisableNotifyAsync ();

				await client.NotifyAsync (true, new List<ImapEventGroup> {
					new ImapEventGroup (ImapMailboxFilter.Selected, new List<ImapEvent> {
						new ImapEvent.MessageNew (items | MessageSummaryItems.References),
						ImapEvent.MessageExpunge,
						ImapEvent.FlagChange
					}),
					new ImapEventGroup (new ImapMailboxFilter.Mailboxes (inbox), new List<ImapEvent> {
						new ImapEvent.MessageNew (),
						ImapEvent.MessageExpunge,
						ImapEvent.MailboxMetadataChange,
						ImapEvent.ServerMetadataChange
					}),
				});

				await client.DisableNotifyAsync ();

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateCompressCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 COMPRESS DEFLATE\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000006 COMPRESS DEFLATE\r\n", Encoding.ASCII.GetBytes ("A00000006 NO [COMPRESSIONACTIVE] DEFLATE active via COMPRESS\r\n"), true),
				new ImapReplayCommand ("A00000007 COMPRESS DEFLATE\r\n", Encoding.ASCII.GetBytes ("A00000007 NO Compress failed for an unknown reason.\r\n"), true),
				new ImapReplayCommand ("A00000008 SELECT INBOX (CONDSTORE)\r\n", "gmail.select-inbox.txt", true),
				new ImapReplayCommand ("A00000009 UID SEARCH RETURN (ALL) ALL\r\n", "gmail.search.txt", true),
				new ImapReplayCommand ("A00000010 UID STORE 1:3,5,7:9,11:14,26:29,31,34,41:43,50 +FLAGS.SILENT (\\Deleted)\r\n", ImapReplayCommandResponse.OK, true),
				new ImapReplayCommand ("A00000011 UID EXPUNGE 1:3\r\n", "gmail.expunge.txt", true),
				new ImapReplayCommand ("A00000012 LOGOUT\r\n", "gmail.logout.txt", true)
			};
		}

		[Test]
		public void TestCompress ()
		{
			var commands = CreateCompressCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				client.Compress ();
				client.Compress ();
				Assert.Throws<ImapCommandException> (() => client.Compress ());

				int changed = 0, expunged = 0;
				var inbox = client.Inbox;

				inbox.Open (FolderAccess.ReadWrite);

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.That (e.Index, Is.EqualTo (0), "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				var uids = inbox.Search (SearchQuery.All);
				inbox.AddFlags (uids, MessageFlags.Deleted, true);

				uids = new UniqueIdRange (0, 1, 3);
				inbox.Expunge (uids);

				Assert.That (expunged, Is.EqualTo (3), "Unexpected number of Expunged events");
				Assert.That (changed, Is.EqualTo (1), "Unexpected number of CountChanged events");
				Assert.That (inbox, Has.Count.EqualTo (18), "Count");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestCompressAsync ()
		{
			var commands = CreateCompressCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				await client.CompressAsync ();
				await client.CompressAsync ();
				Assert.ThrowsAsync<ImapCommandException> (() => client.CompressAsync ());

				int changed = 0, expunged = 0;
				var inbox = client.Inbox;

				await inbox.OpenAsync (FolderAccess.ReadWrite);

				inbox.MessageExpunged += (o, e) => { expunged++; Assert.That (e.Index, Is.EqualTo (0), "Expunged event message index"); };
				inbox.CountChanged += (o, e) => { changed++; };

				var uids = await inbox.SearchAsync (SearchQuery.All);
				await inbox.AddFlagsAsync (uids, MessageFlags.Deleted, true);

				uids = new UniqueIdRange (0, 1, 3);
				await inbox.ExpungeAsync (uids);

				Assert.That (expunged, Is.EqualTo (3), "Unexpected number of Expunged events");
				Assert.That (changed, Is.EqualTo (1), "Unexpected number of CountChanged events");
				Assert.That (inbox, Has.Count.EqualTo (18), "Count");

				await client.DisconnectAsync (true);
			}
		}

		static List<ImapReplayCommand> CreateAccessControlListsCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "acl.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "acl.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 GETACL INBOX\r\n", "acl.getacl.txt"),
				new ImapReplayCommand ("A00000006 LISTRIGHTS INBOX smith\r\n", "acl.listrights.txt"),
				new ImapReplayCommand ("A00000007 MYRIGHTS INBOX\r\n", "acl.myrights.txt"),
				new ImapReplayCommand ("A00000008 SETACL INBOX smith +lrswida\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000009 SETACL INBOX smith -lrswida\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000010 SETACL INBOX smith lrswida\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000011 DELETEACL INBOX smith\r\n", ImapReplayCommandResponse.OK)
			};
		}

		[Test]
		public void TestAccessControlLists ()
		{
			var commands = CreateAccessControlListsCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (AclInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.That (client.Rights.ToString (), Is.EqualTo ("texk"), "Rights");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (AclAuthenticatedCapabilities));

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// GETACL INBOX
				var acl = client.Inbox.GetAccessControlList ();
				Assert.That (acl, Has.Count.EqualTo (2), "The number of access controls does not match.");
				Assert.That (acl[0].Name, Is.EqualTo ("Fred"), "The identifier for the first access control does not match.");
				Assert.That (acl[0].Rights.ToString (), Is.EqualTo ("rwipslxetad"), "The access rights for the first access control does not match.");
				Assert.That (acl[1].Name, Is.EqualTo ("Chris"), "The identifier for the second access control does not match.");
				Assert.That (acl[1].Rights.ToString (), Is.EqualTo ("lrswi"), "The access rights for the second access control does not match.");

				// LISTRIGHTS INBOX smith
				Assert.Throws<ArgumentNullException> (() => client.Inbox.GetAccessRights (null));
				//Assert.Throws<ArgumentException> (() => client.Inbox.GetAccessRights (string.Empty));
				var rights = client.Inbox.GetAccessRights ("smith");
				Assert.That (rights.ToString (), Is.EqualTo ("lrswipkxtecda0123456789"), "The access rights do not match for user smith.");

				// MYRIGHTS INBOX
				rights = client.Inbox.GetMyAccessRights ();
				Assert.That (rights.ToString (), Is.EqualTo ("rwiptsldaex"), "My access rights do not match.");

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
			var commands = CreateAccessControlListsCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (AclInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");
				Assert.That (client.Rights.ToString (), Is.EqualTo ("texk"), "Rights");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (AclAuthenticatedCapabilities));

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// GETACL INBOX
				var acl = await client.Inbox.GetAccessControlListAsync ();
				Assert.That (acl, Has.Count.EqualTo (2), "The number of access controls does not match.");
				Assert.That (acl[0].Name, Is.EqualTo ("Fred"), "The identifier for the first access control does not match.");
				Assert.That (acl[0].Rights.ToString (), Is.EqualTo ("rwipslxetad"), "The access rights for the first access control does not match.");
				Assert.That (acl[1].Name, Is.EqualTo ("Chris"), "The identifier for the second access control does not match.");
				Assert.That (acl[1].Rights.ToString (), Is.EqualTo ("lrswi"), "The access rights for the second access control does not match.");

				// LISTRIGHTS INBOX smith
				Assert.ThrowsAsync<ArgumentNullException> (async () => await client.Inbox.GetAccessRightsAsync (null));
				//Assert.ThrowsAsync<ArgumentException> (async () => await client.Inbox.GetAccessRightsAsync (string.Empty));
				var rights = await client.Inbox.GetAccessRightsAsync ("smith");
				Assert.That (rights.ToString (), Is.EqualTo ("lrswipkxtecda0123456789"), "The access rights do not match for user smith.");

				// MYRIGHTS INBOX
				rights = await client.Inbox.GetMyAccessRightsAsync ();
				Assert.That (rights.ToString (), Is.EqualTo ("rwiptsldaex"), "My access rights do not match.");

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

		static List<ImapReplayCommand> CreateMetadataCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "metadata.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "metadata.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "gmail.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 GETMETADATA \"\" /private/comment\r\n", "metadata.getmetadata.txt"),
				new ImapReplayCommand ("A00000006 GETMETADATA \"\" (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.getmetadata-options.txt"),
				new ImapReplayCommand ("A00000007 GETMETADATA \"\" /private/comment /shared/comment\r\n", "metadata.getmetadata-multi.txt"),
				new ImapReplayCommand ("A00000008 SETMETADATA \"\" (/private/comment \"this is a comment\")\r\n", "metadata.setmetadata-noprivate.txt"),
				new ImapReplayCommand ("A00000009 SETMETADATA \"\" (/private/comment \"this comment is too long!\")\r\n", "metadata.setmetadata-maxsize.txt"),
				new ImapReplayCommand ("A00000010 SETMETADATA \"\" (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.setmetadata-toomany.txt"),
				new ImapReplayCommand ("A00000011 SETMETADATA \"\" (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000012 GETMETADATA INBOX /private/comment\r\n", "metadata.inbox-getmetadata.txt"),
				new ImapReplayCommand ("A00000013 GETMETADATA INBOX (MAXSIZE 1024 DEPTH infinity) (/private)\r\n", "metadata.inbox-getmetadata-options.txt"),
				new ImapReplayCommand ("A00000014 GETMETADATA INBOX /private/comment /shared/comment\r\n", "metadata.inbox-getmetadata-multi.txt"),
				new ImapReplayCommand ("A00000015 SETMETADATA INBOX (/private/comment \"this is a comment\")\r\n", "metadata.inbox-setmetadata-noprivate.txt"),
				new ImapReplayCommand ("A00000016 SETMETADATA INBOX (/private/comment \"this comment is too long!\")\r\n", "metadata.inbox-setmetadata-maxsize.txt"),
				new ImapReplayCommand ("A00000017 SETMETADATA INBOX (/private/comment \"this is a private comment\" /shared/comment \"this is a shared comment\")\r\n", "metadata.inbox-setmetadata-toomany.txt"),
				new ImapReplayCommand ("A00000018 SETMETADATA INBOX (/private/comment NIL)\r\n", ImapReplayCommandResponse.OK)
			};
		}

		[Test]
		public void TestMetadata ()
		{
			var commands = CreateMetadataCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				MetadataCollection metadata;
				MetadataOptions options;

				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (MetadataInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (MetadataAuthenticatedCapabilities));

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// GETMETADATA
				Assert.That (client.GetMetadata (MetadataTag.PrivateComment), Is.EqualTo ("this is a comment"), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = client.GetMetadata (options, new [] { new MetadataTag ("/private") });
				Assert.That (metadata, Has.Count.EqualTo (1), "Expected 1 metadata value.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "Metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "Metadata value did not match.");
				Assert.That (options.LongEntries, Is.EqualTo (2199), "LongEntries does not match.");

				metadata = client.GetMetadata (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.That (metadata, Has.Count.EqualTo (2), "Expected 2 metadata values.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "First metadata tag did not match.");
				Assert.That (metadata[1].Tag.Id, Is.EqualTo (MetadataTag.SharedComment.Id), "Second metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "First metadata value did not match.");
				Assert.That (metadata[1].Value, Is.EqualTo ("this is a shared comment"), "Second metadata value did not match.");

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

				// This will no-op
				client.SetMetadata (new MetadataCollection ());

				client.SetMetadata (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				// GETMETADATA folder
				Assert.That (inbox.GetMetadata (MetadataTag.PrivateComment), Is.EqualTo ("this is a comment"), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = inbox.GetMetadata (options, new [] { new MetadataTag ("/private") });
				Assert.That (metadata, Has.Count.EqualTo (1), "Expected 1 metadata value.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "Metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "Metadata value did not match.");
				Assert.That (options.LongEntries, Is.EqualTo (2199), "LongEntries does not match.");

				metadata = inbox.GetMetadata (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.That (metadata, Has.Count.EqualTo (2), "Expected 2 metadata values.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "First metadata tag did not match.");
				Assert.That (metadata[1].Tag.Id, Is.EqualTo (MetadataTag.SharedComment.Id), "Second metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "First metadata value did not match.");
				Assert.That (metadata[1].Value, Is.EqualTo ("this is a shared comment"), "Second metadata value did not match.");

				// This will shortcut and return an empty collection
				metadata = client.GetMetadata (Array.Empty<MetadataTag> ());
				Assert.That (metadata, Is.Empty, "Expected 0 metadata values.");

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
			var commands = CreateMetadataCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				MetadataCollection metadata;
				MetadataOptions options;

				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				Assert.That (client.Capabilities, Is.EqualTo (MetadataInitialCapabilities));
				Assert.That (client.AuthenticationMechanisms, Has.Count.EqualTo (4));
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH"), "Expected SASL XOAUTH auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("XOAUTH2"), "Expected SASL XOAUTH2 auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN"), "Expected SASL PLAIN auth mechanism");
				Assert.That (client.AuthenticationMechanisms, Does.Contain ("PLAIN-CLIENTTOKEN"), "Expected SASL PLAIN-CLIENTTOKEN auth mechanism");

				// Note: Do not try XOAUTH2
				client.AuthenticationMechanisms.Remove ("XOAUTH2");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.Capabilities, Is.EqualTo (MetadataAuthenticatedCapabilities));

				var inbox = client.Inbox;
				Assert.That (inbox, Is.Not.Null, "Expected non-null Inbox folder.");
				Assert.That (inbox.Attributes, Is.EqualTo (FolderAttributes.Inbox | FolderAttributes.HasNoChildren | FolderAttributes.Subscribed), "Expected Inbox attributes to be \\HasNoChildren.");

				foreach (var special in Enum.GetValues (typeof (SpecialFolder)).OfType<SpecialFolder> ()) {
					var folder = client.GetFolder (special);

					if (special != SpecialFolder.Archive) {
						var expected = GetSpecialFolderAttribute (special) | FolderAttributes.HasNoChildren;

						Assert.That (folder, Is.Not.Null, $"Expected non-null {special} folder.");
						Assert.That (folder.Attributes, Is.EqualTo (expected), $"Expected {special} attributes to be \\HasNoChildren.");
					} else {
						Assert.That (folder, Is.Null, $"Expected null {special} folder.");
					}
				}

				// GETMETADATA
				Assert.That (await client.GetMetadataAsync (MetadataTag.PrivateComment), Is.EqualTo ("this is a comment"), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = await client.GetMetadataAsync (options, new [] { new MetadataTag ("/private") });
				Assert.That (metadata, Has.Count.EqualTo (1), "Expected 1 metadata value.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "Metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "Metadata value did not match.");
				Assert.That (options.LongEntries, Is.EqualTo (2199), "LongEntries does not match.");

				metadata = await client.GetMetadataAsync (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.That (metadata, Has.Count.EqualTo (2), "Expected 2 metadata values.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "First metadata tag did not match.");
				Assert.That (metadata[1].Tag.Id, Is.EqualTo (MetadataTag.SharedComment.Id), "Second metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "First metadata value did not match.");
				Assert.That (metadata[1].Value, Is.EqualTo ("this is a shared comment"), "Second metadata value did not match.");

				// This will shortcut and return an empty collection
				metadata = await client.GetMetadataAsync (Array.Empty<MetadataTag> ());
				Assert.That (metadata, Is.Empty, "Expected 0 metadata values.");

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

				// This will no-op
				await client.SetMetadataAsync (new MetadataCollection ());

				await client.SetMetadataAsync (new MetadataCollection (new [] {
					new Metadata (MetadataTag.PrivateComment, null)
				}));

				// GETMETADATA folder
				Assert.That (await inbox.GetMetadataAsync (MetadataTag.PrivateComment), Is.EqualTo ("this is a comment"), "The shared comment does not match.");

				options = new MetadataOptions { Depth = int.MaxValue, MaxSize = 1024 };
				metadata = await inbox.GetMetadataAsync (options, new [] { new MetadataTag ("/private") });
				Assert.That (metadata, Has.Count.EqualTo (1), "Expected 1 metadata value.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "Metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "Metadata value did not match.");
				Assert.That (options.LongEntries, Is.EqualTo (2199), "LongEntries does not match.");

				metadata = await inbox.GetMetadataAsync (new [] { MetadataTag.PrivateComment, MetadataTag.SharedComment });
				Assert.That (metadata, Has.Count.EqualTo (2), "Expected 2 metadata values.");
				Assert.That (metadata[0].Tag.Id, Is.EqualTo (MetadataTag.PrivateComment.Id), "First metadata tag did not match.");
				Assert.That (metadata[1].Tag.Id, Is.EqualTo (MetadataTag.SharedComment.Id), "Second metadata tag did not match.");
				Assert.That (metadata[0].Value, Is.EqualTo ("this is a private comment"), "First metadata value did not match.");
				Assert.That (metadata[1].Value, Is.EqualTo ("this is a shared comment"), "Second metadata value did not match.");

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

		static List<ImapReplayCommand> CreateNamespaceExtensionCommands ()
		{
			return new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "gmail.greeting.txt"),
				new ImapReplayCommand ("A00000000 CAPABILITY\r\n", "gmail.capability.txt"),
				new ImapReplayCommand ("A00000001 AUTHENTICATE PLAIN AHVzZXJuYW1lAHBhc3N3b3Jk\r\n", "gmail.authenticate.txt"),
				new ImapReplayCommand ("A00000002 NAMESPACE\r\n", "common.namespace.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\" RETURN (SUBSCRIBED CHILDREN)\r\n", "gmail.list-inbox.txt"),
				new ImapReplayCommand ("A00000004 XLIST \"\" \"*\"\r\n", "gmail.xlist.txt"),
				new ImapReplayCommand ("A00000005 LOGOUT\r\n", "gmail.logout.txt")
			};
		}

		[Test]
		public void TestNamespaceExtensions ()
		{
			var commands = CreateNamespaceExtensionCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.PersonalNamespaces, Has.Count.EqualTo (1), "PersonalNamespaces.Count");
				Assert.That (client.PersonalNamespaces[0].Path, Is.EqualTo (string.Empty), "PersonalNamespaces[0].Path");
				Assert.That (client.PersonalNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "PersonalNamespaces[0].DirectorySeparator");

				Assert.That (client.OtherNamespaces, Has.Count.EqualTo (1), "OtherNamespaces.Count");
				Assert.That (client.OtherNamespaces[0].Path, Is.EqualTo ("Other Users"), "OtherNamespaces[0].Path");
				Assert.That (client.OtherNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "OtherNamespaces[0].DirectorySeparator");

				Assert.That (client.SharedNamespaces, Has.Count.EqualTo (1), "SharedNamespaces.Count");
				Assert.That (client.SharedNamespaces[0].Path, Is.EqualTo ("Public Folders"), "SharedNamespaces[0].Path");
				Assert.That (client.SharedNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "SharedNamespaces[0].DirectorySeparator");

				client.Disconnect (true);
			}
		}

		[Test]
		public async Task TestNamespaceExtensionsAsync ()
		{
			var commands = CreateNamespaceExtensionCommands ();

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					await client.ConnectAsync (new ImapReplayStream (commands, true), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					await client.AuthenticateAsync ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.PersonalNamespaces, Has.Count.EqualTo (1), "PersonalNamespaces.Count");
				Assert.That (client.PersonalNamespaces[0].Path, Is.EqualTo (string.Empty), "PersonalNamespaces[0].Path");
				Assert.That (client.PersonalNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "PersonalNamespaces[0].DirectorySeparator");

				Assert.That (client.OtherNamespaces, Has.Count.EqualTo (1), "OtherNamespaces.Count");
				Assert.That (client.OtherNamespaces[0].Path, Is.EqualTo ("Other Users"), "OtherNamespaces[0].Path");
				Assert.That (client.OtherNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "OtherNamespaces[0].DirectorySeparator");

				Assert.That (client.SharedNamespaces, Has.Count.EqualTo (1), "SharedNamespaces.Count");
				Assert.That (client.SharedNamespaces[0].Path, Is.EqualTo ("Public Folders"), "SharedNamespaces[0].Path");
				Assert.That (client.SharedNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "SharedNamespaces[0].DirectorySeparator");

				await client.DisconnectAsync (true);
			}
		}

		[Test]
		public void TestLowercaseImapResponses ()
		{
			var commands = new List<ImapReplayCommand> {
				new ImapReplayCommand ("", "lowercase.greeting.txt"),
				new ImapReplayCommand ("A00000000 LOGIN username password\r\n", ImapReplayCommandResponse.OK),
				new ImapReplayCommand ("A00000001 CAPABILITY\r\n", "lowercase.capability.txt"),
				new ImapReplayCommand ("A00000002 LIST \"\" \"\"\r\n", "lowercase.list.txt"),
				new ImapReplayCommand ("A00000003 LIST \"\" \"INBOX\"\r\n", "lowercase.list.txt"),
				new ImapReplayCommand ("A00000004 LIST (SPECIAL-USE) \"\" \"*\"\r\n", "lowercase.list.txt")
			};

			using (var client = new ImapClient () { TagPrefix = 'A' }) {
				try {
					client.Connect (new ImapReplayStream (commands, false), "localhost", 143, SecureSocketOptions.None);
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Connect: {ex}");
				}

				Assert.That (client.IsConnected, Is.True, "Client failed to connect.");

				try {
					client.Authenticate ("username", "password");
				} catch (Exception ex) {
					Assert.Fail ($"Did not expect an exception in Authenticate: {ex}");
				}

				Assert.That (client.PersonalNamespaces, Has.Count.EqualTo (1), "PersonalNamespaces.Count");
				Assert.That (client.PersonalNamespaces[0].Path, Is.EqualTo (string.Empty), "PersonalNamespaces[0].Path");
				Assert.That (client.PersonalNamespaces[0].DirectorySeparator, Is.EqualTo ('/'), "PersonalNamespaces[0].DirectorySeparator");
				Assert.That (client.OtherNamespaces, Is.Empty, "OtherNamespaces.Count");
				Assert.That (client.SharedNamespaces, Is.Empty, "SharedNamespaces.Count");

				Assert.That (client.Inbox, Is.Not.Null, "Inbox");
			}
		}
	}
}
