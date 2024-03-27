//
// AsyncPop3Client.cs
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
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;

using MailKit.Security;

namespace MailKit.Net.Pop3
{
	public partial class Pop3Client
	{
		Task SendCommandAsync (CancellationToken token, string command)
		{
			engine.QueueCommand (null, Encoding.ASCII, command);

			return engine.RunAsync (true, token);
		}

		Task<string> SendCommandAsync (CancellationToken token, string format, params object[] args)
		{
			return SendCommandAsync (token, Encoding.ASCII, format, args);
		}

		async Task<string> SendCommandAsync (CancellationToken token, Encoding encoding, string format, params object[] args)
		{
			var pc = engine.QueueCommand (null, encoding, format, args);

			await engine.RunAsync (true, token).ConfigureAwait (false);

			return pc.StatusText ?? string.Empty;
		}

		async Task ProbeCapabilitiesAsync (CancellationToken cancellationToken)
		{
			if ((engine.Capabilities & Pop3Capabilities.UIDL) == 0 && (probed & ProbedCapabilities.UIDL) == 0) {
				// if the message count is > 0, we can probe the UIDL command
				if (total > 0) {
					try {
						await GetMessageUidAsync (0, cancellationToken).ConfigureAwait (false);
					} catch (NotSupportedException) {
					}
				}
			}
		}

		async Task<int> UpdateMessageCountAsync (CancellationToken cancellationToken)
		{
			engine.QueueCommand (ProcessStatResponse, "STAT\r\n");

			await engine.RunAsync (true, cancellationToken).ConfigureAwait (false);

			return Count;
		}

		async Task OnAuthenticatedAsync (string message, CancellationToken cancellationToken)
		{
			engine.State = Pop3EngineState.Transaction;

			await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);
			await UpdateMessageCountAsync (cancellationToken).ConfigureAwait (false);
			await ProbeCapabilitiesAsync (cancellationToken).ConfigureAwait (false);
			OnAuthenticated (message);
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
		public override async Task AuthenticateAsync (SaslMechanism mechanism, CancellationToken cancellationToken = default)
		{
			CheckCanAuthenticate (mechanism, cancellationToken);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				var saslUri = new Uri ("pop://" + engine.Uri.Host);
				var ctx = GetSaslAuthContext (mechanism, saslUri);

				var pc = await ctx.AuthenticateAsync (cancellationToken).ConfigureAwait (false);

				if (pc.Status == Pop3CommandStatus.Error)
					throw new AuthenticationException ();

				pc.ThrowIfError ();

				await OnAuthenticatedAsync (ctx.AuthMessage, cancellationToken).ConfigureAwait (false);
			} catch (Exception ex) {
				operation.SetError (ex);
				throw;
			}
		}

		/// <summary>
		/// Asynchronously authenticates using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously authenticates using the supplied credentials.</para>
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
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task AuthenticateAsync (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
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
						message = await SendCommandAsync (cancellationToken, encoding, apop).ConfigureAwait (false);
						engine.State = Pop3EngineState.Transaction;
					} catch (Pop3CommandException) {
					} finally {
						detector.IsAuthenticating = false;
					}

					if (engine.State == Pop3EngineState.Transaction) {
						await OnAuthenticatedAsync (message ?? string.Empty, cancellationToken).ConfigureAwait (false);
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

						var pc = await ctx.AuthenticateAsync (cancellationToken).ConfigureAwait (false);

						if (pc.Status == Pop3CommandStatus.Error)
							continue;

						pc.ThrowIfError ();

						await OnAuthenticatedAsync (ctx.AuthMessage, cancellationToken).ConfigureAwait (false);
						return;
					}
				}

				// fall back to the classic USER & PASS commands...
				cred = credentials.GetCredential (saslUri, "DEFAULT");
				userName = utf8 ? SaslMechanism.SaslPrep (cred.UserName) : cred.UserName;
				password = utf8 ? SaslMechanism.SaslPrep (cred.Password) : cred.Password;
				detector.IsAuthenticating = true;

				try {
					await SendCommandAsync (cancellationToken, encoding, "USER {0}\r\n", userName).ConfigureAwait (false);
					message = await SendCommandAsync (cancellationToken, encoding, "PASS {0}\r\n", password).ConfigureAwait (false);
				} catch (Pop3CommandException) {
					throw new AuthenticationException ();
				} finally {
					detector.IsAuthenticating = false;
				}

				await OnAuthenticatedAsync (message, cancellationToken).ConfigureAwait (false);
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
			probed = ProbedCapabilities.None;

			try {
				ProtocolLogger.LogConnect (engine.Uri);
			} catch {
				stream.Dispose ();
				secure = false;
				throw;
			}

			var pop3 = new Pop3Stream (stream, ProtocolLogger);

			await engine.ConnectAsync (pop3, cancellationToken).ConfigureAwait (false);

			try {
				await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);

				if (options == SecureSocketOptions.StartTls && (engine.Capabilities & Pop3Capabilities.StartTLS) == 0)
					throw new NotSupportedException ("The POP3 server does not support the STLS extension.");

				if (starttls && (engine.Capabilities & Pop3Capabilities.StartTLS) != 0) {
					await SendCommandAsync (cancellationToken, "STLS\r\n").ConfigureAwait (false);

					try {
						var tls = new SslStream (stream, false, ValidateRemoteCertificate);
						engine.Stream.Stream = tls;

						await SslHandshakeAsync (tls, host, cancellationToken).ConfigureAwait (false);
					} catch (Exception ex) {
						throw SslHandshakeException.Create (ref sslValidationInfo, ex, true, "POP3", host, port, 995, 110);
					}

					secure = true;

					// re-issue a CAPA command
					await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);
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
		/// Asynchronously establish a connection to the specified POP3 or POP3/S server.
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
		public override async Task ConnectAsync (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (host, port);

			ComputeDefaultValues (host, ref port, ref options, out var uri, out var starttls);

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Connect);

			try {
				var stream = await ConnectNetworkAsync (host, port, cancellationToken).ConfigureAwait (false);
				stream.WriteTimeout = timeout;
				stream.ReadTimeout = timeout;

				engine.Uri = uri;

				if (options == SecureSocketOptions.SslOnConnect) {
					var ssl = new SslStream (stream, false, ValidateRemoteCertificate);

					try {
						await SslHandshakeAsync (ssl, host, cancellationToken).ConfigureAwait (false);
					} catch (Exception ex) {
						ssl.Dispose ();

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "POP3", host, port, 995, 110);
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
		/// Asynchronously establish a connection to the specified POP3 or POP3/S server using the provided socket.
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
		public override Task ConnectAsync (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (socket, host, port);

			return ConnectAsync (new NetworkStream (socket, true), host, port, options, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified POP3 or POP3/S server using the provided stream.
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
		/// <returns>An asynchronous task context.</returns>
		/// <param name="stream">The socket to use for the connection.</param>
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
		public override async Task ConnectAsync (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
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
						await SslHandshakeAsync (ssl, host, cancellationToken).ConfigureAwait (false);
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
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
		/// </example>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="quit">If set to <c>true</c>, a <c>QUIT</c> command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		public override async Task DisconnectAsync (bool quit, CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					await SendCommandAsync (cancellationToken, "QUIT\r\n").ConfigureAwait (false);
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
		/// Asynchronously get the message count.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the message count.
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
		public override Task<int> GetMessageCountAsync (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return UpdateMessageCountAsync (cancellationToken);
		}

		/// <summary>
		/// Ping the POP3 server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task NoOpAsync (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return SendCommandAsync (cancellationToken, "NOOP\r\n");
		}

		/// <summary>
		/// Asynchronously enable UTF8 mode.
		/// </summary>
		/// <remarks>
		/// The POP3 UTF8 extension allows the client to retrieve messages in the UTF-8 encoding and
		/// may also allow the user to authenticate using a UTF-8 encoded username or password.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public async Task EnableUTF8Async (CancellationToken cancellationToken = default)
		{
			if (!CheckCanEnableUTF8 ())
				return;

			await SendCommandAsync (cancellationToken, "UTF8\r\n").ConfigureAwait (false);
			utf8 = true;
		}

		static async Task ReadLangResponseAsync (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			var langs = (List<Pop3Language>) pc.UserData;

			do {
				var response = await engine.ReadLineAsync (cancellationToken).ConfigureAwait (false);

				if (response == ".")
					break;

				var tokens = response.Split (Space, 2);
				if (tokens.Length != 2)
					continue;

				langs.Add (new Pop3Language (tokens[0], tokens[1]));
			} while (true);
		}

		/// <summary>
		/// Asynchronously get the list of languages supported by the POP3 server.
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
		public async Task<IList<Pop3Language>> GetLanguagesAsync (CancellationToken cancellationToken = default)
		{
			var pc = QueueLangCommand (out var langs);

			await engine.RunAsync (true, cancellationToken).ConfigureAwait (false);

			return new ReadOnlyCollection<Pop3Language> (langs);
		}

		/// <summary>
		/// Asynchronously set the language used by the POP3 server for error messages.
		/// </summary>
		/// <remarks>
		/// If the POP3 server supports the LANG extension, it is possible to
		/// set the language used by the POP3 server for error messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public Task SetLanguageAsync (string lang, CancellationToken cancellationToken = default)
		{
			CheckCanSetLanguage (lang);

			return SendCommandAsync (cancellationToken, $"LANG {lang}\r\n");
		}

		/// <summary>
		/// Asynchronously get the UID of the message at the specified index.
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
		public override async Task<string> GetMessageUidAsync (int index, CancellationToken cancellationToken = default)
		{
			var pc = QueueUidlCommand (index);

			await engine.RunAsync (false, cancellationToken).ConfigureAwait (false);

			return OnUidlComplete<string> (pc);
		}

		static async Task ReadUidlAllResponseAsync (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			do {
				var response = await engine.ReadLineAsync (cancellationToken).ConfigureAwait (false);

				if (response == ".")
					break;

				if (pc.Exception != null)
					continue;

				ParseUidlAllResponse (pc, response);
			} while (true);
		}

		/// <summary>
		/// Asynchronously get the full list of available message UIDs.
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
		public override async Task<IList<string>> GetMessageUidsAsync (CancellationToken cancellationToken = default)
		{
			var pc = QueueUidlCommand ();

			await engine.RunAsync (false, cancellationToken).ConfigureAwait (false);

			return OnUidlComplete<List<string>> (pc);
		}

		/// <summary>
		/// Asynchronously get the size of the specified message, in bytes.
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
		public override async Task<int> GetMessageSizeAsync (int index, CancellationToken cancellationToken = default)
		{
			var pc = QueueListCommand (index);

			await engine.RunAsync (true, cancellationToken).ConfigureAwait (false);

			return (int) pc.UserData;
		}

		static async Task ReadListAllResponseAsync (Pop3Engine engine, Pop3Command pc, CancellationToken cancellationToken)
		{
			do {
				var response = await engine.ReadLineAsync (cancellationToken).ConfigureAwait (false);

				if (response == ".")
					break;

				if (pc.Exception != null)
					continue;

				ParseListAllResponse (pc, response);
			} while (true);
		}

		/// <summary>
		/// Asynchronously get the sizes for all available messages, in bytes.
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
		public override async Task<IList<int>> GetMessageSizesAsync (CancellationToken cancellationToken = default)
		{
			var sizes = QueueListCommand ();

			await engine.RunAsync (true, cancellationToken).ConfigureAwait (false);

			return sizes;
		}

		/// <summary>
		/// Asynchronously get the headers for the message at the specified index.
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
		public override Task<HeaderList> GetMessageHeadersAsync (int index, CancellationToken cancellationToken = default)
		{
			CheckCanDownload (index);

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.DownloadAsync (index, true, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the headers for the messages at the specified indexes.
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
		public override Task<IList<HeaderList>> GetMessageHeadersAsync (IList<int> indexes, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDownload (indexes))
				return Task.FromResult ((IList<HeaderList>) Array.Empty<HeaderList> ());

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.DownloadAsync (indexes, true, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the headers of the messages within the specified range.
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
		public override Task<IList<HeaderList>> GetMessageHeadersAsync (int startIndex, int count, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDownload (startIndex, count))
				return Task.FromResult ((IList<HeaderList>) Array.Empty<HeaderList> ());

			var ctx = new DownloadHeaderContext (this, parser);

			return ctx.DownloadAsync (startIndex, count, true, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the message at the specified index.
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
		public override Task<MimeMessage> GetMessageAsync (int index, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			CheckCanDownload (index);

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.DownloadAsync (index, false, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the messages at the specified indexes.
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
		public override Task<IList<MimeMessage>> GetMessagesAsync (IList<int> indexes, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (indexes))
				return Task.FromResult ((IList<MimeMessage>) Array.Empty<MimeMessage> ());

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.DownloadAsync (indexes, false, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the messages within the specified range.
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
		public override Task<IList<MimeMessage>> GetMessagesAsync (int startIndex, int count, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (startIndex, count))
				return Task.FromResult ((IList<MimeMessage>) Array.Empty<MimeMessage> ());

			var ctx = new DownloadMessageContext (this, parser, progress);

			return ctx.DownloadAsync (startIndex, count, false, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the message or header stream at the specified index.
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
		public override Task<Stream> GetStreamAsync (int index, bool headersOnly = false, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			CheckCanDownload (index);

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.DownloadAsync (index, headersOnly, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the message or header streams at the specified indexes.
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
		public override Task<IList<Stream>> GetStreamsAsync (IList<int> indexes, bool headersOnly = false, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (indexes))
				return Task.FromResult ((IList<Stream>) Array.Empty<Stream> ());

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.DownloadAsync (indexes, headersOnly, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the message or header streams within the specified range.
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
		public override Task<IList<Stream>> GetStreamsAsync (int startIndex, int count, bool headersOnly = false, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (!CheckCanDownload (startIndex, count))
				return Task.FromResult ((IList<Stream>) Array.Empty<Stream> ());

			var ctx = new DownloadStreamContext (this, progress);

			return ctx.DownloadAsync (startIndex, count, headersOnly, cancellationToken);
		}

		/// <summary>
		/// Asynchronously mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="DownloadMessages"/>
		/// </example>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task DeleteMessageAsync (int index, CancellationToken cancellationToken = default)
		{
			CheckCanDelete (index, out string seqid);

			return SendCommandAsync (cancellationToken, $"DELE {seqid}\r\n");
		}

		/// <summary>
		/// Asynchronously mark the specified messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task DeleteMessagesAsync (IList<int> indexes, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDelete (indexes))
				return;

			if ((Capabilities & Pop3Capabilities.Pipelining) == 0) {
				for (int i = 0; i < indexes.Count; i++)
					await SendCommandAsync (cancellationToken, "DELE {0}\r\n", indexes[i] + 1).ConfigureAwait (false);

				return;
			}

			for (int i = 0; i < indexes.Count; i++)
				engine.QueueCommand (null, "DELE {0}\r\n", indexes[i] + 1);

			await engine.RunAsync (true, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Asynchronously mark the specified range of messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\Pop3Examples.cs" region="BatchDownloadMessages"/>
		/// </example>
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task DeleteMessagesAsync (int startIndex, int count, CancellationToken cancellationToken = default)
		{
			if (!CheckCanDelete (startIndex, count))
				return;

			if ((Capabilities & Pop3Capabilities.Pipelining) == 0) {
				for (int i = 0; i < count; i++)
					await SendCommandAsync (cancellationToken, "DELE {0}\r\n", startIndex + i + 1).ConfigureAwait (false);

				return;
			}

			for (int i = 0; i < count; i++)
				engine.QueueCommand (null, "DELE {0}\r\n", startIndex + i + 1);

			await engine.RunAsync (true, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Asynchronously mark all messages for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task DeleteAllMessagesAsync (CancellationToken cancellationToken = default)
		{
			if (total > 0)
				return DeleteMessagesAsync (0, total, cancellationToken);

			return Task.CompletedTask;
		}

		/// <summary>
		/// Asynchronously reset the state of all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="Pop3Client.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <returns>An awaitable task.</returns>
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
		public override Task ResetAsync (CancellationToken cancellationToken = default)
		{
			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return SendCommandAsync (cancellationToken, "RSET\r\n");
		}
	}
}
