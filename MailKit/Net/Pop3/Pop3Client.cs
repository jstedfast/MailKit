//
// Pop3Client.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

#if NETFX_CORE
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Encoding = Portable.Text.Encoding;
using MD5 = MimeKit.Cryptography.MD5;
#else
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
#endif

using MimeKit;
using MimeKit.IO;

using MailKit.Security;

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
	public partial class Pop3Client : MailSpool
	{
		[Flags]
		enum ProbedCapabilities : byte {
			None   = 0,
			Top    = (1 << 0),
			UIDL   = (1 << 1),
			User   = (1 << 2),
		}

		readonly MimeParser parser = new MimeParser (Stream.Null);
		readonly Pop3Engine engine;
		ProbedCapabilities probed;
#if NETFX_CORE
		StreamSocket socket;
#endif
		bool disposed, secure, utf8;
		int timeout = 100000;
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

#if !NETFX_CORE
		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (ServerCertificateValidationCallback != null)
				return ServerCertificateValidationCallback (engine.Uri.Host, certificate, chain, sslPolicyErrors);

#if !NETSTANDARD
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (engine.Uri.Host, certificate, chain, sslPolicyErrors);
#endif

			return DefaultServerCertificateValidationCallback (sender, certificate, chain, sslPolicyErrors);
		}
#endif

		static Exception CreatePop3Exception (Pop3Command pc)
		{
			var command = pc.Command.Split (' ')[0].TrimEnd ();
			var message = string.Format ("POP3 server did not respond with a +OK response to the {0} command.", command);

			if (pc.Status == Pop3CommandStatus.Error)
				return new Pop3CommandException (message, pc.StatusText);

			return new Pop3ProtocolException (message);
		}

		static ProtocolException CreatePop3ParseException (Exception innerException, string format, params object[] args)
		{
			return new Pop3ProtocolException (string.Format (format, args), innerException);
		}

		static ProtocolException CreatePop3ParseException (string format, params object[] args)
		{
			return new Pop3ProtocolException (string.Format (format, args));
		}

		async Task SendCommandAsync (bool doAsync, CancellationToken token, string command)
		{
			var pc = engine.QueueCommand (token, null, Encoding.ASCII, command);
			int id;

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			if (pc.Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (pc);
		}

		Task<string> SendCommandAsync (bool doAsync, CancellationToken token, string format, params object[] args)
		{
			return SendCommandAsync (doAsync, token, Encoding.ASCII, format, args);
		}

		async Task<string> SendCommandAsync (bool doAsync, CancellationToken token, Encoding encoding, string format, params object[] args)
		{
			string okText = string.Empty;
			int id;

			var pc = engine.QueueCommand (token, (pop3, cmd, text, xdoAsync) => {
				if (cmd.Status == Pop3CommandStatus.Ok)
					okText = text;

				return Task.FromResult (true);
			}, encoding, format, args);

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			if (pc.Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (pc);

			return okText;
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

		async Task UpdateMessageCountAsync (bool doAsync, CancellationToken cancellationToken)
		{
			var pc = engine.QueueCommand (cancellationToken, (pop3, cmd, text, xdoAsync) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return Task.FromResult (false);

				// the response should be "<count> <total size>"
				var tokens = text.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (tokens.Length < 2) {
					cmd.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the STAT command.");
					return Task.FromResult (false);
				}

				if (!int.TryParse (tokens[0], out total)) {
					cmd.Exception = CreatePop3ParseException ("Pop3 server returned an invalid response to the STAT command.");
					return Task.FromResult (false);
				}

				return Task.FromResult (true);
			}, "STAT");
			int id;

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			if (pc.Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (pc);

			if (pc.Exception != null)
				throw pc.Exception;
		}

		async Task ProbeCapabilitiesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if ((engine.Capabilities & Pop3Capabilities.UIDL) == 0) {
				// if the message count is > 0, we can probe the UIDL command
				if (total > 0) {
					try {
						await GetMessageUidAsync (0, doAsync, cancellationToken).ConfigureAwait (false);
					} catch (NotSupportedException) {
					}
				}
			}
		}

		async Task QueryCapabilitiesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if (doAsync)
				await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);
			else
				engine.QueryCapabilities (cancellationToken);
		}

		async Task AuthenticateAsync (SaslMechanism mechanism, bool doAsync, CancellationToken cancellationToken)
		{
			if (mechanism == null)
				throw new ArgumentNullException (nameof (mechanism));

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The Pop3Client must be connected before you can authenticate.");

			if (IsAuthenticated)
				throw new InvalidOperationException ("The Pop3Client is already authenticated.");

			CheckDisposed ();

			var uri = new Uri ("pop://" + engine.Uri.Host);
			string authMessage = string.Empty;
			string challenge;
			Pop3Command pc;
			int id;

			cancellationToken.ThrowIfCancellationRequested ();

			mechanism.Uri = uri;

			pc = engine.QueueCommand (cancellationToken, async (pop3, cmd, text, xdoAsync) => {
				if (mechanism.IsAuthenticated) {
					if (cmd.Status == Pop3CommandStatus.Ok)
						authMessage = text;
					return;
				}

				while (!mechanism.IsAuthenticated) {
					challenge = mechanism.Challenge (text);

					var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");
					string response;

					if (xdoAsync) {
						await pop3.Stream.WriteAsync (buf, 0, buf.Length, cmd.CancellationToken).ConfigureAwait (false);
						await pop3.Stream.FlushAsync (cmd.CancellationToken).ConfigureAwait (false);

						response = (await pop3.ReadLineAsync (cmd.CancellationToken).ConfigureAwait (false)).TrimEnd ();
					} else {
						pop3.Stream.Write (buf, 0, buf.Length, cmd.CancellationToken);
						pop3.Stream.Flush (cancellationToken);

						response = pop3.ReadLine (cmd.CancellationToken).TrimEnd ();
					}

					cmd.Status = Pop3Engine.GetCommandStatus (response, out text);
					cmd.StatusText = text;

					if (cmd.Status == Pop3CommandStatus.ProtocolError)
						throw new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
				}
			}, "AUTH {0}", mechanism.MechanismName);

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			if (pc.Status == Pop3CommandStatus.Error)
				throw new AuthenticationException ();

			if (pc.Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (pc);

			if (pc.Exception != null)
				throw pc.Exception;

			engine.State = Pop3EngineState.Transaction;

			await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
			await UpdateMessageCountAsync (doAsync, cancellationToken).ConfigureAwait (false);
			await ProbeCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
			OnAuthenticated (authMessage);
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
		public override void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default (CancellationToken))
		{
			AuthenticateAsync (mechanism, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		async Task AuthenticateAsync (Encoding encoding, ICredentials credentials, bool doAsync, CancellationToken cancellationToken)
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

			var uri = new Uri ("pop://" + engine.Uri.Host);
			string authMessage = string.Empty;
			string userName, password;
			NetworkCredential cred;
			string challenge;
			Pop3Command pc;
			int id;

			if ((engine.Capabilities & Pop3Capabilities.Apop) != 0) {
				cred = credentials.GetCredential (uri, "APOP");
				userName = utf8 ? SaslMechanism.SaslPrep (cred.UserName) : cred.UserName;
				password = utf8 ? SaslMechanism.SaslPrep (cred.Password) : cred.Password;
				challenge = engine.ApopToken + password;
				var md5sum = new StringBuilder ();
				byte[] digest;

				using (var md5 = MD5.Create ())
					digest = md5.ComputeHash (encoding.GetBytes (challenge));

				for (int i = 0; i < digest.Length; i++)
					md5sum.Append (digest[i].ToString ("x2"));

				try {
					authMessage = await SendCommandAsync (doAsync, cancellationToken, encoding, "APOP {0} {1}", userName, md5sum).ConfigureAwait (false);
					engine.State = Pop3EngineState.Transaction;
				} catch (Pop3CommandException) {
				}

				if (engine.State == Pop3EngineState.Transaction) {
					await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
					await UpdateMessageCountAsync (doAsync, cancellationToken).ConfigureAwait (false);
					await ProbeCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
					OnAuthenticated (authMessage);
					return;
				}
			}

			if ((engine.Capabilities & Pop3Capabilities.Sasl) != 0) {
				foreach (var authmech in SaslMechanism.AuthMechanismRank) {
					SaslMechanism sasl;

					if (!engine.AuthenticationMechanisms.Contains (authmech))
						continue;

					if ((sasl = SaslMechanism.Create (authmech, uri, encoding, credentials)) == null)
						continue;

					cancellationToken.ThrowIfCancellationRequested ();

					pc = engine.QueueCommand (cancellationToken, async (pop3, cmd, text, xdoAsync) => {
						if (sasl.IsAuthenticated) {
							if (cmd.Status == Pop3CommandStatus.Ok)
								authMessage = text;
							return;
						}

						while (!sasl.IsAuthenticated) {
							challenge = sasl.Challenge (text);

							var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");
							string response;

							if (xdoAsync) {
								await pop3.Stream.WriteAsync (buf, 0, buf.Length, cmd.CancellationToken).ConfigureAwait (false);
								await pop3.Stream.FlushAsync (cmd.CancellationToken).ConfigureAwait (false);

								response = (await pop3.ReadLineAsync (cmd.CancellationToken).ConfigureAwait (false)).TrimEnd ();
							} else {
								pop3.Stream.Write (buf, 0, buf.Length, cmd.CancellationToken);
								pop3.Stream.Flush (cancellationToken);

								response = pop3.ReadLine (cmd.CancellationToken).TrimEnd ();
							}

							cmd.Status = Pop3Engine.GetCommandStatus (response, out text);
							cmd.StatusText = text;

							if (cmd.Status == Pop3CommandStatus.ProtocolError)
								throw new Pop3ProtocolException (string.Format ("Unexpected response from server: {0}", response));
						}
					}, "AUTH {0}", authmech);

					do {
						if (doAsync)
							id = await engine.IterateAsync ().ConfigureAwait (false);
						else
							id = engine.Iterate ();
					} while (id < pc.Id);

					if (pc.Status == Pop3CommandStatus.Error)
						continue;

					if (pc.Status != Pop3CommandStatus.Ok)
						throw CreatePop3Exception (pc);

					if (pc.Exception != null)
						throw pc.Exception;

					engine.State = Pop3EngineState.Transaction;

					await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
					await UpdateMessageCountAsync (doAsync, cancellationToken).ConfigureAwait (false);
					await ProbeCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
					OnAuthenticated (authMessage);
					return;
				}
			}

			// fall back to the classic USER & PASS commands...
			cred = credentials.GetCredential (uri, "DEFAULT");
			userName = utf8 ? SaslMechanism.SaslPrep (cred.UserName) : cred.UserName;
			password = utf8 ? SaslMechanism.SaslPrep (cred.Password) : cred.Password;

			try {
				await SendCommandAsync (doAsync, cancellationToken, encoding, "USER {0}", userName).ConfigureAwait (false);
				authMessage = await SendCommandAsync (doAsync, cancellationToken, encoding, "PASS {0}", password).ConfigureAwait (false);
			} catch (Pop3CommandException) {
				throw new AuthenticationException ();
			}

			engine.State = Pop3EngineState.Transaction;

			await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
			await UpdateMessageCountAsync (doAsync, cancellationToken).ConfigureAwait (false);
			await ProbeCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
			OnAuthenticated (authMessage);
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the POP3 server supports the APOP authentication mechanism,
		/// then APOP is used.</para>
		/// <para>If the APOP authentication mechanism is not supported and the
		/// server supports one or more SASL authentication mechanisms, then
		/// the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
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
		public override void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken))
		{
			AuthenticateAsync (encoding, credentials, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		internal void ReplayConnect (string host, Stream replayStream, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (replayStream == null)
				throw new ArgumentNullException (nameof (replayStream));

			CheckDisposed ();

			probed = ProbedCapabilities.None;
			secure = false;

			engine.Uri = new Uri ("pop://" + host);
			engine.Connect (new Pop3Stream (replayStream, null, ProtocolLogger), cancellationToken);
			engine.QueryCapabilities (cancellationToken);
			engine.Disconnected += OnEngineDisconnected;
			OnConnected ();
		}

		internal async Task ReplayConnectAsync (string host, Stream replayStream, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (replayStream == null)
				throw new ArgumentNullException (nameof (replayStream));

			CheckDisposed ();

			probed = ProbedCapabilities.None;
			secure = false;

			engine.Uri = new Uri ("pop://" + host);
			await engine.ConnectAsync (new Pop3Stream (replayStream, null, ProtocolLogger), cancellationToken).ConfigureAwait (false);
			await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);
			engine.Disconnected += OnEngineDisconnected;
			OnConnected ();
		}

		static void ComputeDefaultValues (string host, ref int port, ref SecureSocketOptions options, out Uri uri, out bool starttls)
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

			switch (options) {
			case SecureSocketOptions.StartTlsWhenAvailable:
				uri = new Uri ("pop://" + host + ":" + port + "/?starttls=when-available");
				starttls = true;
				break;
			case SecureSocketOptions.StartTls:
				uri = new Uri ("pop://" + host + ":" + port + "/?starttls=always");
				starttls = true;
				break;
			case SecureSocketOptions.SslOnConnect:
				uri = new Uri ("pops://" + host + ":" + port);
				starttls = false;
				break;
			default:
				uri = new Uri ("pop://" + host + ":" + port);
				starttls = false;
				break;
			}
		}

		async Task ConnectAsync (string host, int port, SecureSocketOptions options, bool doAsync, CancellationToken cancellationToken)
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

			Stream stream;
			bool starttls;
			Uri uri;

			ComputeDefaultValues (host, ref port, ref options, out uri, out starttls);

#if !NETFX_CORE
			IPAddress[] ipAddresses;
			Socket socket = null;

			if (doAsync) {
				ipAddresses = await Dns.GetHostAddressesAsync (uri.DnsSafeHost).ConfigureAwait (false);
			} else {
#if NETSTANDARD
				ipAddresses = Dns.GetHostAddressesAsync (uri.DnsSafeHost).GetAwaiter ().GetResult ();
#else
				ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
#endif
			}

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
					throw;
				} catch {
					socket.Dispose ();

					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (socket == null)
				throw new IOException (string.Format ("Failed to resolve host: {0}", host));

			engine.Uri = uri;

			if (options == SecureSocketOptions.SslOnConnect) {
				var ssl = new SslStream (new NetworkStream (socket, true), false, ValidateRemoteCertificate);

				try {
					if (doAsync) {
						await ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).ConfigureAwait (false);
					} else {
#if NETSTANDARD
						ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).GetAwaiter ().GetResult ();
#else
						ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
#endif
					}
				} catch (Exception ex) {
					ssl.Dispose ();

					throw SslHandshakeException.Create (ex, false);
				}

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
				if (doAsync)
					await socket.ConnectAsync (new HostName (host), port.ToString (), protection).AsTask (cancellationToken).ConfigureAwait (false);
				else
					socket.ConnectAsync (new HostName (host), port.ToString (), protection).AsTask (cancellationToken).GetAwaiter ().GetResult ();
			} catch (Exception ex) {
				socket.Dispose ();
				socket = null;

				if (protection != SocketProtectionLevel.PlainSocket)
					throw SslHandshakeException.Create (ex, false);

				throw;
			}

			stream = new DuplexStream (socket.InputStream.AsStreamForRead (0), socket.OutputStream.AsStreamForWrite (0));
			secure = options == SecureSocketOptions.SslOnConnect;
			engine.Uri = uri;
#endif

			probed = ProbedCapabilities.None;
			if (stream.CanTimeout) {
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;
			}

			try {
				ProtocolLogger.LogConnect (uri);
			} catch {
				stream.Dispose ();
				secure = false;
#if NETFX_CORE
				socket = null;
#endif
				throw;
			}

			var pop3 = new Pop3Stream (stream, socket, ProtocolLogger);

			if (doAsync)
				await engine.ConnectAsync (pop3, cancellationToken).ConfigureAwait (false);
			else
				engine.Connect (pop3, cancellationToken);

			try {
				await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (options == SecureSocketOptions.StartTls && (engine.Capabilities & Pop3Capabilities.StartTLS) == 0)
					throw new NotSupportedException ("The POP3 server does not support the STLS extension.");

				if (starttls && (engine.Capabilities & Pop3Capabilities.StartTLS) != 0) {
					await SendCommandAsync (doAsync, cancellationToken, "STLS").ConfigureAwait (false);

					try {
#if !NETFX_CORE
						var tls = new SslStream (stream, false, ValidateRemoteCertificate);
						engine.Stream.Stream = tls;

						if (doAsync) {
							await tls.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).ConfigureAwait (false);
						} else {
#if NETSTANDARD
							tls.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).GetAwaiter ().GetResult ();
#else
							tls.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
#endif
						}
#else
						if (doAsync)
							await socket.UpgradeToSslAsync (SocketProtectionLevel.Tls12, new HostName (host)).AsTask (cancellationToken).ConfigureAwait (false);
						else
							socket.UpgradeToSslAsync (SocketProtectionLevel.Tls12, new HostName (host)).AsTask (cancellationToken).GetAwaiter ().GetResult ();
#endif
					} catch (Exception ex) {
						throw SslHandshakeException.Create (ex, true);
					}

					secure = true;

					// re-issue a CAPA command
					await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
				}
			} catch {
				engine.Disconnect ();
				secure = false;
				throw;
			}

			engine.Disconnected += OnEngineDisconnected;
			OnConnected ();
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
		public override void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default (CancellationToken))
		{
			ConnectAsync (host, port, options, false, cancellationToken).GetAwaiter ().GetResult ();
		}

#if !NETFX_CORE
		async Task ConnectAsync (Socket socket, string host, int port, SecureSocketOptions options, bool doAsync, CancellationToken cancellationToken)
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
				throw new InvalidOperationException ("The Pop3Client is already connected.");

			Stream stream;
			bool starttls;
			Uri uri;

			ComputeDefaultValues (host, ref port, ref options, out uri, out starttls);

			engine.Uri = uri;

			if (options == SecureSocketOptions.SslOnConnect) {
				var ssl = new SslStream (new NetworkStream (socket, true), false, ValidateRemoteCertificate);

				try {
					if (doAsync) {
						await ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).ConfigureAwait (false);
					} else {
#if NETSTANDARD
						ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).GetAwaiter ().GetResult ();
#else
						ssl.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
#endif
					}
				} catch (Exception ex) {
					ssl.Dispose ();

					throw SslHandshakeException.Create (ex, false);
				}

				secure = true;
				stream = ssl;
			} else {
				stream = new NetworkStream (socket, true);
				secure = false;
			}

			probed = ProbedCapabilities.None;
			if (stream.CanTimeout) {
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;
			}

			try {
				ProtocolLogger.LogConnect (uri);
			} catch {
				stream.Dispose ();
				secure = false;
				throw;
			}

			var pop3 = new Pop3Stream (stream, socket, ProtocolLogger);

			if (doAsync)
				await engine.ConnectAsync (pop3, cancellationToken).ConfigureAwait (false);
			else
				engine.Connect (pop3, cancellationToken);

			try {
				await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (options == SecureSocketOptions.StartTls && (engine.Capabilities & Pop3Capabilities.StartTLS) == 0)
					throw new NotSupportedException ("The POP3 server does not support the STLS extension.");

				if (starttls && (engine.Capabilities & Pop3Capabilities.StartTLS) != 0) {
					await SendCommandAsync (doAsync, cancellationToken, "STLS").ConfigureAwait (false);

					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
					engine.Stream.Stream = tls;

					try {
						if (doAsync) {
							await tls.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).ConfigureAwait (false);
						} else {
#if NETSTANDARD
							tls.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).GetAwaiter ().GetResult ();
#else
							tls.AuthenticateAsClient (host, ClientCertificates, SslProtocols, CheckCertificateRevocation);
#endif
						}
					} catch (Exception ex) {
						throw SslHandshakeException.Create (ex, true);
					}

					secure = true;

					// re-issue a CAPA command
					await QueryCapabilitiesAsync (doAsync, cancellationToken).ConfigureAwait (false);
				}
			} catch {
				engine.Disconnect ();
				secure = false;
				throw;
			}

			engine.Disconnected += OnEngineDisconnected;
			OnConnected ();
		}

		/// <summary>
		/// Establish a connection to the specified POP3 or POP3/S server using the provided socket.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified POP3 or POP3/S server using
		/// the provided socket.</para>
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
		public void Connect (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default (CancellationToken))
		{
			ConnectAsync (socket, host, port, options, false, cancellationToken).GetAwaiter ().GetResult ();
		}
#endif

		async Task DisconnectAsync (bool quit, bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					await SendCommandAsync (doAsync, cancellationToken, "QUIT").ConfigureAwait (false);
				} catch (OperationCanceledException) {
				} catch (Pop3ProtocolException) {
				} catch (Pop3CommandException) {
				} catch (IOException) {
				}
			}

#if NETFX_CORE
			socket.Dispose ();
			socket = null;
#endif

			secure = utf8 = false;
			total = 0;

			engine.Disconnect ();
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
		public override void Disconnect (bool quit, CancellationToken cancellationToken = default (CancellationToken))
		{
			DisconnectAsync (quit, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		Task NoOpAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return SendCommandAsync (doAsync, cancellationToken, "NOOP");
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
		public override void NoOp (CancellationToken cancellationToken = default (CancellationToken))
		{
			NoOpAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		void OnEngineDisconnected (object sender, EventArgs e)
		{
			engine.Disconnected -= OnEngineDisconnected;
			secure = utf8 = false;

			OnDisconnected ();
		}

		#endregion

		async Task EnableUTF8Async (bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();

			if (engine.State != Pop3EngineState.Connected)
				throw new InvalidOperationException ("You must enable UTF-8 mode before authenticating.");

			if ((engine.Capabilities & Pop3Capabilities.UTF8) == 0)
				throw new NotSupportedException ("The POP3 server does not support the UTF8 extension.");

			if (utf8)
				return;

			await SendCommandAsync (doAsync, cancellationToken, "UTF8").ConfigureAwait (false);
			utf8 = true;
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
		public void EnableUTF8 (CancellationToken cancellationToken = default (CancellationToken))
		{
			EnableUTF8Async (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		async Task<IList<Pop3Language>> GetLanguagesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();

			if ((Capabilities & Pop3Capabilities.Lang) == 0)
				throw new NotSupportedException ("The POP3 server does not support the LANG extension.");

			var langs = new List<Pop3Language> ();

			var pc = engine.QueueCommand (cancellationToken, async (pop3, cmd, text, xdoAsync) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				do {
					string response;

					if (xdoAsync)
						response = await engine.ReadLineAsync (cmd.CancellationToken).ConfigureAwait (false);
					else
						response = engine.ReadLine (cmd.CancellationToken);

					if (response == ".")
						break;

					var tokens = response.Split (new [] { ' ' }, 2);
					if (tokens.Length != 2)
						continue;

					langs.Add (new Pop3Language (tokens[0], tokens[1]));
				} while (true);
			}, "LANG");
			int id;

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			if (pc.Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (pc);

			if (pc.Exception != null)
				throw pc.Exception;

			return new ReadOnlyCollection<Pop3Language> (langs);
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
		public IList<Pop3Language> GetLanguages (CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetLanguagesAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		Task SetLanguageAsync (string lang, bool doAsync, CancellationToken cancellationToken)
		{
			if (lang == null)
				throw new ArgumentNullException (nameof (lang));

			if (lang.Length == 0)
				throw new ArgumentException ("The language code cannot be empty.", nameof (lang));

			CheckDisposed ();
			CheckConnected ();

			if ((Capabilities & Pop3Capabilities.Lang) == 0)
				throw new NotSupportedException ("The POP3 server does not support the LANG extension.");

			return SendCommandAsync (doAsync, cancellationToken, "LANG {0}", lang);
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
		public void SetLanguage (string lang, CancellationToken cancellationToken = default (CancellationToken))
		{
			SetLanguageAsync (lang, false, cancellationToken).GetAwaiter ().GetResult ();
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

		async Task<string> GetMessageUidAsync (int index, bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (!SupportsUids && (probed & ProbedCapabilities.UIDL) != 0)
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			string uid = null;

			var pc = engine.QueueCommand (cancellationToken, (pop3, cmd, text, xdoAsync) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return Task.FromResult (true);

				// the response should be "<seqid> <uid>"
				var tokens = text.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				int seqid;

				if (tokens.Length < 2) {
					cmd.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the UIDL command.");
					return Task.FromResult (true);
				}

				if (!int.TryParse (tokens[0], out seqid) || seqid < 1) {
					cmd.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected response to the UIDL command.");
					return Task.FromResult (true);
				}

				if (seqid != index + 1) {
					cmd.Exception = CreatePop3ParseException ("Pop3 server returned the UID for the wrong message.");
					return Task.FromResult (true);
				}

				uid = tokens[1];

				return Task.FromResult (true);
			}, "UIDL {0}", index + 1);
			int id;

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			probed |= ProbedCapabilities.UIDL;

			if (pc.Status != Pop3CommandStatus.Ok) {
				if (!SupportsUids)
					throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

				throw CreatePop3Exception (pc);
			}

			if (pc.Exception != null)
				throw pc.Exception;

			engine.Capabilities |= Pop3Capabilities.UIDL;

			return uid;
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
		public override string GetMessageUid (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMessageUidAsync (index, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		async Task<IList<string>> GetMessageUidsAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (!SupportsUids && (probed & ProbedCapabilities.UIDL) != 0)
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			var uids = new List<string> ();
			int id;

			var pc = engine.QueueCommand (cancellationToken, async (pop3, cmd, text, xdoAsync) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				do {
					string response;

					if (xdoAsync)
						response = await engine.ReadLineAsync (cmd.CancellationToken).ConfigureAwait (false);
					else
						response = engine.ReadLine (cmd.CancellationToken);

					if (response == ".")
						break;

					if (cmd.Exception != null)
						continue;

					var tokens = response.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int seqid;

					if (tokens.Length < 2) {
						cmd.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the UIDL command.");
						continue;
					}

					if (!int.TryParse (tokens[0], out seqid)) {
						cmd.Exception = CreatePop3ParseException ("Pop3 server returned an invalid response to the UIDL command.");
						continue;
					}

					uids.Add (tokens[1]);
				} while (true);
			}, "UIDL");

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			probed |= ProbedCapabilities.UIDL;

			if (pc.Status != Pop3CommandStatus.Ok) {
				if (!SupportsUids)
					throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

				throw CreatePop3Exception (pc);
			}

			if (pc.Exception != null)
				throw pc.Exception;

			engine.Capabilities |= Pop3Capabilities.UIDL;

			return uids;
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
		public override IList<string> GetMessageUids (CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMessageUidsAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		class MessageSizeContext
		{
			protected readonly Pop3Engine Engine;
			int[] sizes;
			int index;

			public MessageSizeContext (Pop3Engine engine)
			{
				Engine = engine;
			}

			void Reset (int capacity)
			{
				index = 0;

				if (sizes == null) {
					sizes = new int[capacity];
					return;
				}

				if (capacity != sizes.Length)
					Array.Resize (ref sizes, capacity);
			}

			void Add (int size)
			{
				sizes[index++] = size;
			}

			Task OnDataReceived (Pop3Engine pop3, Pop3Command pc, string text, bool doAsync)
			{
				if (pc.Status != Pop3CommandStatus.Ok)
					return Task.FromResult (true);

				var tokens = text.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				int id, size;

				if (tokens.Length < 2) {
					pc.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the LIST command.");
					return Task.FromResult (true);
				}

				if (!int.TryParse (tokens[0], out id) || id < 1) {
					pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected response to the LIST command.");
					return Task.FromResult (true);
				}

				if (!int.TryParse (tokens[1], out size) || size < 0) {
					pc.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected size token to the LIST command.");
					return Task.FromResult (true);
				}

				Add (size);

				return Task.FromResult (true);
			}

			Pop3Command QueueCommand (int seqid, CancellationToken cancellationToken)
			{
				return Engine.QueueCommand (cancellationToken, OnDataReceived, "LIST {0}", seqid);
			}

			async Task SendCommandAsync (int seqid, bool doAsync, CancellationToken cancellationToken)
			{
				var pc = QueueCommand (seqid, cancellationToken);
				int id;

				do {
					if (doAsync)
						id = await Engine.IterateAsync ().ConfigureAwait (false);
					else
						id = Engine.Iterate ();
				} while (id < pc.Id);

				if (pc.Status != Pop3CommandStatus.Ok)
					throw CreatePop3Exception (pc);

				if (pc.Exception != null)
					throw pc.Exception;
			}

			public async Task<int> GetSizeAsync (int seqid, bool doAsync, CancellationToken cancellationToken)
			{
				sizes = new int[1];
				index = 0;

				await SendCommandAsync (seqid, doAsync, cancellationToken).ConfigureAwait (false);

				return sizes[0];
			}

			public async Task<IList<int>> GetSizesAsync (IList<int> seqids, bool doAsync, CancellationToken cancellationToken)
			{
				sizes = new int[seqids.Count];
				index = 0;

				if ((Engine.Capabilities & Pop3Capabilities.Pipelining) == 0) {
					for (int i = 0; i < seqids.Count; i++)
						await SendCommandAsync (seqids[i], doAsync, cancellationToken);

					return sizes;
				}

				var commands = new Pop3Command[seqids.Count];
				Pop3Command pc = null;
				int id;

				for (int i = 0; i < seqids.Count; i++)
					commands[i] = QueueCommand (seqids[i], cancellationToken);

				pc = commands[commands.Length - 1];

				do {
					if (doAsync)
						id = await Engine.IterateAsync ().ConfigureAwait (false);
					else
						id = Engine.Iterate ();
				} while (id < pc.Id);

				for (int i = 0; i < commands.Length; i++) {
					if (commands[i].Status != Pop3CommandStatus.Ok)
						throw CreatePop3Exception (commands[i]);

					if (commands[i].Exception != null)
						throw commands[i].Exception;
				}

				return sizes;
			}
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
		public override int GetMessageSize (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			var ctx = new MessageSizeContext (engine);

			return ctx.GetSizeAsync (index + 1, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		async Task<IList<int>> GetMessageSizesAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			var sizes = new List<int> ();
			int id;

			var pc = engine.QueueCommand (cancellationToken, async (pop3, cmd, text, xdoAsync) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				do {
					string response;

					if (xdoAsync)
						response = await engine.ReadLineAsync (cmd.CancellationToken).ConfigureAwait (false);
					else
						response = engine.ReadLine (cmd.CancellationToken);

					if (response == ".")
						break;

					if (cmd.Exception != null)
						continue;

					var tokens = response.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int seqid, size;

					if (tokens.Length < 2) {
						cmd.Exception = CreatePop3ParseException ("Pop3 server returned an incomplete response to the LIST command.");
						continue;
					}

					if (!int.TryParse (tokens[0], out seqid) || seqid < 1) {
						cmd.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected response to the LIST command.");
						continue;
					}

					if (seqid != sizes.Count + 1) {
						cmd.Exception = CreatePop3ParseException ("Pop3 server returned the size for the wrong message.");
						continue;
					}

					if (!int.TryParse (tokens[1], out size) || size < 0) {
						cmd.Exception = CreatePop3ParseException ("Pop3 server returned an unexpected size token to the LIST command.");
						continue;
					}

					sizes.Add (size);
				} while (true);
			}, "LIST");

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			if (pc.Status != Pop3CommandStatus.Ok)
				throw CreatePop3Exception (pc);

			if (pc.Exception != null)
				throw pc.Exception;

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
		public override IList<int> GetMessageSizes (CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMessageSizesAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		abstract class DownloadContext<T>
		{
			protected readonly Pop3Client Client;
			readonly ITransferProgress Progress;
			T[] downloaded;
			long nread;
			int index;

			protected DownloadContext (Pop3Client client, ITransferProgress progress)
			{
				Progress = progress;
				Client = client;
			}

			protected Pop3Engine Engine {
				get { return Client.engine; }
			}

			protected abstract T Parse (Pop3Stream data, CancellationToken cancellationToken);

			protected abstract Task<T> ParseAsync (Pop3Stream data, CancellationToken cancellationToken);

			protected void Update (int n)
			{
				if (Progress == null)
					return;

				nread += n;

				Progress.Report (nread);
			}

			void Reset (int capacity)
			{
				nread = 0;
				index = 0;

				if (downloaded == null) {
					downloaded = new T[capacity];
					return;
				}

				if (capacity != downloaded.Length)
					Array.Resize (ref downloaded, capacity);
			}

			void Add (T item)
			{
				downloaded[index++] = item;
			}

			async Task OnDataReceived (Pop3Engine pop3, Pop3Command pc, string text, bool doAsync)
			{
				if (pc.Status != Pop3CommandStatus.Ok)
					return;

				try {
					T item;

					pop3.Stream.Mode = Pop3StreamMode.Data;

					if (doAsync)
						item = await ParseAsync (pop3.Stream, pc.CancellationToken).ConfigureAwait (false);
					else
						item = Parse (pop3.Stream, pc.CancellationToken);

					Add (item);
				} catch (FormatException ex) {
					pc.Exception = CreatePop3ParseException (ex, "Failed to parse data.");

					if (doAsync)
						await pop3.Stream.CopyToAsync (Stream.Null, 4096, pc.CancellationToken).ConfigureAwait (false);
					else
						pop3.Stream.CopyTo (Stream.Null, 4096);
				} finally {
					pop3.Stream.Mode = Pop3StreamMode.Line;
				}
			}

			Pop3Command QueueCommand (int seqid, bool headersOnly, CancellationToken cancellationToken)
			{
				if (headersOnly)
					return Engine.QueueCommand (cancellationToken, OnDataReceived, "TOP {0} 0", seqid);

				return Engine.QueueCommand (cancellationToken, OnDataReceived, "RETR {0}", seqid);
			}

			async Task DownloadItemAsync (int seqid, bool headersOnly, bool doAsync, CancellationToken cancellationToken)
			{
				var pc = QueueCommand (seqid, headersOnly, cancellationToken);
				int id;

				do {
					if (doAsync)
						id = await Engine.IterateAsync ().ConfigureAwait (false);
					else
						id = Engine.Iterate ();
				} while (id < pc.Id);

				if (pc.Status != Pop3CommandStatus.Ok)
					throw CreatePop3Exception (pc);

				if (pc.Exception != null)
					throw pc.Exception;
			}

			public async Task<T> DownloadAsync (int seqid, bool headersOnly, bool doAsync, CancellationToken cancellationToken)
			{
				downloaded = new T[1];
				index = 0;

				await DownloadItemAsync (seqid, headersOnly, doAsync, cancellationToken).ConfigureAwait (false);

				return downloaded[0];
			}

			public async Task<IList<T>> DownloadAsync (IList<int> seqids, bool headersOnly, bool doAsync, CancellationToken cancellationToken)
			{
				downloaded = new T[seqids.Count];
				index = 0;

				if ((Engine.Capabilities & Pop3Capabilities.Pipelining) == 0) {
					for (int i = 0; i < seqids.Count; i++)
						await DownloadItemAsync (seqids[i], headersOnly, doAsync, cancellationToken);

					return downloaded;
				}

				var commands = new Pop3Command[seqids.Count];
				Pop3Command pc = null;
				int id;

				for (int i = 0; i < seqids.Count; i++)
					commands[i] = QueueCommand (seqids[i], headersOnly, cancellationToken);

				pc = commands[commands.Length - 1];

				do {
					if (doAsync)
						id = await Engine.IterateAsync ().ConfigureAwait (false);
					else
						id = Engine.Iterate ();
				} while (id < pc.Id);

				for (int i = 0; i < commands.Length; i++) {
					if (commands[i].Status != Pop3CommandStatus.Ok)
						throw CreatePop3Exception (commands[i]);

					if (commands[i].Exception != null)
						throw commands[i].Exception;
				}

				return downloaded;
			}
		}

		class DownloadStreamContext : DownloadContext<Stream>
		{
			public DownloadStreamContext (Pop3Client client, ITransferProgress progress = null) : base (client, progress)
			{
			}

			protected override Stream Parse (Pop3Stream data, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();

				var stream = new MemoryBlockStream ();
				var buffer = new byte[4096];
				int nread;

				while ((nread = data.Read (buffer, 0, buffer.Length, cancellationToken)) > 0) {
					stream.Write (buffer, 0, nread);
					Update (nread);
				}

				stream.Position = 0;

				return stream;
			}

			protected override async Task<Stream> ParseAsync (Pop3Stream data, CancellationToken cancellationToken)
			{
				cancellationToken.ThrowIfCancellationRequested ();

				var stream = new MemoryBlockStream ();
				var buffer = new byte[4096];
				int nread;

				while ((nread = await data.ReadAsync (buffer, 0, buffer.Length, cancellationToken).ConfigureAwait (false)) > 0) {
					stream.Write (buffer, 0, nread);
					Update (nread);
				}

				stream.Position = 0;

				return stream;
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
					parser.SetStream (ParserOptions.Default, stream);

					return parser.ParseMessage (cancellationToken).Headers;
				}
			}

			protected override async Task<HeaderList> ParseAsync (Pop3Stream data, CancellationToken cancellationToken)
			{
				using (var stream = new ProgressStream (data, Update)) {
					parser.SetStream (ParserOptions.Default, stream);

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
					parser.SetStream (ParserOptions.Default, stream);

					return parser.ParseMessage (cancellationToken);
				}
			}

			protected override Task<MimeMessage> ParseAsync (Pop3Stream data, CancellationToken cancellationToken)
			{
				using (var stream = new ProgressStream (data, Update)) {
					parser.SetStream (ParserOptions.Default, stream);

					return parser.ParseMessageAsync (cancellationToken);
				}
			}
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
		public override HeaderList GetMessageHeaders (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.DownloadAsync (index + 1, true, false, cancellationToken).GetAwaiter ().GetResult ();
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
		/// <para>One or more of the <paramref name="indexes"/> are invalid.</para>
		/// <para>-or-</para>
		/// <para>No indexes were specified.</para>
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
		public override IList<HeaderList> GetMessageHeaders (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes specified.", nameof (indexes));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			var seqids = new int[indexes.Count];

			for (int i = 0; i < indexes.Count; i++) {
				if (indexes[i] < 0 || indexes[i] >= total)
					throw new ArgumentException ("One or more of the indexes are invalid.", nameof (indexes));

				seqids[i] = indexes[i] + 1;
			}

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.DownloadAsync (seqids, true, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override IList<HeaderList> GetMessageHeaders (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (startIndex < 0 || startIndex >= total)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (count < 0 || count > (total - startIndex))
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (count == 0)
				return new HeaderList[0];

			var seqids = new int[count];

			for (int i = 0; i < count; i++)
				seqids[i] = startIndex + i + 1;

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.DownloadAsync (seqids, true, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.DownloadAsync (index + 1, false, false, cancellationToken).GetAwaiter ().GetResult ();
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
		/// <para>One or more of the <paramref name="indexes"/> are invalid.</para>
		/// <para>-or-</para>
		/// <para>No indexes were specified.</para>
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
		public override IList<MimeMessage> GetMessages (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes specified.", nameof (indexes));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			var seqids = new int[indexes.Count];

			for (int i = 0; i < indexes.Count; i++) {
				if (indexes[i] < 0 || indexes[i] >= total)
					throw new ArgumentException ("One or more of the indexes are invalid.", nameof (indexes));

				seqids[i] = indexes[i] + 1;
			}

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.DownloadAsync (seqids, false, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override IList<MimeMessage> GetMessages (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (startIndex < 0 || startIndex >= total)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (count < 0 || count > (total - startIndex))
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (count == 0)
				return new MimeMessage[0];

			var seqids = new int[count];

			for (int i = 0; i < count; i++)
				seqids[i] = startIndex + i + 1;

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.DownloadAsync (seqids, false, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override Stream GetStream (int index, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.DownloadAsync (index + 1, headersOnly, false, cancellationToken).GetAwaiter ().GetResult ();
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
		/// <para>One or more of the <paramref name="indexes"/> are invalid.</para>
		/// <para>-or-</para>
		/// <para>No indexes were specified.</para>
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
		public override IList<Stream> GetStreams (IList<int> indexes, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes specified.", nameof (indexes));

			var seqids = new int[indexes.Count];

			for (int i = 0; i < indexes.Count; i++) {
				if (indexes[i] < 0 || indexes[i] >= total)
					throw new ArgumentException ("One or more of the indexes are invalid.", nameof (indexes));

				seqids[i] = indexes[i] + 1;
			}

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.DownloadAsync (seqids, headersOnly, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override IList<Stream> GetStreams (int startIndex, int count, bool headersOnly = false, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (startIndex < 0 || startIndex >= total)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (count < 0 || count > (total - startIndex))
				throw new ArgumentOutOfRangeException (nameof (count));

			if (count == 0)
				return new Stream[0];

			var seqids = new int[count];

			for (int i = 0; i < count; i++)
				seqids[i] = startIndex + i + 1;

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.DownloadAsync (seqids, headersOnly, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		Task DeleteMessageAsync (int index, bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (index < 0 || index >= total)
				throw new ArgumentOutOfRangeException (nameof (index));

			return SendCommandAsync (doAsync, cancellationToken, "DELE {0}", index + 1);
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
		public override void DeleteMessage (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			DeleteMessageAsync (index, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		async Task DeleteMessagesAsync (IList<int> indexes, bool doAsync, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes specified.", nameof (indexes));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			var seqids = new int[indexes.Count];

			for (int i = 0; i < indexes.Count; i++) {
				if (indexes[i] < 0 || indexes[i] >= total)
					throw new ArgumentException ("One or more of the indexes are invalid.", nameof (indexes));

				seqids[i] = indexes[i] + 1;
			}

			if ((Capabilities & Pop3Capabilities.Pipelining) == 0) {
				for (int i = 0; i < seqids.Length; i++)
					await SendCommandAsync (doAsync, cancellationToken, "DELE {0}", seqids[i]).ConfigureAwait (false);

				return;
			}

			var commands = new Pop3Command[seqids.Length];
			Pop3Command pc = null;
			int id;

			for (int i = 0; i < seqids.Length; i++) {
				pc = engine.QueueCommand (cancellationToken, null, "DELE {0}", seqids[i]);
				commands[i] = pc;
			}

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			for (int i = 0; i < commands.Length; i++) {
				if (commands[i].Status != Pop3CommandStatus.Ok)
					throw CreatePop3Exception (commands[i]);
			}
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
		/// <para>One or more of the <paramref name="indexes"/> are invalid.</para>
		/// <para>-or-</para>
		/// <para>No indexes were specified.</para>
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
		public override void DeleteMessages (IList<int> indexes, CancellationToken cancellationToken = default (CancellationToken))
		{
			DeleteMessagesAsync (indexes, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		async Task DeleteMessagesAsync (int startIndex, int count, bool doAsync, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (startIndex < 0 || startIndex >= total)
				throw new ArgumentOutOfRangeException (nameof (startIndex));

			if (count < 0 || count > (total - startIndex))
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			if (count == 0)
				return;

			if ((Capabilities & Pop3Capabilities.Pipelining) == 0) {
				for (int i = 0; i < count; i++)
					await SendCommandAsync (doAsync, cancellationToken, "DELE {0}", startIndex + i + 1).ConfigureAwait (false);

				return;
			}

			var commands = new Pop3Command[count];
			Pop3Command pc = null;
			int id;

			for (int i = 0; i < count; i++) {
				pc = engine.QueueCommand (cancellationToken, null, "DELE {0}", startIndex + i + 1);
				commands[i] = pc;
			}

			do {
				if (doAsync)
					id = await engine.IterateAsync ().ConfigureAwait (false);
				else
					id = engine.Iterate ();
			} while (id < pc.Id);

			for (int i = 0; i < commands.Length; i++) {
				if (commands[i].Status != Pop3CommandStatus.Ok)
					throw CreatePop3Exception (commands[i]);
			}
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
		public override void DeleteMessages (int startIndex, int count, CancellationToken cancellationToken = default (CancellationToken))
		{
			DeleteMessagesAsync (startIndex, count, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override void DeleteAllMessages (CancellationToken cancellationToken = default (CancellationToken))
		{
			if (total > 0)
				DeleteMessages (0, total, cancellationToken);
		}

		Task ResetAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return SendCommandAsync (doAsync, cancellationToken, "RSET");
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
		public override void Reset (CancellationToken cancellationToken = default (CancellationToken))
		{
			ResetAsync (false, cancellationToken).GetAwaiter ().GetResult ();
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
				engine.Disconnect ();

#if NETFX_CORE
				if (socket != null)
					socket.Dispose ();
#endif

				disposed = true;
			}

			base.Dispose (disposing);
		}
	}
}
