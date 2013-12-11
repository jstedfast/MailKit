//
// Pop3Client.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.IO;
using MailKit.Security;
using System.Security.Cryptography;

namespace MailKit.Net.Pop3 {
	/// <summary>
	/// A POP3 client that can be used to retrieve messages from a server.
	/// </summary>
	/// <remarks>
	/// The <see cref="Pop3Client"/> class supports both the "pop3" and "pop3s"
	/// protocols. The "pop3" protocol makes a clear-text connection to the POP3
	/// server and does not use SSL or TLS unless the POP3 server supports the
	/// STLS extension (as defined by rfc2595). The "pop3s" protocol,
	/// however, connects to the POP3 server using an SSL-wrapped connection.
	/// </remarks>
	public class Pop3Client : IMessageSpool
	{
		[Flags]
		enum ProbedCapabilities : byte {
			None   = 0,
			Top    = (1 << 0),
			UIDL   = (1 << 1),
			User   = (1 << 2),
		}

		readonly Dictionary<string, int> uids = new Dictionary<string, int> ();
		readonly Pop3Engine engine;
		ProbedCapabilities probed;
		bool disposed;
		int count;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Pop3.Pop3Client"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can retrieve messages with the <see cref="Pop3Client"/>, you
		/// must first call the <see cref="Connect"/> method.
		/// </remarks>
		public Pop3Client ()
		{
			engine = new Pop3Engine ();
		}

		/// <summary>
		/// Gets the capabilities supported by the POP3 server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public Pop3Capabilities Capabilities {
			get { return engine.Capabilities; }
		}

		/// <summary>
		/// Gets the expiration policy.
		/// </summary>
		/// <remarks>
		/// <para>If the server supports the EXPIRE capability (<see cref="Pop3Capabilities.Expire"/>), the value
		/// of the <see cref="ExpirePolicy"/> property will reflect the value advertized by the server.</para>
		/// <para>A value of <c>-1</c> indicates that messages will never expire.</para>
		/// <para>A value of <c>0</c> indicates that messages that have been retrieved during the current session
		/// will be purged immediately after the connection is closed via the "QUIT" command.</para>
		/// <para>Values larger than <c>0</c> indicate the minimum number of days that the server will retain
		/// messages which have been retrieved.</para>
		/// </remarks>
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
		/// <value>The login delay.</value>
		public int LoginDelay {
			get { return engine.LoginDelay; }
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("Pop3Client");
		}

		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (sender, certificate, chain, errors);

			return true;
		}

		void SendCommand (CancellationToken token, string command)
		{
			var pc = engine.QueueCommand (token, command);

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception (string.Format ("Pop3 server did not respond with a +OK response to the {0} command.", command));
		}

		void SendCommand (CancellationToken token, string format, params object[] args)
		{
			var pc = engine.QueueCommand (token, format, args);
			var command = format.Split (' ')[0];

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception (string.Format ("Pop3 server did not respond with a +OK response to the {0} command.", command));
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
		/// <para>The authentication mechanisms are queried durring the <see cref="Connect"/> method.</para>
		/// <para>Servers that do not support the SASL capability will typically support either the
		/// <c>"APOP"</c> authentication mechanism (<see cref="Pop3Capabilities.Apop"/>) or the ability to
		/// login using the <c>"USER"</c> and <c>"PASS"</c> commands (<see cref="Pop3Capabilities.User"/>).</para>
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

		/// <summary>
		/// Gets whether or not the client is currently connected to an POP3 server.
		/// </summary>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get { return engine.IsConnected; }
		}

		void Authenticate (string host, ICredentials credentials, CancellationToken token)
		{
			var uri = new Uri ("pop3://" + host);
			NetworkCredential cred;
			string challenge;
			Pop3Command pc;

			if (engine.Capabilities.HasFlag (Pop3Capabilities.Apop)) {
				cred = credentials.GetCredential (uri, "APOP");
				challenge = engine.ApopToken + cred.Password;
				var md5sum = new StringBuilder ();
				byte[] digest;

				using (var md5 = HashAlgorithm.Create ("MD5")) {
					digest = md5.ComputeHash (Encoding.UTF8.GetBytes (challenge));
				}

				for (int i = 0; i < digest.Length; i++)
					md5sum.Append (digest[i].ToString ("x2"));

				SendCommand (token, "APOP {0} {1}", cred.UserName, md5sum);
			}

			if (engine.Capabilities.HasFlag (Pop3Capabilities.Sasl)) {
				foreach (var authmech in engine.AuthenticationMechanisms) {
					if (!SaslMechanism.IsSupported (authmech))
						continue;

					var sasl = SaslMechanism.Create (authmech, uri, credentials);

					token.ThrowIfCancellationRequested ();

					pc = engine.QueueCommand (token, "AUTH {0}", authmech);
					pc.Handler = (pop3, cmd, text) => {
						while (!sasl.IsAuthenticated && cmd.Status == Pop3CommandStatus.Continue) {
							challenge = sasl.Challenge (text);
							string response;

							try {
								var buf = Encoding.ASCII.GetBytes (challenge + "\r\n");
								pop3.Stream.Write (buf, 0, buf.Length);
								response = pop3.ReadLine ();
							} catch (Exception ex) {
								cmd.Exception = ex;
								break;
							}

							cmd.Status = Pop3Engine.GetCommandStatus (response, out text);
							if (cmd.Status == Pop3CommandStatus.ProtocolError) {
								cmd.Exception = new Pop3Exception (string.Format ("Unexpected response from server: {0}", response));
								break;
							}
						}
					};

					while (engine.Iterate () < pc.Id) {
						// continue processing commands
					}

					if (pc.Status != Pop3CommandStatus.Ok)
						throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the AUTH command.");

					if (pc.Exception != null)
						throw pc.Exception;

					return;
				}
			}

			// fall back to good ol' USER & PASS
			cred = credentials.GetCredential (uri, "USER");

			SendCommand (token, "USER {0}", cred.UserName);
			SendCommand (token, "PASS {0}", cred.Password);
		}

		/// <summary>
		/// Establishes a connection to the specified POP3 server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to an POP3 or POP3/S server. If the schema
		/// in the uri is "pop3", a clear-text connection is made and defaults to using
		/// port 110 if no port is specified in the URI. However, if the schema in the
		/// uri is "pop3s", an SSL connection is made using the
		/// <see cref="ClientCertificates"/> and defaults to port 995 unless a port
		/// is specified in the URI.</para>
		/// <para>It should be noted that when using a clear-text POP3 connection,
		/// if the server advertizes support for the STLS extension, the client
		/// will automatically switch into TLS mode before authenticating.</para>
		/// If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// and <see cref="Capabilities"/> properties will be populated.
		/// </remarks>
		/// <param name="uri">The server URI. The <see cref="System.Uri.Scheme"/> should either
		/// be "pop3" to make a clear-text connection or "pop3s" to make an SSL connection.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para>The <paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="System.UnauthorizedAccessException">
		/// The user's <paramref name="credentials"/> were wrong.
		/// </exception>
		/// <exception cref="MailKit.Security.SaslException">
		/// A SASL authentication error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public void Connect (Uri uri, ICredentials credentials, CancellationToken token)
		{
			CheckDisposed ();

			if (IsConnected)
				return;

			bool pop3s = uri.Scheme.ToLowerInvariant () == "pop3s";
			int port = uri.Port > 0 ? uri.Port : (pop3s ? 995 : 110);
			var ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
			Socket socket = null;
			Stream stream;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				token.ThrowIfCancellationRequested ();

				try {
					socket.Connect (ipAddresses[i], port);
				} catch (Exception) {
					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (pop3s) {
				var ssl = new SslStream (new NetworkStream (socket), false, ValidateRemoteCertificate);
				ssl.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Default, true);
				stream = ssl;
			} else {
				stream = new NetworkStream (socket);
			}

			probed = ProbedCapabilities.None;

			try {
				engine.Connect (new Pop3Stream (stream));
				engine.QueryCapabilities (token);

				if (!pop3s && engine.Capabilities.HasFlag (Pop3Capabilities.StartTLS)) {
					SendCommand (token, "STLS");

					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
					tls.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Tls, true);
					engine.Stream.Stream = tls;

					// re-issue a CAPA command
					engine.QueryCapabilities (token);
				}

				Authenticate (uri.Host, credentials, token);
				engine.State = Pop3EngineState.Transaction;

				if (!engine.Capabilities.HasFlag (Pop3Capabilities.UIDL)) {
					// first, get the message count...
					Count (token);

					// if the message count is > 0, we can probe the UIDL command
					if (count > 0) {
						probed |= ProbedCapabilities.UIDL;

						try {
							GetMessageUid (0, token);
							engine.Capabilities |= Pop3Capabilities.UIDL;
						} catch (Pop3Exception) {
							// ignore
						}
					}
				}
			} catch (OperationCanceledException) {
				engine.Disconnect ();
				throw;
			} catch (IOException) {
				engine.Disconnect ();
				throw;
			} catch (Exception ex) {
				engine.Disconnect ();

				throw new Pop3Exception (string.Format ("Failed to connect to {0}", uri), ex);
			}
		}

		/// <summary>
		/// Disconnect the service.
		/// </summary>
		/// <remarks>
		/// If <paramref name="quit"/> is <c>true</c>, a "QUIT" command will be issued in order to disconnect cleanly.
		/// </remarks>
		/// <param name="quit">If set to <c>true</c>, a "QUIT" command will be issued in order to disconnect cleanly.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		public void Disconnect (bool quit, CancellationToken token)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				return;

			if (quit) {
				try {
					SendCommand (token, "QUIT");
				} catch (OperationCanceledException) {
				} catch (Pop3Exception) {
				} catch (IOException) {
				}
			}

			engine.Disconnect ();
			uids.Clear ();
			count = 0;
		}

		/// <summary>
		/// Pings the POP3 server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected or authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// The NOOP command failed.
		/// </exception>
		public void NoOp (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a NOOP command.");

			SendCommand (token, "NOOP");
		}

		#endregion

		#region IMessageSpool implementation

		/// <summary>
		/// Gets whether or not the <see cref="Pop3Client"/> supports referencing messages by UIDs.
		/// </summary>
		/// <remarks>
		/// If the server does not support UIDs, then all methods that take UID arguments along with
		/// <see cref="GetMessageUid"/> and <see cref="GetMessageUids"/> will fail.
		/// </remarks>
		/// <value><c>true</c> if supports UIDs; otherwise, <c>false</c>.</value>
		public bool SupportsUids {
			get { return engine.Capabilities.HasFlag (Pop3Capabilities.UIDL); }
		}

		/// <summary>
		/// Gets the number of messages available in the message spool.
		/// </summary>
		/// <returns>The number of available messages.</returns>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public int Count (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue an STAT command.");

			var pc = engine.QueueCommand (token, "STAT");

			pc.Handler = (pop3, cmd, text) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				// the response should be "<count> <total size>"
				var tokens = text.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (tokens.Length < 2) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an incomplete response to the STAT command.");
					return;
				}

				if (!int.TryParse (tokens[0], out count)) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an invalid response to the STAT command.");
					return;
				}
			};

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the STAT command.");

			if (pc.Exception != null)
				throw pc.Exception;

			return count;
		}

		/// <summary>
		/// Gets the UID of the message at the specified index.
		/// </summary>
		/// <returns>The message UID.</returns>
		/// <param name="index">The message index.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public string GetMessageUid (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a UIDL command.");

			if (!SupportsUids && probed.HasFlag (ProbedCapabilities.UIDL))
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			var pc = engine.QueueCommand (token, "UIDL {0}", index + 1);
			string uid = null;

			pc.Handler = (pop3, cmd, text) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				// the response should be "<seqid> <uid>"
				var tokens = text.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				int seqid;

				if (tokens.Length < 2) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an incomplete response to the UIDL command.");
					return;
				}

				if (!int.TryParse (tokens[0], out seqid)) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an unexpected response to the UIDL command.");
					return;
				}

				if (seqid != index + 1) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned the UID for the wrong message.");
					return;
				}

				uid = tokens[1];
			};

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			probed |= ProbedCapabilities.UIDL;

			if (pc.Status != Pop3CommandStatus.Ok) {
				if (!SupportsUids)
					throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the UIDL command.");
			}

			if (pc.Exception != null)
				throw pc.Exception;

			engine.Capabilities |= Pop3Capabilities.UIDL;

			uids[uid] = index + 1;

			return uid;
		}

		/// <summary>
		/// Gets the full list of available message UIDs.
		/// </summary>
		/// <returns>The message uids.</returns>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public string[] GetMessageUids (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a UIDL command.");

			if (!SupportsUids && probed.HasFlag (ProbedCapabilities.UIDL))
				throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

			var pc = engine.QueueCommand (token, "UIDL");
			uids.Clear ();

			pc.Handler = (pop3, cmd, text) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				do {
					var response = engine.ReadLine ().TrimEnd ();
					if (response == ".")
						break;

					if (cmd.Exception != null)
						continue;

					var tokens = response.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int seqid;

					if (tokens.Length < 2) {
						cmd.Exception = new Pop3Exception ("Pop3 server returned an incomplete response to the UIDL command.");
						continue;
					}

					if (!int.TryParse (tokens[0], out seqid)) {
						cmd.Exception = new Pop3Exception ("Pop3 server returned an invalid response to the UIDL command.");
						continue;
					}

					uids.Add (tokens[1], seqid);
				} while (true);
			};

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			probed |= ProbedCapabilities.UIDL;

			if (pc.Status != Pop3CommandStatus.Ok) {
				if (!SupportsUids)
					throw new NotSupportedException ("The POP3 server does not support the UIDL extension.");

				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the UIDL command.");
			}

			if (pc.Exception != null)
				throw pc.Exception;

			engine.Capabilities |= Pop3Capabilities.UIDL;

			return uids.Keys.ToArray ();
		}

		int GetMessageSizeForSequenceId (int seqid, CancellationToken token)
		{
			var pc = engine.QueueCommand (token, "LIST {0}", seqid);
			int size = -1;

			pc.Handler = (pop3, cmd, text) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				var tokens = text.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				int id;

				if (tokens.Length < 2) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an incomplete response to the LIST command.");
					return;
				}

				if (!int.TryParse (tokens[0], out id)) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an unexpected response to the LIST command.");
					return;
				}

				if (id != seqid) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned the size for the wrong message.");
					return;
				}

				if (!int.TryParse (tokens[1], out size)) {
					cmd.Exception = new Pop3Exception ("Pop3 server returned an unexpected size token to the LIST command.");
					return;
				}
			};

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the LIST command.");

			if (pc.Exception != null)
				throw pc.Exception;

			return size;
		}

		/// <summary>
		/// Gets the size of the specified message, in bytes.
		/// </summary>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public int GetMessageSize (string uid, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a LIST command.");

			if (uid == null)
				throw new ArgumentNullException ("uid");

			int seqid;

			if (!uids.TryGetValue (uid, out seqid))
				throw new ArgumentException ("No such message.", "uid");

			return GetMessageSizeForSequenceId (seqid, token);
		}

		/// <summary>
		/// Gets the size of the specified message, in bytes.
		/// </summary>
		/// <returns>The message size, in bytes.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public int GetMessageSize (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a LIST command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			return GetMessageSizeForSequenceId (index + 1, token);
		}

		/// <summary>
		/// Gets the sizes for all available messages, in bytes.
		/// </summary>
		/// <returns>The message sizes, in bytes.</returns>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public int[] GetMessageSizes (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a LIST command.");

			var pc = engine.QueueCommand (token, "LIST");
			var sizes = new List<int> ();

			pc.Handler = (pop3, cmd, text) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				do {
					var response = engine.ReadLine ().TrimEnd ();
					if (response == ".")
						break;

					if (cmd.Exception != null)
						continue;

					var tokens = response.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					int seqid, size;

					if (tokens.Length < 2) {
						cmd.Exception = new Pop3Exception ("Pop3 server returned an incomplete response to the LIST command.");
						continue;
					}

					if (!int.TryParse (tokens[0], out seqid)) {
						cmd.Exception = new Pop3Exception ("Pop3 server returned an unexpected response to the LIST command.");
						continue;
					}

					if (seqid != sizes.Count + 1) {
						cmd.Exception = new Pop3Exception ("Pop3 server returned the size for the wrong message.");
						continue;
					}

					if (!int.TryParse (tokens[1], out size)) {
						cmd.Exception = new Pop3Exception ("Pop3 server returned an unexpected size token to the LIST command.");
						continue;
					}

					sizes.Add (size);
				} while (true);
			};

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the LIST command.");

			if (pc.Exception != null)
				throw pc.Exception;

			return sizes.ToArray ();
		}

		MimeMessage GetMessageForSequenceId (int seqid, bool headersOnly, CancellationToken token)
		{
			MimeMessage message = null;
			Pop3Command pc;

			if (headersOnly)
				pc = engine.QueueCommand (token, "TOP {0} 0", seqid);
			else
				pc = engine.QueueCommand (token, "RETR {0}", seqid);

			pc.Handler = (pop3, cmd, text) => {
				if (cmd.Status != Pop3CommandStatus.Ok)
					return;

				try {
					pop3.Stream.Mode = Pop3StreamMode.Data;
					using (var memory = new MemoryBlockStream ()) {
						byte[] buf = new byte[4096];
						int nread;

						token.ThrowIfCancellationRequested ();
						while ((nread = pop3.Stream.Read (buf, 0, buf.Length)) > 0) {
							token.ThrowIfCancellationRequested ();
							memory.Write (buf, 0, nread);
						}

						if (headersOnly) {
							buf[0] = (byte) '\n';
							memory.Write (buf, 0, 1);
						}

						memory.Position = 0;

						message = MimeMessage.Load (memory);
					}
				} catch (Exception ex) {
					cmd.Exception = ex;
				} finally {
					pop3.Stream.Mode = Pop3StreamMode.Line;
				}
			};

			while (engine.Iterate () < pc.Id) {
				// continue processing commands
			}

			if (pc.Status != Pop3CommandStatus.Ok) {
				if (headersOnly)
					throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the TOP command.");

				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the LIST command.");
			}

			if (pc.Exception != null)
				throw pc.Exception;

			return message;
		}

		/// <summary>
		/// Gets the headers for the specified message.
		/// </summary>
		/// <returns>The message headers.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public HeaderList GetMessageHeaders (string uid, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a RETR command.");

			if (uid == null)
				throw new ArgumentNullException ("uid");

			int seqid;

			if (!uids.TryGetValue (uid, out seqid))
				throw new ArgumentException ("No such message.", "uid");

			return GetMessageForSequenceId (seqid, true, token).Headers;
		}

		/// <summary>
		/// Gets the headers for the specified message.
		/// </summary>
		/// <returns>The message headers.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public HeaderList GetMessageHeaders (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a TOP command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			return GetMessageForSequenceId (index + 1, true, token).Headers;
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public MimeMessage GetMessage (string uid, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a RETR command.");

			if (uid == null)
				throw new ArgumentNullException ("uid");

			int seqid;

			if (!uids.TryGetValue (uid, out seqid))
				throw new ArgumentException ("No such message.", "uid");

			return GetMessageForSequenceId (seqid, false, token);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public MimeMessage GetMessage (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a RETR command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			return GetMessageForSequenceId (index + 1, false, token);
		}

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMessageService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is not a valid message UID.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The POP3 server does not support the UIDL extension.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public void DeleteMessage (string uid, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a DELE command.");

			if (uid == null)
				throw new ArgumentNullException ("uid");

			int seqid;

			if (!uids.TryGetValue (uid, out seqid))
				throw new ArgumentException ("No such message.", "uid");

			SendCommand (token, "DELE {0}", seqid);
		}

		/// <summary>
		/// Mark the specified message for deletion.
		/// </summary>
		/// <remarks>
		/// Messages marked for deletion are not actually deleted until the session
		/// is cleanly disconnected
		/// (see <see cref="IMessageService.Disconnect(bool, CancellationToken)"/>).
		/// </remarks>
		/// <param name="index">The index of the message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is not a valid message index.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public void DeleteMessage (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a DELE command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			SendCommand (token, "DELE {0}", index + 1);
		}

		/// <summary>
		/// Reset the state of all messages marked for deletion.
		/// </summary>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="Pop3Client"/> has been disposed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="Pop3Client"/> is not connected.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="Pop3Exception">
		/// A POP3 protocol error occurred.
		/// </exception>
		public void Reset (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue an RSET command.");

			SendCommand (token, "RSET");
		}

		#endregion

		#region IDisposable implementation

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.Net.Pop3.Pop3Client"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.Net.Pop3.Pop3Client"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="MailKit.Net.Pop3.Pop3Client"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailKit.Net.Pop3.Pop3Client"/> was occupying.</remarks>
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
