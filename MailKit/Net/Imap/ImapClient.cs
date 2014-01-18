//
// ImapClient.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MailKit.Security;

namespace MailKit.Net.Imap {
	public class ImapClient : IMessageStore
	{
		readonly ImapEngine engine;
		bool disposed;
		string host;

		public ImapClient ()
		{
			// FIXME: should this take a ParserOptions argument?
			engine = new ImapEngine ();
		}

		/// <summary>
		/// Gets the capabilities supported by the IMAP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public ImapCapabilities Capabilities {
			get { return engine.Capabilities; }
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("Pop3Client");
		}

		void CheckConnected ()
		{
			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");
		}

		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (sender, certificate, chain, errors);

			return true;
		}

		#region IMessageService implementation

		/// <summary>
		/// Gets or sets the client SSL certificates.
		/// </summary>
		/// <remarks>
		/// <para>Some servers may require the client SSL certificates in order
		/// to allow the user to connect.</para>
		/// <para>This property should be set before calling <see cref="Connect"/>.</para>
		/// </remarks>
		/// <value>The client SSL certificates.</value>
		public X509CertificateCollection ClientCertificates {
			get; set;
		}

		/// <summary>
		/// Gets the authentication mechanisms supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

		/// <summary>
		/// Gets whether or not the client is currently connected to an IMAP server.
		/// </summary>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return engine.IsConnected; }
		}

		/// <summary>
		/// Authenticates using the supplied credentials.
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
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.Security.Authentication.AuthenticationException">
		/// Authentication using the supplied credentials has failed.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void Authenticate (ICredentials credentials, CancellationToken cancellationToken)
		{
			if (!IsConnected)
				throw new InvalidOperationException ("The ImapClient must be connected before you can authenticate.");

			if (credentials == null)
				throw new ArgumentNullException ("credentials");

			var uri = new Uri ("imap://" + host);
			NetworkCredential cred;
			string challenge;
			ImapCommand ic;

			foreach (var authmech in SaslMechanism.AuthMechanismRank) {
				if (!engine.AuthenticationMechanisms.Contains (authmech))
					continue;

				var sasl = SaslMechanism.Create (authmech, uri, credentials);

				cancellationToken.ThrowIfCancellationRequested ();

				// FIXME: implement sending the AUTHENTICATE command
			}

			if ((Capabilities & ImapCapabilities.LoginDisabled) != 0)
				throw new AuthenticationException ();

			// fall back to the classic LOGIN command...
			cred = credentials.GetCredential (uri, "LOGIN");

			// FIXME: implement the LOGIN command

			engine.State = ImapEngineState.Authenticated;
			//engine.QueryCapabilities (cancellationToken);
		}

		internal void ReplayConnect (string hostName, Stream replayStream, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (hostName == null)
				throw new ArgumentNullException ("hostName");

			if (replayStream == null)
				throw new ArgumentNullException ("replayStream");

			host = hostName;

			engine.Connect (new ImapStream (replayStream), cancellationToken);
			//engine.QueryCapabilities (cancellationToken);
		}

		/// <summary>
		/// Establishes a connection to the specified IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to an IMAP or IMAP/S server. If the schema
		/// in the uri is "imap", a clear-text connection is made and defaults to using
		/// port 143 if no port is specified in the URI. However, if the schema in the
		/// uri is "imaps", an SSL connection is made using the
		/// <see cref="ClientCertificates"/> and defaults to port 993 unless a port
		/// is specified in the URI.</para>
		/// <para>It should be noted that when using a clear-text IMAP connection,
		/// if the server advertizes support for the STARTTLS extension, the client
		/// will automatically switch into TLS mode before authenticating.</para>
		/// If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// and <see cref="Capabilities"/> properties will be populated.
		/// </remarks>
		/// <param name="uri">The server URI. The <see cref="System.Uri.Scheme"/> should either
		/// be "imap" to make a clear-text connection or "imaps" to make an SSL connection.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// The <paramref name="uri"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapException">
		/// An IMAP protocol error occurred.
		/// </exception>
		public void Connect (Uri uri, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (IsConnected)
				return;

			bool imaps = uri.Scheme.ToLowerInvariant () == "imaps";
			int port = uri.Port > 0 ? uri.Port : (imaps ? 993 : 143);
			var ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
			Socket socket = null;
			Stream stream;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				cancellationToken.ThrowIfCancellationRequested ();

				try {
					socket.Connect (ipAddresses[i], port);
				} catch (Exception) {
					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (imaps) {
				var ssl = new SslStream (new NetworkStream (socket), false, ValidateRemoteCertificate);
				ssl.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Default, true);
				stream = ssl;
			} else {
				stream = new NetworkStream (socket);
			}

			host = uri.Host;

			engine.Connect (new ImapStream (stream), cancellationToken);
			//engine.QueryCapabilities (cancellationToken);

			if (!imaps && engine.Capabilities.HasFlag (ImapCapabilities.StartTLS)) {
				//SendCommand (cancellationToken, "STARTTLS");

				var tls = new SslStream (stream, false, ValidateRemoteCertificate);
				tls.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Tls, true);
				engine.Stream.Stream = tls;

				// re-issue a CAPABILITIES command
				//engine.QueryCapabilities (cancellationToken);
			}
		}

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a "LOGOUT" command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <param name="quit">If set to <c>true</c>, a "LOGOUT" command will be issued in order to disconnect cleanly.</param>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		public void Disconnect (bool quit, CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					//SendCommand (cancellationToken, "LOGOUT");
				} catch (OperationCanceledException) {
				} catch (ImapException) {
				} catch (IOException) {
				}
			}

			engine.Disconnect ();
		}

		/// <summary>
		/// Pings the IMAP server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="cancellationToken">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is not connected or authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapException">
		/// The NOOP command failed.
		/// </exception>
		public void NoOp (CancellationToken cancellationToken)
		{
			CheckDisposed ();

			if (engine.State != ImapEngineState.Authenticated && engine.State != ImapEngineState.Selected)
				throw new InvalidOperationException ("You must be authenticated before you can issue a NOOP command.");

			//SendCommand (cancellationToken, "NOOP");
		}

		#endregion

		#region IMessageStore implementation

		public FolderNamespaceCollection PersonalNamespaces {
			get {
				throw new NotImplementedException ();
			}
		}

		public FolderNamespaceCollection SharedNamespaces {
			get {
				throw new NotImplementedException ();
			}
		}

		public FolderNamespaceCollection OtherNamespaces {
			get {
				throw new NotImplementedException ();
			}
		}

		public IFolder Inbox {
			get {
				throw new NotImplementedException ();
			}
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Net.Imap.ImapClient"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.Net.Imap.ImapClient"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.Net.Imap.ImapClient"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="MailKit.Net.Imap.ImapClient"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailKit.Net.Imap.ImapClient"/> was occupying.</remarks>
		public void Dispose ()
		{
			if (!disposed) {
				engine.Disconnect ();
				disposed = true;
			}
		}

		#endregion
	}
}
