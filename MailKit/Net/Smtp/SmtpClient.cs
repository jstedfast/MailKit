//
// SmtpClient.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
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

using MimeKit;
using MimeKit.IO;
using System.Threading.Tasks;

#if NETFX_CORE
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Socket = Windows.Networking.Sockets.StreamSocket;
using Encoding = Portable.Text.Encoding;
#else
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
#endif

using MailKit.Security;

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
	/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessages" />
	/// </example>
	public class SmtpClient : MailTransport
	{
		static readonly byte[] EndData = Encoding.ASCII.GetBytes ("\r\n.\r\n");
		static readonly string [] BrokenSmtpServersThatResetStateAfterAuthEhlo = {
			// Note: Some broken SMTP servers reset their state if they receive an EHLO
			// command after authenticating even though the specifications explicitly
			// state that clients SHOULD send EHLO again after authenticating.
			// See https://github.com/jstedfast/MailKit/issues/162 for details.
			//
			// Don't you love non RFC-compliant mail servers?
			"smtp.strato.de", "smtp.sina.com", "smtp.dm.aliyun.com", "mail.shaw.ca"
		};
		const int MaxLineLength = 998;

		enum SmtpCommand {
			MailFrom,
			RcptTo
		}

		readonly HashSet<string> authenticationMechanisms = new HashSet<string> ();
		readonly List<SmtpCommand> queued = new List<SmtpCommand> ();
		SmtpCapabilities capabilities;
		int timeout = 100000;
		bool authenticated;
		bool connected;
		bool disposed;
		bool secure;
		string host;

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
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessages" />
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
		}

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
		/// <para>Gets an object that can be used to synchronize access to the SMTP server.</para>
		/// <para>When using the non-Async methods from multiple threads, it is important to lock the
		/// <see cref="SyncRoot"/> object for thread safety when using the synchronous methods.</para>
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
		/// Gets or sets the local domain.
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

#if !NETFX_CORE
		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (ServerCertificateValidationCallback != null)
				return ServerCertificateValidationCallback (host, certificate, chain, sslPolicyErrors);

#if !COREFX
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (host, certificate, chain, sslPolicyErrors);
#endif

			return DefaultServerCertificateValidationCallback (sender, certificate, chain, sslPolicyErrors);
		}
#endif

		void QueueCommand (SmtpCommand type, string command, CancellationToken cancellationToken)
		{
			var bytes = Encoding.UTF8.GetBytes (command + "\r\n");

			// Note: queued commands will be buffered by the stream
			Stream.Write (bytes, 0, bytes.Length, cancellationToken);
			queued.Add (type);
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

		void FlushCommandQueue (MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, CancellationToken cancellationToken)
		{
			if (queued.Count == 0)
				return;

			try {
				var responses = new List<SmtpResponse> ();
				Exception rex = null;
				int count = 0;
				int rcpt = 0;

				// Note: queued commands are buffered by the stream
				Stream.Flush (cancellationToken);

				// Note: we need to read all responses from the server before we can process
				// them in case any of them have any errors so that we can RSET the state.
				try {
					for (int i = 0; i < queued.Count; i++)
						responses.Add (Stream.ReadResponse (cancellationToken));
				} catch (Exception ex) {
					// Note: save this exception for later (it may be related to
					// an error response for a MAIL FROM or RCPT TO command).
					rex = ex;
				}

				for (int i = 0; i < responses.Count; i++) {
					switch (queued[i]) {
					case SmtpCommand.MailFrom:
						ProcessMailFromResponse (message, sender, responses[i]);
						break;
					case SmtpCommand.RcptTo:
						if (ProcessRcptToResponse (message, recipients[rcpt++], responses[i]))
							count++;
						break;
					}
				}

				if (count == 0)
					OnNoRecipientsAccepted (message);

				if (rex != null)
					throw new SmtpProtocolException ("Error reading a response from the SMTP server.", rex);
			} finally {
				queued.Clear ();
			}
		}

		SmtpResponse SendCommand (string command, CancellationToken cancellationToken)
		{
			var bytes = Encoding.UTF8.GetBytes (command + "\r\n");

			Stream.Write (bytes, 0, bytes.Length, cancellationToken);
			Stream.Flush (cancellationToken);

			return Stream.ReadResponse (cancellationToken);
		}

		SmtpResponse SendEhlo (bool ehlo, CancellationToken cancellationToken)
		{
			string command = ehlo ? "EHLO " : "HELO ";

#if !NETFX_CORE
			string domain = null;
			IPAddress ip = null;

			if (!string.IsNullOrEmpty (LocalDomain)) {
				if (!IPAddress.TryParse (LocalDomain, out ip))
					domain = LocalDomain;
			} else if (Stream.Socket != null) {
				var ipEndPoint = Stream.Socket.LocalEndPoint as IPEndPoint;

				if (ipEndPoint == null)
					domain = ((DnsEndPoint) Stream.Socket.LocalEndPoint).Host;
				else
					ip = ipEndPoint.Address;
			} else {
				domain = "[127.0.0.1]";
			}

			if (ip != null) {
				if (ip.AddressFamily == AddressFamily.InterNetworkV6)
					domain = "[IPv6:" + ip + "]";
				else
					domain = "[" + ip + "]";
			}

			command += domain;
#else
			if (!string.IsNullOrEmpty (LocalDomain))
				command += LocalDomain;
			else if (!string.IsNullOrEmpty (Stream.Socket.Information.LocalAddress.CanonicalName))
				command += Stream.Socket.Information.LocalAddress.CanonicalName;
			else
				command += "localhost.localdomain";
#endif

			return SendCommand (command, cancellationToken);
		}

		void Ehlo (CancellationToken cancellationToken)
		{
			SmtpResponse response;

			response = SendEhlo (true, cancellationToken);

			// Some SMTP servers do not accept an EHLO after authentication (despite the rfc saying it is required).
			if (authenticated && response.StatusCode == SmtpStatusCode.BadCommandSequence)
				return;

			if (response.StatusCode != SmtpStatusCode.Ok) {
				// Try sending HELO instead...
				response = SendEhlo (false, cancellationToken);
				if (response.StatusCode != SmtpStatusCode.Ok)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			} else {
				// Clear the extensions
				capabilities = SmtpCapabilities.None;
				AuthenticationMechanisms.Clear ();
				MaxSize = 0;

				var lines = response.Response.Split ('\n');
				for (int i = 0; i < lines.Length; i++) {
					// Outlook.com replies with "250-8bitmime" instead of "250-8BITMIME"
					// (strangely, it correctly capitalizes all other extensions...)
					var capability = lines[i].Trim ().ToUpperInvariant ();

					if (capability.StartsWith ("AUTH", StringComparison.Ordinal)) {
						int index = 4;

						capabilities |= SmtpCapabilities.Authentication;

						if (index < capability.Length && capability[index] == '=')
							index++;

						var mechanisms = capability.Substring (index);
						foreach (var mechanism in mechanisms.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
							AuthenticationMechanisms.Add (mechanism);
					} else if (capability.StartsWith ("SIZE", StringComparison.Ordinal)) {
						int index = 4;
						uint size;

						capabilities |= SmtpCapabilities.Size;

						while (index < capability.Length && char.IsWhiteSpace (capability[index]))
							index++;

						if (uint.TryParse (capability.Substring (index), out size))
							MaxSize = size;
					} else if (capability == "DSN") {
						capabilities |= SmtpCapabilities.Dsn;
					} else if (capability == "BINARYMIME") {
						capabilities |= SmtpCapabilities.BinaryMime;
					} else if (capability == "CHUNKING") {
						capabilities |= SmtpCapabilities.Chunking;
					} else if (capability == "ENHANCEDSTATUSCODES") {
						capabilities |= SmtpCapabilities.EnhancedStatusCodes;
					} else if (capability == "8BITMIME") {
						capabilities |= SmtpCapabilities.EightBitMime;
					} else if (capability == "PIPELINING") {
						capabilities |= SmtpCapabilities.Pipelining;
					} else if (capability == "STARTTLS") {
						capabilities |= SmtpCapabilities.StartTLS;
					} else if (capability == "SMTPUTF8") {
						capabilities |= SmtpCapabilities.UTF8;
					}
				}
			}
		}

		static bool IsBrokenSmtpServerThatResetsStateAfterAuthEhlo (string host)
		{
			for (int i = 0; i < BrokenSmtpServersThatResetStateAfterAuthEhlo.Length; i++) {
				if (host.Equals (BrokenSmtpServersThatResetStateAfterAuthEhlo [i], StringComparison.OrdinalIgnoreCase))
					return true;
			}

			return false;
		}

		/// <summary>
		/// Authenticates using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the SMTP server supports authentication, then the SASL mechanisms
		/// that both the client and server support are tried in order of greatest
		/// security to weakest security. Once a SASL authentication mechanism is
		/// found that both client and server support, the credentials are used to
		/// authenticate.</para>
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
		public override void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken))
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

			var uri = new Uri ("smtp://" + host);
			SaslException authException = null;
			SmtpResponse response;
			SaslMechanism sasl;
			bool tried = false;
			string challenge;
			string command;

			foreach (var authmech in SaslMechanism.AuthMechanismRank) {
				if (!AuthenticationMechanisms.Contains (authmech))
					continue;

				if ((sasl = SaslMechanism.Create (authmech, uri, encoding, credentials)) == null)
					continue;

				tried = true;

				cancellationToken.ThrowIfCancellationRequested ();

				// send an initial challenge if the mechanism supports it
				if (sasl.SupportsInitialResponse) {
					challenge = sasl.Challenge (null);
					command = string.Format ("AUTH {0} {1}", authmech, challenge);
				} else {
					command = string.Format ("AUTH {0}", authmech);
				}

				response = SendCommand (command, cancellationToken);

				if (response.StatusCode == SmtpStatusCode.AuthenticationMechanismTooWeak)
					continue;

				try {
					while (!sasl.IsAuthenticated) {
						if (response.StatusCode != SmtpStatusCode.AuthenticationChallenge)
							throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

						challenge = sasl.Challenge (response.Response);
						response = SendCommand (challenge, cancellationToken);
					}
				} catch (SaslException ex) {
					// reset the authentication state
					response = SendCommand (string.Empty, cancellationToken);
					authException = ex;
				}

				if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful) {
					if (!IsBrokenSmtpServerThatResetsStateAfterAuthEhlo (host))
						Ehlo (cancellationToken);
					authenticated = true;
					OnAuthenticated (response.Response);
					return;
				}
			}

			if (tried)
				throw authException ?? new AuthenticationException ();

			throw new NotSupportedException ("No compatible authentication mechanisms found.");
		}

		internal void ReplayConnect (string hostName, Stream replayStream, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (hostName == null)
				throw new ArgumentNullException (nameof (hostName));

			if (replayStream == null)
				throw new ArgumentNullException (nameof (replayStream));

			Stream = new SmtpStream (replayStream, null, ProtocolLogger);
			capabilities = SmtpCapabilities.None;
			AuthenticationMechanisms.Clear ();
			host = hostName;
			secure = false;
			MaxSize = 0;

			try {
				// read the greeting
				var response = Stream.ReadResponse (cancellationToken);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				Ehlo (cancellationToken);

				connected = true;
			} catch {
				Stream.Dispose ();
				Stream = null;
				throw;
			}

			OnConnected ();
		}

		static void ComputeDefaultValues (string host, ref int port, ref SecureSocketOptions options, out Uri uri, out bool starttls)
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

			switch (options) {
			case SecureSocketOptions.StartTlsWhenAvailable:
				uri = new Uri ("smtp://" + host + ":" + port + "/?starttls=when-available");
				starttls = true;
				break;
			case SecureSocketOptions.StartTls:
				uri = new Uri ("smtp://" + host + ":" + port + "/?starttls=always");
				starttls = true;
				break;
			case SecureSocketOptions.SslOnConnect:
				uri = new Uri ("smtps://" + host + ":" + port);
				starttls = false;
				break;
			default:
				uri = new Uri ("smtp://" + host + ":" + port);
				starttls = false;
				break;
			}
		}

		/// <summary>
		/// Establishes a connection to the specified SMTP or SMTP/S server.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// An SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public override void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default (CancellationToken))
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

			capabilities = SmtpCapabilities.None;
			AuthenticationMechanisms.Clear ();
			MaxSize = 0;

			SmtpResponse response;
			Stream stream;
			bool starttls;
			Uri uri;

			ComputeDefaultValues (host, ref port, ref options, out uri, out starttls);

#if !NETFX_CORE
#if COREFX
			var ipAddresses = Dns.GetHostAddressesAsync (uri.DnsSafeHost).GetAwaiter ().GetResult ();
#else
			var ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
#endif
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				try {
					cancellationToken.ThrowIfCancellationRequested ();

					if (LocalEndPoint != null)
						socket.Bind (LocalEndPoint);

					socket.Connect (ipAddresses[i], port);
					break;
				} catch (OperationCanceledException) {
					socket.Dispose ();
					socket = null;
					throw;
				} catch {
					socket.Dispose ();
					socket = null;

					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (socket == null)
				throw new IOException (string.Format ("Failed to resolve host: {0}", host));

			this.host = host;

			if (options == SecureSocketOptions.SslOnConnect) {
				var ssl = new SslStream (new NetworkStream (socket, true), false, ValidateRemoteCertificate);
#if COREFX
				ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, true).GetAwaiter ().GetResult ();
#else
				ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, true);
#endif
				secure = true;
				stream = ssl;
			} else {
				stream = new NetworkStream (socket, true);
				secure = false;
			}
#else
			var protection = options == SecureSocketOptions.SslOnConnect ? SocketProtectionLevel.Tls12 : SocketProtectionLevel.PlainSocket;
			var socket = new StreamSocket ();

			try {
				cancellationToken.ThrowIfCancellationRequested ();
				socket.ConnectAsync (new HostName (host), port.ToString (), protection)
					.AsTask (cancellationToken)
					.GetAwaiter ()
					.GetResult ();
			} catch {
				socket.Dispose ();
				throw;
			}

			stream = new DuplexStream (socket.InputStream.AsStreamForRead (0), socket.OutputStream.AsStreamForWrite (0));
			secure = options == SecureSocketOptions.SslOnConnect;
			this.host = host;
#endif

			if (stream.CanTimeout) {
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;
			}

			ProtocolLogger.LogConnect (uri);

			Stream = new SmtpStream (stream, socket, ProtocolLogger);

			try {
				// read the greeting
				response = Stream.ReadResponse (cancellationToken);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				Ehlo (cancellationToken);

				if (options == SecureSocketOptions.StartTls && (capabilities & SmtpCapabilities.StartTLS) == 0)
					throw new NotSupportedException ("The SMTP server does not support the STARTTLS extension.");

				if (starttls && (capabilities & SmtpCapabilities.StartTLS) != 0) {
					response = SendCommand ("STARTTLS", cancellationToken);
					if (response.StatusCode != SmtpStatusCode.ServiceReady)
						throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

#if !NETFX_CORE
					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
#if COREFX
					tls.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, true).GetAwaiter ().GetResult ();
#else
					tls.AuthenticateAsClient (host, ClientCertificates, SslProtocols, true);
#endif
					Stream.Stream = tls;
#else
					socket.UpgradeToSslAsync (SocketProtectionLevel.Tls12, new HostName (host))
						.AsTask (cancellationToken)
						.GetAwaiter ()
						.GetResult ();
#endif

					secure = true;

					// Send EHLO again and get the new list of supported extensions
					Ehlo (cancellationToken);
				}

				connected = true;
			} catch {
				Stream.Dispose ();
				secure = false;
				Stream = null;
				throw;
			}

			OnConnected ();
		}

#if !NETFX_CORE
		/// <summary>
		/// Establish a connection to the specified SMTP or SMTP/S server using the provided socket.
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
		/// period of time./</note>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpCommandException">
		/// An SMTP command failed.
		/// </exception>
		/// <exception cref="SmtpProtocolException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (socket == null)
				throw new ArgumentNullException (nameof (socket));

			if (!socket.Connected)
				throw new ArgumentException ("The socket is not connected.", nameof (socket));

			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			CheckDisposed ();

			if (IsConnected)
				throw new InvalidOperationException ("The SmtpClient is already connected.");

			capabilities = SmtpCapabilities.None;
			AuthenticationMechanisms.Clear ();
			MaxSize = 0;

			SmtpResponse response;
			Stream stream;
			bool starttls;
			Uri uri;

			ComputeDefaultValues (host, ref port, ref options, out uri, out starttls);

			this.host = host;

			if (options == SecureSocketOptions.SslOnConnect) {
				var ssl = new SslStream (new NetworkStream (socket, true), false, ValidateRemoteCertificate);
#if COREFX
				ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, true).GetAwaiter ().GetResult ();
#else
				ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, true);
#endif
				secure = true;
				stream = ssl;
			} else {
				stream = new NetworkStream (socket, true);
				secure = false;
			}

			if (stream.CanTimeout) {
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;
			}

			ProtocolLogger.LogConnect (uri);

			Stream = new SmtpStream (stream, socket, ProtocolLogger);

			try {
				// read the greeting
				response = Stream.ReadResponse (cancellationToken);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				Ehlo (cancellationToken);

				if (options == SecureSocketOptions.StartTls && (capabilities & SmtpCapabilities.StartTLS) == 0)
					throw new NotSupportedException ("The SMTP server does not support the STARTTLS extension.");

				if (starttls && (capabilities & SmtpCapabilities.StartTLS) != 0) {
					response = SendCommand ("STARTTLS", cancellationToken);
					if (response.StatusCode != SmtpStatusCode.ServiceReady)
						throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
#if COREFX
					tls.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, true).GetAwaiter ().GetResult ();
#else
					tls.AuthenticateAsClient (host, ClientCertificates, SslProtocols, true);
#endif
					Stream.Stream = tls;

					secure = true;

					// Send EHLO again and get the new list of supported extensions
					Ehlo (cancellationToken);
				}

				connected = true;
			} catch {
				Stream.Dispose ();
				secure = false;
				Stream = null;
				throw;
			}

			OnConnected ();
		}
#endif

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
		public override void Disconnect (bool quit, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (!IsConnected)
				return;

			if (quit) {
				try {
					SendCommand ("QUIT", cancellationToken);
				} catch (OperationCanceledException) {
				} catch (SmtpProtocolException) {
				} catch (SmtpCommandException) {
				} catch (IOException) {
				}
			}

			Disconnect ();
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
		public override void NoOp (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var response = SendCommand ("NOOP", cancellationToken);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		void Disconnect ()
		{
			capabilities = SmtpCapabilities.None;
			authenticated = false;
			connected = false;
			secure = false;
			host = null;

			if (Stream != null) {
				Stream.Dispose ();
				Stream = null;
			}

			OnDisconnected ();
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

		static void AddUnique (IList<MailboxAddress> recipients, HashSet<string> unique, IEnumerable<MailboxAddress> mailboxes)
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
		enum SmtpExtension {
			None         = 0,
			EightBitMime = 1 << 0,
			BinaryMime   = 1 << 1,
			UTF8         = 1 << 2,
		}

		class ContentTransferEncodingVisitor : MimeVisitor
		{
			readonly SmtpCapabilities Capabilities;

			public ContentTransferEncodingVisitor (SmtpCapabilities capabilities)
			{
				Capabilities = capabilities;
			}

			public SmtpExtension SmtpExtensions {
				get; private set;
			}

			protected override void VisitMultipart (Multipart multipart)
			{
				if (multipart.ContentType.IsMimeType ("multipart", "signed")) {
					// do not modify children of a multipart/signed
					return;
				}

				base.VisitMultipart (multipart);
			}

			protected override void VisitMimePart (MimePart entity)
			{
				switch (entity.ContentTransferEncoding) {
				case ContentEncoding.EightBit:
					// if the server supports the 8BITMIME extension, use it...
					if ((Capabilities & SmtpCapabilities.EightBitMime) != 0) {
						SmtpExtensions |= SmtpExtension.EightBitMime;
					} else {
						SmtpExtensions |= SmtpExtension.BinaryMime;
					}
					break;
				case ContentEncoding.Binary:
					SmtpExtensions |= SmtpExtension.BinaryMime;
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

		void ProcessMailFromResponse (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
			switch (response.StatusCode) {
			case SmtpStatusCode.Ok:
				OnSenderAccepted (message, mailbox, response);
				break;
			case SmtpStatusCode.MailboxNameNotAllowed:
			case SmtpStatusCode.MailboxUnavailable:
				OnSenderNotAccepted (message, mailbox, response);
				break;
			case SmtpStatusCode.AuthenticationRequired:
				throw new ServiceNotAuthenticatedException (response.Response);
			default:
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			}
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

		void MailFrom (MimeMessage message, MailboxAddress mailbox, SmtpExtension extensions, CancellationToken cancellationToken)
		{
			var utf8 = (extensions & SmtpExtension.UTF8) != 0 ? " SMTPUTF8" : string.Empty;
			var command = string.Format ("MAIL FROM:<{0}>{1}", mailbox.Address, utf8);

			if ((extensions & SmtpExtension.BinaryMime) != 0)
				command += " BODY=BINARYMIME";
			else if ((extensions & SmtpExtension.EightBitMime) != 0)
				command += " BODY=8BITMIME";

			if ((capabilities & SmtpCapabilities.Dsn) != 0) {
				var envid = GetEnvelopeId (message);

				if (!string.IsNullOrEmpty (envid))
					command += " ENVID=" + envid;

				// TODO: RET parameter?
			}

			if ((capabilities & SmtpCapabilities.Pipelining) != 0) {
				QueueCommand (SmtpCommand.MailFrom, command, cancellationToken);
				return;
			}

			var response = SendCommand (command, cancellationToken);

			ProcessMailFromResponse (message, mailbox, response);
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

		bool ProcessRcptToResponse (MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
		{
			switch (response.StatusCode) {
			case SmtpStatusCode.UserNotLocalWillForward:
			case SmtpStatusCode.Ok:
				OnRecipientAccepted (message, mailbox, response);
				return true;
			case SmtpStatusCode.UserNotLocalTryAlternatePath:
			case SmtpStatusCode.MailboxNameNotAllowed:
			case SmtpStatusCode.MailboxUnavailable:
			case SmtpStatusCode.MailboxBusy:
				OnRecipientNotAccepted (message, mailbox, response);
				return false;
			case SmtpStatusCode.AuthenticationRequired:
				throw new ServiceNotAuthenticatedException (response.Response);
			default:
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			}
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
		/// <param name="mailbox">The mailbox.</param>
		protected virtual DeliveryStatusNotification? GetDeliveryStatusNotifications (MimeMessage message, MailboxAddress mailbox)
		{
			return null;
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

		void RcptTo (MimeMessage message, MailboxAddress mailbox, CancellationToken cancellationToken)
		{
			var command = string.Format ("RCPT TO:<{0}>", mailbox.Address);

			if ((capabilities & SmtpCapabilities.Dsn) != 0) {
				var notify = GetDeliveryStatusNotifications (message, mailbox);

				if (notify.HasValue)
					command += " NOTIFY=" + GetNotifyString (notify.Value);
			}

			if ((capabilities & SmtpCapabilities.Pipelining) != 0) {
				QueueCommand (SmtpCommand.RcptTo, command, cancellationToken);
				return;
			}

			var response = SendCommand (command, cancellationToken);

			ProcessRcptToResponse (message, mailbox, response);
		}

		class SendContext
		{
			readonly ITransferProgress progress;
			readonly long? size;
			long nwritten;

			public SendContext (ITransferProgress progress, long? size)
			{
				this.progress = progress;
				this.size = size;
			}

			public void Update (int n)
			{
				nwritten += n;

				if (size.HasValue)
					progress.Report (nwritten, size.Value);
				else
					progress.Report (nwritten);
			}
		}

		void Bdat (FormatOptions options, MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress)
		{
			long size;

			using (var measure = new MeasuringStream ()) {
				message.WriteTo (options, measure, cancellationToken);
				size = measure.Length;
			}

			var bytes = Encoding.UTF8.GetBytes (string.Format ("BDAT {0} LAST\r\n", size));

			Stream.Write (bytes, 0, bytes.Length, cancellationToken);

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

			switch (response.StatusCode) {
			default:
				throw new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, response.StatusCode, response.Response);
			case SmtpStatusCode.AuthenticationRequired:
				throw new ServiceNotAuthenticatedException (response.Response);
			case SmtpStatusCode.Ok:
				OnMessageSent (new MessageSentEventArgs (message, response.Response));
				break;
			}
		}

		void Data (FormatOptions options, MimeMessage message, CancellationToken cancellationToken, ITransferProgress progress)
		{
			var response = SendCommand ("DATA", cancellationToken);

			if (response.StatusCode != SmtpStatusCode.StartMailInput)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

			if (progress != null) {
				var ctx = new SendContext (progress, null);

				using (var stream = new ProgressStream (Stream, ctx.Update)) {
					using (var filtered = new FilteredStream (stream)) {
						filtered.Add (new SmtpDataFilter ());

						message.WriteTo (options, filtered, cancellationToken);
						filtered.Flush ();
					}
				}
			} else {
				using (var filtered = new FilteredStream (Stream)) {
					filtered.Add (new SmtpDataFilter ());

					message.WriteTo (options, filtered, cancellationToken);
					filtered.Flush ();
				}
			}

			Stream.Write (EndData, 0, EndData.Length, cancellationToken);
			Stream.Flush (cancellationToken);

			response = Stream.ReadResponse (cancellationToken);

			switch (response.StatusCode) {
			default:
				throw new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, response.StatusCode, response.Response);
			case SmtpStatusCode.AuthenticationRequired:
				throw new ServiceNotAuthenticatedException (response.Response);
			case SmtpStatusCode.Ok:
				OnMessageSent (new MessageSentEventArgs (message, response.Response));
				break;
			}
		}

		void Reset (CancellationToken cancellationToken)
		{
			try {
				var response = SendCommand ("RSET", cancellationToken);
				if (response.StatusCode != SmtpStatusCode.Ok)
					Disconnect (false, cancellationToken);
			} catch (SmtpCommandException) {
				// do not disconnect
			} catch {
				Disconnect ();
			}
		}

		void Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, CancellationToken cancellationToken, ITransferProgress progress)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var format = options.Clone ();
			format.International = format.International || sender.IsInternational || recipients.Any (x => x.IsInternational);
			format.HiddenHeaders.Add (HeaderId.ContentLength);
			format.HiddenHeaders.Add (HeaderId.ResentBcc);
			format.HiddenHeaders.Add (HeaderId.Bcc);
			format.NewLineFormat = NewLineFormat.Dos;

			if (format.International && (Capabilities & SmtpCapabilities.UTF8) == 0)
				throw new NotSupportedException ("The SMTP server does not support the SMTPUTF8 extension.");

			if (format.International && (Capabilities & SmtpCapabilities.EightBitMime) == 0)
				throw new NotSupportedException ("The SMTP server does not support the 8BITMIME extension.");

			// prepare the message
			if ((Capabilities & SmtpCapabilities.BinaryMime) != 0)
				message.Prepare (EncodingConstraint.None, MaxLineLength);
			else if ((Capabilities & SmtpCapabilities.EightBitMime) != 0)
				message.Prepare (EncodingConstraint.EightBit, MaxLineLength);
			else
				message.Prepare (EncodingConstraint.SevenBit, MaxLineLength);

			// figure out which SMTP extensions we need to use
			var visitor = new ContentTransferEncodingVisitor (capabilities);
			visitor.Visit (message);

			var extensions = visitor.SmtpExtensions;

			if (format.International)
				extensions |= SmtpExtension.UTF8;

			try {
				// Note: if PIPELINING is supported, MailFrom() and RcptTo() will
				// queue their commands instead of sending them immediately.
				MailFrom (message, sender, extensions, cancellationToken);

				for (int i = 0; i < recipients.Count; i++)
					RcptTo (message, recipients[i], cancellationToken);

				// Note: if PIPELINING is supported, this will flush all outstanding
				// MAIL FROM and RCPT TO commands to the server and then process all
				// of their responses.
				FlushCommandQueue (message, sender, recipients, cancellationToken);

				if ((extensions & SmtpExtension.BinaryMime) != 0)
					Bdat (format, message, cancellationToken, progress);
				else
					Data (format, message, cancellationToken, progress);
			} catch (ServiceNotAuthenticatedException) {
				// do not disconnect
				throw;
			} catch (SmtpCommandException) {
				Reset (cancellationToken);
				throw;
			} catch {
				Disconnect ();
				throw;
			}
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
		public override void Send (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (message == null)
				throw new ArgumentNullException (nameof (message));

			var recipients = GetMessageRecipients (message);
			var sender = GetMessageSender (message);

			if (sender == null)
				throw new InvalidOperationException ("No sender has been specified.");

			if (recipients.Count == 0)
				throw new InvalidOperationException ("No recipients have been specified.");

			Send (options, message, sender, recipients, cancellationToken, progress);
		}

		/// <summary>
		/// Send the specified message using the supplied sender and recipients.
		/// </summary>
		/// <remarks>
		/// Sends the message by uploading it to an SMTP server using the supplied sender and recipients.
		/// </remarks>
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
		public override void Send (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
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

			Send (options, message, sender, rcpts, cancellationToken, progress);
		}

		#endregion

		/// <summary>
		/// Expand a mailing address alias.
		/// </summary>
		/// <remarks>
		/// Expands a mailing address alias.
		/// </remarks>
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
		public InternetAddressList Expand (string alias, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (alias == null)
				throw new ArgumentNullException (nameof (alias));

			if (alias.Length == 0)
				throw new ArgumentException ("The alias cannot be empty.", nameof (alias));

			if (alias.IndexOfAny (new [] { '\r', '\n' }) != -1)
				throw new ArgumentException ("The alias cannot contain newline characters.", nameof (alias));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var response = SendCommand (string.Format ("EXPN {0}", alias), cancellationToken);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

			var lines = response.Response.Split ('\n');
			var list = new InternetAddressList ();

			for (int i = 0; i < lines.Length; i++) {
				InternetAddress address;

				if (InternetAddress.TryParse (lines[i], out address))
					list.Add (address);
			}

			return list;
		}

		/// <summary>
		/// Asynchronously expand a mailing address alias.
		/// </summary>
		/// <remarks>
		/// Asynchronously expands a mailing address alias.
		/// </remarks>
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
		public Task<InternetAddressList> ExpandAsync (string alias, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (alias == null)
				throw new ArgumentNullException (nameof (alias));

			if (alias.Length == 0)
				throw new ArgumentException ("The alias cannot be empty.", nameof (alias));

			if (alias.IndexOfAny (new [] { '\r', '\n' }) != -1)
				throw new ArgumentException ("The alias cannot contain newline characters.", nameof (alias));

			return Task.Factory.StartNew (() => {
				lock (SyncRoot) {
					return Expand (alias, cancellationToken);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Verify the existence of a mailbox address.
		/// </summary>
		/// <remarks>
		/// Verifies the existence a mailbox address with the SMTP server, returning the expanded
		/// mailbox address if it exists.
		/// </remarks>
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
		public MailboxAddress Verify (string address, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (address == null)
				throw new ArgumentNullException (nameof (address));

			if (address.Length == 0)
				throw new ArgumentException ("The address cannot be empty.", nameof (address));

			if (address.IndexOfAny (new [] { '\r', '\n' }) != -1)
				throw new ArgumentException ("The address cannot contain newline characters.", nameof (address));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var response = SendCommand (string.Format ("VRFY {0}", address), cancellationToken);

			if (response.StatusCode == SmtpStatusCode.Ok)
				return MailboxAddress.Parse (response.Response);

			throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		/// <summary>
		/// Asynchronously verify the existence of a mailbox address.
		/// </summary>
		/// <remarks>
		/// Asynchronously verifies the existence a mailbox address with the SMTP server,
		/// returning the expanded mailbox address if it exists.
		/// </remarks>
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
		public Task<MailboxAddress> VerifyAsync (string address, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (address == null)
				throw new ArgumentNullException (nameof (address));

			if (address.Length == 0)
				throw new ArgumentException ("The address cannot be empty.", nameof (address));

			if (address.IndexOfAny (new [] { '\r', '\n' }) != -1)
				throw new ArgumentException ("The address cannot contain newline characters.", nameof (address));

			return Task.Factory.StartNew (() => {
				lock (SyncRoot) {
					return Verify (address, cancellationToken);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
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
				Disconnect ();
			}

			base.Dispose (disposed);
		}
	}
}
