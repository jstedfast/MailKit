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

		public Pop3Client ()
		{
			engine = new Pop3Engine ();
		}

		public Pop3Capabilities Capabilities {
			get { return engine.Capabilities; }
		}

		public int Expire {
			get { return engine.Expire; }
		}

		public string Implementation {
			get { return engine.Implementation; }
		}

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

		public X509CertificateCollection ClientCertificates {
			get; set;
		}

		public HashSet<string> AuthenticationMechanisms {
			get { return engine.AuthenticationMechanisms; }
		}

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
			} catch (OperationCanceledException) {
				engine.Disconnect ();
				throw;
			} catch (IOException) {
				engine.Disconnect ();
				throw;
			} catch {
				if (engine.IsConnected)
					SendCommand (token, "QUIT");

				engine.Disconnect ();
				throw;
			}
		}

		public void Disconnect (bool quit, CancellationToken token)
		{
			CheckDisposed ();

			if (!engine.IsConnected)
				throw new InvalidOperationException ("The Pop3Client has not been connected.");

			if (quit) {
				try {
					SendCommand (token, "QUIT");
				} catch (OperationCanceledException) {
					engine.Disconnect ();
					uids.Clear ();
					count = 0;
					throw;
				} catch (IOException) {
				}
			}

			engine.Disconnect ();
			uids.Clear ();
			count = 0;
		}

		#endregion

		#region IMessageSpool implementation

		public bool SupportsUids {
			get { return engine.Capabilities.HasFlag (Pop3Capabilities.UIDL); }
		}

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

		public string GetMessageUid (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a UIDL command.");

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

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the UIDL command.");

			if (pc.Exception != null)
				throw pc.Exception;

			uids[uid] = index + 1;

			return uid;
		}

		public string[] GetMessageUids (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a UIDL command.");

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

			if (pc.Status != Pop3CommandStatus.Ok)
				throw new Pop3Exception ("Pop3 server did not respond with a +OK response to the UIDL command.");

			if (pc.Exception != null)
				throw pc.Exception;

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

		public int GetMessageSize (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a LIST command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			return GetMessageSizeForSequenceId (index + 1, token);
		}

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

		public HeaderList GetMessageHeaders (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a TOP command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			return GetMessageForSequenceId (index + 1, true, token).Headers;
		}

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

		public MimeMessage GetMessage (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a RETR command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			return GetMessageForSequenceId (index + 1, false, token);
		}

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

		public void DeleteMessage (int index, CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a DELE command.");

			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			SendCommand (token, "DELE {0}", index + 1);
		}

		public void Reset (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue an RSET command.");

			SendCommand (token, "RSET");
		}

		public void NoOp (CancellationToken token)
		{
			CheckDisposed ();

			if (engine.State != Pop3EngineState.Transaction)
				throw new InvalidOperationException ("You must be authenticated before you can issue a NOOP command.");

			SendCommand (token, "NOOP");
		}

		#endregion

		#region IDisposable implementation

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
