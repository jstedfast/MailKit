//
// Pop3Client.cs
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
using System.Buffers;
using System.Threading;
using System.Net.Sockets;
using System.Net.Security;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.IO;

using MailKit.Security;

using SslStream = MailKit.Net.SslStream;
using AuthenticationException = MailKit.Security.AuthenticationException;

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// A POP3 client that can be used to retrieve messages from a server.
	/// </summary>
	/// <remarks>
	/// The <see cref="Pop3Client"/> class supports both the "pop" and "pops" protocols. The "pop" protocol
	/// makes a clear-text connection to the POP3 server and does not use SSL or TLS unless the POP3 server
	/// supports the <a href="https://tools.ietf.org/html/rfc2595">STLS</a> extension. The "pops" protocol,
	/// however, connects to the POP3 server using an SSL-wrapped connection.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
	/// </example>
	public partial class Pop3Client : MailSpool, IPop3Client
	{
		[Flags]
		enum ProbedCapabilities : byte {
			None   = 0,
			Top    = (1 << 0),
			UIDL   = (1 << 1)
		}

		static readonly char[] Space = new char[] { ' ' };

		readonly Pop3AuthenticationSecretDetector detector = new Pop3AuthenticationSecretDetector ();
		readonly MimeParser parser = new MimeParser (Stream.Null);
		readonly Pop3Engine engine;
		SslCertificateValidationInfo sslValidationInfo;
		ProbedCapabilities probed;
		bool disposed, disconnecting, secure, utf8;
		int timeout = 2 * 60 * 1000;
		long octets;
		int total;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Client"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="Pop3Client"/>, you must first call
		/// one of the <a href="Overload_MailKit_Net_Pop3_Pop3Client_Connect.htm">Connect</a> methods
		/// and authenticate using one of the
		/// <a href="Overload_MailKit_Net_Pop3_Pop3Client_Authenticate.htm">Authenticate</a> methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="ProtocolLogger"/>
		/// </example>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		public Pop3Client (IProtocolLogger protocolLogger) : base (protocolLogger)
		{
			protocolLogger.AuthenticationSecretDetector = detector;
			engine = new Pop3Engine ();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Client"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="Pop3Client"/>, you must first call
		/// one of the <a href="Overload_MailKit_Net_Pop3_Pop3Client_Connect.htm">Connect</a> methods
		/// and authenticate using one of the
		/// <a href="Overload_MailKit_Net_Pop3_Pop3Client_Authenticate.htm">Authenticate</a> methods.
		/// </remarks>
		public Pop3Client () : this (new NullProtocolLogger ())
		{
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the POP3 server.
		/// </summary>
		/// <remarks>
		/// <para>Gets an object that can be used to synchronize access to the POP3 server.</para>
		/// <para>When using the non-Async methods from multiple threads, it is important to lock the
		/// <see cref="SyncRoot"/> object for thread safety when using the synchronous methods.</para>
		/// </remarks>
		/// <value>The lock object.</value>
		public override object SyncRoot {
			get { return engine; }
		}

		/// <summary>
		/// Gets the protocol supported by the message service.
		/// </summary>
		/// <remarks>
		/// Gets the protocol supported by the message service.
		/// </remarks>
		/// <value>The protocol.</value>
		protected override string Protocol {
			get { return "pop"; }
		}

		/// <summary>
		/// Gets the capabilities supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection has been made 
		/// and may change once the client is authenticated.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The capabilities.</value>
		/// <exception cref="System.ArgumentException">
		/// Capabilities cannot be enabled, they may only be disabled.
		/// </exception>
		public Pop3Capabilities Capabilities {
			get { return engine.Capabilities; }
			set {
				if ((engine.Capabilities | value) > engine.Capabilities)
					throw new ArgumentException ("Capabilities cannot be enabled, they may only be disabled.", nameof (value));

				engine.Capabilities = value;
			}
		}

		/// <summary>
		/// Gets the expiration policy.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports the EXPIRE capability (<see cref="Pop3Capabilities.Expire"/>), the value
		/// of the <see cref="ExpirePolicy"/> property will reflect the value advertized by the server.</para>
		/// <para>A value of <c>-1</c> indicates that messages will never expire.</para>
		/// <para>A value of <c>0</c> indicates that messages that have been retrieved during the current session
		/// will be purged immediately after the connection is closed via the <c>QUIT</c> command.</para>
		/// <para>Values larger than <c>0</c> indicate the minimum number of days that the server will retain
		/// messages which have been retrieved.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The expiration policy.</value>
		public int ExpirePolicy {
			get { return engine.ExpirePolicy; }
		}

		/// <summary>
		/// Gets the implementation details of the server.
		/// </summary>
		/// <remarks>
		/// If the server advertizes its implementation details, this value will be set to a string containing the
		/// information details provided by the server.
		/// </remarks>
		/// <value>The implementation details.</value>
		public string Implementation {
			get { return engine.Implementation; }
		}

		/// <summary>
		/// Gets the minimum delay, in milliseconds, between logins.
		/// </summary>
		/// <remarks>
		/// If the server supports the LOGIN-DELAY capability (<see cref="Pop3Capabilities.LoginDelay"/>), this value
		/// will be set to the minimum number of milliseconds that the client must wait between logins.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The login delay.</value>
		public int LoginDelay {
			get { return engine.LoginDelay; }
		}

		/// <summary>
		/// Get the size of the POP3 mailbox, in bytes.
		/// </summary>
		/// <remarks>
		/// <para>Gets the size of the POP3 mailbox, in bytes.</para>
		/// <para>This value is updated as a side-effect of calling <see cref="GetMessageCount"/> or <see cref="GetMessageCountAsync"/>.</para>
		/// </remarks>
		/// <value>The size of the mailbox if available.</value>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		public long Size {
			get {
				CheckDisposed ();
				CheckConnected ();
				CheckAuthenticated ();

				return octets;
			}
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException (nameof (Pop3Client));
		}

		void CheckConnected ()
		{
			if (!IsConnected)
				throw new ServiceNotConnectedException ("The Pop3Client is not connected.");
		}

		void CheckAuthenticated ()
		{
			if (!IsAuthenticated)
				throw new ServiceNotAuthenticatedException ("The Pop3Client has not been authenticated.");
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

		static ProtocolException CreatePop3ParseException (Exception innerException, string format, params object[] args)
		{
			return new Pop3ProtocolException (string.Format (CultureInfo.InvariantCulture, format, args), innerException);
		}

		static ProtocolException CreatePop3ParseException (string format, params object[] args)
		{
			return new Pop3ProtocolException (string.Format (CultureInfo.InvariantCulture, format, args));
		}

		static int GetExpectedSequenceId (Pop3Command pc)
		{
			int index = pc.Command.IndexOf (' ') + 1;
			int endIndex = pc.Command.IndexOf ('\r', index);

#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
			var seqid = pc.Command.AsSpan (index, endIndex - index);
#else
			var seqid = pc.Command.Substring (index, endIndex - index);
#endif

			return int.Parse (seqid, NumberStyles.None, CultureInfo.InvariantCulture);
		}

		void SendCommand (CancellationToken token, string command)
		{
			engine.QueueCommand (null, Encoding.ASCII, command);

			engine.Run (true, token);
		}

		string SendCommand (CancellationToken token, string format, params object[] args)
		{
			return SendCommand (token, Encoding.ASCII, format, args);
		}

		string SendCommand (CancellationToken token, Encoding encoding, string format, params object[] args)
		{
			var pc = engine.QueueCommand (null, encoding, format, args);

			engine.Run (true, token);

			return pc.StatusText ?? string.Empty;
		}

		#region IMailService implementation

		/// <summary>
		/// Gets the authentication mechanisms supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// <para>The authentication mechanisms are queried as part of the
		/// connection process.</para>
		/// <para>Servers that do not support the SASL capability will typically
		/// support either the <c>APOP</c> authentication mechanism
		/// (<see cref="Pop3Capabilities.Apop"/>) or the ability to login using the
		/// <c>USER</c> and <c>PASS</c> commands (<see cref="Pop3Capabilities.User"/>).
		/// </para>
		/// <note type="tip"><para>To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before authenticating.</para>
		/// <para>In the case of the APOP authentication mechanism, remove it from the
		/// <see cref="Capabilities"/> property instead.</para></note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The authentication mechanisms.</value>
		public override HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

		/// <summary>
		/// Gets or sets the timeout for network streaming operations, in milliseconds.
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
		/// Gets whether or not the client is currently connected to an POP3 server.
		/// </summary>
		/// <remarks>
		/// <para>The <see cref="IsConnected"/> state is set to <c>true</c> immediately after
		/// one of the <a href="Overload_MailKit_Net_Pop3_Pop3Client_Connect.htm">Connect</a>
		/// methods succeeds and is not set back to <c>false</c> until either the client
		/// is disconnected via <see cref="Disconnect(bool,CancellationToken)"/> or until a
		/// <see cref="Pop3ProtocolException"/> is thrown while attempting to read or write to
		/// the underlying network socket.</para>
		/// <para>When an <see cref="Pop3ProtocolException"/> is caught, the connection state of the
		/// <see cref="Pop3Client"/> should be checked before continuing.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="ExceptionHandling"/>
		/// </example>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="SslConnectionInformation"/>
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
		/// Get whether or not the client is currently authenticated with the POP3 server.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the client is currently authenticated with the POP3 server.</para>
		/// <para>To authenticate with the POP3 server, use one of the
		/// <a href="Overload_MailKit_Net_Pop3_Pop3Client_Authenticate.htm">Authenticate</a> methods.</para>
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public override bool IsAuthenticated {
			get { return engine.State == Pop3EngineState.Transaction; }
		}

		Task ProcessStatResponse (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			// the response should be "<count> <total size>"
			var tokens = text.Split (Space, StringSplitOptions.RemoveEmptyEntries);

			if (tokens.Length < 2) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the STAT command: {0}", text);
				return Task.CompletedTask;
			}

			if (!int.TryParse (tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out total) || total < 0) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an invalid response to the STAT command: {0}", text);
				return Task.CompletedTask;
			}

			if (!long.TryParse (tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out octets)) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an invalid response to the STAT command: {0}", text);
				return Task.CompletedTask;
			}

			return Task.CompletedTask;
		}

		int UpdateMessageCount (CancellationToken cancellationToken)
		{
			engine.QueueCommand (ProcessStatResponse, "STAT\r\n");

			engine.Run (true, cancellationToken);

			return Count;
		}

		void ProbeCapabilities (CancellationToken cancellationToken)
		{
			if ((engine.Capabilities & Pop3Capabilities.UIDL) == 0 && (probed & ProbedCapabilities.UIDL) == 0) {
				// if the message count is > 0, we can probe the UIDL command
				if (total > 0) {
					try {
						GetMessageUid (0, cancellationToken);
					} catch (NotSupportedException) {
					}
				}
			}
		}

		class SaslAuthContext
		{
			readonly SaslMechanism mechanism;
			readonly Pop3Client client;

			public SaslAuthContext (Pop3Client client, SaslMechanism mechanism)
			{
				this.mechanism = mechanism;
				this.client = client;
			}

			public string AuthMessage {
				get; private set;
			}

			Pop3Engine Engine {
				get { return client.engine; }
			}

			void OnDataReceived (Pop3Engine pop3, Pop3Command pc, string text, CancellationToken cancellationToken)
			{
				while (pc.Status == Pop3CommandStatus.Continue && !mechanism.IsAuthenticated) {
					var challenge = mechanism.Challenge (text, cancellationToken);
					var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");

					pop3.Stream.Write (buf, 0, buf.Length, cancellationToken);
					pop3.Stream.Flush (cancellationToken);

					var response = pop3.ReadLine (cancellationToken).TrimEnd ();
					pc.Status = Pop3Engine.GetCommandStatus (response, out text);
					pc.StatusText = text;

					if (pc.Status == Pop3CommandStatus.ProtocolError)
						throw new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
				}

				AuthMessage = text;
			}

			async Task OnDataReceivedAsync (Pop3Engine pop3, Pop3Command pc, string text, CancellationToken cancellationToken)
			{
				while (pc.Status == Pop3CommandStatus.Continue && !mechanism.IsAuthenticated) {
					var challenge = await mechanism.ChallengeAsync (text, cancellationToken).ConfigureAwait (false);
					var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");

					await pop3.Stream.WriteAsync (buf, 0, buf.Length, cancellationToken).ConfigureAwait (false);
					await pop3.Stream.FlushAsync (cancellationToken).ConfigureAwait (false);

					var response = (await pop3.ReadLineAsync (cancellationToken).ConfigureAwait (false)).TrimEnd ();
					pc.Status = Pop3Engine.GetCommandStatus (response, out text);
					pc.StatusText = text;

					if (pc.Status == Pop3CommandStatus.ProtocolError)
						throw new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
				}

				AuthMessage = text;
			}

			Task OnDataReceived (Pop3Engine pop3, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
			{
				if (doAsync)
					return OnDataReceivedAsync (pop3, pc, text, cancellationToken);

				OnDataReceived (pop3, pc, text, cancellationToken);
				return Task.CompletedTask;
			}

			public Pop3Command Authenticate (CancellationToken cancellationToken)
			{
				var pc = Engine.QueueCommand (OnDataReceived, "AUTH {0}\r\n", mechanism.MechanismName);

				AuthMessage = string.Empty;

				client.detector.IsAuthenticating = true;

				try {
					// Note: We defer throwing exceptions on command failure so that our caller can continue trying other authentication mechanisms.
					Engine.Run (false, cancellationToken);
				} finally {
					client.detector.IsAuthenticating = false;
				}

				return pc;
			}

			public async Task<Pop3Command> AuthenticateAsync (CancellationToken cancellationToken)
			{
				var pc = Engine.QueueCommand (OnDataReceived, "AUTH {0}\r\n", mechanism.MechanismName);

				AuthMessage = string.Empty;

				client.detector.IsAuthenticating = true;

				try {
					// Note: We defer throwing exceptions on command failure so that our caller can continue trying other authentication mechanisms.
					await Engine.RunAsync (false, cancellationToken).ConfigureAwait (false);
				} finally {
					client.detector.IsAuthenticating = false;
				}

				return pc;
			}
		}

		void CheckCanAuthenticate (SaslMechanism mechanism, CancellationToken cancellationToken)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The Pop3Client must be connected before you can authenticate.");

			if (IsAuthenticated)
				throw new InvalidOperationException ("The Pop3Client is already authenticated.");

			CheckDisposed ();

			cancellationToken.ThrowIfCancellationRequested ();
		}

		SaslAuthContext GetSaslAuthContext (SaslMechanism mechanism, Uri saslUri)
		{
			mechanism.ChannelBindingContext = engine.Stream.Stream as IChannelBindingContext;
			mechanism.Uri = saslUri;

			return new SaslAuthContext (this, mechanism);
		}

		void OnAuthenticated (string message, CancellationToken cancellationToken)
		{
			engine.State = Pop3EngineState.Transaction;

			engine.QueryCapabilities (cancellationToken);
			UpdateMessageCount (cancellationToken);
			ProbeCapabilities (cancellationToken);
			OnAuthenticated (message);
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
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is already authenticated.
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
		/// <exception cref="Pop3CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// An POP3 protocol error occurred.
		/// </exception>
		public override void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default)
		{
			CheckCanAuthenticate (mechanism, cancellationToken);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				var saslUri = new Uri ("pop://" + engine.Uri.Host);
				var ctx = GetSaslAuthContext (mechanism, saslUri);

				var pc = ctx.Authenticate (cancellationToken);

				if (pc.Status == Pop3CommandStatus.Error)
					throw new AuthenticationException ();

				pc.ThrowIfError ();

				OnAuthenticated (ctx.AuthMessage, cancellationToken);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		void CheckCanAuthenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The Pop3Client must be connected before you can authenticate.");

			if (IsAuthenticated)
				throw new InvalidOperationException ("The Pop3Client is already authenticated.");

			CheckDisposed ();
		}

		string GetApopCommand (Encoding encoding, ICredentials credentials, Uri saslUri)
		{
			var cred = credentials.GetCredential (saslUri, "APOP");
			var userName = utf8 ? SaslMechanism.SaslPrep (cred.UserName) : cred.UserName;
			var password = utf8 ? SaslMechanism.SaslPrep (cred.Password) : cred.Password;
			var challenge = engine.ApopToken + password;
			var md5sum = new StringBuilder ();
			byte[] digest;

			using (var md5 = MD5.Create ())
				digest = md5.ComputeHash (encoding.GetBytes (challenge));

			for (int i = 0; i < digest.Length; i++)
				md5sum.Append (digest[i].ToString ("x2"));

			return $"APOP {userName} {md5sum}\r\n";
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		/// <para>If the POP3 server supports the APOP authentication mechanism,
		/// then APOP is used.</para>
		/// <para>If the APOP authentication mechanism is not supported and the
		/// server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support (not including
		/// any OAUTH mechanisms) are tried in order of greatest security to weakest
		/// security. Once a SASL authentication mechanism is found that both client
		/// and server support, the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the <c>USER</c> and <c>PASS</c> commands are used as a
		/// fallback.</para>
		/// <note type="tip"><para>To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</para>
		/// <para>In the case of the APOP authentication mechanism, remove it from the
		/// <see cref="Capabilities"/> property instead.</para></note>
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
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is already authenticated.
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
		/// <exception cref="Pop3CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// An POP3 protocol error occurred.
		/// </exception>
		public override void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
		{
			CheckCanAuthenticate (encoding, credentials, cancellationToken);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				var saslUri = new Uri ("pop://" + engine.Uri.Host);
				string userName, password, message = null;
				NetworkCredential cred;

				if ((engine.Capabilities & Pop3Capabilities.Apop) != 0) {
					var apop = GetApopCommand (encoding, credentials, saslUri);

					detector.IsAuthenticating = true;

					try {
						message = SendCommand (cancellationToken, encoding, apop);
						engine.State = Pop3EngineState.Transaction;
					} catch (Pop3CommandException) {
					} finally {
						detector.IsAuthenticating = false;
					}

					if (engine.State == Pop3EngineState.Transaction) {
						OnAuthenticated (message ?? string.Empty, cancellationToken);
						return;
					}
				}

				if ((engine.Capabilities & Pop3Capabilities.Sasl) != 0) {
					foreach (var authmech in SaslMechanism.Rank (engine.AuthenticationMechanisms)) {
						SaslMechanism sasl;

						cred = credentials.GetCredential (saslUri, authmech);

						if ((sasl = SaslMechanism.Create (authmech, encoding, cred)) == null)
							continue;

						cancellationToken.ThrowIfCancellationRequested ();

						var ctx = GetSaslAuthContext (sasl, saslUri);

						var pc = ctx.Authenticate (cancellationToken);

						if (pc.Status == Pop3CommandStatus.Error)
							continue;

						pc.ThrowIfError ();

						OnAuthenticated (ctx.AuthMessage, cancellationToken);
						return;
					}
				}

				// fall back to the classic USER & PASS commands...
				cred = credentials.GetCredential (saslUri, "DEFAULT");
				userName = utf8 ? SaslMechanism.SaslPrep (cred.UserName) : cred.UserName;
				password = utf8 ? SaslMechanism.SaslPrep (cred.Password) : cred.Password;
				detector.IsAuthenticating = true;

				try {
					SendCommand (cancellationToken, encoding, "USER {0}\r\n", userName);
					message = SendCommand (cancellationToken, encoding, "PASS {0}\r\n", password);
				} catch (Pop3CommandException) {
					throw new AuthenticationException ();
				} finally {
					detector.IsAuthenticating = false;
				}

				OnAuthenticated (message, cancellationToken);
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
					port = 110;
				break;
			case SecureSocketOptions.Auto:
				switch (port) {
				case 0: port = 110; goto default;
				case 995: options = SecureSocketOptions.SslOnConnect; break;
				default: options = SecureSocketOptions.StartTlsWhenAvailable; break;
				}
				break;
			case SecureSocketOptions.SslOnConnect:
				if (port == 0)
					port = 995;
				break;
			}

			if (IPAddress.TryParse (host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
				host = "[" + host + "]";

			switch (options) {
			case SecureSocketOptions.StartTlsWhenAvailable:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "pop://{0}:{1}/?starttls=when-available", host, port));
				starttls = true;
				break;
			case SecureSocketOptions.StartTls:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "pop://{0}:{1}/?starttls=always", host, port));
				starttls = true;
				break;
			case SecureSocketOptions.SslOnConnect:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "pops://{0}:{1}", host, port));
				starttls = false;
				break;
			default:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "pop://{0}:{1}", host, port));
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
				throw new InvalidOperationException ("The Pop3Client is already connected.");
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
			probed = ProbedCapabilities.None;

			try {
				ProtocolLogger.LogConnect (engine.Uri);
			} catch {
				stream.Dispose ();
				secure = false;
				throw;
			}

			var pop3 = new Pop3Stream (stream, ProtocolLogger);

			engine.Connect (pop3, cancellationToken);

			try {
				engine.QueryCapabilities (cancellationToken);

				if (options == SecureSocketOptions.StartTls && (engine.Capabilities & Pop3Capabilities.StartTLS) == 0)
					throw new NotSupportedException ("The POP3 server does not support the STLS extension.");

				if (starttls && (engine.Capabilities & Pop3Capabilities.StartTLS) != 0) {
					SendCommand (cancellationToken, "STLS\r\n");

					try {
						var tls = new SslStream (stream, false, ValidateRemoteCertificate);
						engine.Stream.Stream = tls;

						SslHandshake (tls, host, cancellationToken);
					} catch (Exception ex) {
						throw SslHandshakeException.Create (ref sslValidationInfo, ex, true, "POP3", host, port, 995, 110);
					}

					secure = true;

					// re-issue a CAPA command
					engine.QueryCapabilities (cancellationToken);
				}
			} catch (Exception ex) {
				engine.Disconnect (ex);
				secure = false;
				throw;
			}

			engine.Disconnected += OnEngineDisconnected;
			OnConnected (host, port, options);
		}

		/// <summary>
		/// Establish a connection to the specified POP3 or POP3/S server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified POP3 or POP3/S server.</para>
		/// <para>If the <paramref name="port"/> has a value of <c>0</c>, then the
		/// <paramref name="options"/> parameter is used to determine the default port to
		/// connect to. The default port used with <see cref="SecureSocketOptions.SslOnConnect"/>
		/// is <c>995</c>. All other values will use a default port of <c>110</c>.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>995</c>, then the default options used will be
		/// <see cref="SecureSocketOptions.SslOnConnect"/>. All other values will use
		/// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.</para>
		/// <para>Once a connection is established, properties such as
		/// <see cref="AuthenticationMechanisms"/> and <see cref="Capabilities"/> will be
		/// populated.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
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
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is already connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the POP3 server does not support the STLS extension.
		/// </exception>
		/// <exception cref="System.Net.Sockets.SocketException">
		/// A socket error occurred trying to connect to the remote host.
		/// </exception>
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
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

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "POP3", host, port, 995, 110);
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
		/// Establish a connection to the specified POP3 or POP3/S server using the provided socket.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified POP3 or POP3/S server using
		/// the provided socket.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>995</c>, then the default options used will be
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
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the POP3 server does not support the STLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (socket, host, port);

			Connect (new NetworkStream (socket, true), host, port, options, cancellationToken);
		}

		/// <summary>
		/// Establish a connection to the specified POP3 or POP3/S server using the provided stream.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified POP3 or POP3/S server using
		/// the provided stream.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>995</c>, then the default options used will be
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
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the POP3 server does not support the STLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void Connect (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (stream, host, port);

			Stream network;

			ComputeDefaultValues (host, ref port, ref options, out var uri, out var starttls);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				engine.Uri = uri;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						SslHandshake (ssl, host, cancellationToken);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "POP3", host, port, 995, 110);
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
		/// If <paramref name="quit"/> is <c>true</c>, a <c>QUIT</c> command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
		/// </example>
		/// <param name="quit">If set to <c>true</c>, a <c>QUIT</c> command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		public override void Disconnect (bool quit, CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					SendCommand (cancellationToken, "QUIT\r\n");
				} catch (OperationCanceledException) {
				} catch (Pop3ProtocolException) {
				} catch (Pop3CommandException) {
				} catch (IOException) {
				}
			}

			disconnecting = true;
			engine.Disconnect (null);
		}

		/// <summary>
		/// Get the message count.
		/// </summary>
		/// <remarks>
		/// Gets the message count.
		/// </remarks>
		/// <returns>The message count.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override int GetMessageCount (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return UpdateMessageCount (cancellationToken);
		}

		/// <summary>
		/// Ping the POP3 server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void NoOp (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			SendCommand (cancellationToken, "NOOP\r\n");
		}

		void OnEngineDisconnected (object sender, EventArgs e)
		{
			var options = SecureSocketOptions.None;
			bool requested = disconnecting;
			string host = null;
			int port = 0;

			if (engine.Uri != null) {
				options = GetSecureSocketOptions (engine.Uri);
				host = engine.Uri.Host;
				port = engine.Uri.Port;
			}

			engine.Disconnected -= OnEngineDisconnected;
			disconnecting = secure = utf8 = false;
			octets = total = 0;
			engine.Uri = null;

			if (host != null)
				OnDisconnected (host, port, options, requested);
		}

		#endregion

		bool CheckCanEnableUTF8 ()
		{
			CheckDisposed ();
			CheckConnected ();

			if (engine.State != Pop3EngineState.Connected)
				throw new InvalidOperationException ("You must enable UTF-8 mode before authenticating.");

			if ((engine.Capabilities & Pop3Capabilities.UTF8) == 0)
				throw new NotSupportedException ("The POP3 server does not support the UTF8 extension.");

			return !utf8;
		}

		/// <summary>
		/// Enable UTF8 mode.
		/// </summary>
		/// <remarks>
		/// The POP3 UTF8 extension allows the client to retrieve messages in the UTF-8 encoding and
		/// may also allow the user to authenticate using a UTF-8 encoded username or password.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> has already been authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UTF8 extension.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public void EnableUTF8 (CancellationToken cancellationToken = default)
		{
			if (!CheckCanEnableUTF8 ())
				return;

			SendCommand (cancellationToken, "UTF8\r\n");
			utf8 = true;
		}

		static void ReadLangResponse (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			var langs = (List<Pop3Language>) pc.UserData;

			do {
				var response = engine.ReadLine (cancellationToken);

				if (response == ".")
					break;

				var tokens = response.Split (Space, 2);
				if (tokens.Length != 2)
					continue;

				langs.Add (new Pop3Language (tokens[0], tokens[1]));
			} while (true);
		}

		static Task ProcessLangResponse (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			if (doAsync)
				return ReadLangResponseAsync (engine, pc, cancellationToken);

			ReadLangResponse (engine, pc, cancellationToken);

			return Task.CompletedTask;
		}

		Pop3Command QueueLangCommand (out List<Pop3Language> langs)
		{
			CheckDisposed ();
			CheckConnected ();

			if ((Capabilities & Pop3Capabilities.Lang) == 0)
				throw new NotSupportedException ("The POP3 server does not support the LANG extension.");

			var pc = engine.QueueCommand (ProcessLangResponse, "LANG\r\n");
			pc.UserData = langs = new List<Pop3Language> ();

			return pc;
		}

		/// <summary>
		/// Get the list of languages supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// If the POP3 server supports the LANG extension, it is possible to
		/// query the list of languages supported by the POP3 server that can
		/// be used for error messages.
		/// </remarks>
		/// <returns>The supported languages.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the LANG extension.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public IList<Pop3Language> GetLanguages (CancellationToken cancellationToken = default)
		{
			var pc = QueueLangCommand (out var langs);

			engine.Run (true, cancellationToken);

			return new ReadOnlyCollection<Pop3Language> (langs);
		}

		void CheckCanSetLanguage (string lang)
		{
			CheckDisposed ();
			CheckConnected ();

			if (lang == null)
				throw new ArgumentNullException (nameof (lang));

			if (lang.Length == 0)
				throw new ArgumentException ("The language code cannot be empty.", nameof (lang));

			if ((Capabilities & Pop3Capabilities.Lang) == 0)
				throw new NotSupportedException ("The POP3 server does not support the LANG extension.");
		}

		/// <summary>
		/// Set the language used by the POP3 server for error messages.
		/// </summary>
		/// <remarks>
		/// If the POP3 server supports the LANG extension, it is possible to
		/// set the language used by the POP3 server for error messages.
		/// </remarks>
		/// <param name="lang">The language code.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="lang"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="lang"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the LANG extension.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public void SetLanguage (string lang, CancellationToken cancellationToken = default)
		{
			CheckCanSetLanguage (lang);

			SendCommand (cancellationToken, $"LANG {lang}\r\n");
		}

		#region IMailSpool implementation

		/// <summary>
		/// Get the number of messages available in the message spool.
		/// </summary>
		/// <remarks>
		/// <para>Gets the number of messages available on the POP3 server.</para>
		/// <para>Once authenticated, the <see cref="Count"/> property will be set
		/// to the number of available messages on the POP3 server.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
		/// </example>
		/// <value>The message count.</value>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		public override int Count {
			get {
				CheckDisposed ();
				CheckConnected ();
				CheckAuthenticated ();

				return total;
			}
		}

		/// <summary>
		/// Gets whether or not the <see cref="Pop3Client"/> supports referencing messages by UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Not all servers support referencing messages by UID, so this property should
		/// be checked before using <see cref="GetMessageUid(int, CancellationToken)"/>
		/// and <see cref="GetMessageUids(CancellationToken)"/>.</para>
		/// <para>If the server does not support UIDs, then all methods that take UID arguments
		/// along with <see cref="GetMessageUid(int, CancellationToken)"/> and
		/// <see cref="GetMessageUids(CancellationToken)"/> will fail.</para>
		/// </remarks>
		/// <value><c>true</c> if supports UIDs; otherwise, <c>false</c>.</value>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		public override bool SupportsUids {
			get {
				CheckDisposed ();
				CheckConnected ();
				CheckAuthenticated ();

				return (engine.Capabilities & Pop3Capabilities.UIDL) != 0;
			}
		}

		static Task ProcessUidlResponse (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			var tokens = text.Split (Space, StringSplitOptions.RemoveEmptyEntries);
			int seqid = GetExpectedSequenceId (pc);

			if (tokens.Length < 2) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the UIDL command.");
				return Task.CompletedTask;
			}

			if (!int.TryParse (tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id != seqid) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected response to the UIDL command.");
				return Task.CompletedTask;
			}

			pc.UserData = tokens[1];

			return Task.CompletedTask;
		}

		Pop3Command QueueUidlCommand (int index)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (!SupportsUids && (probed & ProbedCapabilities.UIDL) != 0)
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			return engine.QueueCommand (ProcessUidlResponse, "UIDL {0}\r\n", index + 1);
		}

		T OnUidlComplete<T> (Pop3Command pc)
		{
			probed |= ProbedCapabilities.UIDL;

			if (pc.Status != Pop3CommandStatus.Ok && !SupportsUids)
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			pc.ThrowIfError ();

			engine.Capabilities |= Pop3Capabilities.UIDL;

			return (T) pc.UserData;
		}

		/// <summary>
		/// Get the UID of the message at the specified index.
		/// </summary>
		/// <remarks>
		/// <para>Gets the UID of the message at the specified index.</para>
		/// <note type="warning">Not all servers support UIDs, so you should first check the
		/// <see cref="Capabilities"/> property for the <see cref="Pop3Capabilities.UIDL"/> flag or
		/// the <see cref="SupportsUids"/> convenience property.</note>
		/// </remarks>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override string GetMessageUid (int index, CancellationToken cancellationToken = default)
		{
			var pc = QueueUidlCommand (index);

			engine.Run (false, cancellationToken);

			return OnUidlComplete<string> (pc);
		}

		static void ParseUidlAllResponse (Pop3Command pc, string response)
		{
			var tokens = response.Split (Space, StringSplitOptions.RemoveEmptyEntries);
			var uids = (List<string>) pc.UserData;

			if (tokens.Length < 2) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the UIDL command.");
				return;
			}

			if (!int.TryParse (tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int seqid) || seqid != uids.Count + 1) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an invalid response to the UIDL command.");
				return;
			}

			uids.Add (tokens[1]);
		}

		static void ReadUidlAllResponse (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			do {
				var response = engine.ReadLine (cancellationToken);

				if (response == ".")
					break;

				if (pc.Exception != null)
					continue;

				ParseUidlAllResponse (pc, response);
			} while (true);
		}

		static Task ProcessUidlAllResponse (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			if (doAsync)
				return ReadUidlAllResponseAsync (engine, pc, cancellationToken);

			ReadUidlAllResponse (engine, pc, cancellationToken);

			return Task.CompletedTask;
		}

		Pop3Command QueueUidlCommand ()
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (!SupportsUids && (probed & ProbedCapabilities.UIDL) != 0)
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			var pc = engine.QueueCommand (ProcessUidlAllResponse, "UIDL\r\n");
			var uids = new List<string> ();
			pc.UserData = uids;

			return pc;
		}

		/// <summary>
		/// Get the full list of available message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Gets the full list of available message UIDs.</para>
		/// <note type="warning">Not all servers support UIDs, so you should first check the
		/// <see cref="Capabilities"/> property for the <see cref="Pop3Capabilities.UIDL"/> flag or
		/// the <see cref="SupportsUids"/> convenience property.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadNewMessages"/>
		/// </example>
		/// <returns>The message uids.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<string> GetMessageUids (CancellationToken cancellationToken = default)
		{
			var pc = QueueUidlCommand ();

			engine.Run (false, cancellationToken);

			return OnUidlComplete<List<string>> (pc);
		}

		Task ProcessListResponse (Pop3Engine pop3, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			var tokens = text.Split (Space, StringSplitOptions.RemoveEmptyEntries);
			int seqid = GetExpectedSequenceId (pc);

			if (tokens.Length < 2) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the LIST command: {0}", text);
				return Task.CompletedTask;
			}

			if (!int.TryParse (tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int id) || id != seqid) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected sequence-id token to the LIST command: {0}", tokens[0]);
				return Task.CompletedTask;
			}

			if (!int.TryParse (tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size < 0) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected size token to the LIST command: {0}", tokens[1]);
				return Task.CompletedTask;
			}

			pc.UserData = size;

			return Task.CompletedTask;
		}

		Pop3Command QueueListCommand (int index)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			return engine.QueueCommand (ProcessListResponse, "LIST {0}\r\n", index + 1);
		}

		/// <summary>
		/// Get the size of the specified message, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the size of the specified message, in bytes.
		/// </remarks>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override int GetMessageSize (int index, CancellationToken cancellationToken = default)
		{
			var pc = QueueListCommand (index);

			engine.Run (true, cancellationToken);

			return (int) pc.UserData;
		}

		static void ParseListAllResponse (Pop3Command pc, string response)
		{
			var tokens = response.Split (Space, StringSplitOptions.RemoveEmptyEntries);
			var sizes = (List<int>) pc.UserData;

			if (tokens.Length < 2) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the LIST command: {0}", response);
				return;
			}

			if (!int.TryParse (tokens[0], NumberStyles.None, CultureInfo.InvariantCulture, out int seqid) || seqid != sizes.Count + 1) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected sequence-id token to the LIST command: {0}", tokens[0]);
				return;
			}

			if (!int.TryParse (tokens[1], NumberStyles.None, CultureInfo.InvariantCulture, out int size) || size < 0) {
				pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected size token to the LIST command: {0}", tokens[1]);
				return;
			}

			sizes.Add (size);
		}

		static void ReadListAllResponse (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			do {
				var response = engine.ReadLine (cancellationToken);

				if (response == ".")
					break;

				if (pc.Exception != null)
					continue;

				ParseListAllResponse (pc, response);
			} while (true);
		}

		static Task ProcessListAllResponse (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
		{
			if (pc.Status != Pop3CommandStatus.Ok)
				return Task.CompletedTask;

			if (doAsync)
				return ReadListAllResponseAsync (engine, pc, cancellationToken);

			ReadListAllResponse (engine, pc, cancellationToken);

			return Task.CompletedTask;
		}

		List<int> QueueListCommand ()
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			var pc = engine.QueueCommand (ProcessListAllResponse, "LIST\r\n");
			var sizes = new List<int> ();
			pc.UserData = sizes;

			return sizes;
		}

		/// <summary>
		/// Get the sizes for all available messages, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the sizes for all available messages, in bytes.
		/// </remarks>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<int> GetMessageSizes (CancellationToken cancellationToken = default)
		{
			var sizes = QueueListCommand ();

			engine.Run (true, cancellationToken);

			return sizes;
		}

		abstract class DownloadContext<T>
		{
			readonly ITransferProgress progress;
			readonly Pop3Client client;
			T[] downloaded;
			long nread;
			int idx;

			protected DownloadContext (Pop3Client client, ITransferProgress progress)
			{
				this.progress = progress;
				this.client = client;
			}

			protected Pop3Engine Engine {
				get { return client.engine; }
			}

			protected abstract T Parse (Pop3Stream data, CancellationToken cancellationToken);

			protected abstract Task<T> ParseAsync (Pop3Stream data, CancellationToken cancellationToken);

			protected void Update (int n)
			{
				if (progress == null)
					return;

				nread += n;

				progress.Report (nread);
			}

			void OnDataReceived (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
			{
				try {
					engine.Stream.Mode = Pop3StreamMode.Data;

					var item = Parse (engine.Stream, cancellationToken);

					downloaded[idx++] = item;
				} catch (FormatException ex) {
					pc.Exception = CreatePop3ParseException (ex, "Failed to parse data.");

					engine.Stream.CopyTo (Stream.Null, 4096);
				} finally {
					engine.Stream.Mode = Pop3StreamMode.Line;
				}
			}

			async Task OnDataReceivedAsync (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
			{
				try {
					engine.Stream.Mode = Pop3StreamMode.Data;

					var item = await ParseAsync (engine.Stream, cancellationToken).ConfigureAwait (false);

					downloaded[idx++] = item;
				} catch (FormatException ex) {
					pc.Exception = CreatePop3ParseException (ex, "Failed to parse data.");

					await engine.Stream.CopyToAsync (Stream.Null, 4096, cancellationToken).ConfigureAwait (false);
				} finally {
					engine.Stream.Mode = Pop3StreamMode.Line;
				}
			}

			Task OnDataReceived (Pop3Engine engine, Pop3Command pc, string text, bool doAsync, CancellationToken cancellationToken)
			{
				if (pc.Status != Pop3CommandStatus.Ok)
					return Task.CompletedTask;

				if (doAsync)
					return OnDataReceivedAsync (engine, pc, cancellationToken);

				OnDataReceived (engine, pc, cancellationToken);

				return Task.CompletedTask;
			}

			Pop3Command QueueCommand (int index, bool headersOnly)
			{
				if (headersOnly)
					return Engine.QueueCommand (OnDataReceived, "TOP {0} 0\r\n", index + 1);

				return Engine.QueueCommand (OnDataReceived, "RETR {0}\r\n", index + 1);
			}

			void DownloadItem (int index, bool headersOnly, CancellationToken cancellationToken)
			{
				QueueCommand (index, headersOnly);

				Engine.Run (true, cancellationToken);
			}

			async Task DownloadItemAsync (int index, bool headersOnly, CancellationToken cancellationToken)
			{
				QueueCommand (index, headersOnly);

				await Engine.RunAsync (true, cancellationToken).ConfigureAwait (false);
			}

			public T Download (int index, bool headersOnly, CancellationToken cancellationToken)
			{
				downloaded = new T[1];
				idx = 0;

				DownloadItem (index, headersOnly, cancellationToken);

				return downloaded[0];
			}

			public async Task<T> DownloadAsync (int index, bool headersOnly, CancellationToken cancellationToken)
			{
				downloaded = new T[1];
				idx = 0;

				await DownloadItemAsync (index, headersOnly, cancellationToken).ConfigureAwait (false);

				return downloaded[0];
			}

			public IList<T> Download (IList<int> indexes, bool headersOnly, CancellationToken cancellationToken)
			{
				downloaded = new T[indexes.Count];
				idx = 0;

				if ((Engine.Capabilities & Pop3Capabilities.Pipelining) == 0) {
					for (int i = 0; i < indexes.Count; i++)
						DownloadItem (indexes[i], headersOnly, cancellationToken);

					return downloaded;
				}

				for (int i = 0; i < indexes.Count; i++)
					QueueCommand (indexes[i], headersOnly);

				Engine.Run (true, cancellationToken);

				return downloaded;
			}

			public async Task<IList<T>> DownloadAsync (IList<int> indexes, bool headersOnly, CancellationToken cancellationToken)
			{
				downloaded = new T[indexes.Count];
				idx = 0;

				if ((Engine.Capabilities & Pop3Capabilities.Pipelining) == 0) {
					for (int i = 0; i < indexes.Count; i++)
						await DownloadItemAsync (indexes[i], headersOnly, cancellationToken).ConfigureAwait (false);

					return downloaded;
				}

				for (int i = 0; i < indexes.Count; i++)
					QueueCommand (indexes[i], headersOnly);

				await Engine.RunAsync (true, cancellationToken).ConfigureAwait (false);

				return downloaded;
			}

			public IList<T> Download (int startIndex, int count, bool headersOnly, CancellationToken cancellationToken)
			{
				downloaded = new T[count];
				idx = 0;

				if ((Engine.Capabilities & Pop3Capabilities.Pipelining) == 0) {
					for (int i = 0; i < count; i++)
						DownloadItem (startIndex + i, headersOnly, cancellationToken);

					return downloaded;
				}

				for (int i = 0; i < count; i++)
					QueueCommand (startIndex + i, headersOnly);

				Engine.Run (true, cancellationToken);

				return downloaded;
			}

			public async Task<IList<T>> DownloadAsync (int startIndex, int count, bool headersOnly, CancellationToken cancellationToken)
			{
				downloaded = new T[count];
				idx = 0;

				if ((Engine.Capabilities & Pop3Capabilities.Pipelining) == 0) {
					for (int i = 0; i < count; i++)
						await DownloadItemAsync (startIndex + i, headersOnly, cancellationToken).ConfigureAwait (false);

					return downloaded;
				}

				for (int i = 0; i < count; i++)
					QueueCommand (startIndex + i, headersOnly);

				await Engine.RunAsync (true, cancellationToken).ConfigureAwait (false);

				return downloaded;
			}
		}

		class DownloadStreamContext : DownloadContext<Stream>
		{
			const int BufferSize = 4096;

			public DownloadStreamContext (Pop3Client client, ITransferProgress progress = null) : base (client, progress)
			{
			}

			protected override Stream Parse (Pop3Stream data, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();

				var buffer = ArrayPool<byte>.Shared.Rent (BufferSize);
				var stream = new MemoryBlockStream ();

				try {
					int nread;

					while ((nread = data.Read (buffer, 0, BufferSize, cancellationToken)) > 0) {
						stream.Write (buffer, 0, nread);
						Update (nread);
					}

					stream.Position = 0;

					return stream;
				} catch {
					stream.Dispose ();
					throw;
				} finally {
					ArrayPool<byte>.Shared.Return (buffer);
				}
			}

			protected override async Task<Stream> ParseAsync (Pop3Stream data, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();

				var buffer = ArrayPool<byte>.Shared.Rent (BufferSize);
				var stream = new MemoryBlockStream ();

				try {
					int nread;

					while ((nread = await data.ReadAsync (buffer, 0, BufferSize, cancellationToken).ConfigureAwait (false)) > 0) {
						stream.Write (buffer, 0, nread);
						Update (nread);
					}

					stream.Position = 0;

					return stream;
				} catch {
					stream.Dispose ();
					throw;
				} finally {
					ArrayPool<byte>.Shared.Return (buffer);
				}
			}
		}

		class DownloadHeaderContext : DownloadContext<HeaderList>
		{
			readonly MimeParser parser;

			public DownloadHeaderContext (Pop3Client client, MimeParser parser) : base (client, null)
			{
				this.parser = parser;
			}

			protected override HeaderList Parse (Pop3Stream data, CancellationToken cancellationToken)
			{
				using (var stream = new ProgressStream (data, Update)) {
					parser.SetStream (stream);

					return parser.ParseMessage (cancellationToken).Headers;
				}
			}

			protected override async Task<HeaderList> ParseAsync (Pop3Stream data, CancellationToken cancellationToken)
			{
				using (var stream = new ProgressStream (data, Update)) {
					parser.SetStream (stream);

					return (await parser.ParseMessageAsync (cancellationToken).ConfigureAwait (false)).Headers;
				}
			}
		}

		class DownloadMessageContext : DownloadContext<MimeMessage>
		{
			readonly MimeParser parser;

			public DownloadMessageContext (Pop3Client client, MimeParser parser, ITransferProgress progress = null) : base (client, progress)
			{
				this.parser = parser;
			}

			protected override MimeMessage Parse (Pop3Stream data, CancellationToken cancellationToken)
			{
				using (var stream = new ProgressStream (data, Update)) {
					parser.SetStream (stream);

					return parser.ParseMessage (cancellationToken);
				}
			}

			protected override Task<MimeMessage> ParseAsync (Pop3Stream data, CancellationToken cancellationToken)
			{
				using (var stream = new ProgressStream (data, Update)) {
					parser.SetStream (stream);

					return parser.ParseMessageAsync (cancellationToken);
				}
			}
		}

		void CheckCanDownload (int index)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));
		}

		bool CheckCanDownload (IList<int> indexes)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				return false;

			for (int i = 0; i < indexes.Count; i++) {
				if (indexes[i] < 0 || indexes[i] >= total)
					throw new ArgumentException ("One or more of the indexes are invalid.", nameof (indexes));
			}

			return true;
		}

		bool CheckCanDownload (int startIndex, int count)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (startIndex < 0 || startIndex >= total)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (count < 0 || count > (total - startIndex))
				throw new ArgumentOutOfRangeException (nameof (count));

			return count > 0;
		}

		/// <summary>
		/// Get the headers for the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the headers for the message at the specified index.
		/// </remarks>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override HeaderList GetMessageHeaders (int index, CancellationToken cancellationToken = default)
		{
			CheckCanDownload (index);

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.Download (index, true, cancellationToken);
		}

		/// <summary>
		/// Get the headers for the messages at the specified indexes.
		/// </summary>
		/// <remarks>
		/// <para>Gets the headers for the messages at the specified indexes.</para>
		/// <para>When the POP3 server supports the <see cref="Pop3Capabilities.Pipelining"/>
		/// extension, this method will likely be more efficient than using
		/// <see cref="GetMessageHeaders(int,CancellationToken)"/> for each message because
		/// it will batch the commands to reduce latency.</para>
		/// </remarks>
		/// <returns>The headers for the specified messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> are invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<HeaderList> GetMessageHeaders (IList<int> indexes, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDownload (indexes))
				return Array.Empty<HeaderList> ();

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.Download (indexes, true, cancellationToken);
		}

		/// <summary>
		/// Get the headers of the messages within the specified range.
		/// </summary>
		/// <remarks>
		/// <para>Gets the headers of the messages within the specified range.</para>
		/// <para>When the POP3 server supports the <see cref="Pop3Capabilities.Pipelining"/>
		/// extension, this method will likely be more efficient than using
		/// <see cref="GetMessageHeaders(int,CancellationToken)"/> for each message because
		/// it will batch the commands to reduce latency.</para>
		/// </remarks>
		/// <returns>The headers of the messages within the specified range.</returns>
		/// <param name="startIndex">The index of the first message to get.</param>
		/// <param name="count">The number of messages to get.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="startIndex"/> and <paramref name="count"/> do not specify
		/// a valid range of messages.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<HeaderList> GetMessageHeaders (int startIndex, int count, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDownload (startIndex, count))
				return Array.Empty<HeaderList> ();

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.Download (startIndex, count, true, cancellationToken);
		}

		/// <summary>
		/// Get the message at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the message at the specified index.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
		/// </example>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override MimeMessage GetMessage (int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			CheckCanDownload (index);

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.Download (index, false, cancellationToken);
		}

		/// <summary>
		/// Get the messages at the specified indexes.
		/// </summary>
		/// <remarks>
		/// <para>Gets the messages at the specified indexes.</para>
		/// <para>When the POP3 server supports the <see cref="Pop3Capabilities.Pipelining"/>
		/// extension, this method will likely be more efficient than using
		/// <see cref="GetMessage(int,CancellationToken,ITransferProgress)"/> for each message
		/// because it will batch the commands to reduce latency.</para>
		/// </remarks>
		/// <returns>The messages.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> are invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<MimeMessage> GetMessages (IList<int> indexes, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (indexes))
				return Array.Empty<MimeMessage> ();

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.Download (indexes, false, cancellationToken);
		}

		/// <summary>
		/// Get the messages within the specified range.
		/// </summary>
		/// <remarks>
		/// <para>Gets the messages within the specified range.</para>
		/// <para>When the POP3 server supports the <see cref="Pop3Capabilities.Pipelining"/>
		/// extension, this method will likely be more efficient than using
		/// <see cref="GetMessage(int,CancellationToken,ITransferProgress)"/> for each message
		/// because it will batch the commands to reduce latency.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="BatchDownloadMessages"/>
		/// </example>
		/// <returns>The messages.</returns>
		/// <param name="startIndex">The index of the first message to get.</param>
		/// <param name="count">The number of messages to get.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="startIndex"/> and <paramref name="count"/> do not specify
		/// a valid range of messages.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<MimeMessage> GetMessages (int startIndex, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (startIndex, count))
				return Array.Empty<MimeMessage> ();

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.Download (startIndex, count, false, cancellationToken);
		}

		/// <summary>
		/// Get the message or header stream at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the message or header stream at the specified index.
		/// </remarks>
		/// <returns>The message or header stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override Stream GetStream (int index, bool headersOnly = false, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			CheckCanDownload (index);

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.Download (index, headersOnly, cancellationToken);
		}

		/// <summary>
		/// Get the message or header streams at the specified indexes.
		/// </summary>
		/// <remarks>
		/// <para>Get the message or header streams at the specified indexes.</para>
		/// <para>If the POP3 server supports the <see cref="Pop3Capabilities.Pipelining"/>
		/// extension, this method will likely be more efficient than using
		/// <see cref="GetStream(int,bool,CancellationToken,ITransferProgress)"/> for each message
		/// because it will batch the commands to reduce latency.</para>
		/// </remarks>
		/// <returns>The message or header streams.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> are invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<Stream> GetStreams (IList<int> indexes, bool headersOnly = false, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (indexes))
				return Array.Empty<Stream> ();

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.Download (indexes, headersOnly, cancellationToken);
		}

		/// <summary>
		/// Get the message or header streams within the specified range.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message or header streams within the specified range.</para>
		/// <para>If the POP3 server supports the <see cref="Pop3Capabilities.Pipelining"/>
		/// extension, this method will likely be more efficient than using
		/// <see cref="GetStream(int,bool,CancellationToken,ITransferProgress)"/> for each message
		/// because it will batch the commands to reduce latency.</para>
		/// </remarks>
		/// <returns>The message or header streams.</returns>
		/// <param name="startIndex">The index of the first stream to get.</param>
		/// <param name="count">The number of streams to get.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be retrieved; otherwise, <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="startIndex"/> and <paramref name="count"/> do not specify
		/// a valid range of messages.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IList<Stream> GetStreams (int startIndex, int count, bool headersOnly = false, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (startIndex, count))
				return Array.Empty<Stream> ();

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.Download (startIndex, count, headersOnly, cancellationToken);
		}

		void CheckCanDelete (int index, out string seqid)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			seqid = (index + 1).ToString (CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
		/// </example>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void DeleteMessage (int index, CancellationToken cancellationToken = default)
		{
			CheckCanDelete (index, out string seqid);

			SendCommand (cancellationToken, $"DELE {seqid}\r\n");
		}

		bool CheckCanDelete (IList<int> indexes)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				return false;

			for (int i = 0; i < indexes.Count; i++) {
				if (indexes[i] < 0 || indexes[i] >= total)
					throw new ArgumentException ("One or more of the indexes are invalid.", nameof (indexes));
			}

			return true;
		}

		/// <summary>
		/// Mark the specified messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> are invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void DeleteMessages (IList<int> indexes, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDelete (indexes))
				return;

			if ((Capabilities & Pop3Capabilities.Pipelining) == 0) {
				for (int i = 0; i < indexes.Count; i++)
					SendCommand (cancellationToken, "DELE {0}\r\n", indexes[i] + 1);

				return;
			}

			for (int i = 0; i < indexes.Count; i++)
				engine.QueueCommand (null, "DELE {0}\r\n", indexes[i] + 1);

			engine.Run (true, cancellationToken);
		}

		bool CheckCanDelete (int startIndex, int count)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (startIndex < 0 || startIndex >= total)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (count < 0 || count > (total - startIndex))
				throw new ArgumentOutOfRangeException (nameof (count));

			return count >= 0;
		}

		/// <summary>
		/// Mark the specified range of messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="BatchDownloadMessages"/>
		/// </example>
		/// <param name="startIndex">The index of the first message to mark for deletion.</param>
		/// <param name="count">The number of messages to mark for deletion.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="startIndex"/> and <paramref name="count"/> do not specify
		/// a valid range of messages.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void DeleteMessages (int startIndex, int count, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDelete (startIndex, count))
				return;

			if ((Capabilities & Pop3Capabilities.Pipelining) == 0) {
				for (int i = 0; i < count; i++)
					SendCommand (cancellationToken, "DELE {0}\r\n", startIndex + i + 1);

				return;
			}

			for (int i = 0; i < count; i++)
				engine.QueueCommand (null, "DELE {0}\r\n", startIndex + i + 1);

			engine.Run (true, cancellationToken);
		}

		/// <summary>
		/// Mark all messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void DeleteAllMessages (CancellationToken cancellationToken = default)
		{
			if (total > 0)
				DeleteMessages (0, total, cancellationToken);
		}

		/// <summary>
		/// Reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// The POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override void Reset (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			SendCommand (cancellationToken, "RSET\r\n");
		}

		#endregion

		#region IEnumerable<MimeMessage> implementation

		/// <summary>
		/// Get an enumerator for the messages in the folder.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the messages in the folder.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="Pop3Client"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3CommandException">
		/// A POP3 command failed.
		/// </exception>
		/// <exception cref="Pop3ProtocolException">
		/// A POP3 protocol error occurred.
		/// </exception>
		public override IEnumerator<MimeMessage> GetEnumerator ()
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			for (int i = 0; i < total; i++)
				yield return GetMessage (i, CancellationToken.None);

			yield break;
		}

		#endregion

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="Pop3Client"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="Pop3Client"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				engine.Disconnect (null);
				disposed = true;
			}

			base.Dispose (disposing);
		}
	}
}
