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
using System.Threading.Tasks;
using System.Collections.Generic;

#if NETFX_CORE
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
	public class ImapClient : MailStore
	{
		readonly IProtocolLogger logger;
		readonly ImapEngine engine;
#if NETFX_CORE
		StreamSocket socket;
#endif
		int timeout = 100000;
		bool disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="ImapClient"/>, you must first
		/// call the <see cref="Connect(Uri,CancellationToken)"/> method and authenticate with
		/// the <see cref="Authenticate(ICredentials,CancellationToken)"/> method.
		/// </remarks>
		public ImapClient () : this (new NullProtocolLogger ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="ImapClient"/>, you must first
		/// call the <see cref="Connect(Uri,CancellationToken)"/> method and authenticate with
		/// the <see cref="Authenticate(ICredentials,CancellationToken)"/> method.
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
			engine.Alert += OnEngineAlert;
			logger = protocolLogger;
		}

		/// <summary>
		/// Gets the lock object used by the default Async methods.
		/// </summary>
		/// <remarks>
		/// Gets the lock object used by the default Async methods.
		/// </remarks>
		/// <value>The lock object.</value>
		public override object SyncRoot {
			get { return engine; }
		}

		/// <summary>
		/// Get the protocol supported by the message service.
		/// </summary>
		/// <remarks>
		/// Gets the protocol supported by the message service.
		/// </remarks>
		/// <value>The protocol.</value>
		protected override string Protocol {
			get { return "imap"; }
		}

		/// <summary>
		/// Get the capabilities supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection has been made via
		/// the <see cref="Connect(Uri,CancellationToken)"/> method and may change as a side-effect
		/// of the <see cref="Authenticate(ICredentials,CancellationToken)"/> method.
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

#if !NETFX_CORE
		static bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (sender, certificate, chain, errors);

			return true;
		}
#endif

		/// <summary>
		/// Enable the QRESYNC feature.
		/// </summary>
		/// <remarks>
		/// <para>The QRESYNC extension improves resynchronization performance of folders by
		/// querying the IMAP server for a list of changes when the folder is opened using the
		/// <see cref="ImapFolder.Open(FolderAccess,UniqueId,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="MailFolder.MessageExpunged"/> event is replaced
		/// with the <see cref="MailFolder.MessagesVanished"/> event.</para>
		/// <para>This method needs to be called immediately after
		/// <see cref="Authenticate(ICredentials,CancellationToken)"/>, before the opening of any folders.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
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
		public override void EnableQuickResync (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient must be connected before you can enable QRESYNC.");

			if (engine.State > ImapEngineState.Authenticated)
				throw new InvalidOperationException ("QRESYNC needs to be enabled immediately after authenticating.");

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if ((engine.Capabilities & ImapCapabilities.QuickResync) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QRESYNC extension.");

			if (engine.QResyncEnabled)
				return;

			var ic = engine.QueueCommand (cancellationToken, null, "ENABLE QRESYNC CONDSTORE\r\n");

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("ENABLE", ic);

			engine.QResyncEnabled = true;
		}

		/// <summary>
		/// Enable the UTF8=ACCEPT extension.
		/// </summary>
		/// <remarks>
		/// Enables the UTF8=ACCEPT extension.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is not connected, not authenticated, or a folder has been selected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UTF8=ACCEPT extension.
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
		public void EnableUTF8 (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient must be connected before you can enable UTF8=ACCEPT.");

			if (engine.State > ImapEngineState.Authenticated)
				throw new InvalidOperationException ("UTF8=ACCEPT needs to be enabled immediately after authenticating.");

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if ((engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8=ACCEPT extension.");

			if (engine.UTF8Enabled)
				return;

			var ic = engine.QueueCommand (cancellationToken, null, "ENABLE UTF8=ACCEPT\r\n");

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("ENABLE", ic);

			engine.UTF8Enabled = true;
		}

		/// <summary>
		/// Enable the UTF8=ACCEPT extension.
		/// </summary>
		/// <remarks>
		/// Enables the UTF8=ACCEPT extension.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is not connected, not authenticated, or a folder has been selected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UTF8=ACCEPT extension.
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
		public Task EnableUTF8Async (CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				lock (SyncRoot) {
					EnableUTF8 (cancellationToken);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		#region IMailService implementation

		/// <summary>
		/// Get the authentication mechanisms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried as part of the <see cref="Connect(Uri,CancellationToken)"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public override HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

		/// <summary>
		/// Get the threading algorithms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The threading algorithms are queried as part of the <see cref="Connect(Uri,CancellationToken)"/> and
		/// <see cref="Authenticate(ICredentials,CancellationToken)"/> methods.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<ThreadingAlgorithm> ThreadingAlgorithms {
			get { return engine.ThreadingAlgorithms; }
		}

		/// <summary>
		/// Get or set the timeout for network streaming operations, in milliseconds.
		/// </summary>
		/// <remarks>
		/// Gets or sets the underlying socket stream's <see cref="System.IO.Stream.ReadTimeout"/>
		/// and <see cref="System.IO.Stream.WriteTimeout"/> values.
		/// </remarks>
		/// <value>The timeout in milliseconds.</value>
		public override int Timeout {
			get { return timeout; }
			set {
				if (IsConnected && engine.Stream.CanTimeout) {
					engine.Stream.WriteTimeout = value;
					engine.Stream.ReadTimeout = value;
				}

				timeout = value;
			}
		}

		/// <summary>
		/// Get whether or not the client is currently connected to an IMAP server.
		/// </summary>
		/// <remarks>
		/// When an <see cref="ImapProtocolException"/> is caught, the connection state of the
		/// <see cref="ImapClient"/> should be checked before continuing.
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public override bool IsConnected {
			get { return engine.IsConnected; }
		}

		static AuthenticationException CreateAuthenticationException (ImapCommand ic)
		{
			if (string.IsNullOrEmpty (ic.ResultText)) {
				for (int i = 0; i < ic.RespCodes.Count; i++) {
					if (ic.RespCodes[i].IsError)
						return new AuthenticationException (ic.RespCodes[i].Message);
				}

				return new AuthenticationException ();
			}

			return new AuthenticationException (ic.ResultText);
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
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
		/// <param name="cancellationToken">The cancellation token.</param>
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
		public override void Authenticate (ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (credentials == null)
				throw new ArgumentNullException ("credentials");

			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient must be connected before you can authenticate.");

			if (engine.State >= ImapEngineState.Authenticated)
				throw new InvalidOperationException ("The ImapClient is already authenticated.");

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			int capabilitiesVersion = engine.CapabilitiesVersion;
			var uri = new Uri ("imap://" + engine.Uri.Host);
			NetworkCredential cred;
			ImapCommand ic = null;

			foreach (var authmech in SaslMechanism.AuthMechanismRank) {
				if (!engine.AuthenticationMechanisms.Contains (authmech))
					continue;

				var sasl = SaslMechanism.Create (authmech, uri, credentials);

				cancellationToken.ThrowIfCancellationRequested ();

				var command = string.Format ("AUTHENTICATE {0}", sasl.MechanismName);

				if ((engine.Capabilities & ImapCapabilities.SaslIR) != 0 && sasl.SupportsInitialResponse) {
					var ir = sasl.Challenge (null);
					command += " " + ir + "\r\n";
				} else {
					command += "\r\n";
				}

				ic = engine.QueueCommand (cancellationToken, null, command);
				ic.ContinuationHandler = (imap, cmd, text) => {
					string challenge;

					if (sasl.IsAuthenticated) {
						// The server claims we aren't done authenticating, but our SASL mechanism thinks we are...
						// Send an empty string to abort the AUTHENTICATE command.
						challenge = string.Empty;
					} else {
						challenge = sasl.Challenge (text);
					}

					var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");
					imap.Stream.Write (buf, 0, buf.Length, cmd.CancellationToken);
					imap.Stream.Flush (cmd.CancellationToken);
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
				OnAuthenticated (ic.ResultText);
				return;
			}

			if ((Capabilities & ImapCapabilities.LoginDisabled) != 0) {
				if (ic == null)
					throw new AuthenticationException ("The LOGIN command is disabled.");

				throw CreateAuthenticationException (ic);
			}

			// fall back to the classic LOGIN command...
			cred = credentials.GetCredential (uri, "DEFAULT");

			ic = engine.QueueCommand (cancellationToken, null, "LOGIN %S %S\r\n", cred.UserName, cred.Password);

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw CreateAuthenticationException (ic);

			engine.State = ImapEngineState.Authenticated;

			// Query the CAPABILITIES again if the server did not include an
			// untagged CAPABILITIES response to the LOGIN command.
			if (engine.CapabilitiesVersion == capabilitiesVersion)
				engine.QueryCapabilities (cancellationToken);

			engine.QueryNamespaces (cancellationToken);
			engine.QuerySpecialFolders (cancellationToken);
			OnAuthenticated (ic.ResultText);
		}

		internal void ReplayConnect (string hostName, Stream replayStream, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (hostName == null)
				throw new ArgumentNullException ("hostName");

			if (replayStream == null)
				throw new ArgumentNullException ("replayStream");

			var uri = new Uri ("imap://" + hostName);

			engine.Connect (uri, new ImapStream (replayStream, null, logger), cancellationToken);
			engine.TagPrefix = 'A';

			if (engine.CapabilitiesVersion == 0)
				engine.QueryCapabilities (cancellationToken);

			engine.Disconnected += OnEngineDisconnected;
			OnConnected ();
		}

		/// <summary>
		/// Connect to the specified server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to an IMAP or IMAP/S server. If the schema
		/// in the uri is "imap", a clear-text connection is made and defaults to using
		/// port 143 if no port is specified in the URI. However, if the schema in the
		/// uri is "imaps", an SSL connection is made using the
		/// <see cref="MailService.ClientCertificates"/> and defaults to port 993 unless a port
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
		/// <param name="cancellationToken">The cancellation token.</param>
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
		public override void Connect (Uri uri, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uri == null)
				throw new ArgumentNullException ("uri");

			if (!uri.IsAbsoluteUri)
				throw new ArgumentException ("The uri must be absolute.", "uri");

			CheckDisposed ();

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if (IsConnected)
				throw new InvalidOperationException ("The ImapClient is already connected.");

			var imaps = uri.Scheme.ToLowerInvariant () == "imaps";
			var port = uri.Port > 0 ? uri.Port : (imaps ? 993 : 143);
			var query = uri.ParsedQuery ();
			Stream stream;
			string value;

			var starttls = !imaps && (!query.TryGetValue ("starttls", out value) || Convert.ToBoolean (value));
			var compress = !imaps && (!query.TryGetValue ("compress", out value) || Convert.ToBoolean (value));

#if !NETFX_CORE
			var ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					cancellationToken.ThrowIfCancellationRequested ();
					socket.Connect (ipAddresses[i], port);
					break;
				} catch (OperationCanceledException) {
					socket.Dispose ();
					throw;
				} catch {
					socket.Dispose ();

					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (socket == null)
				throw new IOException (string.Format ("Failed to resolve host: {0}", uri.Host));

			if (imaps) {
				var ssl = new SslStream (new NetworkStream (socket, true), false, ValidateRemoteCertificate);
				ssl.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, true);
				stream = ssl;
			} else {
				stream = new NetworkStream (socket, true);
			}
#else
			socket = new StreamSocket ();

			try {
				cancellationToken.ThrowIfCancellationRequested ();
				socket.ConnectAsync (new HostName (uri.DnsSafeHost), port.ToString (), imaps ? SocketProtectionLevel.Tls12 : SocketProtectionLevel.PlainSocket)
					.AsTask (cancellationToken)
					.GetAwaiter ()
					.GetResult ();
			} catch {
				socket.Dispose ();
				socket = null;
				throw;
			}

			stream = new DuplexStream (socket.InputStream.AsStreamForRead (0), socket.OutputStream.AsStreamForWrite (0));
#endif

			if (stream.CanTimeout) {
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;
			}

			logger.LogConnect (uri);

			engine.Connect (uri, new ImapStream (stream, socket, logger), cancellationToken);

			// Only query the CAPABILITIES if the greeting didn't include them.
			if (engine.CapabilitiesVersion == 0)
				engine.QueryCapabilities (cancellationToken);

			if (starttls && (engine.Capabilities & ImapCapabilities.StartTLS) != 0) {
				var ic = engine.QueueCommand (cancellationToken, null, "STARTTLS\r\n");

				engine.Wait (ic);

				if (ic.Result == ImapCommandResult.Ok) {
#if !NETFX_CORE
					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
					tls.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, true);
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

			engine.Disconnected += OnEngineDisconnected;
			OnConnected ();
		}

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a "LOGOUT" command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <param name="quit">If set to <c>true</c>, a "LOGOUT" command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		public override void Disconnect (bool quit, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				if (engine.IsProcessingCommands)
					throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

				try {
					var ic = engine.QueueCommand (cancellationToken, null, "LOGOUT\r\n");

					engine.Wait (ic);
				} catch (OperationCanceledException) {
				} catch (ImapProtocolException) {
				} catch (ImapCommandException) {
				} catch (IOException) {
				}
			}

			#if NETFX_CORE
			socket.Dispose ();
			socket = null;
			#endif

			engine.Disconnect ();
		}

		/// <summary>
		/// Ping the IMAP server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
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
		public override void NoOp (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if (!engine.IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			var ic = engine.QueueCommand (cancellationToken, null, "NOOP\r\n");

			engine.Wait (ic);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("NOOP", ic);
		}

		static void IdleComplete (object state)
		{
			var ctx = (ImapIdleContext) state;

			if (ctx.Engine.State == ImapEngineState.Idle) {
				var buf = Encoding.ASCII.GetBytes ("DONE\r\n");

				ctx.Engine.Stream.Write (buf, 0, buf.Length);
				ctx.Engine.Stream.Flush ();

				ctx.Engine.State = ImapEngineState.Selected;
			}
		}

		/// <summary>
		/// Toggle the <see cref="ImapClient"/> into the IDLE state.
		/// </summary>
		/// <remarks>
		/// <para>When a client enters the IDLE state, the IMAP server will send
		/// events to the client as they occur on the selected folder. These events
		/// may include notifications of new messages arriving, expunge notifications,
		/// flag changes, etc.</para>
		/// <para>Due to the nature of the IDLE command, a folder must be selected
		/// before a client can enter into the IDLE state. This can be done by
		/// opening a folder using
		/// <see cref="MailKit.MailFolder.Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// or any of the other variants.</para>
		/// </remarks>
		/// <param name="doneToken">The cancellation token used to return to the non-idle state.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="doneToken"/> must be cancellable (i.e. <see cref="System.Threading.CancellationToken.None"/> cannot be used).
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="ImapFolder"/> has not been opened.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the IDLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public void Idle (CancellationToken doneToken, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (!doneToken.CanBeCanceled)
				throw new ArgumentException ("The doneToken must be cancellable.", "doneToken");

			CheckDisposed ();

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if (!engine.IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if ((engine.Capabilities & ImapCapabilities.Idle) == 0)
				throw new NotSupportedException ();

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			if (engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("An ImapFolder has not been opened.");

			using (var context = new ImapIdleContext (engine, doneToken, cancellationToken)) {
				var ic = engine.QueueCommand (cancellationToken, null, "IDLE\r\n");
				ic.UserData = context;

				ic.ContinuationHandler = (imap, cmd, text) => {
					imap.State = ImapEngineState.Idle;

					doneToken.Register (IdleComplete, context);
				};

				engine.Wait (ic);

				if (ic.Result != ImapCommandResult.Ok)
					throw ImapCommandException.Create ("IDLE", ic);
			}
		}

		/// <summary>
		/// Asynchronously toggle the <see cref="ImapClient"/> into the IDLE state.
		/// </summary>
		/// <remarks>
		/// <para>When a client enters the IDLE state, the IMAP server will send
		/// events to the client as they occur on the selected folder. These events
		/// may include notifications of new messages arriving, expunge notifications,
		/// flag changes, etc.</para>
		/// <para>Due to the nature of the IDLE command, a folder must be selected
		/// before a client can enter into the IDLE state. This can be done by
		/// opening a folder using
		/// <see cref="MailKit.MailFolder.Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// or any of the other variants.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="doneToken">The cancellation token used to return to the non-idle state.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="doneToken"/> must be cancellable (i.e. <see cref="System.Threading.CancellationToken.None"/> cannot be used).
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>A <see cref="ImapFolder"/> has not been opened.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the IDLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public Task IdleAsync (CancellationToken doneToken, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (!doneToken.CanBeCanceled)
				throw new ArgumentException ("The doneToken must be cancellable.", "doneToken");

			CheckDisposed ();

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if (!engine.IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if ((engine.Capabilities & ImapCapabilities.Idle) == 0)
				throw new NotSupportedException ();

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			if (engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("An ImapFolder has not been opened.");

			return Task.Factory.StartNew (() => {
				Idle (doneToken, cancellationToken);
			}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}

		#endregion

		#region IMailStore implementation

		/// <summary>
		/// Get the personal namespaces.
		/// </summary>
		/// <remarks>
		/// The personal folder namespaces contain a user's personal mailbox folders.
		/// </remarks>
		/// <value>The personal namespaces.</value>
		public override FolderNamespaceCollection PersonalNamespaces {
			get { return engine.PersonalNamespaces; }
		}

		/// <summary>
		/// Get the shared namespaces.
		/// </summary>
		/// <remarks>
		/// The shared folder namespaces contain mailbox folders that are shared with the user.
		/// </remarks>
		/// <value>The shared namespaces.</value>
		public override FolderNamespaceCollection SharedNamespaces {
			get { return engine.SharedNamespaces; }
		}

		/// <summary>
		/// Get the other namespaces.
		/// </summary>
		/// <remarks>
		/// The other folder namespaces contain other mailbox folders.
		/// </remarks>
		/// <value>The other namespaces.</value>
		public override FolderNamespaceCollection OtherNamespaces {
			get { return engine.OtherNamespaces; }
		}

		/// <summary>
		/// Get whether or not the mail store supports quotas.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the mail store supports quotas.
		/// </remarks>
		/// <value><c>true</c> if the mail store supports quotas; otherwise, <c>false</c>.</value>
		public override bool SupportsQuotas {
			get { return (engine.Capabilities & ImapCapabilities.Quota) != 0; }
		}

		/// <summary>
		/// Get the Inbox folder.
		/// </summary>
		/// <remarks>
		/// The Inbox folder is the default folder and always exists.
		/// </remarks>
		/// <value>The Inbox folder.</value>
		public override IMailFolder Inbox {
			get { return engine.Inbox; }
		}

		/// <summary>
		/// Get the specified special folder.
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
		public override IMailFolder GetFolder (SpecialFolder folder)
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
		/// Get the folder for the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets the folder for the specified namespace.
		/// </remarks>
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
		public override IMailFolder GetFolder (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException ("namespace");

			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			var encodedName = engine.EncodeMailboxName (@namespace.Path);
			ImapFolder folder;

			if (engine.FolderCache.TryGetValue (encodedName, out folder))
				return folder;

			throw new FolderNotFoundException (@namespace.Path);
		}

		/// <summary>
		/// Get the folder for the specified path.
		/// </summary>
		/// <remarks>
		/// Gets the folder for the specified path.
		/// </remarks>
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		public override IMailFolder GetFolder (string path, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (path == null)
				throw new ArgumentNullException ("path");

			CheckDisposed ();

			if (engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			return engine.GetFolder (path, cancellationToken);
		}

		#endregion

		void OnEngineAlert (object sender, AlertEventArgs e)
		{
			OnAlert (e);
		}

		void OnEngineDisconnected (object sender, EventArgs e)
		{
			engine.Disconnected -= OnEngineDisconnected;
			OnDisconnected ();
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="ImapClient"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="ImapClient"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				engine.Dispose ();
				logger.Dispose ();

#if NETFX_CORE
				if (socket != null)
					socket.Dispose ();
#endif

				disposed = true;
			}
		}
	}
}
