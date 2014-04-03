//
// ImapClient.cs
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
using System.Text;
using System.Threading;
using System.IO.Compression;
using System.Collections.Generic;

#if NETFX_CORE || WINDOWS_APP || WINDOWS_PHONE_APP
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Encoding = Portable.Text.Encoding;
#else
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SslProtocols = System.Security.Authentication.SslProtocols;
#endif

using MailKit.Security;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP client that can be used to retrieve messages from a server.
	/// </summary>
	/// <remarks>
	/// The <see cref="ImapClient"/> class supports both the "imap" and "imaps"
	/// protocols. The "imap" protocol makes a clear-text connection to the IMAP
	/// server and does not use SSL or TLS unless the IMAP server supports the
	/// STARTTLS extension (as defined by rfc3501). The "imaps" protocol,
	/// however, connects to the IMAP server using an SSL-wrapped connection.
	/// </remarks>
	public class ImapClient : IMessageStore
	{
		readonly IProtocolLogger logger;
		readonly ImapEngine engine;
#if NETFX_CORE || WINDOWS_APP || WINDOWS_PHONE_APP
		StreamSocket socket;
#endif
		bool disposed;
		string host;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="ImapClient"/>, you
		/// must first call the <see cref="Connect"/> method and authenticate with
		/// the <see cref="Authenticate"/> method.
		/// </remarks>
		public ImapClient () : this (new NullProtocolLogger ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="ImapClient"/>, you
		/// must first call the <see cref="Connect"/> method and authenticate with
		/// the <see cref="Authenticate"/> method.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		public ImapClient (IProtocolLogger protocolLogger)
		{
			if (protocolLogger == null)
				throw new ArgumentNullException ("protocolLogger");

			// FIXME: should this take a ParserOptions argument?
			engine = new ImapEngine ();
			engine.Alert += OnAlert;
			logger = protocolLogger;
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="MailKit.Net.Imap.ImapClient"/> is reclaimed by garbage collection.
		/// </summary>
		~ImapClient ()
		{
			Dispose (false);
		}

		/// <summary>
		/// Gets the capabilities supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="Connect"/> method and may change
		/// as a side-effect of the <see cref="Authenticate"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		/// <exception cref="System.ArgumentException">
		/// Capabilities cannot be enabled, they may only be disabled.
		/// </exception>
		public ImapCapabilities Capabilities {
			get { return engine.Capabilities; }
			set {
				if ((engine.Capabilities | value) > engine.Capabilities)
					throw new ArgumentException ("Capabilities cannot be enabled, they may only be disabled.", "value");

				engine.Capabilities = value;
			}
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("ImapClient");
		}

#if !NETFX_CORE && !WINDOWS_APP && !WINDOWS_PHONE_APP
		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (sender, certificate, chain, errors);

			return true;
		}
#endif

		/// <summary>
		/// Enables the QRESYNC feature.
		/// </summary>
		/// <remarks>
		/// <para>The QRESYNC extension improves resynchronization performance of folders by
		/// querying the IMAP server for a list of changes when the folder is opened using the
		/// <see cref="ImapFolder.Open(FolderAccess,UniqueId,ulong,UniqueId[],System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="ImapFolder.Expunged"/> event is replaced
		/// with the <see cref="ImapFolder.Vanished"/> event.</para>
		/// <para>This method needs to be called immediately after <see cref="Authenticate"/>, before
		/// the opening any folders.</para>
		/// </remarks>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is not connected, not authenticated, or a folder has been selected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the QRESYNC extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void EnableQuickResync (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient must be connected before you can enable QRESYNC.");

			if (engine.State > ImapEngineState.Authenticated)
				throw new InvalidOperationException ("QRESYNC needs to be enabled immediately after authenticating.");

			if ((engine.Capabilities & ImapCapabilities.QuickResync) == 0)
				throw new NotSupportedException ();

			if (engine.QResyncEnabled)
				return;

			var ic = engine.QueueCommand (cancellationToken, null, "ENABLE QRESYNC CONDSTORE\r\n");

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("ENABLE", ic.Result);

			engine.QResyncEnabled = true;
		}

		#region IMessageService implementation

#if !NETFX_CORE && !WINDOWS_APP && !WINDOWS_PHONE_APP
		/// <summary>
		/// Gets or sets the client SSL certificates.
		/// </summary>
		/// <remarks>
		/// <para>Some servers may require the client SSL certificates in order
		/// to allow the user to connect.</para>
		/// <para>This property should be set before calling <see cref="Connect"/>.</para>
		/// </remarks>
		/// <value>The client SSL certificates.</value>
		public X509CertificateCollection ClientCertificates {
			get; set;
		}
#endif

		/// <summary>
		/// Gets the authentication mechanisms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried as part of the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

		/// <summary>
		/// Gets the threading algorithms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The threading algorithms are queried as part of the <see cref="Connect"/> and
		/// <see cref="Authenticate"/> methods.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<ThreadingAlgorithm> ThreadingAlgorithms {
			get { return engine.ThreadingAlgorithms; }
		}

		/// <summary>
		/// Gets whether or not the client is currently connected to an IMAP server.
		/// </summary>
		/// <remarks>
		/// When an <see cref="ImapProtocolException"/> is caught, the connection state of the
		/// <see cref="ImapClient"/> should be checked before continuing.
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return engine.IsConnected; }
		}

		/// <summary>
		/// Authenticates using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then LOGIN command is used as a fallback.</para>
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is not connected or is already authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="MailKit.Security.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void Authenticate (ICredentials credentials, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient must be connected before you can authenticate.");

			if (engine.State >= ImapEngineState.Authenticated)
				throw new InvalidOperationException ("The ImapClient is already authenticated.");

			if (credentials == null)
				throw new ArgumentNullException ("credentials");

			int capabilitiesVersion = engine.CapabilitiesVersion;
			var uri = new Uri ("imap://" + host);
			NetworkCredential cred;
			ImapCommand ic;

			// (Erik) Is this needed for WinRT?
#if !NETFX_CORE && !WINDOWS_APP && !WINDOWS_PHONE_APP
			foreach (var authmech in SaslMechanism.AuthMechanismRank) {
				if (!engine.AuthenticationMechanisms.Contains (authmech))
					continue;

				var sasl = SaslMechanism.Create (authmech, uri, credentials);

				cancellationToken.ThrowIfCancellationRequested ();

				var command = string.Format ("AUTHENTICATE {0}", sasl.MechanismName);
				var ir = sasl.Challenge (null);

				if ((engine.Capabilities & ImapCapabilities.SaslIR) != 0 && ir != null) {
					command += " " + ir + "\r\n";
				} else {
					command += "\r\n";
					sasl.Reset ();
				}

				ic = engine.QueueCommand (cancellationToken, null, command);
				ic.ContinuationHandler = (imap, cmd, text) => {
					string challenge;

					if (sasl.IsAuthenticated) {
						// the server claims we aren't done authenticating, but our SASL mechanism thinks we are...
						// FIXME: will sending an empty string abort the AUTHENTICATE command?
						challenge = string.Empty;
					} else {
						challenge = sasl.Challenge (text);
					}

					cmd.CancellationToken.ThrowIfCancellationRequested ();

					var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");
					imap.Stream.Write (buf, 0, buf.Length);
					imap.Stream.Flush ();
				};

				engine.Wait (ic);

				if (ic.Result != ImapCommandResult.Ok)
					continue;

				engine.State = ImapEngineState.Authenticated;

				// Query the CAPABILITIES again if the server did not include an
				// untagged CAPABILITIES response to the AUTHENTICATE command.
				if (engine.CapabilitiesVersion == capabilitiesVersion)
					engine.QueryCapabilities (cancellationToken);

				engine.QueryNamespaces (cancellationToken);
				engine.QuerySpecialFolders (cancellationToken);
				return;
			}
#endif

			if ((Capabilities & ImapCapabilities.LoginDisabled) != 0)
				throw new AuthenticationException ();

			// fall back to the classic LOGIN command...
			cred = credentials.GetCredential (uri, "LOGIN");

			ic = engine.QueueCommand (cancellationToken, null, "LOGIN %S %S\r\n", cred.UserName, cred.Password);

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw new AuthenticationException ();

			engine.State = ImapEngineState.Authenticated;

			// Query the CAPABILITIES again if the server did not include an
			// untagged CAPABILITIES response to the LOGIN command.
			if (engine.CapabilitiesVersion == capabilitiesVersion)
				engine.QueryCapabilities (cancellationToken);

			engine.QueryNamespaces (cancellationToken);
			engine.QuerySpecialFolders (cancellationToken);
		}

		internal void ReplayConnect (string hostName, Stream replayStream, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (hostName == null)
				throw new ArgumentNullException ("hostName");

			if (replayStream == null)
				throw new ArgumentNullException ("replayStream");

			host = hostName;

			engine.Connect (new ImapStream (replayStream, logger), cancellationToken);
			engine.TagPrefix = 'A';

			if (engine.CapabilitiesVersion == 0)
				engine.QueryCapabilities (cancellationToken);
		}

		/// <summary>
		/// Establishes a connection to the specified IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to an IMAP or IMAP/S server. If the schema
		/// in the uri is "imap", a clear-text connection is made and defaults to using
		/// port 143 if no port is specified in the URI. However, if the schema in the
		/// uri is "imaps", an SSL connection is made using the
		/// <see cref="ClientCertificates"/> and defaults to port 993 unless a port
		/// is specified in the URI.</para>
		/// <para>It should be noted that when using a clear-text IMAP connection,
		/// if the server advertizes support for the STARTTLS extension, the client
		/// will automatically switch into TLS mode before authenticating unless the
		/// <paramref name="uri"/> contains a query string to disable it.</para>
		/// <para>If the IMAP server advertizes the COMPRESS extension and either does not
		/// support the STARTTLS extension or the <paramref name="uri"/> explicitly disabled
		/// the use of the STARTTLS extension, then the client will automatically opt into
		/// using a compressed data connection to optimize bandwidth usage unless the
		/// <paramref name="uri"/> contains a query string to explicitly disable it.</para>
		/// <para>If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// and <see cref="Capabilities"/> properties will be populated.</para>
		/// </remarks>
		/// <param name="uri">The server URI. The <see cref="System.Uri.Scheme"/> should either
		/// be "imap" to make a clear-text connection or "imaps" to make an SSL connection.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="uri"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="uri"/> is not an absolute URI.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void Connect (Uri uri, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (uri == null)
				throw new ArgumentNullException ("uri");

			if (!uri.IsAbsoluteUri)
				throw new ArgumentException ("The uri must be absolute.", "uri");

			if (IsConnected)
				throw new InvalidOperationException ("The ImapClient is already connected.");

			var imaps = uri.Scheme.ToLowerInvariant () == "imaps";
			var port = uri.Port > 0 ? uri.Port : (imaps ? 993 : 143);
			var query = uri.ParsedQuery ();
			Stream stream;
			string value;

			var starttls = !imaps && (!query.TryGetValue ("starttls", out value) || Convert.ToBoolean (value));
			var compress = !imaps && (!query.TryGetValue ("compress", out value) || Convert.ToBoolean (value));

#if !NETFX_CORE && !WINDOWS_APP && !WINDOWS_PHONE_APP
			var ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				cancellationToken.ThrowIfCancellationRequested ();

				try {
					socket.Connect (ipAddresses[i], port);
					break;
				} catch {
					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (imaps) {
				var ssl = new SslStream (new NetworkStream (socket, true), false, ValidateRemoteCertificate);
				ssl.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Default, true);
				stream = ssl;
			} else {
				stream = new NetworkStream (socket, true);
			}
#else
			socket = new StreamSocket ();

			cancellationToken.ThrowIfCancellationRequested ();
			socket.ConnectAsync (new HostName (uri.DnsSafeHost), port.ToString (), imaps ? SocketProtectionLevel.Tls12 : SocketProtectionLevel.PlainSocket)
				.AsTask (cancellationToken)
				.GetAwaiter ()
				.GetResult ();

			stream = new DuplexStream (socket.InputStream.AsStreamForRead (), socket.OutputStream.AsStreamForWrite ());
#endif
			host = uri.Host;

			logger.LogConnect (uri);

			engine.Connect (new ImapStream (stream, logger), cancellationToken);

			// Only query the CAPABILITIES if the greeting didn't include them.
			if (engine.CapabilitiesVersion == 0)
				engine.QueryCapabilities (cancellationToken);

            if (starttls && (engine.Capabilities & ImapCapabilities.StartTLS) != 0) {
				var ic = engine.QueueCommand (cancellationToken, null, "STARTTLS\r\n");

				engine.Wait (ic);

				if (ic.Result == ImapCommandResult.Ok) {
#if !NETFX_CORE && !WINDOWS_APP && !WINDOWS_PHONE_APP
					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
					tls.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Tls, true);
					engine.Stream.Stream = tls;
#else
					socket.UpgradeToSslAsync (SocketProtectionLevel.Tls12, new HostName (uri.DnsSafeHost))
						.AsTask (cancellationToken)
						.GetAwaiter ()
						.GetResult ();
#endif

					// Query the CAPABILITIES again if the server did not include an
					// untagged CAPABILITIES response to the STARTTLS command.
					if (engine.CapabilitiesVersion == 1)
						engine.QueryCapabilities (cancellationToken);
				}
			} else if (compress && (engine.Capabilities & ImapCapabilities.Compress) != 0) {
				var ic = engine.QueueCommand (cancellationToken, null, "COMPRESS DEFLATE\r\n");

				engine.Wait (ic);

				if (ic.Result == ImapCommandResult.Ok) {
					var unzip = new DeflateStream (stream, CompressionMode.Decompress);
					var zip = new DeflateStream (stream, CompressionMode.Compress);

					engine.Stream.Stream = new DuplexStream (unzip, zip);

					// Query the CAPABILITIES again if the server did not include an
					// untagged CAPABILITIES response to the COMPRESS command.
					if (engine.CapabilitiesVersion == 1)
						engine.QueryCapabilities (cancellationToken);
				}
			}
		}

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a "LOGOUT" command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <param name="quit">If set to <c>true</c>, a "LOGOUT" command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		public void Disconnect (bool quit, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					var ic = engine.QueueCommand (cancellationToken, null, "LOGOUT\r\n");

					engine.Wait (ic);
				} catch (OperationCanceledException) {
				} catch (ImapProtocolException) {
				} catch (ImapCommandException) {
				} catch (IOException) {
				}
			}

			engine.Disconnect ();

#if NETFX_CORE || WINDOWS_APP || WINDOWS_PHONE_APP
			socket.Dispose ();
			socket = null;
#endif
		}

		/// <summary>
		/// Pings the IMAP server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOOP command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public void NoOp (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			var ic = engine.QueueCommand (cancellationToken, null, "NOOP\r\n");

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("NOOP", ic.Result);
		}

		#endregion

		#region IMessageStore implementation

		/// <summary>
		/// Gets the personal namespaces.
		/// </summary>
		/// <remarks>
		/// The personal folder namespaces contain a user's personal mailbox folders.
		/// </remarks>
		/// <value>The personal namespaces.</value>
		public FolderNamespaceCollection PersonalNamespaces {
			get { return engine.PersonalNamespaces; }
		}

		/// <summary>
		/// Gets the shared namespaces.
		/// </summary>
		/// <remarks>
		/// The shared folder namespaces contain mailbox folders that are shared with the user.
		/// </remarks>
		/// <value>The shared namespaces.</value>
		public FolderNamespaceCollection SharedNamespaces {
			get { return engine.SharedNamespaces; }
		}

		/// <summary>
		/// Gets the other namespaces.
		/// </summary>
		/// <remarks>
		/// The other folder namespaces contain other mailbox folders.
		/// </remarks>
		/// <value>The other namespaces.</value>
		public FolderNamespaceCollection OtherNamespaces {
			get { return engine.OtherNamespaces; }
		}

		/// <summary>
		/// Gets the Inbox folder.
		/// </summary>
		/// <remarks>
		/// The Inbox folder is the default folder and always exists.
		/// </remarks>
		/// <value>The Inbox folder.</value>
		public IFolder Inbox {
			get { return engine.Inbox; }
		}

		/// <summary>
		/// Gets the specified special folder.
		/// </summary>
		/// <remarks>
		/// Not all IMAP servers support special folders. Only IMAP servers
		/// supporting the <see cref="ImapCapabilities.SpecialUse"/> or
		/// <see cref="ImapCapabilities.XList"/> extensions may have
		/// special folders.
		/// </remarks>
		/// <returns>The folder if available; otherwise <c>null</c>.</returns>
		/// <param name="folder">The type of special folder.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="folder"/> is out of range.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// </exception>
		public IFolder GetFolder (SpecialFolder folder)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			switch (folder) {
			case SpecialFolder.All:     return engine.All;
			case SpecialFolder.Archive: return engine.Archive;
			case SpecialFolder.Drafts:  return engine.Drafts;
			case SpecialFolder.Flagged: return engine.Flagged;
			case SpecialFolder.Junk:    return engine.Junk;
			case SpecialFolder.Sent:    return engine.Sent;
			case SpecialFolder.Trash:   return engine.Trash;
			default: throw new ArgumentOutOfRangeException ("folder");
			}
		}

		/// <summary>
		/// Gets the folder for the specified namespace.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		public IFolder GetFolder (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException ("namespace");

			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			var encodedName = ImapEncoding.Encode (@namespace.Path);
			ImapFolder folder;

			if (engine.FolderCache.TryGetValue (encodedName, out folder))
				return folder;

			throw new FolderNotFoundException (@namespace.Path);
		}

		/// <summary>
		/// Gets the folder for the specified path.
		/// </summary>
		/// <returns>The folder.</returns>
		/// <param name="path">The folder path.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="path"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		public IFolder GetFolder (string path, CancellationToken cancellationToken)
		{
			if (path == null)
				throw new ArgumentNullException ("path");

			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			return engine.GetFolder (path, cancellationToken);
		}

		/// <summary>
		/// Occurs when a remote message store receives an alert message from the server.
		/// </summary>
		public event EventHandler<AlertEventArgs> Alert;

		void OnAlert (object sender, AlertEventArgs e)
		{
			var handler = Alert;

			if (handler != null)
				handler (this, e);
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ImapClient"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected virtual void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				engine.Dispose ();
				logger.Dispose ();

#if NETFX_CORE || WINDOWS_APP || WINDOWS_PHONE_APP
				if (socket != null)
					socket.Dispose ();
#endif

				disposed = true;
			}
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Net.Imap.ImapClient"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="MailKit.Net.Imap.ImapClient"/>. The
		/// <see cref="Dispose()"/> method leaves the <see cref="MailKit.Net.Imap.ImapClient"/> in an unusable state. After
		/// calling <see cref="Dispose()"/>, you must release all references to the <see cref="MailKit.Net.Imap.ImapClient"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailKit.Net.Imap.ImapClient"/> was occupying.</remarks>
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}

		#endregion
	}
}
