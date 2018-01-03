//
// MailService.cs
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
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

#if !NETFX_CORE
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using SslProtocols = System.Security.Authentication.SslProtocols;
#else
using Encoding = Portable.Text.Encoding;
#endif

using MailKit.Security;

namespace MailKit {
	/// <summary>
	/// An abstract mail service implementation.
	/// </summary>
	/// <remarks>
	/// An abstract mail service implementation.
	/// </remarks>
	public abstract class MailService : IMailService
	{
#if NET_4_5 || __MOBILE__ || NETSTANDARD
		const SslProtocols DefaultSslProtocols = SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12;
#elif !NETFX_CORE
		const SslProtocols DefaultSslProtocols = SslProtocols.Tls;
#endif

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		protected MailService (IProtocolLogger protocolLogger)
		{
			if (protocolLogger == null)
				throw new ArgumentNullException (nameof (protocolLogger));

#if !NETFX_CORE
			SslProtocols = DefaultSslProtocols;
			CheckCertificateRevocation = true;
#endif
			ProtocolLogger = protocolLogger;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="MailKit.MailService"/> class.
		/// </remarks>
		protected MailService () : this (new NullProtocolLogger ())
		{
#if !NETFX_CORE
            CheckCertificateRevocation = true;
#endif
		}

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="MailService"/> is reclaimed by garbage collection.
		/// </summary>
		/// <remarks>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="MailService"/> is reclaimed by garbage collection.
		/// </remarks>
		~MailService ()
		{
			Dispose (false);
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the service.
		/// </summary>
		/// <remarks>
		/// <para>Gets an object that can be used to synchronize access to the service.</para>
		/// <para>When using the non-Async methods from multiple threads, it is important to lock the
		/// <see cref="SyncRoot"/> object for thread safety when using the synchronous methods.</para>
		/// </remarks>
		/// <value>The sync root.</value>
		public abstract object SyncRoot {
			get;
		}

		/// <summary>
		/// Gets the protocol supported by the message service.
		/// </summary>
		/// <remarks>
		/// Gets the protocol supported by the message service.
		/// </remarks>
		/// <value>The protocol.</value>
		protected abstract string Protocol {
			get;
		}

		/// <summary>
		/// Get the protocol logger.
		/// </summary>
		/// <remarks>
		/// Gets the protocol logger.
		/// </remarks>
		/// <value>The protocol logger.</value>
		public IProtocolLogger ProtocolLogger {
			get; private set;
		}

#if !NETFX_CORE
		/// <summary>
		/// Gets or sets the SSL/TLS protocols that the client is allowed to use.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the SSL/TLS protocols that the client is allowed to use.</para>
		/// <para>This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> methods.</para>
		/// </remarks>
		/// <value>The ssl protocols.</value>
		public SslProtocols SslProtocols {
			get; set;
		}

		/// <summary>
		/// Gets or sets the client SSL certificates.
		/// </summary>
		/// <remarks>
		/// <para>Some servers may require the client SSL certificates in order
		/// to allow the user to connect.</para>
		/// <para>This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> methods.</para>
		/// </remarks>
		/// <value>The client SSL certificates.</value>
		public X509CertificateCollection ClientCertificates {
			get; set;
		}

		/// <summary>
		/// Get or set whether connecting via SSL/TLS should check certificate revocation.
		/// </summary>
		/// <remarks>
		/// Gets or sets whether connecting via SSL/TLS should check certificate revocation.
		/// </remarks>
		/// <value><c>true</c> certificate revocation should be checked; otherwise, <c>false</c>.</value>
		public bool CheckCertificateRevocation {
			get; set;
		}

		/// <summary>
		/// Get or sets a callback function to validate the server certificate.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a callback function to validate the server certificate.</para>
		/// <para>This property should be set before calling any of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> methods.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\InvalidSslCertificate.cs" region="Simple"/>
		/// </example>
		/// <value>The server certificate validation callback function.</value>
		public RemoteCertificateValidationCallback ServerCertificateValidationCallback {
			get; set;
		}

		/// <summary>
		/// Get or set the local IP end point to use when connecting to the remote host.
		/// </summary>
		/// <remarks>
		/// Gets or sets the local IP end point to use when connecting to the remote host.
		/// </remarks>
		/// <value>The local IP end point or <c>null</c> to use the default end point.</value>
		public IPEndPoint LocalEndPoint {
			get; set;
		}
#endif

		/// <summary>
		/// Gets the authentication mechanisms supported by the mail server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried as part of the
		/// <a href="Overload_MailKit_MailService_Connect.htm">Connect</a> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public abstract HashSet<string> AuthenticationMechanisms {
			get;
		}

		/// <summary>
		/// Gets whether or not the client is currently connected to an mail server.
		/// </summary>
		/// <remarks>
		///<para>The <see cref="IsConnected"/> state is set to <c>true</c> immediately after
		/// one of the <a href="Overload_MailKit_MailService_Connect.htm">Connect</a>
		/// methods succeeds and is not set back to <c>false</c> until either the client
		/// is disconnected via <see cref="Disconnect(bool,CancellationToken)"/> or until a
		/// <see cref="ProtocolException"/> is thrown while attempting to read or write to
		/// the underlying network socket.</para>
		/// <para>When an <see cref="ProtocolException"/> is caught, the connection state of the
		/// <see cref="MailService"/> should be checked before continuing.</para>
		/// </remarks>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public abstract bool IsConnected {
			get;
		}

		/// <summary>
		/// Get whether or not the connection is secure (typically via SSL or TLS).
		/// </summary>
		/// <remarks>
		/// Gets whether or not the connection is secure (typically via SSL or TLS).
		/// </remarks>
		/// <value><c>true</c> if the connection is secure; otherwise, <c>false</c>.</value>
		public abstract bool IsSecure {
			get;
		}

		/// <summary>
		/// Get whether or not the client is currently authenticated with the mail server.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the client is currently authenticated with the mail server.</para>
		/// <para>To authenticate with the mail server, use one of the
		/// <a href="Overload_MailKit_MailService_Authenticate.htm">Authenticate</a> methods
		/// or any of the Async alternatives.</para>
		/// </remarks>
		/// <value><c>true</c> if the client is authenticated; otherwise, <c>false</c>.</value>
		public abstract bool IsAuthenticated {
			get;
		}

		/// <summary>
		/// Gets or sets the timeout for network streaming operations, in milliseconds.
		/// </summary>
		/// <remarks>
		/// Gets or sets the underlying socket stream's <see cref="System.IO.Stream.ReadTimeout"/>
		/// and <see cref="System.IO.Stream.WriteTimeout"/> values.
		/// </remarks>
		/// <value>The timeout in milliseconds.</value>
		public abstract int Timeout {
			get; set;
		}

#if !NETFX_CORE
		/// <summary>
		/// The default server certificate validation callback used when connecting via SSL or TLS.
		/// </summary>
		/// <remarks>
		/// <para>The default server certificate validation callback considers self-signed certificates to be
		/// valid so long as the only error in the certificate chain is an untrusted root.</para>
		/// <note type="security">It should be noted that self-signed certificates may be an indication of
		/// a man-in-the-middle (MITM) attack and so it is recommended that the client implement a custom
		/// server certificate validation callback that presents the certificate to the user in some way,
		/// allowing the user to confirm or deny its validity.</note>
		/// </remarks>
		/// <returns><c>true</c> if the certificate is deemed valid; otherwise, <c>false</c>.</returns>
		/// <param name="sender">The object that is connecting via SSL or TLS.</param>
		/// <param name="certificate">The server's SSL certificate.</param>
		/// <param name="chain">The server's SSL certificate chain.</param>
		/// <param name="sslPolicyErrors">The SSL policy errors.</param>
		public static bool DefaultServerCertificateValidationCallback (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors == SslPolicyErrors.None)
				return true;

			// if there are errors in the certificate chain, look at each error to determine the cause
			if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0) {
				if (chain != null && chain.ChainStatus != null) {
					foreach (var status in chain.ChainStatus) {
						if ((certificate.Subject == certificate.Issuer) && (status.Status == X509ChainStatusFlags.UntrustedRoot)) {
							// treat self-signed certificates with an untrusted root as valid since they are so
							// common among mail server installations
							continue;
						}

						if (status.Status != X509ChainStatusFlags.NoError) {
							// if there are any other errors in the certificate chain, the certificate is invalid,
							// so return false
							return false;
						}
					}
				}

				// Note: If we get this far, then the only errors in the certificate chain are untrusted root errors for
				// self-signed certificates. Since self-signed certificates are so common for mail server installations,
				// treat the certificate as valid.
				return true;
			}

			return false;
		}
#endif

		/// <summary>
		/// Establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Establishes a connection to the specified mail server.
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
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Connect (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Asynchronously establishes a connection to the specified mail server.
		/// </remarks>
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
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract Task ConnectAsync (string host, int port = 0, SecureSocketOptions options = SecureSocketOptions.Auto, CancellationToken cancellationToken = default (CancellationToken));

		SecureSocketOptions GetSecureSocketOptions (Uri uri)
		{
			var protocol = uri.Scheme.ToLowerInvariant ();
			var query = uri.ParsedQuery ();
			string value;

			// Note: early versions of MailKit used "pop3" and "pop3s"
			if (protocol == "pop3s")
				protocol = "pops";
			else if (protocol == "pop3")
				protocol = "pop";

			if (protocol == Protocol + "s")
				return SecureSocketOptions.SslOnConnect;

			if (protocol != Protocol)
				throw new ArgumentException ("Unknown URI scheme.", nameof (uri));

			if (query.TryGetValue ("starttls", out value)) {
				switch (value.ToLowerInvariant ()) {
				default:
					return SecureSocketOptions.StartTlsWhenAvailable;
				case "always": case "true": case "yes":
					return SecureSocketOptions.StartTls;
				case "never": case "false": case "no":
					return SecureSocketOptions.None;
				}
			}

			return SecureSocketOptions.StartTlsWhenAvailable;
		}

		/// <summary>
		/// Establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Establishes a connection to the specified mail server.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessageUri"/>
		/// </example>
		/// <param name="uri">The server URI.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="uri"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="uri"/> is not an absolute URI.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Connect (Uri uri, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (!uri.IsAbsoluteUri)
				throw new ArgumentException ("The uri must be absolute.", nameof (uri));

			var options = GetSecureSocketOptions (uri);

			Connect (uri.Host, uri.Port < 0 ? 0 : uri.Port, options, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// Asynchronously establishes a connection to the specified mail server.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uri">The server URI.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="uri"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="uri"/> is not an absolute URI.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task ConnectAsync (Uri uri, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			if (!uri.IsAbsoluteUri)
				throw new ArgumentException ("The uri must be absolute.", nameof (uri));

			var options = GetSecureSocketOptions (uri);

			return ConnectAsync (uri.Host, uri.Port < 0 ? 0 : uri.Port, options, cancellationToken);
		}

		/// <summary>
		/// Establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to the specified mail server.</para>
		/// <note type="note">
		/// <para>The <paramref name="useSsl"/> argument only controls whether or
		/// not the client makes an SSL-wrapped connection. In other words, even if the
		/// <paramref name="useSsl"/> parameter is <c>false</c>, SSL/TLS may still be used if
		/// the mail server supports the STARTTLS extension.</para>
		/// <para>To disable all use of SSL/TLS, use the
		/// <see cref="Connect(string,int,MailKit.Security.SecureSocketOptions,System.Threading.CancellationToken)"/>
		/// overload with a value of
		/// <see cref="MailKit.Security.SecureSocketOptions.None">SecureSocketOptions.None</see>
		/// instead.</para>
		/// </note>
		/// </remarks>
		/// <param name="host">The host to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="useSsl"><value>true</value> if the client should make an SSL-wrapped connection to the server; otherwise, <value>false</value>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is out of range (<value>0</value> to <value>65535</value>, inclusive).
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Connect (string host, int port, bool useSsl, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			Connect (host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
		}

		/// <summary>
		/// Asynchronously establish a connection to the specified mail server.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously establishes a connection to the specified mail server.</para>
		/// <note type="note">
		/// <para>The <paramref name="useSsl"/> argument only controls whether or
		/// not the client makes an SSL-wrapped connection. In other words, even if the
		/// <paramref name="useSsl"/> parameter is <c>false</c>, SSL/TLS may still be used if
		/// the mail server supports the STARTTLS extension.</para>
		/// <para>To disable all use of SSL/TLS, use the
		/// <see cref="ConnectAsync(string,int,MailKit.Security.SecureSocketOptions,System.Threading.CancellationToken)"/>
		/// overload with a value of
		/// <see cref="MailKit.Security.SecureSocketOptions.None">SecureSocketOptions.None</see>
		/// instead.</para>
		/// </note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="host">The host to connect to.</param>
		/// <param name="port">The port to connect to. If the specified port is <c>0</c>, then the default port will be used.</param>
		/// <param name="useSsl"><value>true</value> if the client should make an SSL-wrapped connection to the server; otherwise, <value>false</value>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="host"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="port"/> is out of range (<value>0</value> to <value>65535</value>, inclusive).
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The <paramref name="host"/> is a zero-length string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is already connected.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task ConnectAsync (string host, int port, bool useSsl, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (host == null)
				throw new ArgumentNullException (nameof (host));

			if (host.Length == 0)
				throw new ArgumentException ("The host name cannot be empty.", nameof (host));

			if (port < 0 || port > 65535)
				throw new ArgumentOutOfRangeException (nameof (port));

			return ConnectAsync (host, port, useSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Authenticate (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract Task AuthenticateAsync (Encoding encoding, ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Authenticate (ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken))
		{
			Authenticate (Encoding.UTF8, credentials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously authenticate using the supplied credentials.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task AuthenticateAsync (ICredentials credentials, CancellationToken cancellationToken = default (CancellationToken))
		{
			return AuthenticateAsync (Encoding.UTF8, credentials, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Authenticate (Encoding encoding, string userName, string password, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var credentials = new NetworkCredential (userName, password);

			Authenticate (encoding, credentials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task AuthenticateAsync (Encoding encoding, string userName, string password, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var credentials = new NetworkCredential (userName, password);

			return AuthenticateAsync (encoding, credentials, cancellationToken);
		}

		/// <summary>
		/// Authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public void Authenticate (string userName, string password, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var credentials = new NetworkCredential (userName, password);

			Authenticate (Encoding.UTF8, credentials, cancellationToken);
		}

		/// <summary>
		/// Asynchronously authenticate using the specified user name and password.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports one or more SASL authentication mechanisms,
		/// then the SASL mechanisms that both the client and server support are tried
		/// in order of greatest security to weakest security. Once a SASL
		/// authentication mechanism is found that both client and server support,
		/// the credentials are used to authenticate.</para>
		/// <para>If the server does not support SASL or if no common SASL mechanisms
		/// can be found, then the default login command is used as a fallback.</para>
		/// <note type="tip">To prevent the usage of certain authentication mechanisms,
		/// simply remove them from the <see cref="AuthenticationMechanisms"/> hash set
		/// before calling this method.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public Task AuthenticateAsync (string userName, string password, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (userName == null)
				throw new ArgumentNullException (nameof (userName));

			if (password == null)
				throw new ArgumentNullException (nameof (password));

			var credentials = new NetworkCredential (userName, password);

			return AuthenticateAsync (Encoding.UTF8, credentials, cancellationToken);
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
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract void Authenticate (SaslMechanism mechanism, CancellationToken cancellationToken = default (CancellationToken));

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
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailService"/> is not connected or is already authenticated.
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
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		public abstract Task AuthenticateAsync (SaslMechanism mechanism, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a logout/quit command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\SmtpExamples.cs" region="SendMessage"/>
		/// </example>
		/// <param name="quit">If set to <c>true</c>, a logout/quit command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		public abstract void Disconnect (bool quit, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a logout/quit command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="quit">If set to <c>true</c>, a logout/quit command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		public abstract Task DisconnectAsync (bool quit, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Ping the mail server to keep the connection alive.
		/// </summary>
		/// <remarks>
		/// Mail servers, if left idle for too long, will automatically drop the connection.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="MailService"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="MailService"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract void NoOp (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously ping the mail server to keep the connection alive.
		/// </summary>
		/// <remarks>
		/// Mail servers, if left idle for too long, will automatically drop the connection.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="MailService"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="MailService"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command was rejected by the mail server.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server responded with an unexpected token.
		/// </exception>
		public abstract Task NoOpAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Occurs when the client has been successfully connected.
		/// </summary>
		/// <remarks>
		/// The <see cref="Connected"/> event is raised when the client
		/// successfully connects to the mail server.
		/// </remarks>
		public event EventHandler<EventArgs> Connected;

		/// <summary>
		/// Raise the connected event.
		/// </summary>
		/// <remarks>
		/// Raises the connected event.
		/// </remarks>
		protected virtual void OnConnected ()
		{
			var handler = Connected;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when the client gets disconnected.
		/// </summary>
		/// <remarks>
		/// The <see cref="Disconnected"/> event is raised whenever the client
		/// gets disconnected.
		/// </remarks>
		public event EventHandler<EventArgs> Disconnected;

		/// <summary>
		/// Raise the disconnected event.
		/// </summary>
		/// <remarks>
		/// Raises the disconnected event.
		/// </remarks>
		protected virtual void OnDisconnected ()
		{
			var handler = Disconnected;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when the client has been successfully authenticated.
		/// </summary>
		/// <remarks>
		/// The <see cref="Authenticated"/> event is raised whenever the client
		/// has been authenticated.
		/// </remarks>
		public event EventHandler<AuthenticatedEventArgs> Authenticated;

		/// <summary>
		/// Raise the authenticated event.
		/// </summary>
		/// <remarks>
		/// Raises the authenticated event.
		/// </remarks>
		/// <param name="message">The notification sent by the server when the client successfully authenticates.</param>
		protected virtual void OnAuthenticated (string message)
		{
			var handler = Authenticated;

			if (handler != null)
				handler (this, new AuthenticatedEventArgs (message));
		}

		/// <summary>
		/// Releases the unmanaged resources used by the <see cref="MailService"/> and
		/// optionally releases the managed resources.
		/// </summary>
		/// <remarks>
		/// Releases the unmanaged resources used by the <see cref="MailService"/> and
		/// optionally releases the managed resources.
		/// </remarks>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources;
		/// <c>false</c> to release only the unmanaged resources.</param>
		protected virtual void Dispose (bool disposing)
		{
			if (disposing)
				ProtocolLogger.Dispose ();
		}

		/// <summary>
		/// Releases all resource used by the <see cref="MailService"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose()"/> when you are finished using the <see cref="MailService"/>. The
		/// <see cref="Dispose()"/> method leaves the <see cref="MailService"/> in an unusable state. After
		/// calling <see cref="Dispose()"/>, you must release all references to the <see cref="MailService"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailService"/> was occupying.</remarks>
		public void Dispose ()
		{
			Dispose (true);
			GC.SuppressFinalize (this);
		}
	}
}
