//
// AsyncSmtpClient.cs
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
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;
using MimeKit.IO;

using MailKit.Security;

namespace MailKit.Net.Smtp
{
	public partial class SmtpClient
	{
		async Task QueueCommandAsync (SmtpCommand type, string command, CancellationToken cancellationToken)
		{
			await Stream.QueueCommandAsync (command, cancellationToken).ConfigureAwait (false);
			queued.Add (type);
		}

		async Task<QueueResults> FlushCommandQueueAsync (MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, CancellationToken cancellationToken)
		{
			try {
				// Note: Queued commands are buffered by the stream
				await Stream.FlushAsync (cancellationToken).ConfigureAwait (false);
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
					var response = await Stream.ReadResponseAsync (cancellationToken).ConfigureAwait (false);
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

		async Task<SmtpResponse> SendCommandInternalAsync (string command, CancellationToken cancellationToken)
		{
			try {
				return await Stream.SendCommandAsync (command, cancellationToken).ConfigureAwait (false);
			} catch {
				Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), false);
				throw;
			}
		}

		/// <summary>
		/// Asynchronously send a custom command to the SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously sends a custom command to the SMTP server.</para>
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
		protected Task<SmtpResponse> SendCommandAsync (string command, CancellationToken cancellationToken = default)
		{
			if (command == null)
				throw new ArgumentNullException (nameof (command));

			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient must be connected before you can send commands.");

			if (!command.EndsWith ("\r\n", StringComparison.Ordinal))
				command += "\r\n";

			return SendCommandInternalAsync (command, cancellationToken);
		}

		Task<SmtpResponse> SendEhloAsync (bool connecting, string helo, CancellationToken cancellationToken)
		{
			var command = CreateEhloCommand (helo);

			if (connecting)
				return Stream.SendCommandAsync (command, cancellationToken);

			return SendCommandInternalAsync (command, cancellationToken);
		}

		async Task EhloAsync (bool connecting, CancellationToken cancellationToken)
		{
			var response = await SendEhloAsync (connecting, "EHLO", cancellationToken).ConfigureAwait (false);

			if (response.StatusCode != SmtpStatusCode.Ok) {
				// Try sending HELO instead...
				response = await SendEhloAsync (connecting, "HELO", cancellationToken).ConfigureAwait (false);

				if (response.StatusCode != SmtpStatusCode.Ok)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			} else {
				UpdateCapabilities (response);
			}
		}

		/// <summary>
		/// Asynchronously authenticate using the specified SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Authenticates using the specified SASL mechanism.</para>
		/// <para>For a list of available SASL authentication mechanisms supported by the server,
		/// check the <see cref="AuthenticationMechanisms"/> property after the service has been
		/// connected.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task AuthenticateAsync (SaslMechanism mechanism, CancellationToken cancellationToken = default)
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
					challenge = await mechanism.ChallengeAsync (null, cancellationToken).ConfigureAwait (false);
					command = string.Format ("AUTH {0} {1}\r\n", mechanism.MechanismName, challenge);
				} else {
					command = string.Format ("AUTH {0}\r\n", mechanism.MechanismName);
				}

				detector.IsAuthenticating = true;

				try {
					response = await SendCommandInternalAsync (command, cancellationToken).ConfigureAwait (false);

					if (response.StatusCode == SmtpStatusCode.AuthenticationMechanismTooWeak)
						throw new AuthenticationException (response.Response);

					try {
						while (response.StatusCode == SmtpStatusCode.AuthenticationChallenge) {
							challenge = await mechanism.ChallengeAsync (response.Response, cancellationToken).ConfigureAwait (false);
							response = await SendCommandInternalAsync (challenge + "\r\n", cancellationToken).ConfigureAwait (false);
						}

						saslException = null;
					} catch (SaslException ex) {
						// reset the authentication state
						response = await SendCommandInternalAsync ("\r\n", cancellationToken).ConfigureAwait (false);
						saslException = ex;
					}
				} finally {
					detector.IsAuthenticating = false;
				}

				if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful) {
					if (mechanism.NegotiatedSecurityLayer)
						await EhloAsync (false, cancellationToken).ConfigureAwait (false);
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

		/// <summary>
		/// Asynchronously authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously authenticates using the supplied credentials.</para>
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
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task AuthenticateAsync (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
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
						challenge = await sasl.ChallengeAsync (null, cancellationToken).ConfigureAwait (false);
						command = string.Format ("AUTH {0} {1}\r\n", authmech, challenge);
					} else {
						command = string.Format ("AUTH {0}\r\n", authmech);
					}

					detector.IsAuthenticating = true;
					saslException = null;

					try {
						response = await SendCommandInternalAsync (command, cancellationToken).ConfigureAwait (false);

						if (response.StatusCode == SmtpStatusCode.AuthenticationMechanismTooWeak)
							continue;

						try {
							while (!sasl.IsAuthenticated) {
								if (response.StatusCode != SmtpStatusCode.AuthenticationChallenge)
									break;

								challenge = await sasl.ChallengeAsync (response.Response, cancellationToken).ConfigureAwait (false);
								response = await SendCommandInternalAsync (challenge + "\r\n", cancellationToken).ConfigureAwait (false);
							}

							saslException = null;
						} catch (SaslException ex) {
							// reset the authentication state
							response = await SendCommandInternalAsync ("\r\n", cancellationToken).ConfigureAwait (false);
							saslException = ex;
						}
					} finally {
						detector.IsAuthenticating = false;
					}

					if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful) {
						if (sasl.NegotiatedSecurityLayer)
							await EhloAsync (false, cancellationToken).ConfigureAwait (false);
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

		async Task SslHandshakeAsync (SslStream ssl, string host, CancellationToken cancellationToken)
		{
#if NET5_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
			await ssl.AuthenticateAsClientAsync (GetSslClientAuthenticationOptions (host, ValidateRemoteCertificate), cancellationToken).ConfigureAwait (false);
#else
			await ssl.AuthenticateAsClientAsync (host, ClientCertificates, SslProtocols, CheckCertificateRevocation).ConfigureAwait (false);
#endif
		}

		async Task PostConnectAsync (Stream stream, string host, int port, SecureSocketOptions options, bool starttls, CancellationToken cancellationToken)
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
				var response = await Stream.ReadResponseAsync (cancellationToken).ConfigureAwait (false);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				await EhloAsync (true, cancellationToken).ConfigureAwait (false);

				if (options == SecureSocketOptions.StartTls && (capabilities & SmtpCapabilities.StartTLS) == 0)
					throw new NotSupportedException ("The SMTP server does not support the STARTTLS extension.");

				if (starttls && (capabilities & SmtpCapabilities.StartTLS) != 0) {
					response = await Stream.SendCommandAsync ("STARTTLS\r\n", cancellationToken).ConfigureAwait (false);
					if (response.StatusCode != SmtpStatusCode.ServiceReady)
						throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

					try {
						var tls = new SslStream (stream, false, ValidateRemoteCertificate);
						Stream.Stream = tls;

						await SslHandshakeAsync (tls, host, cancellationToken).ConfigureAwait (false);
					} catch (Exception ex) {
						throw SslHandshakeException.Create (ref sslValidationInfo, ex, true, "SMTP", host, port, 465, 25, 587);
					}

					secure = true;

					// Send EHLO again and get the new list of supported extensions
					await EhloAsync (true, cancellationToken).ConfigureAwait (false);
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

		/// <summary>
		/// Asynchronously establish a connection to the specified SMTP or SMTP/S server.
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
		/// <a href="Overload_MailKit_Net_Smtp_SmtpClient_ConnectAsync.htm">Connect</a>
		/// methods may be re-used if an application wishes to send multiple messages
		/// to the same SMTP server. Since connecting and authenticating can be expensive
		/// operations, re-using a connection can significantly improve performance when
		/// sending a large number of messages to the same SMTP server over a short
		/// period of time.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task ConnectAsync (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			ValidateArguments (host, port);

			capabilities = SmtpCapabilities.None;
			AuthenticationMechanisms.Clear ();
			MaxSize = 0;

			ComputeDefaultValues (host, ref port, ref options, out uri, out var starttls);

			using var operation = StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				var stream = await ConnectNetworkAsync (host, port, cancellationToken).ConfigureAwait (false);
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						await SslHandshakeAsync (ssl, host, cancellationToken).ConfigureAwait (false);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "SMTP", host, port, 465, 25, 587);
					}

					secure = true;
					stream = ssl;
				} else {
					secure = false;
				}

				await PostConnectAsync (stream, host, port, options, starttls, cancellationToken).ConfigureAwait (false);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified SMTP or SMTP/S server using the provided socket.
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
		/// <returns>An asynchronous task context.</returns>
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
		public override Task ConnectAsync (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			ValidateArguments (socket, host, port);

			return ConnectAsync (new NetworkStream (socket, true), host, port, options, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified SMTP or SMTP/S server using the provided socket.
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
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task ConnectAsync (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
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
						await SslHandshakeAsync (ssl, host, cancellationToken).ConfigureAwait (false);
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

				await PostConnectAsync (network, host, port, options, starttls, cancellationToken).ConfigureAwait (false);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		/// <summary>
		/// Asynchronously disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a <c>QUIT</c> command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="quit">If set to <c>true</c>, a <c>QUIT</c> command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		public override async Task DisconnectAsync (bool quit, CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!IsConnected)
				return;

			if (quit) {
				try {
					await Stream.SendCommandAsync ("QUIT\r\n", cancellationToken).ConfigureAwait (false);
				} catch (OperationCanceledException) {
				} catch (SmtpProtocolException) {
				} catch (SmtpCommandException) {
				} catch (IOException) {
				}
			}

			Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), true);
		}

		/// <summary>
		/// Asynchronously ping the SMTP server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task NoOpAsync (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new ServiceNotConnectedException ("The SmtpClient is not connected.");

			var response = await SendCommandInternalAsync ("NOOP\r\n", cancellationToken).ConfigureAwait (false);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpCommandException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		/// <summary>
		/// Asynchronously get the size of the message.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously calculates the size of the message in bytes.</para>
		/// <para>This method is called by <a href="Overload_MailKit_MailTransport_SendAsync.htm">SendAsync</a>
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
		protected virtual async Task<long> GetSizeAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken)
		{
			using (var measure = new MeasuringStream ()) {
				await message.WriteToAsync (options, measure, cancellationToken).ConfigureAwait (false);

				return measure.Length;
			}
		}

		async Task MailFromAsync (FormatOptions options, MimeMessage message, MailboxAddress mailbox, SmtpExtensions extensions, long size, bool pipeline, CancellationToken cancellationToken)
		{
			var command = CreateMailFromCommand (options, message, mailbox, extensions, size);

			if (pipeline) {
				await QueueCommandAsync (SmtpCommand.MailFrom, command, cancellationToken).ConfigureAwait (false);
				return;
			}

			var response = await Stream.SendCommandAsync (command, cancellationToken).ConfigureAwait (false);

			ParseMailFromResponse (message, mailbox, response);
		}

		async Task<bool> RcptToAsync (FormatOptions options, MimeMessage message, MailboxAddress mailbox, bool pipeline, CancellationToken cancellationToken)
		{
			var command = CreateRcptToCommand (options, message, mailbox);

			if (pipeline) {
				await QueueCommandAsync (SmtpCommand.RcptTo, command, cancellationToken).ConfigureAwait (false);
				return false;
			}

			var response = await Stream.SendCommandAsync (command, cancellationToken).ConfigureAwait (false);

			return ParseRcptToResponse (message, mailbox, response);
		}

		async Task<string> BdatAsync (FormatOptions options, MimeMessage message, long size, CancellationToken cancellationToken, ITransferProgress progress)
		{
			var command = string.Format (CultureInfo.InvariantCulture, "BDAT {0} LAST\r\n", size);

			await Stream.QueueCommandAsync (command, cancellationToken).ConfigureAwait (false);

			if (progress != null) {
				var ctx = new SendContext (progress, size);

				using (var stream = new ProgressStream (Stream, ctx.Update)) {
					await message.WriteToAsync (options, stream, cancellationToken).ConfigureAwait (false);
					await stream.FlushAsync (cancellationToken).ConfigureAwait (false);
				}
			} else {
				await message.WriteToAsync (options, Stream, cancellationToken).ConfigureAwait (false);
				await Stream.FlushAsync (cancellationToken).ConfigureAwait (false);
			}

			var response = await Stream.ReadResponseAsync (cancellationToken).ConfigureAwait (false);

			return ParseBdatResponse (message, response);
		}

		async Task<string> MessageDataAsync (FormatOptions options, MimeMessage message, long size, CancellationToken cancellationToken, ITransferProgress progress)
		{
			if (progress != null) {
				var ctx = new SendContext (progress, size);

				using (var stream = new ProgressStream (Stream, ctx.Update)) {
					using (var filtered = new FilteredStream (stream)) {
						filtered.Add (new SmtpDataFilter ());

						await message.WriteToAsync (options, filtered, cancellationToken).ConfigureAwait (false);
						await filtered.FlushAsync (cancellationToken).ConfigureAwait (false);
					}
				}
			} else {
				using (var filtered = new FilteredStream (Stream)) {
					filtered.Add (new SmtpDataFilter ());

					await message.WriteToAsync (options, filtered, cancellationToken).ConfigureAwait (false);
					await filtered.FlushAsync (cancellationToken).ConfigureAwait (false);
				}
			}

			await Stream.WriteAsync (EndData, 0, EndData.Length, cancellationToken).ConfigureAwait (false);
			await Stream.FlushAsync (cancellationToken).ConfigureAwait (false);

			var response = await Stream.ReadResponseAsync (cancellationToken).ConfigureAwait (false);

			return ParseMessageDataResponse (message, response);
		}

		async Task ResetAsync (CancellationToken cancellationToken)
		{
			var response = await SendCommandInternalAsync ("RSET\r\n", cancellationToken).ConfigureAwait (false);

			if (response.StatusCode != SmtpStatusCode.Ok)
				Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), false);
		}

		async Task<string> SendAsync (FormatOptions options, MimeMessage message, MailboxAddress sender, IList<MailboxAddress> recipients, CancellationToken cancellationToken, ITransferProgress progress)
		{
			var format = Prepare (options, message, sender, recipients, out var extensions);
			var pipeline = (capabilities & SmtpCapabilities.Pipelining) != 0;
			var bdat = UseBdatCommand (extensions);
			long size;

			if (bdat || (Capabilities & SmtpCapabilities.Size) != 0 || progress != null) {
				size = await GetSizeAsync (format, message, cancellationToken).ConfigureAwait (false);
			} else {
				size = -1;
			}

			using var operation = StartNetworkOperation (NetworkOperationKind.Send);

			try {
				// Note: if PIPELINING is supported, MailFrom() and RcptTo() will
				// queue their commands instead of sending them immediately.
				await MailFromAsync (format, message, sender, extensions, size, pipeline, cancellationToken).ConfigureAwait (false);

				int recipientsAccepted = 0;
				for (int i = 0; i < recipients.Count; i++) {
					if (await RcptToAsync (format, message, recipients[i], pipeline, cancellationToken).ConfigureAwait (false))
						recipientsAccepted++;
				}

				if (queued.Count > 0) {
					// Note: if PIPELINING is supported, this will flush all outstanding
					// MAIL FROM and RCPT TO commands to the server and then process
					// all of their responses.
					var results = await FlushCommandQueueAsync (message, sender, recipients, cancellationToken).ConfigureAwait (false);

					recipientsAccepted = results.RecipientsAccepted;

					if (results.FirstException != null)
						throw results.FirstException;
				}

				if (recipientsAccepted == 0) {
					OnNoRecipientsAccepted (message);
					throw new SmtpCommandException (SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "No recipients were accepted.");
				}

				if (bdat)
					return await BdatAsync (format, message, size, cancellationToken, progress).ConfigureAwait (false);

				var dataResponse = await Stream.SendCommandAsync ("DATA\r\n", cancellationToken).ConfigureAwait (false);

				ParseDataResponse (dataResponse);
				dataResponse = null;

				return await MessageDataAsync (format, message, size, cancellationToken, progress).ConfigureAwait (false);
			} catch (ServiceNotAuthenticatedException ex) {
				operation.SetError (ex);

				// do not disconnect
				await ResetAsync (cancellationToken).ConfigureAwait (false);
				throw;
			} catch (SmtpCommandException ex) {
				operation.SetError (ex);

				// do not disconnect
				await ResetAsync (cancellationToken).ConfigureAwait (false);
				throw;
			} catch (Exception ex) {
				operation.SetError (ex);

				Disconnect (uri.Host, uri.Port, GetSecureSocketOptions (uri), false);
				throw;
			}
		}

		/// <summary>
		/// Asynchronously send the specified message.
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
		public override Task<string> SendAsync (FormatOptions options, MimeMessage message, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			ValidateArguments (options, message, out var sender, out var recipients);

			return SendAsync (options, message, sender, recipients, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously send the specified message using the supplied sender and recipients.
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
		public override Task<string> SendAsync (FormatOptions options, MimeMessage message, MailboxAddress sender, IEnumerable<MailboxAddress> recipients, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var rcpts = ValidateArguments (options, message, sender, recipients);

			return SendAsync (options, message, sender, rcpts, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously expand a mailing address alias.
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
		public async Task<InternetAddressList> ExpandAsync (string alias, CancellationToken cancellationToken = default)
		{
			var response = await SendCommandInternalAsync (CreateExpandCommand (alias), cancellationToken).ConfigureAwait (false);

			return ParseExpandResponse (response);
		}

		/// <summary>
		/// Asynchronously verify the existence of a mailbox address.
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
		public async Task<MailboxAddress> VerifyAsync (string address, CancellationToken cancellationToken = default)
		{
			var response = await SendCommandInternalAsync (CreateVerifyCommand (address), cancellationToken).ConfigureAwait (false);

			return ParseVerifyResponse (response);
		}
	}
}
