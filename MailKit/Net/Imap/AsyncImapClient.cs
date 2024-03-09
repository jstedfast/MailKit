//
// AsyncImapClient.cs
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

using MailKit.Security;

namespace MailKit.Net.Imap
{
	public partial class ImapClient
	{
		/// <summary>
		/// Asynchronously enable compression over the IMAP connection.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously enables compression over the IMAP connection.</para>
		/// <para>If the IMAP server supports the <see cref="ImapCapabilities.Compress"/> extension,
		/// it is possible at any point after connecting to enable compression to reduce network
		/// bandwidth usage. Ideally, this method should be called before authenticating.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		/// The IMAP server does not support the COMPRESS extension.
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
		public async Task CompressAsync (CancellationToken cancellationToken = default)
		{
			var ic = QueueCompressCommand (cancellationToken);

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessCompressResponse (ic);
		}

		/// <summary>
		/// Asynchronously enable the QRESYNC feature.
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
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task EnableQuickResyncAsync (CancellationToken cancellationToken = default)
		{
			if (!TryQueueEnableQuickResyncCommand (cancellationToken, out var ic))
				return;

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessEnableResponse (ic);
		}

		/// <summary>
		/// Asynchronously enable the UTF8=ACCEPT extension.
		/// </summary>
		/// <remarks>
		/// Enables the <a href="https://tools.ietf.org/html/rfc6855">UTF8=ACCEPT</a> extension.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public async Task EnableUTF8Async (CancellationToken cancellationToken = default)
		{
			if (!TryQueueEnableUTF8Command (cancellationToken, out var ic))
				return;

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessEnableResponse (ic);
		}

		/// <summary>
		/// Asynchronously identify the client implementation to the server and obtain the server implementation details.
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
		public async Task<ImapImplementation> IdentifyAsync (ImapImplementation clientImplementation, CancellationToken cancellationToken = default)
		{
			var ic = QueueIdentifyCommand (clientImplementation, cancellationToken);

			await engine.RunAsync (ic).ConfigureAwait (false);

			return ProcessIdentifyResponse (ic);
		}

		async Task OnAuthenticatedAsync (string message, CancellationToken cancellationToken)
		{
			await engine.QueryNamespacesAsync (cancellationToken).ConfigureAwait (false);
			await engine.QuerySpecialFoldersAsync (cancellationToken).ConfigureAwait (false);
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
		public override async Task AuthenticateAsync (SaslMechanism mechanism, CancellationToken cancellationToken = default)
		{
			CheckCanAuthenticate (mechanism, cancellationToken);

			int capabilitiesVersion = engine.CapabilitiesVersion;
			ImapCommand ic = null;

			ConfigureSaslMechanism (mechanism);

			var command = string.Format ("AUTHENTICATE {0}", mechanism.MechanismName);

			if ((engine.Capabilities & ImapCapabilities.SaslIR) != 0 && mechanism.SupportsInitialResponse) {
				string ir = await mechanism.ChallengeAsync (null, cancellationToken).ConfigureAwait (false);
				command += " " + ir + "\r\n";
			} else {
				command += "\r\n";
			}

			ic = engine.QueueCommand (cancellationToken, null, command);
			ic.ContinuationHandler = async (imap, cmd, text, xdoAsync) => {
				string challenge = await mechanism.ChallengeAsync (text, cmd.CancellationToken).ConfigureAwait (false);
				var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");

				await imap.Stream.WriteAsync (buf, 0, buf.Length, cmd.CancellationToken).ConfigureAwait (false);
				await imap.Stream.FlushAsync (cmd.CancellationToken).ConfigureAwait (false);
			};

			using var operation = engine.StartNetworkOperation (NetworkOperationKind.Authenticate);

			try {
				detector.IsAuthenticating = true;

				try {
					await engine.RunAsync (ic).ConfigureAwait (false);
				} finally {
					detector.IsAuthenticating = false;
				}

				ProcessAuthenticateResponse (ic, mechanism);

				// Query the CAPABILITIES again if the server did not include an
				// untagged CAPABILITIES response to the AUTHENTICATE command.
				if (engine.CapabilitiesVersion == capabilitiesVersion)
					await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);

				await OnAuthenticatedAsync (ic.ResponseText ?? string.Empty, cancellationToken).ConfigureAwait (false);
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
		public override async Task AuthenticateAsync (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default)
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
						string ir = await sasl.ChallengeAsync (null, cancellationToken).ConfigureAwait (false);

						command += " " + ir + "\r\n";
					} else {
						command += "\r\n";
					}

					ic = engine.QueueCommand (cancellationToken, null, command);
					ic.ContinuationHandler = async (imap, cmd, text, xdoAsync) => {
						string challenge = await sasl.ChallengeAsync (text, cmd.CancellationToken).ConfigureAwait (false);
						var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");

						await imap.Stream.WriteAsync (buf, 0, buf.Length, cmd.CancellationToken).ConfigureAwait (false);
						await imap.Stream.FlushAsync (cmd.CancellationToken).ConfigureAwait (false);
					};

					detector.IsAuthenticating = true;

					try {
						await engine.RunAsync (ic).ConfigureAwait (false);
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
						await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);

					await OnAuthenticatedAsync (ic.ResponseText ?? string.Empty, cancellationToken).ConfigureAwait (false);
					return;
				}

				CheckCanLogin (ic);

				// fall back to the classic LOGIN command...
				cred = credentials.GetCredential (uri, "DEFAULT");

				ic = engine.QueueCommand (cancellationToken, null, "LOGIN %S %S\r\n", cred.UserName, cred.Password);

				detector.IsAuthenticating = true;

				try {
					await engine.RunAsync (ic).ConfigureAwait (false);
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
					await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);

				await OnAuthenticatedAsync (ic.ResponseText ?? string.Empty, cancellationToken).ConfigureAwait (false);
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
			try {
				ProtocolLogger.LogConnect (engine.Uri);
			} catch {
				stream.Dispose ();
				secure = false;
				throw;
			}

			connecting = true;

			try {
				await engine.ConnectAsync (new ImapStream (stream, ProtocolLogger), cancellationToken).ConfigureAwait (false);
			} catch {
				connecting = false;
				secure = false;
				throw;
			}

			try {
				// Only query the CAPABILITIES if the greeting didn't include them.
				if (engine.CapabilitiesVersion == 0)
					await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);

				if (options == SecureSocketOptions.StartTls && (engine.Capabilities & ImapCapabilities.StartTLS) == 0)
					throw new NotSupportedException ("The IMAP server does not support the STARTTLS extension.");

				if (starttls && (engine.Capabilities & ImapCapabilities.StartTLS) != 0) {
					var ic = engine.QueueCommand (cancellationToken, null, "STARTTLS\r\n");

					await engine.RunAsync (ic).ConfigureAwait (false);

					if (ic.Response == ImapCommandResponse.Ok) {
						try {
							var tls = new SslStream (stream, false, ValidateRemoteCertificate);
							engine.Stream.Stream = tls;

							await SslHandshakeAsync (tls, host, cancellationToken).ConfigureAwait (false);
						} catch (Exception ex) {
							throw SslHandshakeException.Create (ref sslValidationInfo, ex, true, "IMAP", host, port, 993, 143);
						}

						secure = true;

						// Query the CAPABILITIES again if the server did not include an
						// untagged CAPABILITIES response to the STARTTLS command.
						if (engine.CapabilitiesVersion == 1)
							await engine.QueryCapabilitiesAsync (cancellationToken).ConfigureAwait (false);
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
				await OnAuthenticatedAsync (string.Empty, cancellationToken).ConfigureAwait (false);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified IMAP server.
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
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
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

						throw SslHandshakeException.Create (ref sslValidationInfo, ex, false, "IMAP", host, port, 993, 143);
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
		/// Asynchronously establish a connection to the specified IMAP or IMAP/S server using the provided socket.
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
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
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
		public override Task ConnectAsync (Socket socket, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
		{
			CheckCanConnect (socket, host, port);

			return ConnectAsync (new NetworkStream (socket, true), host, port, options, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified IMAP or IMAP/S server using the provided stream.
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
		/// <exception cref="SslHandshakeException">
		/// An error occurred during the SSL/TLS negotiations.
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
		public override async Task ConnectAsync (Stream stream, string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default)
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
						await SslHandshakeAsync (ssl, host, cancellationToken).ConfigureAwait (false);
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
		/// If <paramref name="quit"/> is <c>true</c>, a <c>LOGOUT</c> command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessagesByUniqueId"/>
		/// </example>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="quit">If set to <c>true</c>, a <c>LOGOUT</c> command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		public override async Task DisconnectAsync (bool quit, CancellationToken cancellationToken = default)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					var ic = engine.QueueCommand (cancellationToken, null, "LOGOUT\r\n");
					await engine.RunAsync (ic).ConfigureAwait (false);
				} catch (OperationCanceledException) {
				} catch (ImapProtocolException) {
				} catch (ImapCommandException) {
				} catch (IOException) {
				}
			}

			disconnecting = true;

			engine.Disconnect (null);
		}

		/// <summary>
		/// Asynchronously ping the IMAP server to keep the connection alive.
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
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task NoOpAsync (CancellationToken cancellationToken = default)
		{
			var ic = QueueNoOpCommand (cancellationToken);

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessNoOpResponse (ic);
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
		/// <para>While the IDLE command is running, no other commands may be issued until the
		/// <paramref name="doneToken"/> is cancelled.</para>
		/// <note type="note">It is especially important to cancel the <paramref name="doneToken"/>
		/// before cancelling the <paramref name="cancellationToken"/> when using SSL or TLS due to
		/// the fact that <see cref="System.Net.Security.SslStream"/> cannot be polled.</note>
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
		public async Task IdleAsync (CancellationToken doneToken, CancellationToken cancellationToken = default)
		{
			CheckCanIdle (doneToken);

			if (doneToken.IsCancellationRequested)
				return;

			using (var context = new ImapIdleContext (engine, doneToken, cancellationToken)) {
				var ic = QueueIdleCommand (context, cancellationToken);

				await engine.RunAsync (ic).ConfigureAwait (false);

				ProcessIdleResponse (ic);
			}
		}

		/// <summary>
		/// Asynchronously request the specified notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>The <a href="https://tools.ietf.org/html/rfc5465">NOTIFY</a> command is used to expand
		/// which notifications the client wishes to be notified about, including status notifications
		/// about folders other than the currently selected folder. It can also be used to automatically
		/// FETCH information about new messages that have arrived in the currently selected folder.</para>
		/// <para>This, combined with <see cref="IdleAsync(CancellationToken, CancellationToken)"/>,
		/// can be used to get instant notifications for changes to any of the specified folders.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public async Task NotifyAsync (bool status, IList<ImapEventGroup> eventGroups, CancellationToken cancellationToken = default)
		{
			var ic = QueueNotifyCommand (status, eventGroups, cancellationToken, out bool notifySelectedNewExpunge);

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessNotifyResponse (ic, notifySelectedNewExpunge);
		}

		/// <summary>
		/// Asynchronously disable any previously requested notification events from the IMAP server.
		/// </summary>
		/// <remarks>
		/// Disables any notification events requested in a prior call to 
		/// <see cref="NotifyAsync(bool, IList{ImapEventGroup}, CancellationToken)"/>.
		/// request.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public async Task DisableNotifyAsync (CancellationToken cancellationToken = default)
		{
			var ic = QueueDisableNotifyCommand (cancellationToken);

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessNotifyResponse (ic, false);
		}

		/// <summary>
		/// Asynchronously get all of the folders within the specified namespace.
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
		public override Task<IList<IMailFolder>> GetFoldersAsync (FolderNamespace @namespace, StatusItems items = StatusItems.None, bool subscribedOnly = false, CancellationToken cancellationToken = default)
		{
			if (@namespace == null)
				throw new ArgumentNullException (nameof (@namespace));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return engine.GetFoldersAsync (@namespace, items, subscribedOnly, cancellationToken);
		}

		/// <summary>
		/// Asynchronously get the folder for the specified path.
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
		public override Task<IMailFolder> GetFolderAsync (string path, CancellationToken cancellationToken = default)
		{
			if (path == null)
				throw new ArgumentNullException (nameof (path));

			CheckDisposed ();
			CheckConnected ();
			CheckAuthenticated ();

			return engine.GetFolderAsync (path, cancellationToken);
		}

		/// <summary>
		/// Asynchronously gets the specified metadata.
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
		public override async Task<string> GetMetadataAsync (MetadataTag tag, CancellationToken cancellationToken = default)
		{
			var ic = QueueGetMetadataCommand (tag, cancellationToken);

			await engine.RunAsync (ic).ConfigureAwait (false);

			return ProcessGetMetadataResponse (ic, tag);
		}

		/// <summary>
		/// Asynchronously gets the specified metadata.
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
		public override async Task<MetadataCollection> GetMetadataAsync (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default)
		{
			if (!TryQueueGetMetadataCommand (options, tags, cancellationToken, out var ic))
				return new MetadataCollection ();

			await engine.RunAsync (ic).ConfigureAwait (false);

			return ProcessGetMetadataResponse (ic, options);
		}

		/// <summary>
		/// Asynchronously gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override async Task SetMetadataAsync (MetadataCollection metadata, CancellationToken cancellationToken = default)
		{
			if (!TryQueueSetMetadataCommand (metadata, cancellationToken, out var ic))
				return;

			await engine.RunAsync (ic).ConfigureAwait (false);

			ProcessSetMetadataResponse (ic);
		}
	}
}
