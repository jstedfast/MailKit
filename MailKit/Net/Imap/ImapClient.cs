//
// ImapClient.cs
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

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net.Security;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MailKit.Security;

using SslStream = MailKit.Net.SslStream;
using AuthenticationException = MailKit.Security.AuthenticationException;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP client that can be used to retrieve messages from a server.
	/// </summary>
	/// <remarks>
	/// The <see cref="ImapClient"/> class supports both the "imap" and "imaps"
	/// protocols. The "imap" protocol makes a clear-text connection to the IMAP
	/// server and does not use SSL or TLS unless the IMAP server supports the
	/// <a href="https://tools.ietf.org/html/rfc3501#section-6.2.1">STARTTLS</a> extension.
	/// The "imaps" protocol, however, connects to the IMAP server using an
	/// SSL-wrapped connection.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessagesByUniqueId"/>
	/// </example>
	/// <example>
	/// <code language="c#" source="Examples\ImapBodyPartExamples.cs" region="GetBodyPartsByUniqueId"/>
	/// </example>
	public partial class ImapClient : MailStore, IImapClient
	{
		static readonly char[] ReservedUriCharacters = { ';', '/', '?', ':', '@', '&', '=', '+', '$', ',', '%' };
		const string HexAlphabet = "0123456789ABCDEF";

		readonly ImapAuthenticationSecretDetector detector = new ImapAuthenticationSecretDetector ();
		readonly ImapEngine engine;
		SslCertificateValidationInfo sslValidationInfo;
		int timeout = 2 * 60 * 1000;
		string identifier;
		bool disconnecting;
		bool connecting;
		bool disposed;
		bool secure;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="ImapClient"/>, you must first
		/// call one of the <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a>
		/// methods and then authenticate with the one of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a>
		/// methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessagesByUniqueId"/>
		/// </example>
		public ImapClient () : this (new NullProtocolLogger ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="ImapClient"/>, you must first
		/// call one of the <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a>
		/// methods and then authenticate with the one of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a>
		/// methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="ProtocolLogger"/>
		/// </example>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		public ImapClient (IProtocolLogger protocolLogger) : base (protocolLogger)
		{
			protocolLogger.AuthenticationSecretDetector = detector;

			// FIXME: should this take a ParserOptions argument?
			engine = new ImapEngine (CreateImapFolder);
			engine.MetadataChanged += OnEngineMetadataChanged;
			engine.FolderCreated += OnEngineFolderCreated;
			engine.Disconnected += OnEngineDisconnected;
			engine.WebAlert += OnEngineWebAlert;
			engine.Alert += OnEngineAlert;
		}

		// Note: This is only needed for UnitTests.
		internal char TagPrefix {
			set { engine.TagPrefix = value; }
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets an object that can be used to synchronize access to the IMAP server.</para>
		/// <para>When using the non-Async methods from multiple threads, it is important to lock the
		/// <see cref="SyncRoot"/> object for thread safety when using the synchronous methods.</para>
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
		/// The capabilities will not be known until a successful connection has been made via one of
		/// the <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a> methods and may
		/// change as a side-effect of calling one of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a>
		/// methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The capabilities.</value>
		/// <exception cref="System.ArgumentException">
		/// Capabilities cannot be enabled, they may only be disabled.
		/// </exception>
		public ImapCapabilities Capabilities {
			get { return engine.Capabilities; }
			set {
				if ((engine.Capabilities | value) > engine.Capabilities)
					throw new ArgumentException ("Capabilities cannot be enabled, they may only be disabled.", nameof (value));

				engine.Capabilities = value;
			}
		}

		/// <summary>
		/// Get the maximum size of a message that can be appended to a folder.
		/// </summary>
		/// <remarks>
		/// <para>Gets the maximum size of a message, in bytes, that can be appended to a folder.</para>
		/// <note type="note">If the value is not set, then the limit is unspecified.</note>
		/// </remarks>
		/// <value>The append limit.</value>
		public uint? AppendLimit {
			get { return engine.AppendLimit; }
		}

		/// <summary>
		/// Get the internationalization level supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets the internationalization level supported by the IMAP server.</para>
		/// <para>For more information, see
		/// <a href="https://tools.ietf.org/html/rfc5255#section-4">section 4 of rfc5255</a>.</para>
		/// </remarks>
		/// <value>The internationalization level.</value>
		public int InternationalizationLevel {
			get { return engine.I18NLevel; }
		}

		/// <summary>
		/// Get the access rights supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// These rights are additional rights supported by the IMAP server beyond the standard rights
		/// defined in <a href="https://tools.ietf.org/html/rfc4314#section-2.1">section 2.1 of rfc4314</a>
		/// and will not be populated until the client is successfully connected.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The rights.</value>
		public AccessRights Rights {
			get { return engine.Rights; }
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException (nameof (ImapClient));
		}

		void CheckConnected ()
		{
			if (!IsConnected)
				throw new ServiceNotConnectedException ("The ImapClient is not connected.");
		}

		void CheckAuthenticated ()
		{
			if (!IsAuthenticated)
				throw new ServiceNotAuthenticatedException ("The ImapClient is not authenticated.");
		}

		/// <summary>
		/// Instantiate a new <see cref="ImapFolder"/>.
		/// </summary>
		/// <remarks>
		/// <para>Creates a new <see cref="ImapFolder"/> instance.</para>
		/// <note type="note">This method's purpose is to allow subclassing <see cref="ImapFolder"/>.</note>
		/// </remarks>
		/// <returns>The IMAP folder instance.</returns>
		/// <param name="args">The constructior arguments.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="args"/> is <c>null</c>.
		/// </exception>
		protected virtual ImapFolder CreateImapFolder (ImapFolderConstructorArgs args)
		{
			var folder = new ImapFolder (args);

			folder.UpdateAppendLimit (AppendLimit);

			return folder;
		}

		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			bool valid;

			sslValidationInfo?.Dispose ();
			sslValidationInfo = null;

			if (ServerCertificateValidationCallback != null) {
				valid = ServerCertificateValidationCallback (engine.Uri.Host, certificate, chain, sslPolicyErrors);
			} else if (ServicePointManager.ServerCertificateValidationCallback != null) {
				valid = ServicePointManager.ServerCertificateValidationCallback (engine.Uri.Host, certificate, chain, sslPolicyErrors);
			} else {
				valid = DefaultServerCertificateValidationCallback (engine.Uri.Host, certificate, chain, sslPolicyErrors);
			}

			if (!valid) {
				// Note: The SslHandshakeException.Create() method will nullify this once it's done using it.
				sslValidationInfo = new SslCertificateValidationInfo (sender, certificate, chain, sslPolicyErrors);
			}

			return valid;
		}

		ImapCommand QueueCompressCommand (CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();

			if ((engine.Capabilities & ImapCapabilities.Compress) == 0)
				throw new NotSupportedException ("The IMAP server does not support the COMPRESS extension.");

			if (engine.State >= ImapEngineState.Selected)
				throw new InvalidOperationException ("Compression must be enabled before selecting a folder.");

#if MAILKIT_LITE
			throw new NotSupportedException ("MailKitLite does not support the COMPRESS extension.");
#else
			return engine.QueueCommand (cancellationToken, null, "COMPRESS DEFLATE\r\n");
#endif
		}

		void ProcessCompressResponse (ImapCommand ic)
		{
#if !MAILKIT_LITE
			if (ic.Response != ImapCommandResponse.Ok) {
				for (int i = 0; i < ic.RespCodes.Count; i++) {
					if (ic.RespCodes[i].Type == ImapResponseCodeType.CompressionActive)
						return;
				}

				throw ImapCommandException.Create ("COMPRESS", ic);
			}

			engine.Stream.Stream = new CompressedStream (engine.Stream.Stream);
#endif
		}

		/// <summary>
		/// Enable compression over the IMAP connection.
		/// </summary>
		/// <remarks>
		/// <para>Enables compression over the IMAP connection.</para>
		/// <para>If the IMAP server supports the <see cref="ImapCapabilities.Compress"/> extension,
		/// it is possible at any point after connecting to enable compression to reduce network
		/// bandwidth usage. Ideally, this method should be called before authenticating.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Compression must be enabled before a folder has been selected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the <see cref="ImapCapabilities.Compress"/> extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the COMPRESS command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void Compress (CancellationToken cancellationToken = default)
		{
			var ic = QueueCompressCommand (cancellationToken);

			engine.Run (ic);

			ProcessCompressResponse (ic);
		}

		bool TryQueueEnableQuickResyncCommand (CancellationToken cancellationToken, out ImapCommand ic)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (engine.State != ImapEngineState.Authenticated)
				throw new InvalidOperationException ("QRESYNC needs to be enabled immediately after authenticating.");

			if ((engine.Capabilities & ImapCapabilities.QuickResync) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QRESYNC extension.");

			if (engine.QResyncEnabled) {
				ic = null;
				return false;
			}

			ic = engine.QueueCommand (cancellationToken, null, "ENABLE QRESYNC CONDSTORE\r\n");

			return true;
		}

		static void ProcessEnableResponse (ImapCommand ic)
		{
			ic.ThrowIfNotOk ("ENABLE");
		}

		/// <summary>
		/// Enable the QRESYNC feature.
		/// </summary>
		/// <remarks>
		/// <para>Enables the <a href="https://tools.ietf.org/html/rfc5162">QRESYNC</a> feature.</para>
		/// <para>The QRESYNC extension improves resynchronization performance of folders by
		/// querying the IMAP server for a list of changes when the folder is opened using the
		/// <see cref="ImapFolder.Open(FolderAccess,uint,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="MailFolder.MessageExpunged"/> event is replaced
		/// with the <see cref="MailFolder.MessagesVanished"/> event.</para>
		/// <para>This method needs to be called immediately after calling one of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a> methods, before
		/// opening any folders.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Quick resynchronization needs to be enabled before selecting a folder.
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
		/// <exception cref="ImapCommandException">
		/// The server replied to the ENABLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public override void EnableQuickResync (CancellationToken cancellationToken = default)
		{
			if (!TryQueueEnableQuickResyncCommand (cancellationToken, out var ic))
				return;

			engine.Run (ic);

			ProcessEnableResponse (ic);
		}

		bool TryQueueEnableUTF8Command (CancellationToken cancellationToken, out ImapCommand ic)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (engine.State != ImapEngineState.Authenticated)
				throw new InvalidOperationException ("UTF8=ACCEPT needs to be enabled immediately after authenticating.");

			if ((engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8=ACCEPT extension.");

			if (engine.UTF8Enabled) {
				ic = null;
				return false;
			}

			ic = engine.QueueCommand (cancellationToken, null, "ENABLE UTF8=ACCEPT\r\n");

			return true;
		}

		/// <summary>
		/// Enable the UTF8=ACCEPT extension.
		/// </summary>
		/// <remarks>
		/// Enables the <a href="https://tools.ietf.org/html/rfc6855">UTF8=ACCEPT</a> extension.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// UTF8=ACCEPT needs to be enabled before selecting a folder.
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
		/// <exception cref="ImapCommandException">
		/// The server replied to the ENABLE command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void EnableUTF8 (CancellationToken cancellationToken = default)
		{
			if (!TryQueueEnableUTF8Command (cancellationToken, out var ic))
				return;

			engine.Run (ic);

			ProcessEnableResponse (ic);
		}

		ImapCommand QueueIdentifyCommand (ImapImplementation clientImplementation, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();

			if ((engine.Capabilities & ImapCapabilities.Id) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ID extension.");

			var command = new StringBuilder ("ID ");
			var args = new List<object> ();

			if (clientImplementation != null && clientImplementation.Properties.Count > 0) {
				command.Append ('(');
				foreach (var property in clientImplementation.Properties) {
					command.Append ("%Q ");
					args.Add (property.Key);

					if (property.Value != null) {
						command.Append ("%Q ");
						args.Add (property.Value);
					} else {
						command.Append ("NIL ");
					}
				}
				command[command.Length - 1] = ')';
				command.Append ("\r\n");
			} else {
				command.Append ("NIL\r\n");
			}

			var ic = new ImapCommand (engine, cancellationToken, null, command.ToString (), args.ToArray ());
			ic.RegisterUntaggedHandler ("ID", ImapUtils.UntaggedIdHandler);

			engine.QueueCommand (ic);

			return ic;
		}

		static ImapImplementation ProcessIdentifyResponse (ImapCommand ic)
		{
			ic.ThrowIfNotOk ("ID");

			return (ImapImplementation) ic.UserData;
		}

		/// <summary>
		/// Identify the client implementation to the server and obtain the server implementation details.
		/// </summary>
		/// <remarks>
		/// <para>Passes along the client implementation details to the server while also obtaining implementation
		/// details from the server.</para>
		/// <para>If the <paramref name="clientImplementation"/> is <c>null</c> or no properties have been set, no
		/// identifying information will be sent to the server.</para>
		/// <note type="security">
		/// <para>Security Implications</para>
		/// <para>This command has the danger of violating the privacy of users if misused. Clients should
		/// notify users that they send the ID command.</para>
		/// <para>It is highly desirable that implementations provide a method of disabling ID support, perhaps by
		/// not calling this method at all, or by passing <c>null</c> as the <paramref name="clientImplementation"/>
		/// argument.</para>
		/// <para>Implementors must exercise extreme care in adding properties to the <paramref name="clientImplementation"/>.
		/// Some properties, such as a processor ID number, Ethernet address, or other unique (or mostly unique) identifier
		/// would allow tracking of users in ways that violate user privacy expectations and may also make it easier for
		/// attackers to exploit security holes in the client.</para>
		/// </note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <returns>The implementation details of the server if available; otherwise, <c>null</c>.</returns>
		/// <param name="clientImplementation">The client implementation.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ID extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the ID command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public ImapImplementation Identify (ImapImplementation clientImplementation, CancellationToken cancellationToken = default)
		{
			var ic = QueueIdentifyCommand (clientImplementation, cancellationToken);

			engine.Run (ic);

			return ProcessIdentifyResponse (ic);
		}

		#region IMailService implementation

		/// <summary>
		/// Get the authentication mechanisms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>The authentication mechanisms are queried as part of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a>
		/// method.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before authenticating.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The authentication mechanisms.</value>
		public override HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

		/// <summary>
		/// Get the threading algorithms supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The threading algorithms are queried as part of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a>
		/// and <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a> methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The supported threading algorithms.</value>
		public override HashSet<ThreadingAlgorithm> ThreadingAlgorithms {
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
		/// <para>The <see cref="IsConnected"/> state is set to <c>true</c> immediately after
		/// one of the <a href="Overload_MailKit_Net_Imap_ImapClient_Connect.htm">Connect</a>
		/// methods succeeds and is not set back to <c>false</c> until either the client
		/// is disconnected via <see cref="Disconnect(bool,CancellationToken)"/> or until an
		/// <see cref="ImapProtocolException"/> is thrown while attempting to read or write to
		/// the underlying network socket.</para>
		/// <para>When an <see cref="ImapProtocolException"/> is caught, the connection state of the
		/// <see cref="ImapClient"/> should be checked before continuing.</para>
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public override bool IsConnected {
			get { return engine.IsConnected; }
		}

		/// <summary>
		/// Get whether or not the connection is secure (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is secure (typically via SSL or TLS).
		/// </remarks>
		/// <value><c>true</c> if the connection is secure; otherwise, <c>false</c>.</value>
		public override bool IsSecure {
			get { return IsConnected && secure; }
		}

		/// <summary>
		/// Get whether or not the connection is encrypted (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is encrypted (typically via SSL or TLS).
		/// </remarks>
		/// <value><c>true</c> if the connection is encrypted; otherwise, <c>false</c>.</value>
		public override bool IsEncrypted {
			get { return IsSecure && (engine.Stream.Stream is SslStream sslStream) && sslStream.IsEncrypted; }
		}

		/// <summary>
		/// Get whether or not the connection is signed (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is signed (typically via SSL or TLS).
		/// </remarks>
		/// <value><c>true</c> if the connection is signed; otherwise, <c>false</c>.</value>
		public override bool IsSigned {
			get { return IsSecure && (engine.Stream.Stream is SslStream sslStream) && sslStream.IsSigned; }
		}

		/// <summary>
		/// Get the negotiated SSL or TLS protocol version.
		/// </summary>
		/// <remarks>
		/// <para>Gets the negotiated SSL or TLS protocol version once an SSL or TLS connection has been made.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS protocol version.</value>
		public override SslProtocols SslProtocol {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.SslProtocol;

				return SslProtocols.None;
			}
		}

		/// <summary>
		/// Get the negotiated SSL or TLS cipher algorithm.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS cipher algorithm once an SSL or TLS connection has been made.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS cipher algorithm.</value>
		public override CipherAlgorithmType? SslCipherAlgorithm {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.CipherAlgorithm;

				return null;
			}
		}

		/// <summary>
		/// Get the negotiated SSL or TLS cipher algorithm strength.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS cipher algorithm strength once an SSL or TLS connection has been made.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS cipher algorithm strength.</value>
		public override int? SslCipherStrength {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.CipherStrength;

				return null;
			}
		}

#if NET5_0_OR_GREATER
		/// <summary>
		/// Get the negotiated SSL or TLS cipher suite.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS cipher suite once an SSL or TLS connection has been made.
		/// </remarks>
		/// <value>The negotiated SSL or TLS cipher suite.</value>
		public override TlsCipherSuite? SslCipherSuite {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.NegotiatedCipherSuite;

				return null;
			}
		}
#endif

		/// <summary>
		/// Get the negotiated SSL or TLS hash algorithm.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS hash algorithm once an SSL or TLS connection has been made.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS hash algorithm.</value>
		public override HashAlgorithmType? SslHashAlgorithm {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.HashAlgorithm;

				return null;
			}
		}

		/// <summary>
		/// Get the negotiated SSL or TLS hash algorithm strength.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS hash algorithm strength once an SSL or TLS connection has been made.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS hash algorithm strength.</value>
		public override int? SslHashStrength {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.HashStrength;

				return null;
			}
		}

		/// <summary>
		/// Get the negotiated SSL or TLS key exchange algorithm.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS key exchange algorithm once an SSL or TLS connection has been made.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS key exchange algorithm.</value>
		public override ExchangeAlgorithmType? SslKeyExchangeAlgorithm {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.KeyExchangeAlgorithm;

				return null;
			}
		}

		/// <summary>
		/// Get the negotiated SSL or TLS key exchange algorithm strength.
		/// </summary>
		/// <remarks>
		/// Gets the negotiated SSL or TLS key exchange algorithm strength once an SSL or TLS connection has been made.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS key exchange algorithm strength.</value>
		public override int? SslKeyExchangeStrength {
			get {
				if (IsSecure && (engine.Stream.Stream is SslStream sslStream))
					return sslStream.KeyExchangeStrength;

				return null;
			}
		}

		/// <summary>
		/// Get whether or not the client is currently authenticated with the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the client is currently authenticated with the IMAP server.</para>
		/// <para>To authenticate with the IMAP server, use one of the
		/// <a href="Overload_MailKit_Net_Imap_ImapClient_Authenticate.htm">Authenticate</a>
		/// methods.</para>
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public override bool IsAuthenticated {
			get { return engine.State >= ImapEngineState.Authenticated; }
		}

		/// <summary>
		/// Get whether or not the client is currently in the IDLE state.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the client is currently in the IDLE state.
		/// </remarks>
		/// <value><c>true</c> if an IDLE command is active; otherwise, <c>false</c>.</value>
		public bool IsIdle {
			get { return engine.State == ImapEngineState.Idle; }
		}

		static AuthenticationException CreateAuthenticationException (ImapCommand ic)
		{
			for (int i = 0; i < ic.RespCodes.Count; i++) {
				if (ic.RespCodes[i].IsError || ic.RespCodes[i].Type == ImapResponseCodeType.Alert)
					return new AuthenticationException (ic.RespCodes[i].Message);
			}

			if (ic.ResponseText != null)
				return new AuthenticationException (ic.ResponseText);

			return new AuthenticationException ();
		}

		void EmitAndThrowOnAlert (ImapCommand ic)
		{
			for (int i = 0; i < ic.RespCodes.Count; i++) {
				if (ic.RespCodes[i].Type != ImapResponseCodeType.Alert)
					continue;

				OnAlert (ic.RespCodes[i].Message);

				throw new AuthenticationException (ic.ResponseText ?? ic.RespCodes[i].Message);
			}
		}

		static bool IsHexDigit (char c)
		{
			return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
		}

		static uint HexUnescape (uint c)
		{
			if (c >= 'a')
				return (c - 'a') + 10;

			if (c >= 'A')
				return (c - 'A') + 10;

			return c - '0';
		}

		static char HexUnescape (string pattern, ref int index)
		{
			uint value, c;

			if (pattern[index++] != '%' || !IsHexDigit (pattern[index]) || !IsHexDigit (pattern[index + 1]))
				return '%';

			c = (uint) pattern[index++];
			value = HexUnescape (c) << 4;
			c = pattern[index++];
			value |= HexUnescape (c);

			return (char) value;
		}

		internal static string UnescapeUserName (string escaped)
		{
			int index;

			if ((index = escaped.IndexOf ('%')) == -1)
				return escaped;

			var userName = new StringBuilder (escaped.Length);
			int startIndex = 0;

			do {
				userName.Append (escaped, startIndex, index - startIndex);
				userName.Append (HexUnescape (escaped, ref index));
				startIndex = index;

				if (startIndex >= escaped.Length)
					break;

				index = escaped.IndexOf ('%', startIndex);
			} while (index != -1);

			if (index == -1)
				userName.Append (escaped, startIndex, escaped.Length - startIndex);

			return userName.ToString ();
		}

		static void HexEscape (StringBuilder builder, char c)
		{
			builder.Append ('%');
			builder.Append (HexAlphabet[(c >> 4) & 0xF]);
			builder.Append (HexAlphabet[c & 0xF]);
		}

		internal static void EscapeUserName (StringBuilder builder, string userName)
		{
			int index = userName.IndexOfAny (ReservedUriCharacters);
			int startIndex = 0;

			while (index != -1) {
				builder.Append (userName, startIndex, index - startIndex);
				HexEscape (builder, userName[index++]);
				startIndex = index;

				if (startIndex >= userName.Length)
					break;

				index = userName.IndexOfAny (ReservedUriCharacters, startIndex);
			}

			builder.Append (userName, startIndex, userName.Length - startIndex);
		}

		string GetSessionIdentifier (string userName)
		{
			var builder = new StringBuilder ();
			var uri = engine.Uri;

			builder.Append (uri.Scheme);
			builder.Append ("://");
			EscapeUserName (builder, userName);
			builder.Append ('@');
			builder.Append (uri.Host);
			builder.Append (':');
			builder.Append (uri.Port.ToString (CultureInfo.InvariantCulture));

			return builder.ToString ();
		}

		void OnAuthenticated (string message, CancellationToken cancellationToken)
		{
			engine.QueryNamespaces (cancellationToken);
			engine.QuerySpecialFolders (cancellationToken);
			OnAuthenticated (message);
		}

		void CheckCanAuthenticate (SaslMechanism mechanism, CancellationToken cancellationToken)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			CheckDisposed ();
			CheckConnected ();

			if (engine.State >= ImapEngineState.Authenticated)
				throw new InvalidOperationException ("The ImapClient is already authenticated.");

			cancellationToken.ThrowIfCancellationRequested ();
		}

		void ConfigureSaslMechanism (SaslMechanism mechanism, Uri uri)
		{
			mechanism.ChannelBindingContext = engine.Stream.Stream as IChannelBindingContext;
			mechanism.Uri = uri;
		}

		void ConfigureSaslMechanism (SaslMechanism mechanism)
		{
			var uri = new Uri ("imap://" + engine.Uri.Host);

			ConfigureSaslMechanism (mechanism, uri);
		}

		void ProcessAuthenticateResponse (ImapCommand ic, SaslMechanism mechanism)
		{
			if (ic.Response != ImapCommandResponse.Ok) {
				EmitAndThrowOnAlert (ic);

				throw new AuthenticationException ();
			}

			engine.State = ImapEngineState.Authenticated;

			var id = GetSessionIdentifier (mechanism.Credentials.UserName);
			if (id != identifier) {
				engine.FolderCache.Clear ();
				identifier = id;
			}
		}

		/// <summary>
		/// Authenticate using the specified SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the specified SASL mechanism.</para>
		/// <para>For a list of available SASL authentication mechanisms supported by the server,
		/// check the <see cref="AuthenticationMechanisms"/> property after the service has been
		/// connected.</para>
		/// </remarks>
		/// <param name="mechanism">The SASL mechanism.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mechanism"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is already authenticated.
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
		/// <exception cref="ImapCommandException">
		/// An IMAP command failed.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public override void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default)
		{
			CheckCanAuthenticate (mechanism, cancellationToken);

			int capabilitiesVersion = engine.CapabilitiesVersion;
			ImapCommand ic = null;

			ConfigureSaslMechanism (mechanism);

			var command = string.Format ("AUTHENTICATE {0}", mechanism.MechanismName);

			if ((engine.Capabilities & ImapCapabilities.SaslIR) != 0 && mechanism.SupportsInitialResponse) {
				string ir = mechanism.Challenge (null, cancellationToken);

				command += " " + ir + "\r\n";
			} else {
				command += "\r\n";
			}

			ic = engine.QueueCommand (cancellationToken, null, command);
			ic.ContinuationHandler = (imap, cmd, text, xdoAsync) => {
				string challenge = mechanism.Challenge (text, cmd.CancellationToken);
				var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");

				imap.Stream.Write (buf, 0, buf.Length, cmd.CancellationToken);
				imap.Stream.Flush (cmd.CancellationToken);

				return Task.CompletedTask;
			};

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				detector.IsAuthenticating = true;

				try {
					engine.Run (ic);
				} finally {
					detector.IsAuthenticating = false;
				}

				ProcessAuthenticateResponse (ic, mechanism);

				// Query the CAPABILITIES again if the server did not include an
				// untagged CAPABILITIES response to the AUTHENTICATE command.
				if (engine.CapabilitiesVersion == capabilitiesVersion)
					engine.QueryCapabilities (cancellationToken);

				OnAuthenticated (ic.ResponseText ?? string.Empty, cancellationToken);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		void CheckCanAuthenticate (Encoding encoding, ICredentials credentials)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			CheckDisposed ();
			CheckConnected ();

			if (engine.State >= ImapEngineState.Authenticated)
				throw new InvalidOperationException ("The ImapClient is already authenticated.");
		}

		void CheckCanLogin (ImapCommand ic)
		{
			if ((Capabilities & ImapCapabilities.LoginDisabled) != 0) {
				if (ic == null)
					throw new AuthenticationException ("The LOGIN command is disabled.");

				throw CreateAuthenticationException (ic);
			}
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		/// <para>If the IMAP server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support (not including
		/// any OAUTH mechanisms) are tried in order of greatest security to weakest security.
		/// Once a SASL authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then LOGIN command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="encoding">The text encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is already authenticated.
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
		/// <exception cref="ImapCommandException">
		/// An IMAP command failed.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public override void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
		{
			CheckCanAuthenticate (encoding, credentials);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				int capabilitiesVersion = engine.CapabilitiesVersion;
				var uri = new Uri ("imap://" + engine.Uri.Host);
				NetworkCredential cred;
				ImapCommand ic = null;
				SaslMechanism sasl;
				string id;

				foreach (var authmech in SaslMechanism.Rank (engine.AuthenticationMechanisms)) {
					cred = credentials.GetCredential (uri, authmech);

					if ((sasl = SaslMechanism.Create (authmech, encoding, cred)) == null)
						continue;

					ConfigureSaslMechanism (sasl, uri);

					cancellationToken.ThrowIfCancellationRequested ();

					var command = string.Format ("AUTHENTICATE {0}", sasl.MechanismName);

					if ((engine.Capabilities & ImapCapabilities.SaslIR) != 0 && sasl.SupportsInitialResponse) {
						string ir = sasl.Challenge (null, cancellationToken);

						command += " " + ir + "\r\n";
					} else {
						command += "\r\n";
					}

					ic = engine.QueueCommand (cancellationToken, null, command);
					ic.ContinuationHandler = (imap, cmd, text, xdoAsync) => {
						string challenge = sasl.Challenge (text, cmd.CancellationToken);

						var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");

						imap.Stream.Write (buf, 0, buf.Length, cmd.CancellationToken);
						imap.Stream.Flush (cmd.CancellationToken);

						return Task.CompletedTask;
					};

					detector.IsAuthenticating = true;

					try {
						engine.Run (ic);
					} finally {
						detector.IsAuthenticating = false;
					}

					if (ic.Response != ImapCommandResponse.Ok) {
						EmitAndThrowOnAlert (ic);
						if (ic.Bye)
							throw new ImapProtocolException (ic.ResponseText);
						continue;
					}

					engine.State = ImapEngineState.Authenticated;

					cred = credentials.GetCredential (uri, sasl.MechanismName);
					id = GetSessionIdentifier (cred.UserName);
					if (id != identifier) {
						engine.FolderCache.Clear ();
						identifier = id;
					}

					// Query the CAPABILITIES again if the server did not include an
					// untagged CAPABILITIES response to the AUTHENTICATE command.
					if (engine.CapabilitiesVersion == capabilitiesVersion)
						engine.QueryCapabilities (cancellationToken);

					OnAuthenticated (ic.ResponseText ?? string.Empty, cancellationToken);
					return;
				}

				CheckCanLogin (ic);

				// fall back to the classic LOGIN command...
				cred = credentials.GetCredential (uri, "DEFAULT");

				ic = engine.QueueCommand (cancellationToken, null, "LOGIN %S %S\r\n", cred.UserName, cred.Password);

				detector.IsAuthenticating = true;

				try {
					engine.Run (ic);
				} finally {
					detector.IsAuthenticating = false;
				}

				if (ic.Response != ImapCommandResponse.Ok)
					throw CreateAuthenticationException (ic);

				engine.State = ImapEngineState.Authenticated;

				id = GetSessionIdentifier (cred.UserName);
				if (id != identifier) {
					engine.FolderCache.Clear ();
					identifier = id;
				}

				// Query the CAPABILITIES again if the server did not include an
				// untagged CAPABILITIES response to the LOGIN command.
				if (engine.CapabilitiesVersion == capabilitiesVersion)
					engine.QueryCapabilities (cancellationToken);

				OnAuthenticated (ic.ResponseText ?? string.Empty, cancellationToken);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		internal static void ComputeDefaultValues (string host, ref int port, ref SecureSocketOptions options, out Uri uri, out bool starttls)
		{
			switch (options) {
			default:
				if (port == 0)
					port = 143;
				break;
			case SecureSocketOptions.Auto:
				switch (port) {
				case 0: port = 143; goto default;
				case 993: options = SecureSocketOptions.SslOnConnect; break;
				default: options = SecureSocketOptions.StartTlsWhenAvailable; break;
				}
				break;
			case SecureSocketOptions.SslOnConnect:
				if (port == 0)
					port = 993;
				break;
			}

			if (IPAddress.TryParse (host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
				host = "[" + host + "]";

			switch (options) {
			case SecureSocketOptions.StartTlsWhenAvailable:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "imap://{0}:{1}/?starttls=when-available", host, port));
				starttls = true;
				break;
			case SecureSocketOptions.StartTls:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "imap://{0}:{1}/?starttls=always", host, port));
				starttls = true;
				break;
			case SecureSocketOptions.SslOnConnect:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "imaps://{0}:{1}", host, port));
				starttls = false;
				break;
			default:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "imap://{0}:{1}", host, port));
				starttls = false;
				break;
			}
		}

		void CheckCanConnect (string host, int port)
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			CheckDisposed ();

			if (IsConnected)
				throw new InvalidOperationException ("The ImapClient is already connected.");
		}

		void SslHandshake (SslStream ssl, string host, CancellationToken cancellationToken)
		{
#if NET5_0_OR_GREATER
			ssl.AuthenticateAsClient (GetSslClientAuthenticationOptions (host, ValidateRemoteCertificate));
#else
			ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
#endif
		}

		void PostConnect (Stream stream, string host, int port, SecureSocketOptions options, bool starttls, CancellationToken cancellationToken)
		{
			try {
				ProtocolLogger.LogConnect (engine.Uri);
			} catch {
				stream.Dispose ();
				secure = false;
				throw;
			}

			connecting = true;

			try {
				engine.Connect (new ImapStream (stream, ProtocolLogger), cancellationToken);
			} catch {
				connecting = false;
				secure = false;
				throw;
			}

			try {
				// Only query the CAPABILITIES if the greeting didn't include them.
				if (engine.CapabilitiesVersion == 0)
					engine.QueryCapabilities (cancellationToken);

				if (options == SecureSocketOptions.StartTls && (engine.Capabilities & ImapCapabilities.StartTLS) == 0)
					throw new NotSupportedException ("The IMAP server does not support the STARTTLS extension.");

				if (starttls && (engine.Capabilities & ImapCapabilities.StartTLS) != 0) {
					var ic = engine.QueueCommand (cancellationToken, null, "STARTTLS\r\n");

					engine.Run (ic);

					if (ic.Response == ImapCommandResponse.Ok) {
						try {
							var tls = new SslStream (stream, false, ValidateRemoteCertificate);
							engine.Stream.Stream = tls;

							SslHandshake (tls, host, cancellationToken);
						} catch (Exception ex) {
							throw SslHandshakeException.Create (ref sslValidationInfo, ex, true, "IMAP", host, port, 993, 143);
						}

						secure = true;

						// Query the CAPABILITIES again if the server did not include an
						// untagged CAPABILITIES response to the STARTTLS command.
						if (engine.CapabilitiesVersion == 1)
							engine.QueryCapabilities (cancellationToken);
					} else if (options == SecureSocketOptions.StartTls) {
						throw ImapCommandException.Create ("STARTTLS", ic);
					}
				}
			} catch (Exception ex) {
				secure = false;
				engine.Disconnect (ex);
				throw;
			} finally {
				connecting = false;
			}

			// Note: we capture the state here in case someone calls Authenticate() from within the Connected event handler.
			var authenticated = engine.State == ImapEngineState.Authenticated;

			OnConnected (host, port, options);

			if (authenticated)
				OnAuthenticated (string.Empty, cancellationToken);
		}

		/// <summary>
		/// Establish a connection to the specified IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified IMAP or IMAP/S server.</para>
		/// <para>If the <paramref name="port"/> has a value of <c>0</c>, then the
		/// <paramref name="options"/> parameter is used to determine the default port to
		/// connect to. The default port used with <see cref="SecureSocketOptions.SslOnConnect"/>
		/// is <c>993</c>. All other values will use a default port of <c>143</c>.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>993</c>, then the default options used will be
		/// <see cref="SecureSocketOptions.SslOnConnect"/>. All other values will use
		/// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.</para>
		/// <para>Once a connection is established, properties such as
		/// <see cref="AuthenticationMechanisms"/> and <see cref="Capabilities"/> will be
		/// populated.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessagesByUniqueId"/>
		/// </example>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the IMAP server does not support the STARTTLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// An IMAP command failed.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public override void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (host, port);

			ComputeDefaultValues (host, ref port, ref options, out var uri, out var starttls);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				var stream = ConnectNetwork (host, port, cancellationToken);
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;

				engine.Uri = uri;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						SslHandshake (ssl, host, cancellationToken);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "IMAP", host, port, 993, 143);
					}

					secure = true;
					stream = ssl;
				} else {
					secure = false;
				}

				PostConnect (stream, host, port, options, starttls, cancellationToken);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		void CheckCanConnect (Stream stream, string host, int port)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			CheckCanConnect (host, port);
		}

		void CheckCanConnect (Socket socket, string host, int port)
		{
			if (socket == null)
				throw new ArgumentNullException (nameof (socket));

			if (!socket.Connected)
				throw new ArgumentException ("The socket is not connected.", nameof (socket));

			CheckCanConnect (host, port);
		}

		/// <summary>
		/// Establish a connection to the specified IMAP or IMAP/S server using the provided socket.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified IMAP or IMAP/S server using
		/// the provided socket.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>993</c>, then the default options used will be
		/// <see cref="SecureSocketOptions.SslOnConnect"/>. All other values will use
		/// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.</para>
		/// <para>Once a connection is established, properties such as
		/// <see cref="AuthenticationMechanisms"/> and <see cref="Capabilities"/> will be
		/// populated.</para>
		/// <note type="info">With the exception of using the <paramref name="port"/> to determine the
		/// default <see cref="SecureSocketOptions"/> to use when the <paramref name="options"/> value
		/// is <see cref="SecureSocketOptions.Auto"/>, the <paramref name="host"/> and
		/// <paramref name="port"/> parameters are only used for logging purposes.</note>
		/// </remarks>
		/// <param name="socket">The socket to use for the connection.</param>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="socket"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="host"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="socket"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="host"/> is a zero-length string.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the IMAP server does not support the STARTTLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// An IMAP command failed.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public override void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (socket, host, port);

			Connect (new NetworkStream (socket, true), host, port, options, cancellationToken);
		}

		/// <summary>
		/// Establish a connection to the specified IMAP or IMAP/S server using the provided stream.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified IMAP or IMAP/S server using
		/// the provided stream.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>993</c>, then the default options used will be
		/// <see cref="SecureSocketOptions.SslOnConnect"/>. All other values will use
		/// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.</para>
		/// <para>Once a connection is established, properties such as
		/// <see cref="AuthenticationMechanisms"/> and <see cref="Capabilities"/> will be
		/// populated.</para>
		/// <note type="info">With the exception of using the <paramref name="port"/> to determine the
		/// default <see cref="SecureSocketOptions"/> to use when the <paramref name="options"/> value
		/// is <see cref="SecureSocketOptions.Auto"/>, the <paramref name="host"/> and
		/// <paramref name="port"/> parameters are only used for logging purposes.</note>
		/// </remarks>
		/// <param name="stream">The stream to use for the connection.</param>
		/// <param name="host">The host name to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="options">The secure socket options to when connecting.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="stream"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="host"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is not between <c>0</c> and <c>65535</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the IMAP server does not support the STARTTLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// An IMAP command failed.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public override void Connect (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (stream, host, port);

			ComputeDefaultValues (host, ref port, ref options, out var uri, out var starttls);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				Stream network;

				engine.Uri = uri;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						SslHandshake (ssl, host, cancellationToken);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "IMAP", host, port, 993, 143);
					}

					network = ssl;
					secure = true;
				} else {
					network = stream;
					secure = false;
				}

				if (network.CanTimeout) {
					network.WriteTimeout = timeout;
					network.ReadTimeout = timeout;
				}

				PostConnect (network, host, port, options, starttls, cancellationToken);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a <c>LOGOUT</c> command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessagesByUniqueId"/>
		/// </example>
		/// <param name="quit">If set to <c>true</c>, a <c>LOGOUT</c> command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		public override void Disconnect (bool quit, CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					var ic = engine.QueueCommand (cancellationToken, null, "LOGOUT\r\n");
					engine.Run (ic);
				} catch (OperationCanceledException) {
				} catch (ImapProtocolException) {
				} catch (ImapCommandException) {
				} catch (IOException) {
				}
			}

			disconnecting = true;

			engine.Disconnect (null);
		}

		ImapCommand QueueNoOpCommand (CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return engine.QueueCommand (cancellationToken, null, "NOOP\r\n");
		}

		static void ProcessNoOpResponse (ImapCommand ic)
		{
			ic.ThrowIfNotOk ("NOOP");
		}

		/// <summary>
		/// Ping the IMAP server to keep the connection alive.
		/// </summary>
		/// <remarks>
		/// <para>The <c>NOOP</c> command is typically used to keep the connection with the IMAP server
		/// alive. When a client goes too long (typically 30 minutes) without sending any commands to the
		/// IMAP server, the IMAP server will close the connection with the client, forcing the client to
		/// reconnect before it can send any more commands.</para>
		/// <para>The <c>NOOP</c> command also provides a great way for a client to check for new
		/// messages.</para>
		/// <para>When the IMAP server receives a <c>NOOP</c> command, it will reply to the client with a
		/// list of pending updates such as <c>EXISTS</c> and <c>RECENT</c> counts on the currently
		/// selected folder. To receive these notifications, subscribe to the
		/// <see cref="MailFolder.CountChanged"/> and <see cref="MailFolder.RecentChanged"/> events,
		/// respectively.</para>
		/// <para>For more information about the <c>NOOP</c> command, see
		/// <a href="https://tools.ietf.org/html/rfc3501#section-6.1.2">rfc3501</a>.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapIdleExample.cs"/>
		/// </example>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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
		public override void NoOp (CancellationToken cancellationToken = default)
		{
			var ic = QueueNoOpCommand (cancellationToken);

			engine.Run (ic);

			ProcessNoOpResponse (ic);
		}

		void CheckCanIdle (CancellationToken doneToken)
		{
			if (!doneToken.CanBeCanceled)
				throw new ArgumentException ("The doneToken must be cancellable.", nameof (doneToken));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((engine.Capabilities & ImapCapabilities.Idle) == 0)
				throw new NotSupportedException ("The IMAP server does not support the IDLE extension.");

			if (engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("An ImapFolder has not been opened.");
		}

		ImapCommand QueueIdleCommand (ImapIdleContext context, CancellationToken cancellationToken)
		{
			var ic = engine.QueueCommand (cancellationToken, null, "IDLE\r\n");
			ic.ContinuationHandler = context.ContinuationHandler;
			ic.UserData = context;

			return ic;
		}

		static void ProcessIdleResponse (ImapCommand ic)
		{
			ic.ThrowIfNotOk ("IDLE");
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
		/// <para>While the IDLE command is running, no other commands may be issued until the
		/// <paramref name="doneToken"/> is cancelled.</para>
		/// <note type="note">It is especially important to cancel the <paramref name="doneToken"/>
		/// before cancelling the <paramref name="cancellationToken"/> when using SSL or TLS due to
		/// the fact that <see cref="System.Net.Security.SslStream"/> cannot be polled.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapIdleExample.cs"/>
		/// </example>
		/// <param name="doneToken">The cancellation token used to return to the non-idle state.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="doneToken"/> must be cancellable (i.e. <see cref="System.Threading.CancellationToken.None"/> cannot be used).
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// A <see cref="ImapFolder"/> has not been opened.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the IDLE extension.
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
		public void Idle (CancellationToken doneToken, CancellationToken cancellationToken = default)
		{
			CheckCanIdle (doneToken);

			if (doneToken.IsCancellationRequested)
				return;

			using (var context = new ImapIdleContext (engine, doneToken, cancellationToken)) {
				var ic = QueueIdleCommand (context, cancellationToken);

				engine.Run (ic);

				ProcessIdleResponse (ic);
			}
		}

		ImapCommand QueueNotifyCommand (bool status, IList<ImapEventGroup> eventGroups, CancellationToken cancellationToken, out bool notifySelectedNewExpunge)
		{
			if (eventGroups == null)
				throw new ArgumentNullException (nameof (eventGroups));

			if (eventGroups.Count == 0)
				throw new ArgumentException ("No event groups specified.", nameof (eventGroups));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((engine.Capabilities & ImapCapabilities.Notify) == 0)
				throw new NotSupportedException ("The IMAP server does not support the NOTIFY extension.");

			notifySelectedNewExpunge = false;

			var command = new StringBuilder ("NOTIFY SET");
			var args = new List<object> ();

			if (status)
				command.Append (" STATUS");

			foreach (var group in eventGroups) {
				command.Append (' ');

				group.Format (engine, command, args, ref notifySelectedNewExpunge);
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (engine, cancellationToken, null, command.ToString (), args.ToArray ());

			engine.QueueCommand (ic);

			return ic;
		}

		void ProcessNotifyResponse (ImapCommand ic, bool notifySelectedNewExpunge)
		{
			ic.ThrowIfNotOk ("NOTIFY");

			engine.NotifySelectedNewExpunge = notifySelectedNewExpunge;
		}

		/// <summary>
		/// Request the specified notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>The <a href="https://tools.ietf.org/html/rfc5465">NOTIFY</a> command is used to expand
		/// which notifications the client wishes to be notified about, including status notifications
		/// about folders other than the currently selected folder. It can also be used to automatically
		/// FETCH information about new messages that have arrived in the currently selected folder.</para>
		/// <para>This, combined with <see cref="Idle(CancellationToken, CancellationToken)"/>,
		/// can be used to get instant notifications for changes to any of the specified folders.</para>
		/// </remarks>
		/// <param name="status"><c>true</c> if the server should immediately notify the client of the
		/// selected folder's status; otherwise, <c>false</c>.</param>
		/// <param name="eventGroups">The specific event groups that the client would like to receive notifications for.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="eventGroups"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="eventGroups"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// One or more <see cref="ImapEventGroup"/> is invalid.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the NOTIFY extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOTIFY command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public void Notify (bool status, IList<ImapEventGroup> eventGroups, CancellationToken cancellationToken = default)
		{
			var ic = QueueNotifyCommand (status, eventGroups, cancellationToken, out bool notifySelectedNewExpunge);

			engine.Run (ic);

			ProcessNotifyResponse (ic, notifySelectedNewExpunge);
		}

		ImapCommand QueueDisableNotifyCommand (CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((engine.Capabilities & ImapCapabilities.Notify) == 0)
				throw new NotSupportedException ("The IMAP server does not support the NOTIFY extension.");

			var ic = new ImapCommand (engine, cancellationToken, null, "NOTIFY NONE\r\n");

			engine.QueueCommand (ic);

			return ic;
		}

		/// <summary>
		/// Disable any previously requested notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// Disables any notification events requested in a prior call to 
		/// <see cref="Notify(bool, IList{ImapEventGroup}, CancellationToken)"/>.
		/// request.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the NOTIFY extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the NOTIFY command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public void DisableNotify (CancellationToken cancellationToken = default)
		{
			var ic = QueueDisableNotifyCommand (cancellationToken);

			engine.Run (ic);

			ProcessNotifyResponse (ic, false);
		}

		#endregion

		#region IMailStore implementation

		/// <summary>
		/// Get the personal namespaces.
		/// </summary>
		/// <remarks>
		/// The personal folder namespaces contain a user's personal mailbox folders.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Namespaces"/>
		/// </example>
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
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Namespaces"/>
		/// </example>
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
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Namespaces"/>
		/// </example>
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
		/// <para>The Inbox folder is the default folder and always exists on the server.</para>
		/// <note type="note">This property will only be available after the client has been authenticated.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessagesByUniqueId"/>
		/// </example>
		/// <value>The Inbox folder.</value>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		public override IMailFolder Inbox {
			get {
				CheckDisposed ();
				CheckConnected ();
				CheckAuthenticated ();

				return engine.Inbox;
			}
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the SPECIAL-USE nor XLIST extensions.
		/// </exception>
		public override IMailFolder GetFolder (SpecialFolder folder)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((Capabilities & (ImapCapabilities.SpecialUse | ImapCapabilities.XList)) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SPECIAL-USE nor XLIST extensions.");

			switch (folder) {
			case SpecialFolder.All:       return engine.All;
			case SpecialFolder.Archive:   return engine.Archive;
			case SpecialFolder.Drafts:    return engine.Drafts;
			case SpecialFolder.Flagged:   return engine.Flagged;
			case SpecialFolder.Important: return engine.Important;
			case SpecialFolder.Junk:      return engine.Junk;
			case SpecialFolder.Sent:      return engine.Sent;
			case SpecialFolder.Trash:     return engine.Trash;
			default: throw new ArgumentOutOfRangeException (nameof (folder));
			}
		}

		/// <summary>
		/// Get the folder for the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets the folder for the specified namespace.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Namespaces"/>
		/// </example>
		/// <returns>The folder.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		public override IMailFolder GetFolder (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException (nameof (@namespace));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			var encodedName = engine.EncodeMailboxName (@namespace.Path);

			if (engine.TryGetCachedFolder (encodedName, out var folder))
				return folder;

			throw new FolderNotFoundException (@namespace.Path);
		}

		/// <summary>
		/// Get all of the folders within the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets all of the folders within the specified namespace.
		/// </remarks>
		/// <returns>The folders.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <param name="items">The status items to pre-populate.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The namespace folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the LIST or LSUB command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public override IList<IMailFolder> GetFolders (FolderNamespace @namespace, StatusItems items = StatusItems.None, bool subscribedOnly = false, CancellationToken cancellationToken = default)
		{
			if (@namespace == null)
				throw new ArgumentNullException (nameof (@namespace));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return engine.GetFolders (@namespace, items, subscribedOnly, cancellationToken);
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied to the LIST command with a NO or BAD response.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public override IMailFolder GetFolder (string path, CancellationToken cancellationToken = default)
		{
			if (path == null)
				throw new ArgumentNullException (nameof (path));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return engine.GetFolder (path, cancellationToken);
		}

		ImapCommand QueueGetMetadataCommand (MetadataTag tag, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((engine.Capabilities & (ImapCapabilities.Metadata | ImapCapabilities.MetadataServer)) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA extension.");

			var ic = new ImapCommand (engine, cancellationToken, null, "GETMETADATA \"\" %S\r\n", tag.Id);
			ic.RegisterUntaggedHandler ("METADATA", ImapUtils.UntaggedMetadataHandler);
			var metadata = new MetadataCollection ();
			ic.UserData = metadata;

			engine.QueueCommand (ic);

			return ic;
		}

		string ProcessGetMetadataResponse (ImapCommand ic, MetadataTag tag)
		{
			ic.ThrowIfNotOk ("GETMETADATA");

			var metadata = (MetadataCollection) ic.UserData;
			string value = null;

			for (int i = 0; i < metadata.Count; i++) {
				if (metadata[i].EncodedName.Length == 0 && metadata[i].Tag.Id == tag.Id) {
					value = metadata[i].Value;
					metadata.RemoveAt (i);
					break;
				}
			}

			engine.ProcessMetadataChanges (metadata);

			return value;
		}

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata value.</returns>
		/// <param name="tag">The metadata tag.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the METADATA or METADATA-SERVER extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override string GetMetadata (MetadataTag tag, CancellationToken cancellationToken = default)
		{
			var ic = QueueGetMetadataCommand (tag, cancellationToken);

			engine.Run (ic);

			return ProcessGetMetadataResponse (ic, tag);
		}

		bool TryQueueGetMetadataCommand (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken, out ImapCommand ic)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (tags == null)
				throw new ArgumentNullException (nameof (tags));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((engine.Capabilities & (ImapCapabilities.Metadata | ImapCapabilities.MetadataServer)) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA or METADATA-SERVER extension.");

			var command = new StringBuilder ("GETMETADATA \"\"");
			var args = new List<object> ();
			bool hasOptions = false;

			if (options.MaxSize.HasValue || options.Depth != 0) {
				command.Append (" (");
				if (options.MaxSize.HasValue) {
					command.Append ("MAXSIZE ");
					command.Append (options.MaxSize.Value.ToString (CultureInfo.InvariantCulture));
					command.Append (' ');
				}
				if (options.Depth > 0) {
					command.Append ("DEPTH ");
					command.Append (options.Depth == int.MaxValue ? "infinity" : "1");
					command.Append (' ');
				}
				command[command.Length - 1] = ')';
				command.Append (' ');
				hasOptions = true;
			}

			int startIndex = command.Length;
			foreach (var tag in tags) {
				command.Append (" %S");
				args.Add (tag.Id);
			}

			if (hasOptions) {
				command[startIndex] = '(';
				command.Append (')');
			}

			command.Append ("\r\n");

			if (args.Count == 0) {
				ic = null;
				return false;
			}

			ic = new ImapCommand (engine, cancellationToken, null, command.ToString (), args.ToArray ());
			ic.RegisterUntaggedHandler ("METADATA", ImapUtils.UntaggedMetadataHandler);
			ic.UserData = new MetadataCollection ();
			options.LongEntries = 0;

			engine.QueueCommand (ic);

			return true;
		}

		MetadataCollection ProcessGetMetadataResponse (ImapCommand ic, MetadataOptions options)
		{
			ic.ThrowIfNotOk ("GETMETADATA");

			if (ic.RespCodes.Count > 0 && ic.RespCodes[ic.RespCodes.Count - 1].Type == ImapResponseCodeType.Metadata) {
				var metadata = (MetadataResponseCode) ic.RespCodes[ic.RespCodes.Count - 1];

				if (metadata.SubType == MetadataResponseCodeSubType.LongEntries)
					options.LongEntries = metadata.Value;
			}

			return engine.FilterMetadata ((MetadataCollection) ic.UserData, string.Empty);
		}

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="options">The metadata options.</param>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="tags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the METADATA or METADATA-SERVER extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override MetadataCollection GetMetadata (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default)
		{
			if (!TryQueueGetMetadataCommand (options, tags, cancellationToken, out var ic))
				return new MetadataCollection ();

			engine.Run (ic);

			return ProcessGetMetadataResponse (ic, options);
		}

		bool TryQueueSetMetadataCommand (MetadataCollection metadata, CancellationToken cancellationToken, out ImapCommand ic)
		{
			if (metadata == null)
				throw new ArgumentNullException (nameof (metadata));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if ((engine.Capabilities & (ImapCapabilities.Metadata | ImapCapabilities.MetadataServer)) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA or METADATA-SERVER extension.");

			if (metadata.Count == 0) {
				ic = null;
				return false;
			}

			var command = new StringBuilder ("SETMETADATA \"\" (");
			var args = new List<object> ();

			for (int i = 0; i < metadata.Count; i++) {
				if (i > 0)
					command.Append (' ');

				if (metadata[i].Value != null) {
					command.Append ("%S %S");
					args.Add (metadata[i].Tag.Id);
					args.Add (metadata[i].Value);
				} else {
					command.Append ("%S NIL");
					args.Add (metadata[i].Tag.Id);
				}
			}
			command.Append (")\r\n");

			ic = new ImapCommand (engine, cancellationToken, null, command.ToString (), args.ToArray ());

			engine.QueueCommand (ic);

			return true;
		}

		static void ProcessSetMetadataResponse (ImapCommand ic)
		{
			ic.ThrowIfNotOk ("SETMETADATA");
		}

		/// <summary>
		/// Sets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
		/// <param name="metadata">The metadata.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="metadata"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the METADATA or METADATA-SERVER extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override void SetMetadata (MetadataCollection metadata, CancellationToken cancellationToken = default)
		{
			if (!TryQueueSetMetadataCommand (metadata, cancellationToken, out var ic))
				return;

			engine.Run (ic);

			ProcessSetMetadataResponse (ic);
		}

		#endregion

		void OnEngineMetadataChanged (object sender, MetadataChangedEventArgs e)
		{
			OnMetadataChanged (e.Metadata);
		}

		void OnEngineFolderCreated (object sender, FolderCreatedEventArgs e)
		{
			OnFolderCreated (e.Folder);
		}

		void OnEngineAlert (object sender, AlertEventArgs e)
		{
			OnAlert (e.Message);
		}

		void OnEngineWebAlert (object sender, WebAlertEventArgs e)
		{
			OnWebAlert (e.WebUri, e.Message);
		}

		/// <summary>
		/// Occurs when a Google Mail server sends a WEBALERT response code to the client.
		/// </summary>
		/// <remarks>
		/// The <see cref="WebAlert"/> event is raised whenever the Google Mail server sends a
		/// WEBALERT message.
		/// </remarks>
		public event EventHandler<WebAlertEventArgs> WebAlert;

		/// <summary>
		/// Raise the web alert event.
		/// </summary>
		/// <remarks>
		/// Raises the web alert event.
		/// </remarks>
		/// <param name="uri">The web alert URI.</param>
		/// <param name="message">The web alert message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		protected virtual void OnWebAlert (Uri uri, string message)
		{
			WebAlert?.Invoke (this, new WebAlertEventArgs (uri, message));
		}

		void OnEngineDisconnected (object sender, EventArgs e)
		{
			if (connecting)
				return;

			var requested = disconnecting;
			var uri = engine.Uri;

			disconnecting = false;
			secure = false;

			OnDisconnected (uri.Host, uri.Port, GetSecureSocketOptions (uri), requested);
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
				engine.MetadataChanged -= OnEngineMetadataChanged;
				engine.FolderCreated -= OnEngineFolderCreated;
				engine.Disconnected -= OnEngineDisconnected;
				engine.WebAlert -= OnEngineWebAlert;
				engine.Alert -= OnEngineAlert;
				engine.Dispose ();
				disposed = true;
			}

			base.Dispose (disposing);
		}
	}
}
