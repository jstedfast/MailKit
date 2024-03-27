//
// SmtpClient.cs
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
using System.Linq;
using System.Text;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Security;
using System.Globalization;
using System.Collections.Generic;
#if NET6_0_OR_GREATER
using System.Diagnostics.Metrics;
#endif
using System.Net.NetworkInformation;
using System.Security.Authentication;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.IO;
using MimeKit.Cryptography;

using MailKit.Security;

using SslStream = MailKit.Net.SslStream;
using AuthenticationException = MailKit.Security.AuthenticationException;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An SMTP client that can be used to send email messages.
	/// </summary>
	/// <remarks>
	/// <para>The <see cref="SmtpClient"/> class supports both the "smtp" and "smtps" protocols. The "smtp"
	/// protocol makes a clear-text connection to the SMTP server and does not use SSL or TLS unless the SMTP
	/// server supports the <a href="https://tools.ietf.org/html/rfc3207">STARTTLS</a> extension. The "smtps"
	/// protocol, however, connects to the SMTP server using an SSL-wrapped connection.</para>
	/// <para>The connection established by any of the
	/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Connect.htm">Connect</a> methods may be re-used if an
	/// application wishes to send multiple messages to the same SMTP server. Since connecting and authenticating
	/// can be expensive operations, re-using a connection can significantly improve performance when sending a
	/// large number of messages to the same SMTP server over a short period of time.</para>
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessages"/>
	/// </example>
	public partial class SmtpClient : MailTransport, ISmtpClient
	{
		static readonly byte[] EndData = Encoding.ASCII.GetBytes (".\r\n");
		static readonly char[] NewLineCharacters = { '\r', '\n' };
		internal static string DefaultLocalDomain;
		const int MaxLineLength = 998;

		enum SmtpCommand {
			MailFrom,
			RcptTo
		}

		readonly HashSet<string> authenticationMechanisms = new HashSet<string> (StringComparer.Ordinal);
		readonly SmtpAuthenticationSecretDetector detector = new SmtpAuthenticationSecretDetector ();
		readonly List<SmtpCommand> queued = new List<SmtpCommand> ();
		SslCertificateValidationInfo sslValidationInfo;
#if NET6_0_OR_GREATER
		readonly ClientMetrics metrics;
#endif
		long clientConnectedTimestamp;
		SmtpCapabilities capabilities;
		int timeout = 2 * 60 * 1000;
		bool authenticated;
		bool connected;
		bool disposed;
		bool secure;
		Uri uri;

		internal static string GetSafeHostName (string hostName)
		{
			var idn = new IdnMapping ();

			if (!string.IsNullOrEmpty (hostName)) {
				hostName = hostName.Replace ('_', '-');

				try {
					return idn.GetAscii (hostName);
				} catch {
					// This can happen if the hostName contains illegal unicode characters.
					var ascii = new StringBuilder ();
					for (int i = 0; i < hostName.Length; i++) {
						if (hostName[i] <= 0x7F)
							ascii.Append (hostName[i]);
					}

					return ascii.Length > 0 ? ascii.ToString () : null;
				}
			} else {
				return null;
			}
		}

		static SmtpClient ()
		{
			var hostName = GetSafeHostName (IPGlobalProperties.GetIPGlobalProperties ().HostName);

			DefaultLocalDomain = hostName ?? "localhost";
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can send messages with the <see cref="SmtpClient"/>, you must first call one of
		/// the <a href="Overload_MailKit_Net_Smtp_SmtpClient_Connect.htm">Connect</a> methods.
		/// Depending on whether the SMTP server requires authenticating or not, you may also need to
		/// authenticate using one of the
		/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Authenticate.htm">Authenticate</a> methods.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessages"/>
		/// </example>
		public SmtpClient () : this (new NullProtocolLogger ())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can send messages with the <see cref="SmtpClient"/>, you must first call one of
		/// the <a href="Overload_MailKit_Net_Smtp_SmtpClient_Connect.htm">Connect</a> methods.
		/// Depending on whether the SMTP server requires authenticating or not, you may also need to
		/// authenticate using one of the
		/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Authenticate.htm">Authenticate</a> methods.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ProtocolLogger"/>
		/// </example>
		public SmtpClient (IProtocolLogger protocolLogger) : base (protocolLogger)
		{
			protocolLogger.AuthenticationSecretDetector = detector;

#if NET6_0_OR_GREATER
			// Use the globally configured SmtpClient metrics.
			metrics = Telemetry.SmtpClient.Metrics;
#endif
		}

#if NET8_0_OR_GREATER
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can send messages with the <see cref="SmtpClient"/>, you must first call one of
		/// the <a href="Overload_MailKit_Net_Smtp_SmtpClient_Connect.htm">Connect</a> methods.
		/// Depending on whether the SMTP server requires authenticating or not, you may also need to
		/// authenticate using one of the
		/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Authenticate.htm">Authenticate</a> methods.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <param name="meterFactory">The meter factory.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="protocolLogger"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="meterFactory"/> is <c>null</c>.</para>
		/// </exception>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ProtocolLogger"/>
		/// </example>
		public SmtpClient (IProtocolLogger protocolLogger, IMeterFactory meterFactory) : base (protocolLogger)
		{
			if (meterFactory == null)
				throw new ArgumentNullException (nameof (meterFactory));

			protocolLogger.AuthenticationSecretDetector = detector;

			var meter = meterFactory.Create (Telemetry.SmtpClient.MeterName, Telemetry.SmtpClient.MeterVersion);
			metrics = Telemetry.SmtpClient.CreateMetrics (meter);
		}
#endif

		/// <summary>
		/// Get the underlying SMTP stream.
		/// </summary>
		/// <remarks>
		/// Gets the underlying SMTP stream.
		/// </remarks>
		/// <value>The SMTP stream.</value>
		SmtpStream Stream {
			get; set;
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets an object that can be used to synchronize access to the SMTP server between multiple threads.</para>
		/// <para>When using <see cref="SmtpClient"/> methods from multiple threads, it is important to lock the
		/// <see cref="SyncRoot"/> object for thread safety.</para>
		/// </remarks>
		/// <value>The lock object.</value>
		public override object SyncRoot {
			get { return this; }
		}

		/// <summary>
		/// Get the protocol supported by the message service.
		/// </summary>
		/// <remarks>
		/// Gets the protocol supported by the message service.
		/// </remarks>
		/// <value>The protocol.</value>
		protected override string Protocol {
			get { return "smtp"; }
		}

		/// <summary>
		/// Get the capabilities supported by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection has been made
		/// and may change once the client is authenticated.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The capabilities.</value>
		/// <exception cref="System.ArgumentException">
		/// Capabilities cannot be enabled, they may only be disabled.
		/// </exception>
		public SmtpCapabilities Capabilities {
			get { return capabilities; }
			set {
				if ((capabilities | value) > capabilities)
					throw new ArgumentException ("Capabilities cannot be enabled, they may only be disabled.", nameof (value));

				capabilities = value;
			}
		}

		/// <summary>
		/// Get or set the local domain.
		/// </summary>
		/// <remarks>
		/// The local domain is used in the HELO or EHLO commands sent to
		/// the SMTP server. If left unset, the local IP address will be
		/// used instead.
		/// </remarks>
		/// <value>The local domain.</value>
		public string LocalDomain {
			get; set;
		}

		/// <summary>
		/// Get whether or not the BDAT command is preferred over the DATA command.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the <c>BDAT</c> command is preferred over the standard <c>DATA</c>
		/// command.</para>
		/// <para>The <c>BDAT</c> command is normally only used when the message being sent contains binary data
		/// (e.g. one mor more MIME parts contains a <c>Content-Transfer-Encoding: binary</c> header). This
		/// option provides a way to override this behavior, forcing the <see cref="SmtpClient"/> to send
		/// messages using the <c>BDAT</c> command instead of the <c>DATA</c> command even when it is not
		/// necessary to do so.</para>
		/// </remarks>
		/// <value><c>true</c> if the <c>BDAT</c> command is preferred over the <c>DATA</c> command; otherwise, <c>false</c>.</value>
		protected virtual bool PreferSendAsBinaryData {
			get { return false; }
		}

		/// <summary>
		/// Get the maximum message size supported by the server.
		/// </summary>
		/// <remarks>
		/// <para>The maximum message size will not be known until a successful connection has
		/// been made and may change once the client is authenticated.</para>
		/// <note type="note">This value is only relevant if the <see cref="Capabilities"/> includes
		/// the <see cref="SmtpCapabilities.Size"/> flag.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The maximum message size supported by the server.</value>
		public uint MaxSize {
			get; private set;
		}

		/// <summary>
		/// Get or set whether the client should use the REQUIRETLS extension if it is available.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets whether the client should use the REQUIRETLS extension if it is available.</para>
		/// <para>The REQUIRETLS extension (as defined in rfc8689) is a way to ensure that every SMTP server
		/// that a message passes through on its way to the recipient is required to use a TLS connection in
		/// order to transfer the message to the next SMTP server.</para>
		/// <note type="note">This feature is only available if <see cref="Capabilities"/> contains the
		/// <see cref="SmtpCapabilities.RequireTLS"/> flag when sending the message.</note>
		/// </remarks>
		/// <value><c>true</c> if the REQUIRETLS extension should be used; otherwise, <c>false</c>.</value>
		public bool RequireTLS {
			get; set;
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException (nameof (SmtpClient));
		}

		#region IMailService implementation

		/// <summary>
		/// Get the authentication mechanisms supported by the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>The authentication mechanisms are queried as part of the connection
		/// process.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before authenticating.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The authentication mechanisms.</value>
		public override HashSet<string> AuthenticationMechanisms {
			get { return authenticationMechanisms; }
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
				if (IsConnected && Stream.CanTimeout) {
					Stream.WriteTimeout = value;
					Stream.ReadTimeout = value;
				}

				timeout = value;
			}
		}

		/// <summary>
		/// Get whether or not the client is currently connected to an SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>The <see cref="IsConnected"/> state is set to <c>true</c> immediately after
		/// one of the <a href="Overload_MailKit_Net_Smtp_SmtpClient_Connect.htm">Connect</a>
		/// methods succeeds and is not set back to <c>false</c> until either the client
		/// is disconnected via <see cref="Disconnect(bool,CancellationToken)"/> or until an
		/// <see cref="SmtpProtocolException"/> is thrown while attempting to read or write to
		/// the underlying network socket.</para>
		/// <para>When an <see cref="SmtpProtocolException"/> is caught, the connection state of the
		/// <see cref="SmtpClient"/> should be checked before continuing.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExceptionHandling"/>
		/// </example>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public override bool IsConnected {
			get { return connected; }
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
			get { return IsSecure && (Stream.Stream is SslStream sslStream) && sslStream.IsEncrypted; }
		}

		/// <summary>
		/// Get whether or not the connection is signed (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is signed (typically via SSL or TLS).
		/// </remarks>
		/// <value><c>true</c> if the connection is signed; otherwise, <c>false</c>.</value>
		public override bool IsSigned {
			get { return IsSecure && (Stream.Stream is SslStream sslStream) && sslStream.IsSigned; }
		}

		/// <summary>
		/// Get the negotiated SSL or TLS protocol version.
		/// </summary>
		/// <remarks>
		/// <para>Gets the negotiated SSL or TLS protocol version once an SSL or TLS connection has been made.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS protocol version.</value>
		public override SslProtocols SslProtocol {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS cipher algorithm.</value>
		public override CipherAlgorithmType? SslCipherAlgorithm {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS cipher algorithm strength.</value>
		public override int? SslCipherStrength {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS hash algorithm.</value>
		public override HashAlgorithmType? SslHashAlgorithm {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS hash algorithm strength.</value>
		public override int? SslHashStrength {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS key exchange algorithm.</value>
		public override ExchangeAlgorithmType? SslKeyExchangeAlgorithm {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SslConnectionInformation"/>
		/// </example>
		/// <value>The negotiated SSL or TLS key exchange algorithm strength.</value>
		public override int? SslKeyExchangeStrength {
			get {
				if (IsSecure && (Stream.Stream is SslStream sslStream))
					return sslStream.KeyExchangeStrength;

				return null;
			}
		}

		/// <summary>
		/// Get whether or not the client is currently authenticated with the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the client is currently authenticated with the SMTP server.</para>
		/// <para>To authenticate with the SMTP server, use one of the
		/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Authenticate.htm">Authenticate</a>
		/// methods.</para>
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public override bool IsAuthenticated {
			get { return authenticated; }
		}

		NetworkOperation StartNetworkOperation (NetworkOperationKind kind)
		{
#if NET6_0_OR_GREATER
			return NetworkOperation.Start (kind, uri, Telemetry.SmtpClient.ActivitySource, metrics);
#else
			return NetworkOperation.Start (kind, uri);
#endif
		}

		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			bool valid;

			sslValidationInfo?.Dispose ();
			sslValidationInfo = null;

			if (ServerCertificateValidationCallback != null) {
				valid = ServerCertificateValidationCallback (uri.Host, certificate, chain, sslPolicyErrors);
			} else if (ServicePointManager.ServerCertificateValidationCallback != null) {
				valid = ServicePointManager.ServerCertificateValidationCallback (uri.Host, certificate, chain, sslPolicyErrors);
			} else {
				valid = DefaultServerCertificateValidationCallback (uri.Host, certificate, chain, sslPolicyErrors);
			}

			if (!valid) {
				// Note: The SslHandshakeException.Create() method will nullify this once it's done using it.
				sslValidationInfo = new SslCertificateValidationInfo (sender, certificate, chain, sslPolicyErrors);
			}

			return valid;
		}

		/// <summary>
		/// Invoked only when no recipients were accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// If <see cref="OnRecipientNotAccepted"/> is overridden to not throw
		/// an exception, this method should be overridden to throw an appropriate
		/// exception instead.
		/// </remarks>
		/// <param name="message">The message being sent.</param>
		protected virtual void OnNoRecipientsAccepted (MimeMessage message)
		{
		}

		void QueueCommand (SmtpCommand type, string command, CancellationToken cancellationToken)
		{
			Stream.QueueCommand (command, cancellationToken);
			queued.Add (type);
		}

		struct QueueResults
		{
			public readonly int RecipientsAccepted;
			public Exception FirstException;

			public QueueResults (int recipientsAccepted, Exception firstException)
			{
				RecipientsAccepted = recipientsAccepted;
				FirstException = firstException;
			}
		}

		QueueResults ParseCommandQueueResponses (MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, List<SmtpResponse> responses, Exception readResponseException)
		{
			Exception firstException = null;
			int recipientsAccepted = 0;
			int rcpt = 0;

			try {
				// process the responses
				for (int i = 0; i < responses.Count; i++) {
					switch (queued[i]) {
					case SmtpCommand.MailFrom:
						try {
							ParseMailFromResponse (message, sender, responses[i]);
						} catch (Exception ex) {
							firstException ??= ex;
						}
						break;
					case SmtpCommand.RcptTo:
						try {
							if (ParseRcptToResponse (message, recipients[rcpt++], responses[i]))
								recipientsAccepted++;
						} catch (Exception ex) {
							firstException ??= ex;
						}
						break;
					}
				}
			} finally {
				queued.Clear ();
			}

			return new QueueResults (recipientsAccepted, firstException ?? readResponseException);
		}

		QueueResults FlushCommandQueue (MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, CancellationToken cancellationToken)
		{
			try {
				// Note: Queued commands are buffered by the stream
				Stream.Flush (cancellationToken);
			} catch {
				queued.Clear ();
				throw;
			}

			var responses = new List<SmtpResponse> (queued.Count);
			Exception rex = null;

			// Note: We need to read all responses from the server before we can process
			// them in case any of them have any errors so that we can RSET the state.
			try {
				for (int i = 0; i < queued.Count; i++) {
					var response = Stream.ReadResponse (cancellationToken);
					responses.Add (response);
				}
			} catch (Exception ex) {
				// Note: Most likely this exception is due to an unexpected disconnect.
				// Usually, before an SMTP server disconnects the client, it will send an
				// error code response that will be more useful to the user than an error
				// stating that the server has unexpected disconnected. Save this exception
				// in case the server didn't give us a response with an error code.
				rex = ex;
			}

			return ParseCommandQueueResponses (message, sender, recipients, responses, rex);
		}

		SmtpResponse SendCommandInternal (string command, CancellationToken cancellationToken)
		{
			try {
				return Stream.SendCommand (command, cancellationToken);
			} catch {
				Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), false);
				throw;
			}
		}

		/// <summary>
		/// Send a custom command to the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Sends a custom command to the SMTP server.</para>
		/// <note type="note">The command string should not include the terminating <c>\r\n</c> sequence.</note>
		/// </remarks>
		/// <returns>The command response.</returns>
		/// <param name="command">The command.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="command"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol exception occurred.
		/// </exception>
		protected SmtpResponse SendCommand (string command, CancellationToken cancellationToken = default)
		{
			if (command == null)
				throw new ArgumentNullException (nameof (command));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient must be connected before you can send commands.");

			if (!command.EndsWith ("\r\n", StringComparison.Ordinal))
				command += "\r\n";

			return SendCommandInternal (command, cancellationToken);
		}

		static bool ReadNextLine (string text, ref int index, out int lineStartIndex, out int lineEndIndex)
		{
			lineStartIndex = 0;
			lineEndIndex = 0;

			if (index >= text.Length)
				return false;

			lineStartIndex = index;
			lineEndIndex = index;

			do {
				char c = text[index++];

				if (c == '\n')
					break;

				// Only update lineEndIndex when we see a non-whitespace character. This effectively Trim()'s the end.
				if (!char.IsWhiteSpace (c))
					lineEndIndex = index;
			} while (index < text.Length);

			return true;
		}

		static bool IsCapability (string capability, string text, int startIndex, int endIndex, bool hasValue = false)
		{
			int length = endIndex - startIndex;

			if (hasValue) {
				if (length <= capability.Length)
					return false;
			} else {
				if (length != capability.Length)
					return false;
			}

			if (string.Compare (text, startIndex, capability, 0, capability.Length, StringComparison.OrdinalIgnoreCase) != 0)
				return false;

			if (hasValue) {
				int index = startIndex + capability.Length;

				return length > capability.Length && (text[index] == ' ' || text[index] == '=');
			}

			return true;
		}

		void AddAuthenticationMechanisms (string mechanisms, int startIndex, int endIndex)
		{
			int index = startIndex;

			do {
				while (index < endIndex && char.IsWhiteSpace (mechanisms[index]))
					index++;

				int mechanismIndex = index;

				while (index < endIndex && !char.IsWhiteSpace (mechanisms[index]))
					index++;

				if (index > mechanismIndex) {
					var mechanism = mechanisms.Substring (mechanismIndex, index - mechanismIndex);

					AuthenticationMechanisms.Add (mechanism);
				}
			} while (index < endIndex);
		}

		void SetMaxSize (string capability, int startIndex, int endIndex)
		{
			int index = startIndex;

			while (index < endIndex && char.IsWhiteSpace (capability[index]))
				index++;

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
			var value = capability.AsSpan (index, endIndex - index);
#else
			var value = capability.Substring (index, endIndex - index);
#endif

			if (index < endIndex && uint.TryParse (value, NumberStyles.None, CultureInfo.InvariantCulture, out uint size))
				MaxSize = size;
		}

		void UpdateCapabilities (SmtpResponse response)
		{
			// Clear the extensions except STARTTLS so that this capability stays set after a STARTTLS command.
			capabilities &= SmtpCapabilities.StartTLS;
			AuthenticationMechanisms.Clear ();
			MaxSize = 0;

			string text = response.Response;
			int index = 0;

			while (ReadNextLine (text, ref index, out int lineStartIndex, out int lineEndIndex)) {
				if (IsCapability ("AUTH", text, lineStartIndex, lineEndIndex, true)) {
					int startIndex = lineStartIndex + 5;

					AddAuthenticationMechanisms (text, startIndex, lineEndIndex);
					capabilities |= SmtpCapabilities.Authentication;
				} else if (IsCapability ("X-EXPS", text, lineStartIndex, lineEndIndex, true)) {
					int startIndex = lineStartIndex + 7;

					AddAuthenticationMechanisms (text, startIndex, lineEndIndex);
					capabilities |= SmtpCapabilities.Authentication;
				} else if (IsCapability ("SIZE", text, lineStartIndex, lineEndIndex, true)) {
					int startIndex = lineStartIndex + 5;

					SetMaxSize (text, startIndex, lineEndIndex);
					capabilities |= SmtpCapabilities.Size;
				} else if (IsCapability ("DSN", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.Dsn;
				} else if (IsCapability ("BINARYMIME", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.BinaryMime;
				} else if (IsCapability ("CHUNKING", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.Chunking;
				} else if (IsCapability ("ENHANCEDSTATUSCODES", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.EnhancedStatusCodes;
				} else if (IsCapability ("8BITMIME", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.EightBitMime;
				} else if (IsCapability ("PIPELINING", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.Pipelining;
				} else if (IsCapability ("STARTTLS", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.StartTLS;
				} else if (IsCapability ("SMTPUTF8", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.UTF8;
				} else if (IsCapability ("REQUIRETLS", text, lineStartIndex, lineEndIndex)) {
					capabilities |= SmtpCapabilities.RequireTLS;
				}
			}
		}

		string CreateEhloCommand (string helo)
		{
			string domain;

			if (!string.IsNullOrEmpty (LocalDomain)) {
				if (IPAddress.TryParse (LocalDomain, out var ip)) {
					if (ip.IsIPv4MappedToIPv6) {
						try {
							ip = ip.MapToIPv4 ();
						} catch (ArgumentOutOfRangeException) {
							// .NET 4.5.2 bug on Windows 7 SP1 (issue #814)
						}
					}

					if (ip.AddressFamily == AddressFamily.InterNetworkV6)
						return string.Format ("{0} [IPv6:{1}]\r\n", helo, ip);

					return string.Format ("{0} [{1}]\r\n", helo, ip);
				} else {
					domain = LocalDomain;
				}
			} else {
				domain = DefaultLocalDomain;
			}

			return string.Format ("{0} {1}\r\n", helo, domain);
		}

		SmtpResponse SendEhlo (bool connecting, string helo, CancellationToken cancellationToken)
		{
			var command = CreateEhloCommand (helo);

			if (connecting)
				return Stream.SendCommand (command, cancellationToken);

			return SendCommandInternal (command, cancellationToken);
		}

		void Ehlo (bool connecting, CancellationToken cancellationToken)
		{
			var response = SendEhlo (connecting, "EHLO", cancellationToken);

			if (response.StatusCode != SmtpStatusCode.Ok) {
				// Try sending HELO instead...
				response = SendEhlo (connecting, "HELO", cancellationToken);

				if (response.StatusCode != SmtpStatusCode.Ok)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			} else {
				UpdateCapabilities (response);
			}
		}

		void ValidateArguments (SaslMechanism mechanism)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient must be connected before you can authenticate.");

			if (IsAuthenticated)
				throw new InvalidOperationException ("The SmtpClient is already authenticated.");

			if ((capabilities & SmtpCapabilities.Authentication) == 0)
				throw new NotSupportedException ("The SMTP server does not support authentication.");

			mechanism.ChannelBindingContext = Stream.Stream as IChannelBindingContext;
			mechanism.Uri = new Uri ($"smtp://{uri.Host}");
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="SmtpClient"/> is already authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The SMTP server does not support authentication.
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
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default)
		{
			ValidateArguments (mechanism);

			cancellationToken.ThrowIfCancellationRequested ();

			using var operation = StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				SaslException saslException = null;
				SmtpResponse response;
				string challenge;
				string command;

				// send an initial challenge if the mechanism supports it
				if (mechanism.SupportsInitialResponse) {
					challenge = mechanism.Challenge (null, cancellationToken);
					command = string.Format ("AUTH {0} {1}\r\n", mechanism.MechanismName, challenge);
				} else {
					command = string.Format ("AUTH {0}\r\n", mechanism.MechanismName);
				}

				detector.IsAuthenticating = true;

				try {
					response = SendCommandInternal (command, cancellationToken);

					if (response.StatusCode == SmtpStatusCode.AuthenticationMechanismTooWeak)
						throw new AuthenticationException (response.Response);

					try {
						while (response.StatusCode == SmtpStatusCode.AuthenticationChallenge) {
							challenge = mechanism.Challenge (response.Response, cancellationToken);
							response = SendCommandInternal (challenge + "\r\n", cancellationToken);
						}

						saslException = null;
					} catch (SaslException ex) {
						// reset the authentication state
						response = SendCommandInternal ("\r\n", cancellationToken);
						saslException = ex;
					}
				} finally {
					detector.IsAuthenticating = false;
				}

				if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful) {
					if (mechanism.NegotiatedSecurityLayer)
						Ehlo (false, cancellationToken);
					authenticated = true;
					OnAuthenticated (response.Response);
					return;
				}

				var message = string.Format (CultureInfo.InvariantCulture, "{0}: {1}", (int) response.StatusCode, response.Response);

				if (saslException != null)
					throw new AuthenticationException (message, saslException);

				throw new AuthenticationException (message);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		void ValidateArguments (Encoding encoding, ICredentials credentials)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (credentials == null)
				throw new ArgumentNullException (nameof (credentials));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient must be connected before you can authenticate.");

			if (IsAuthenticated)
				throw new InvalidOperationException ("The SmtpClient is already authenticated.");

			if ((capabilities & SmtpCapabilities.Authentication) == 0)
				throw new NotSupportedException ("The SMTP server does not support authentication.");
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the supplied credentials.</para>
		/// <para>If the SMTP server supports authentication, then the SASL mechanisms
		/// that both the client and server support (not including any OAUTH mechanisms)
		/// are tried in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support, the
		/// credentials are used to authenticate.</para>
		/// <para>If, on the other hand, authentication is not supported by the SMTP
		/// server, then this method will throw <see cref="System.NotSupportedException"/>.
		/// The <see cref="Capabilities"/> property can be checked for the
		/// <see cref="SmtpCapabilities.Authentication"/> flag to make sure the
		/// SMTP server supports authentication before calling this method.</para>
		/// <note type="tip"> To prevent the usage of certain authentication mechanisms,
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="SmtpClient"/> is already authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The SMTP server does not support authentication.
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
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
		{
			ValidateArguments (encoding, credentials);

			using var operation = StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				var saslUri = new Uri ($"smtp://{uri.Host}");
				AuthenticationException authException = null;
				SaslException saslException;
				SmtpResponse response;
				SaslMechanism sasl;
				bool tried = false;
				string challenge;
				string command;

				foreach (var authmech in SaslMechanism.Rank (AuthenticationMechanisms)) {
					var cred = credentials.GetCredential (uri, authmech);

					if ((sasl = SaslMechanism.Create (authmech, encoding, cred)) == null)
						continue;

					sasl.ChannelBindingContext = Stream.Stream as IChannelBindingContext;
					sasl.Uri = saslUri;

					tried = true;

					cancellationToken.ThrowIfCancellationRequested ();

					// send an initial challenge if the mechanism supports it
					if (sasl.SupportsInitialResponse) {
						challenge = sasl.Challenge (null, cancellationToken);
						command = string.Format ("AUTH {0} {1}\r\n", authmech, challenge);
					} else {
						command = string.Format ("AUTH {0}\r\n", authmech);
					}

					detector.IsAuthenticating = true;
					saslException = null;

					try {
						response = SendCommandInternal (command, cancellationToken);

						if (response.StatusCode == SmtpStatusCode.AuthenticationMechanismTooWeak)
							continue;

						try {
							while (!sasl.IsAuthenticated) {
								if (response.StatusCode != SmtpStatusCode.AuthenticationChallenge)
									break;

								challenge = sasl.Challenge (response.Response, cancellationToken);
								response = SendCommandInternal (challenge + "\r\n", cancellationToken);
							}

							saslException = null;
						} catch (SaslException ex) {
							// reset the authentication state
							response = SendCommandInternal ("\r\n", cancellationToken);
							saslException = ex;
						}
					} finally {
						detector.IsAuthenticating = false;
					}

					if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful) {
						if (sasl.NegotiatedSecurityLayer)
							Ehlo (false, cancellationToken);
						authenticated = true;
						OnAuthenticated (response.Response);
						return;
					}

					var message = string.Format (CultureInfo.InvariantCulture, "{0}: {1}", (int) response.StatusCode, response.Response);
					Exception inner;

					if (saslException != null)
						inner = new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response, saslException);
					else
						inner = new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

					authException = new AuthenticationException (message, inner);
				}

				if (tried)
					throw authException ?? new AuthenticationException ();

				throw new NotSupportedException ("No compatible authentication mechanisms found.");
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
					port = 25;
				break;
			case SecureSocketOptions.Auto:
				switch (port) {
				case 0: port = 25; goto default;
				case 465: options = SecureSocketOptions.SslOnConnect; break;
				default: options = SecureSocketOptions.StartTlsWhenAvailable; break;
				}
				break;
			case SecureSocketOptions.SslOnConnect:
				if (port == 0)
					port = 465;
				break;
			}

			if (IPAddress.TryParse (host, out var ip) && ip.AddressFamily == AddressFamily.InterNetworkV6)
				host = "[" + host + "]";

			switch (options) {
			case SecureSocketOptions.StartTlsWhenAvailable:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "smtp://{0}:{1}/?starttls=when-available", host, port));
				starttls = true;
				break;
			case SecureSocketOptions.StartTls:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "smtp://{0}:{1}/?starttls=always", host, port));
				starttls = true;
				break;
			case SecureSocketOptions.SslOnConnect:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "smtps://{0}:{1}", host, port));
				starttls = false;
				break;
			default:
				uri = new Uri (string.Format (CultureInfo.InvariantCulture, "smtp://{0}:{1}", host, port));
				starttls = false;
				break;
			}
		}

		void SslHandshake (SslStream ssl, string host, CancellationToken cancellationToken)
		{
#if NET5_0_OR_GREATER
			ssl.AuthenticateAsClient (GetSslClientAuthenticationOptions (host, ValidateRemoteCertificate));
#else
			ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
#endif
		}

		void RecordClientDisconnected (Exception ex)
		{
#if NET6_0_OR_GREATER
			metrics?.RecordClientDisconnected (clientConnectedTimestamp, uri, ex);
#endif
			clientConnectedTimestamp = 0;
		}

		void PostConnect (Stream stream, string host, int port, SecureSocketOptions options, bool starttls, CancellationToken cancellationToken)
		{
			clientConnectedTimestamp = Stopwatch.GetTimestamp ();

			try {
				ProtocolLogger.LogConnect (uri);
			} catch {
				stream.Dispose ();
				secure = false;
				throw;
			}

			Stream = new SmtpStream (stream, ProtocolLogger);

			try {
				// read the greeting
				var response = Stream.ReadResponse (cancellationToken);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				Ehlo (true, cancellationToken);

				if (options == SecureSocketOptions.StartTls && (capabilities & SmtpCapabilities.StartTLS) == 0)
					throw new NotSupportedException ("The SMTP server does not support the STARTTLS extension.");

				if (starttls && (capabilities & SmtpCapabilities.StartTLS) != 0) {
					response = Stream.SendCommand ("STARTTLS\r\n", cancellationToken);
					if (response.StatusCode != SmtpStatusCode.ServiceReady)
						throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

					try {
						var tls = new SslStream (stream, false, ValidateRemoteCertificate);
						Stream.Stream = tls;

						SslHandshake (tls, host, cancellationToken);
					} catch (Exception ex) {
						throw SslHandshakeException.Create (ref sslValidationInfo, ex, true, "SMTP", host, port, 465, 25, 587);
					}

					secure = true;

					// Send EHLO again and get the new list of supported extensions
					Ehlo (true, cancellationToken);
				}

				connected = true;
			} catch (Exception ex) {
				RecordClientDisconnected (ex);
				Stream.Dispose ();
				secure = false;
				Stream = null;
				throw;
			}

			OnConnected (host, port, options);
		}

		void ValidateArguments (string host, int port)
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			CheckDisposed ();

			if (IsConnected)
				throw new InvalidOperationException ("The SmtpClient is already connected.");
		}

		/// <summary>
		/// Establish a connection to the specified SMTP or SMTP/S server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified SMTP or SMTP/S server.</para>
		/// <para>If the <paramref name="port"/> has a value of <c>0</c>, then the
		/// <paramref name="options"/> parameter is used to determine the default port to
		/// connect to. The default port used with <see cref="SecureSocketOptions.SslOnConnect"/>
		/// is <c>465</c>. All other values will use a default port of <c>25</c>.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>465</c>, then the default options used will be
		/// <see cref="SecureSocketOptions.SslOnConnect"/>. All other values will use
		/// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/>.</para>
		/// <para>Once a connection is established, properties such as
		/// <see cref="AuthenticationMechanisms"/> and <see cref="Capabilities"/> will be
		/// populated.</para>
		/// <note type="note">The connection established by any of the
		/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_Connect.htm">Connect</a>
		/// methods may be re-used if an application wishes to send multiple messages
		/// to the same SMTP server. Since connecting and authenticating can be expensive
		/// operations, re-using a connection can significantly improve performance when
		/// sending a large number of messages to the same SMTP server over a short
		/// period of time.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
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
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="SmtpClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the SMTP server does not support the STARTTLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
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
		/// <exception cref="SmtpCommandException">
		/// An SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			ValidateArguments (host, port);

			capabilities = SmtpCapabilities.None;
			AuthenticationMechanisms.Clear ();
			MaxSize = 0;

			ComputeDefaultValues (host, ref port, ref options, out uri, out var starttls);

			using var operation = StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				var stream = ConnectNetwork (host, port, cancellationToken);
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						SslHandshake (ssl, host, cancellationToken);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "SMTP", host, port, 465, 25, 587);
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

		void ValidateArguments (Socket socket, string host, int port)
		{
			if (socket == null)
				throw new ArgumentNullException (nameof (socket));

			if (!socket.Connected)
				throw new ArgumentException ("The socket is not connected.", nameof (socket));

			ValidateArguments (host, port);
		}

		/// <summary>
		/// Establish a connection to the specified SMTP or SMTP/S server using the provided socket.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified SMTP or SMTP/S server using the provided socket.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>465</c>, then the default options used will be
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
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="SmtpClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the SMTP server does not support the STARTTLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// An SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			ValidateArguments (socket, host, port);

			Connect (new NetworkStream (socket, true), host, port, options, cancellationToken);
		}

		void ValidateArguments (Stream stream, string host, int port)
		{
			if (stream == null)
				throw new ArgumentNullException (nameof (stream));

			ValidateArguments (host, port);
		}

		/// <summary>
		/// Establish a connection to the specified SMTP or SMTP/S server using the provided stream.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified SMTP or SMTP/S server using the provided stream.</para>
		/// <para>If the <paramref name="options"/> has a value of
		/// <see cref="SecureSocketOptions.Auto"/>, then the <paramref name="port"/> is used
		/// to determine the default security options. If the <paramref name="port"/> has a value
		/// of <c>465</c>, then the default options used will be
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
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="SmtpClient"/> is already connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <paramref name="options"/> was set to
		/// <see cref="MailKit.Security.SecureSocketOptions.StartTls"/>
		/// and the SMTP server does not support the STARTTLS extension.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// An SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void Connect (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			ValidateArguments (stream, host, port);

			capabilities = SmtpCapabilities.None;
			AuthenticationMechanisms.Clear ();
			MaxSize = 0;

			ComputeDefaultValues (host, ref port, ref options, out uri, out var starttls);

			using var operation = StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				Stream network;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						SslHandshake (ssl, host, cancellationToken);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "SMTP", host, port, 465, 25, 587);
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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="quit">If set to <c>true</c>, a <c>QUIT</c> command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		public override void Disconnect (bool quit, CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!IsConnected)
				return;

			if (quit) {
				try {
					Stream.SendCommand ("QUIT\r\n", cancellationToken);
				} catch (OperationCanceledException) {
				} catch (SmtpProtocolException) {
				} catch (SmtpCommandException) {
				} catch (IOException) {
				}
			}

			Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), true);
		}

		/// <summary>
		/// Ping the SMTP server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void NoOp (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var response = SendCommandInternal ("NOOP\r\n", cancellationToken);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		void Disconnect (string host, int port, SecureSocketOptions options, bool requested)
		{
			RecordClientDisconnected (null);

			capabilities = SmtpCapabilities.None;
			authenticated = false;
			connected = false;
			secure = false;
			queued.Clear ();
			uri = null;

			if (Stream != null) {
				Stream.Dispose ();
				Stream = null;
			}

			if (host != null)
				OnDisconnected (host, port, options, requested);
		}

#endregion

		#region IMailTransport implementation

		static MailboxAddress GetMessageSender (MimeMessage message)
		{
			if (message.ResentSender != null)
				return message.ResentSender;

			if (message.ResentFrom.Count > 0)
				return message.ResentFrom.Mailboxes.FirstOrDefault ();

			if (message.Sender != null)
				return message.Sender;

			return message.From.Mailboxes.FirstOrDefault ();
		}

		static void AddUnique (List<MailboxAddress> recipients, HashSet<string> unique, IEnumerable<MailboxAddress> mailboxes)
		{
			foreach (var mailbox in mailboxes) {
				if (unique.Add (mailbox.Address))
					recipients.Add (mailbox);
			}
		}

		static IList<MailboxAddress> GetMessageRecipients (MimeMessage message)
		{
			var unique = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			var recipients = new List<MailboxAddress> ();

			if (message.ResentSender != null || message.ResentFrom.Count > 0) {
				AddUnique (recipients, unique, message.ResentTo.Mailboxes);
				AddUnique (recipients, unique, message.ResentCc.Mailboxes);
				AddUnique (recipients, unique, message.ResentBcc.Mailboxes);
			} else {
				AddUnique (recipients, unique, message.To.Mailboxes);
				AddUnique (recipients, unique, message.Cc.Mailboxes);
				AddUnique (recipients, unique, message.Bcc.Mailboxes);
			}

			return recipients;
		}

		[Flags]
		enum SmtpExtensions {
			None         = 0,
			EightBitMime = 1 << 0,
			BinaryMime   = 1 << 1,
			UTF8         = 1 << 2,
		}

		class ContentTransferEncodingVisitor : MimeVisitor
		{
			readonly SmtpCapabilities capabilities;

			public ContentTransferEncodingVisitor (SmtpCapabilities capabilities)
			{
				this.capabilities = capabilities;
			}

			public SmtpExtensions SmtpExtensions {
				get; private set;
			}

			protected override void VisitMimePart (MimePart entity)
			{
				switch (entity.ContentTransferEncoding) {
				case ContentEncoding.EightBit:
					if ((capabilities & SmtpCapabilities.EightBitMime) != 0)
						SmtpExtensions |= SmtpExtensions.EightBitMime;
					break;
				case ContentEncoding.Binary:
					if ((capabilities & SmtpCapabilities.BinaryMime) != 0)
						SmtpExtensions |= SmtpExtensions.BinaryMime;
					break;
				}
			}
		}

		/// <summary>
		/// Invoked when the sender is accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The default implementation does nothing.
		/// </remarks>
		/// <param name="message">The message being sent.</param>
		/// <param name="mailbox">The mailbox used in the <c>MAIL FROM</c> command.</param>
		/// <param name="response">The response to the <c>MAIL FROM</c> command.</param>
		protected virtual void OnSenderAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
		}

		/// <summary>
		/// Invoked when a recipient is not accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The default implementation throws an appropriate <see cref="SmtpCommandException"/>.
		/// </remarks>
		/// <param name="message">The message being sent.</param>
		/// <param name="mailbox">The mailbox used in the <c>MAIL FROM</c> command.</param>
		/// <param name="response">The response to the <c>MAIL FROM</c> command.</param>
		protected virtual void OnSenderNotAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
			throw new SmtpCommandException (SmtpErrorCode.SenderNotAccepted, response.StatusCode, mailbox, response.Response);
		}

		/// <summary>
		/// Get the envelope identifier to be used with delivery status notifications.
		/// </summary>
		/// <remarks>
		/// <para>The envelope identifier, if non-empty, is useful in determining which message a delivery
		/// status notification was issued for.</para>
		/// <para>The envelope identifier should be unique and may be up to 100 characters in length, but
		/// must consist only of printable ASCII characters and no white space.</para>
		/// <para>For more information, see
		/// <a href="https://tools.ietf.org/html/rfc3461#section-4.4">rfc3461, section 4.4</a>.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="DeliveryStatusNotification"/>
		/// </example>
		/// <returns>The envelope identifier.</returns>
		/// <param name="message">The message.</param>
		protected virtual string GetEnvelopeId (MimeMessage message)
		{
			return null;
		}

		/// <summary>
		/// Get or set how much of the message to include in any failed delivery status notifications.
		/// </summary>
		/// <remarks>
		/// Gets or sets how much of the message to include in any failed delivery status notifications.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="DeliveryStatusNotification"/>
		/// </example>
		/// <value>A value indicating how much of the message to include in a failure delivery status notification.</value>
		public DeliveryStatusNotificationType DeliveryStatusNotificationType {
			get; set;
		}

		static void AppendHexEncoded (StringBuilder builder, string value)
		{
			int index = 0;

			while (index < value.Length) {
				char c = value[index];

				if (c < 33 || c > 126 || c == (byte) '+' || c == (byte) '=')
					break;

				index++;
			}

			builder.Append (value, 0, index);

			if (index == value.Length)
				return;

			int length = value.Length - index;
			var buffer = ArrayPool<byte>.Shared.Rent (length * 3);

			try {
				int n = Encoding.UTF8.GetBytes (value, index, length, buffer, 0);
				const string HexAlphabet = "0123456789ABCDEF";

				for (index = 0; index < n; index++) {
					byte c = buffer[index];

					if (c >= 33 && c <= 126 && c != (byte) '+' && c != (byte) '=') {
						builder.Append ((char) c);
					} else {
						builder.Append ('+');
						builder.Append (HexAlphabet[(c >> 4) & 0xF]);
						builder.Append (HexAlphabet[c & 0xF]);
					}
				}
			} finally {
				ArrayPool<byte>.Shared.Return (buffer);
			}
		}

		string CreateMailFromCommand (FormatOptions options, MimeMessage message, MailboxAddress mailbox, SmtpExtensions extensions, long size)
		{
			var idnEncode = (extensions & SmtpExtensions.UTF8) == 0;
			var builder = new StringBuilder ("MAIL FROM:<");

			var addrspec = mailbox.GetAddress (idnEncode);
			builder.Append (addrspec);
			builder.Append ('>');

			if (!idnEncode)
				builder.Append (" SMTPUTF8");

			if ((Capabilities & SmtpCapabilities.Size) != 0 && size != -1) {
				builder.Append (" SIZE=");
				builder.Append (size.ToString (CultureInfo.InvariantCulture));
			}

			if ((extensions & SmtpExtensions.BinaryMime) != 0)
				builder.Append (" BODY=BINARYMIME");
			else if ((extensions & SmtpExtensions.EightBitMime) != 0)
				builder.Append (" BODY=8BITMIME");

			if ((capabilities & SmtpCapabilities.Dsn) != 0) {
				var envid = GetEnvelopeId (message);

				if (!string.IsNullOrEmpty (envid)) {
					builder.Append (" ENVID=");
					AppendHexEncoded (builder, envid);
				}

				switch (DeliveryStatusNotificationType) {
				case DeliveryStatusNotificationType.HeadersOnly:
					builder.Append (" RET=HDRS");
					break;
				case DeliveryStatusNotificationType.Full:
					builder.Append (" RET=FULL");
					break;
				}
			}

			if (RequireTLS && (Capabilities & SmtpCapabilities.RequireTLS) != 0) {
				// Check to see if the message has a TLS-Required header. If it does, then the only defined value it can have is "No".
				var index = message.Headers.IndexOf (HeaderId.TLSRequired);

				if (index == -1)
					builder.Append (" REQUIRETLS");
			}

			builder.Append ("\r\n");

			return builder.ToString ();
		}

		void ParseMailFromResponse (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
			if (response.StatusCode >= SmtpStatusCode.Ok && response.StatusCode < (SmtpStatusCode) 260) {
				OnSenderAccepted (message, mailbox, response);
				return;
			}

			if (response.StatusCode == SmtpStatusCode.AuthenticationRequired)
				throw new ServiceNotAuthenticatedException (response.Response);

			OnSenderNotAccepted (message, mailbox, response);
		}

		void MailFrom (FormatOptions options, MimeMessage message, MailboxAddress mailbox, SmtpExtensions extensions, long size, bool pipeline, CancellationToken cancellationToken)
		{
			var command = CreateMailFromCommand (options, message, mailbox, extensions, size);

			if (pipeline) {
				QueueCommand (SmtpCommand.MailFrom, command, cancellationToken);
				return;
			}

			var response = Stream.SendCommand (command, cancellationToken);

			ParseMailFromResponse (message, mailbox, response);
		}

		/// <summary>
		/// Invoked when a recipient is accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The default implementation does nothing.
		/// </remarks>
		/// <param name="message">The message being sent.</param>
		/// <param name="mailbox">The mailbox used in the <c>RCPT TO</c> command.</param>
		/// <param name="response">The response to the <c>RCPT TO</c> command.</param>
		protected virtual void OnRecipientAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
		}

		/// <summary>
		/// Invoked when a recipient is not accepted by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The default implementation throws an appropriate <see cref="SmtpCommandException"/>.
		/// </remarks>
		/// <param name="message">The message being sent.</param>
		/// <param name="mailbox">The mailbox used in the <c>RCPT TO</c> command.</param>
		/// <param name="response">The response to the <c>RCPT TO</c> command.</param>
		protected virtual void OnRecipientNotAccepted (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
			throw new SmtpCommandException (SmtpErrorCode.RecipientNotAccepted, response.StatusCode, mailbox, response.Response);
		}

		/// <summary>
		/// Get the types of delivery status notification desired for the specified recipient mailbox.
		/// </summary>
		/// <remarks>
		/// Gets the types of delivery status notification desired for the specified recipient mailbox.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="DeliveryStatusNotification"/>
		/// </example>
		/// <returns>The desired delivery status notification type.</returns>
		/// <param name="message">The message being sent.</param>
		/// <param name="mailbox">The recipient mailbox.</param>
		protected virtual DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
		{
			return null;
		}

		class OriginalRecipient
		{
			public readonly string AddrType;
			public readonly string Address;

			public OriginalRecipient (string addrType, string address)
			{
				AddrType = addrType;
				Address = address;
			}
		}

		/// <summary>
		/// Get the original intended recipient address and address type.
		/// </summary>
		/// <remarks>
		/// <para>Gets the original intended recipient address and address type for the purpose of delivery status notification.</para>
		/// <para>When initially submitting a message via SMTP, the address returned by this method MUST be identical to the <paramref name="mailbox"/>
		/// address. Likewise, when a mailing list submits a message via SMTP to be distributed to the list subscribers, the address returned by
		/// this method MUST match the new RCPT TO address of each recipient, not the address specified by the original sender of the message.)</para>
		/// </remarks>
		/// <param name="message">The message being sent.</param>
		/// <param name="mailbox">The recipient mailbox.</param>
		/// <returns>The original recipient address and the address type.</returns>
		OriginalRecipient GetOriginalRecipientAddress (MimeMessage message, MailboxAddress mailbox)
		{
			var idnEncode = (Capabilities & SmtpCapabilities.UTF8) == 0;

			return new OriginalRecipient ("rfc822", mailbox.GetAddress (idnEncode));
		}

		static string GetNotifyString (DeliveryStatusNotification notify)
		{
			string value = string.Empty;

			if (notify == DeliveryStatusNotification.Never)
				return "NEVER";

			if ((notify & DeliveryStatusNotification.Success) != 0)
				value += "SUCCESS,";

			if ((notify & DeliveryStatusNotification.Failure) != 0)
				value += "FAILURE,";

			if ((notify & DeliveryStatusNotification.Delay) != 0)
				value += "DELAY";

			return value.TrimEnd (',');
		}

		string CreateRcptToCommand (FormatOptions options, MimeMessage message, MailboxAddress mailbox)
		{
			var idnEncode = (Capabilities & SmtpCapabilities.UTF8) == 0;
			var command = new StringBuilder ("RCPT TO:<");

			command.Append (mailbox.GetAddress (idnEncode));
			command.Append ('>');

			if ((capabilities & SmtpCapabilities.Dsn) != 0) {
				var notify = GetDeliveryStatusNotifications (message, mailbox);

				if (notify.HasValue) {
					command.Append (" NOTIFY=");
					command.Append (GetNotifyString (notify.Value));

					var orcpt = GetOriginalRecipientAddress (message, mailbox);
					command.Append (" ORCPT=");
					command.Append (orcpt.AddrType);
					command.Append (';');
					AppendHexEncoded (command, orcpt.Address);
				}
			}

			command.Append ("\r\n");

			return command.ToString ();
		}

		bool ParseRcptToResponse (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
			if (response.StatusCode < (SmtpStatusCode) 300) {
				OnRecipientAccepted (message, mailbox, response);
				return true;
			}

			if (response.StatusCode == SmtpStatusCode.AuthenticationRequired)
				throw new ServiceNotAuthenticatedException (response.Response);

			OnRecipientNotAccepted (message, mailbox, response);

			return false;
		}

		bool RcptTo (FormatOptions options, MimeMessage message, MailboxAddress mailbox, bool pipeline, CancellationToken cancellationToken)
		{
			var command = CreateRcptToCommand (options, message, mailbox);

			if (pipeline) {
				QueueCommand (SmtpCommand.RcptTo, command, cancellationToken);
				return false;
			}

			var response = Stream.SendCommand (command, cancellationToken);

			return ParseRcptToResponse (message, mailbox, response);
		}

		class SendContext
		{
			readonly ITransferProgress progress;
			readonly long size;
			long nwritten;

			public SendContext (ITransferProgress progress, long size)
			{
				this.progress = progress;
				this.size = size;
			}

			public void Update (int n)
			{
				nwritten += n;

				if (size != -1)
					progress.Report (nwritten, size);
				else
					progress.Report (nwritten);
			}
		}

		string ParseBdatResponse (MimeMessage message, SmtpResponse response)
		{
			switch (response.StatusCode) {
			default:
				throw new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, response.StatusCode, response.Response);
			case SmtpStatusCode.AuthenticationRequired:
				throw new ServiceNotAuthenticatedException (response.Response);
			case SmtpStatusCode.Ok:
				OnMessageSent (new MessageSentEventArgs (message, response.Response));
				return response.Response;
			}
		}

		string Bdat (FormatOptions options, MimeMessage message, long size, CancellationToken cancellationToken, ITransferProgress progress)
		{
			var command = string.Format (CultureInfo.InvariantCulture, "BDAT {0} LAST\r\n", size);

			Stream.QueueCommand (command, cancellationToken);

			if (progress != null) {
				var ctx = new SendContext (progress, size);

				using (var stream = new ProgressStream (Stream, ctx.Update)) {
					message.WriteTo (options, stream, cancellationToken);
					stream.Flush (cancellationToken);
				}
			} else {
				message.WriteTo (options, Stream, cancellationToken);
				Stream.Flush (cancellationToken);
			}

			var response = Stream.ReadResponse (cancellationToken);

			return ParseBdatResponse (message, response);
		}

		static void ParseDataResponse (SmtpResponse response)
		{
			if (response.StatusCode != SmtpStatusCode.StartMailInput)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		string ParseMessageDataResponse (MimeMessage message, SmtpResponse response)
		{
			switch (response.StatusCode) {
			default:
				throw new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, response.StatusCode, response.Response);
			case SmtpStatusCode.AuthenticationRequired:
				throw new ServiceNotAuthenticatedException (response.Response);
			case SmtpStatusCode.Ok:
				OnMessageSent (new MessageSentEventArgs (message, response.Response));
				return response.Response;
			}
		}

		string MessageData (FormatOptions options, MimeMessage message, long size, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (progress != null) {
				var ctx = new SendContext (progress, size);

				using (var stream = new ProgressStream (Stream, ctx.Update)) {
					using (var filtered = new FilteredStream (stream)) {
						filtered.Add (new SmtpDataFilter ());

						message.WriteTo (options, filtered, cancellationToken);
						filtered.Flush (cancellationToken);
					}
				}
			} else {
				using (var filtered = new FilteredStream (Stream)) {
					filtered.Add (new SmtpDataFilter ());

					message.WriteTo (options, filtered, cancellationToken);
					filtered.Flush (cancellationToken);
				}
			}

			Stream.Write (EndData, 0, EndData.Length, cancellationToken);
			Stream.Flush (cancellationToken);

			var response = Stream.ReadResponse (cancellationToken);

			return ParseMessageDataResponse (message, response);
		}

		void Reset (CancellationToken cancellationToken)
		{
			var response = SendCommandInternal ("RSET\r\n", cancellationToken);

			if (response.StatusCode != SmtpStatusCode.Ok)
				Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), false);
		}

		/// <summary>
		/// Prepare the message for transport with the specified constraints.
		/// </summary>
		/// <remarks>
		/// <para>Prepares the message for transport with the specified constraints.</para>
		/// <para>Typically, this involves calling <see cref="MimeMessage.Prepare(EncodingConstraint, int)"/> on
		/// the message with the provided constraints.</para>
		/// </remarks>
		/// <param name="options">The format options.</param>
		/// <param name="message">The message.</param>
		/// <param name="constraint">The encoding constraint.</param>
		/// <param name="maxLineLength">The max line length supported by the server.</param>
		protected virtual void Prepare (FormatOptions options, MimeMessage message, EncodingConstraint constraint, int maxLineLength)
		{
			if (!message.Headers.Contains (HeaderId.DomainKeySignature) &&
				!message.Headers.Contains (HeaderId.DkimSignature) &&
				!message.Headers.Contains (HeaderId.ArcSeal)) {
				// prepare the message
				message.Prepare (constraint, maxLineLength);
			} else {
				// Note: we do not want to risk reformatting of headers to the international
				// UTF-8 encoding, so disable it.
				options.International = false;
			}
		}

		/// <summary>
		/// Get the size of the message.
		/// </summary>
		/// <remarks>
		/// <para>Calculates the size of the message in bytes.</para>
		/// <para>This method is called by <a href="Overload_MailKit_MailTransport_Send.htm">Send</a>
		/// methods in the following conditions:</para>
		/// <list type="bullet">
		/// <item>The SMTP server supports the <c>SIZE=</c> parameter in the <c>MAIL FROM</c> command.</item>
		/// <item>The <see cref="ITransferProgress"/> parameter is non-null.</item>
		/// <item>The SMTP server supports the <c>CHUNKING</c> extension.</item>
		/// </list>
		/// </remarks>
		/// <returns>The size of the message, in bytes.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		protected virtual long GetSize (FormatOptions options, MimeMessage message, CancellationToken cancellationToken)
		{
			using (var measure = new MeasuringStream ()) {
				message.WriteTo (options, measure, cancellationToken);

				return measure.Length;
			}
		}

		FormatOptions Prepare (FormatOptions options, MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, out SmtpExtensions extensions)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;
			format.EnsureNewLine = true;

			if (format.International && (Capabilities & SmtpCapabilities.UTF8) == 0)
				format.International = false;

			if (format.International && (Capabilities & SmtpCapabilities.EightBitMime) == 0)
				throw new NotSupportedException ("The SMTP server does not support the 8BITMIME extension.");

			EncodingConstraint constraint;

			if ((Capabilities & SmtpCapabilities.BinaryMime) != 0)
				constraint = EncodingConstraint.None;
			else if ((Capabilities & SmtpCapabilities.EightBitMime) != 0)
				constraint = EncodingConstraint.EightBit;
			else
				constraint = EncodingConstraint.SevenBit;

			Prepare (format, message, constraint, MaxLineLength);

			// figure out which SMTP extensions we need to use
			var visitor = new ContentTransferEncodingVisitor (capabilities);
			visitor.Visit (message);

			extensions = visitor.SmtpExtensions;

			if ((Capabilities & SmtpCapabilities.UTF8) != 0 && (format.International || sender.IsInternational || recipients.Any (x => x.IsInternational)))
				extensions |= SmtpExtensions.UTF8;

			return format;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		bool UseBdatCommand (SmtpExtensions extensions)
		{
			return (extensions & SmtpExtensions.BinaryMime) != 0 || (PreferSendAsBinaryData && (Capabilities & (SmtpCapabilities.BinaryMime | SmtpCapabilities.Chunking)) != 0);
		}

		string Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, CancellationToken cancellationToken, ITransferProgress progress)
		{
			var format = Prepare (options, message, sender, recipients, out var extensions);
			var pipeline = (capabilities & SmtpCapabilities.Pipelining) != 0;
			var bdat = UseBdatCommand (extensions);
			long size;

			if (bdat || (Capabilities & SmtpCapabilities.Size) != 0 || progress != null) {
				size = GetSize (format, message, cancellationToken);
			} else {
				size = -1;
			}

			using var operation = StartNetworkOperation (NetworkOperationKind.Send);

			try {
				// Note: if PIPELINING is supported, MailFrom() and RcptTo() will
				// queue their commands instead of sending them immediately.
				MailFrom (format, message, sender, extensions, size, pipeline, cancellationToken);

				int recipientsAccepted = 0;
				for (int i = 0; i < recipients.Count; i++) {
					if (RcptTo (format, message, recipients[i], pipeline, cancellationToken))
						recipientsAccepted++;
				}

				if (queued.Count > 0) {
					// Note: if PIPELINING is supported, this will flush all outstanding
					// MAIL FROM and RCPT TO commands to the server and then process
					// all of their responses.
					var results = FlushCommandQueue (message, sender, recipients, cancellationToken);

					recipientsAccepted = results.RecipientsAccepted;

					if (results.FirstException != null)
						throw results.FirstException;
				}

				if (recipientsAccepted == 0) {
					OnNoRecipientsAccepted (message);
					throw new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "No recipients were accepted.");
				}

				if (bdat)
					return Bdat (format, message, size, cancellationToken, progress);

				var dataResponse = Stream.SendCommand ("DATA\r\n", cancellationToken);

				ParseDataResponse (dataResponse);
				dataResponse = null;

				return MessageData (format, message, size, cancellationToken, progress);
			} catch (ServiceNotAuthenticatedException ex) {
				operation.SetError (ex);

				// do not disconnect
				Reset (cancellationToken);
				throw;
			} catch (SmtpCommandException ex) {
				operation.SetError (ex);

				// do not disconnect
				Reset (cancellationToken);
				throw;
			} catch (Exception ex) {
				operation.SetError (ex);

				Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), false);
				throw;
			}
		}

		static void ValidateArguments (FormatOptions options, MimeMessage message, out MailboxAddress sender, out IList<MailboxAddress> recipients)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (message == null)
				throw new ArgumentNullException (nameof (message));

			recipients = GetMessageRecipients (message);
			sender = GetMessageSender (message);

			if (sender == null)
				throw new InvalidOperationException ("No sender has been specified.");

			if (recipients.Count == 0)
				throw new InvalidOperationException ("No recipients have been specified.");
		}

		/// <summary>
		/// Send the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Sends the specified message.</para>
		/// <para>The sender address is determined by checking the following
		/// message headers (in order of precedence): Resent-Sender,
		/// Resent-From, Sender, and From.</para>
		/// <para>If either the Resent-Sender or Resent-From addresses are present,
		/// the recipients are collected from the Resent-To, Resent-Cc, and
		/// Resent-Bcc headers, otherwise the To, Cc, and Bcc headers are used.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessageWithOptions"/>
		/// </example>
		/// <returns>The final free-form text response from the server.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol exception occurred.
		/// </exception>
		public override string Send (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			ValidateArguments (options, message, out var sender, out var recipients);

			return Send (options, message, sender, recipients, cancellationToken, progress);
		}

		static List<MailboxAddress> ValidateArguments (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (message == null)
				throw new ArgumentNullException (nameof (message));

			if (sender == null)
				throw new ArgumentNullException (nameof (sender));

			if (recipients == null)
				throw new ArgumentNullException (nameof (recipients));

			var unique = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			var rcpts = new List<MailboxAddress> ();

			AddUnique (rcpts, unique, recipients);

			if (rcpts.Count == 0)
				throw new InvalidOperationException ("No recipients have been specified.");

			return rcpts;
		}

		/// <summary>
		/// Send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Sends the message by uploading it to an SMTP server using the supplied sender and recipients.
		/// </remarks>
		/// <returns>The final free-form text response from the server.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="sender">The mailbox address to use for sending the message.</param>
		/// <param name="recipients">The mailbox addresses that should receive the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="sender"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="recipients"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before sending a message.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol exception occurred.
		/// </exception>
		public override string Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var rcpts = ValidateArguments (options, message, sender, recipients);

			return Send (options, message, sender, rcpts, cancellationToken, progress);
		}

#endregion

		string CreateExpandCommand (string alias)
		{
			if (alias == null)
				throw new ArgumentNullException (nameof (alias));

			if (alias.Length == 0)
				throw new ArgumentException ("The alias cannot be empty.", nameof (alias));

			if (alias.IndexOfAny (NewLineCharacters) != -1)
				throw new ArgumentException ("The alias cannot contain newline characters.", nameof (alias));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			return string.Format ("EXPN {0}\r\n", alias);
		}

		static InternetAddressList ParseExpandResponse (SmtpResponse response)
		{
			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

			var lines = response.Response.Split ('\n');
			var list = new InternetAddressList ();

			for (int i = 0; i < lines.Length; i++) {
				if (InternetAddress.TryParse (lines[i], out var address))
					list.Add (address);
			}

			return list;
		}

		/// <summary>
		/// Expand a mailing address alias.
		/// </summary>
		/// <remarks>
		/// Expands a mailing address alias.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="ExpandAlias"/>
		/// </example>
		/// <returns>The expanded list of mailbox addresses.</returns>
		/// <param name="alias">The mailing address alias.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="alias"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="alias"/> is an empty string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before expanding an alias.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol exception occurred.
		/// </exception>
		public InternetAddressList Expand (string alias, CancellationToken cancellationToken = default)
		{
			var response = SendCommandInternal (CreateExpandCommand (alias), cancellationToken);

			return ParseExpandResponse (response);
		}

		string CreateVerifyCommand (string address)
		{
			if (address == null)
				throw new ArgumentNullException (nameof (address));

			if (address.Length == 0)
				throw new ArgumentException ("The address cannot be empty.", nameof (address));

			if (address.IndexOfAny (NewLineCharacters) != -1)
				throw new ArgumentException ("The address cannot contain newline characters.", nameof (address));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			return string.Format ("VRFY {0}\r\n", address);
		}

		static MailboxAddress ParseVerifyResponse (SmtpResponse response)
		{
			if (response.StatusCode == SmtpStatusCode.Ok)
				return MailboxAddress.Parse (response.Response);

			throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		/// <summary>
		/// Verify the existence of a mailbox address.
		/// </summary>
		/// <remarks>
		/// Verifies the existence a mailbox address with the SMTP server, returning the expanded
		/// mailbox address if it exists.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="VerifyAddress"/>
		/// </example>
		/// <returns>The expanded mailbox address.</returns>
		/// <param name="address">The mailbox address.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="address"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="address"/> is an empty string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// Authentication is required before verifying the existence of an address.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// The SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol exception occurred.
		/// </exception>
		public MailboxAddress Verify (string address, CancellationToken cancellationToken = default)
		{
			var response = SendCommandInternal (CreateVerifyCommand (address), cancellationToken);

			return ParseVerifyResponse (response);
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="SmtpClient"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="SmtpClient"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && !disposed) {
				disposed = true;
				Disconnect (null, 0, SecureSocketOptions.None, false);
			}

			base.Dispose (disposed);
		}
	}
}
