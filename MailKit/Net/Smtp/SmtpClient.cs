//
// SmtpClient.cs
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
using System.Diagnostics;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.IO;

using MailKit.Security;

namespace MailKit.Net.Smtp {
	/// <summary>
	/// An SMTP client that can be used to send email messages.
	/// </summary>
	/// <remarks>
	/// The <see cref="SmtpClient"/> class supports both the "smtp" and "smtps"
	/// protocols. The "smtp" protocol makes a clear-text connection to the SMTP
	/// server and does not use SSL or TLS unless the SMTP server supports the
	/// STARTTLS extension (as defined by rfc3207). The "smtps" protocol,
	/// however, connects to the SMTP server using an SSL-wrapped connection.
	/// </remarks>
	public class SmtpClient : IMessageTransport
	{
		static readonly byte[] EndData = Encoding.ASCII.GetBytes ("\r\n.\r\n");
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		readonly HashSet<string> authmechs = new HashSet<string> ();
		readonly byte[] buffer = new byte[2048];
		Stream stream;
		bool disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Smtp.SmtpClient"/> class.
		/// </summary>
		/// <remarks>
		/// Before you can send messages with the <see cref="SmtpClient"/>, you
		/// must first call the <see cref="Connect"/> method.
		/// </remarks>
		public SmtpClient ()
		{
		}

		/// <summary>
		/// Gets the capabilities supported by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The capabilities will not be known until a successful connection
		/// has been made via the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The capabilities.</value>
		public SmtpCapabilities Capabilities {
			get; private set;
		}

		/// <summary>
		/// Gets the maximum message size supported by the server.
		/// </summary>
		/// <remarks>
		/// The maximum message size will not be known until a successful
		/// connect has been made via the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The maximum message size supported by the server.</value>
		public uint MaxSize {
			get; private set;
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("SmtpClient");
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
		/// Gets the authentication mechanisms supported by the SMTP server.
		/// </summary>
		/// <remarks>
		/// The authentication mechanisms are queried durring the <see cref="Connect"/> method.
		/// </remarks>
		/// <value>The authentication mechanisms.</value>
		public HashSet<string> AuthenticationMechanisms {
			get { return authmechs; }
		}

		/// <summary>
		/// Gets whether or not the client is currently connected to an SMTP server.
		/// </summary>
		/// <value><c>true</c> if the client is connected; otherwise, <c>false</c>.</value>
		public bool IsConnected {
			get; private set;
		}

		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (sender, certificate, chain, errors);

			return true;
		}

		static bool TryParseInt32 (byte[] text, ref int index, int endIndex, out int value)
		{
			int startIndex = index;

			value = 0;

			while (index < endIndex && text[index] >= (byte) '0' && text[index] <= (byte) '9')
				value = (value * 10) + (text[index++] - (byte) '0');

			return index > startIndex;
		}

		SmtpResponse ReadResponse (CancellationToken token)
		{
			using (var memory = new MemoryStream ()) {
				bool complete = false;
				bool newLine = true;
				bool more = true;
				int index = 0;
				int code = 0;

				do {
					token.ThrowIfCancellationRequested ();

					int nread = stream.Read (buffer, index, buffer.Length - index);
					int endIndex = index + nread;

					if (nread == 0)
						throw new SmtpException (SmtpErrorCode.ProtocolError, "The server replied with an incomplete response.");

					complete = false;
					index = 0;

					do {
						int startIndex = index;

						if (newLine && index < endIndex) {
							int value;

							if (!TryParseInt32 (buffer, ref index, endIndex, out value))
								throw new SmtpException (SmtpErrorCode.ProtocolError, "Unable to parse status code returned by the server.");

							if (index == endIndex) {
								index = startIndex;
								break;
							}

							if (code == 0) {
								code = value;
							} else if (value != code) {
								throw new SmtpException (SmtpErrorCode.ProtocolError, "The status codes returned by the server did not match.");
							}

							newLine = false;

							if (buffer[index] != (byte) '\r' && buffer[index] != (byte) '\n')
								more = buffer[index++] == (byte) '-';
							else
								more = false;

							startIndex = index;
						}

						while (index < endIndex && buffer[index] != (byte) '\r' && buffer[index] != (byte) '\n')
							index++;

						memory.Write (buffer, startIndex, index - startIndex);

						if (index < endIndex && buffer[index] == (byte) '\r')
							index++;

						if (index < endIndex && buffer[index] == (byte) '\n') {
							if (more)
								memory.WriteByte (buffer[index]);
							complete = true;
							newLine = true;
							index++;
						}
					} while (index < endIndex);

					int n = endIndex - index;
					for (int i = 0; i < n; i++)
						buffer[i] = buffer[index++];

					index = n;
				} while (more || !complete);

				string message = null;

				try {
					message = Encoding.UTF8.GetString (memory.GetBuffer (), 0, (int) memory.Length);
				} catch {
					message = Latin1.GetString (memory.GetBuffer (), 0, (int) memory.Length);
				}

				#if DEBUG
				var lines = message.Split ('\n');
				for (int i = 0; i < lines.Length; i++)
					Console.WriteLine ("S: {0}{1}{2}", code, i + 1 < lines.Length ? '-' : ' ', lines[i]);
				#endif

				return new SmtpResponse ((SmtpStatusCode) code, message);
			}
		}

		SmtpResponse SendCommand (string command, CancellationToken token)
		{
			token.ThrowIfCancellationRequested ();

			var bytes = Encoding.UTF8.GetBytes (command);

			#if DEBUG
			Console.Write ("C: {0}", command);
			#endif

			stream.Write (bytes, 0, bytes.Length);

			return ReadResponse (token);
		}

		SmtpResponse SendEhlo (EndPoint localEndPoint, bool ehlo, CancellationToken token)
		{
			string command = ehlo ? "EHLO " : "HELO ";
			var ip = localEndPoint as IPEndPoint;

			if (ip != null) {
				command += "[";
				if (localEndPoint.AddressFamily == AddressFamily.InterNetworkV6)
					command += "IPv6:";
				command += ip.Address;
				command += "]\r\n";
			} else {
				command += ((DnsEndPoint) localEndPoint).Host;
				command += "\r\n";
			}

			return SendCommand (command, token);
		}

		void Ehlo (EndPoint localEndPoint, CancellationToken token)
		{
			SmtpResponse response;

			// Clear the extensions
			Capabilities = SmtpCapabilities.None;
			authmechs.Clear ();
			MaxSize = 0;

			response = SendEhlo (localEndPoint, true, token);
			if (response.StatusCode != SmtpStatusCode.Ok) {
				response = SendEhlo (localEndPoint, false, token);
				if (response.StatusCode != SmtpStatusCode.Ok)
					throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			} else {
				var lines = response.Response.Split ('\n');
				for (int i = 0; i < lines.Length; i++) {
					var capability = lines[i].Trim ();

					if (capability.StartsWith ("AUTH", StringComparison.Ordinal)) {
						int index = 4;

						Capabilities |= SmtpCapabilities.Authentication;

						if (index < capability.Length && capability[index] == '=')
							index++;

						var mechanisms = capability.Substring (index);
						foreach (var mechanism in mechanisms.Split (new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
							authmechs.Add (mechanism);
					} else if (capability.StartsWith ("SIZE", StringComparison.Ordinal)) {
						int index = 4;
						uint size;

						while (index < capability.Length && char.IsWhiteSpace (capability[index]))
							index++;

						if (uint.TryParse (capability.Substring (index), out size))
							MaxSize = size;
					} else if (capability == "BINARYMIME") {
						Capabilities |= SmtpCapabilities.BinaryMime;
					} else if (capability == "CHUNKING") {
						Capabilities |= SmtpCapabilities.Chunking;
					} else if (capability == "ENHANCEDSTATUSCODES") {
						Capabilities |= SmtpCapabilities.EnhancedStatusCodes;
					} else if (capability == "8BITMIME") {
						Capabilities |= SmtpCapabilities.EightBitMime;
					} else if (capability == "PIPELINING") {
						Capabilities |= SmtpCapabilities.Pipelining;
					} else if (capability == "STARTTLS") {
						Capabilities |= SmtpCapabilities.StartTLS;
					} else if (capability == "SMTPUTF8") {
						Capabilities |= SmtpCapabilities.UTF8;
					}
				}
			}
		}

		void Authenticate (string host, ICredentials credentials, CancellationToken token)
		{
			var uri = new Uri ("smtp://" + host);
			SmtpResponse response;
			string challenge;
			string command;

			foreach (var authmech in authmechs) {
				if (!SaslMechanism.IsSupported (authmech))
					continue;

				var sasl = SaslMechanism.Create (authmech, uri, credentials);

				token.ThrowIfCancellationRequested ();

				// send an initial challenge if the mechanism supports it
				if ((challenge = sasl.Challenge (null)) != null) {
					command = string.Format ("AUTH {0} {1}\r\n", authmech, challenge);
				} else {
					command = string.Format ("AUTH {0}\r\n", authmech);
				}

				response = SendCommand (command, token);

				if (response.StatusCode == SmtpStatusCode.AuthenticationMechanismTooWeak)
					continue;

				while (!sasl.IsAuthenticated) {
					if (response.StatusCode != SmtpStatusCode.AuthenticationChallenge)
						throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

					challenge = sasl.Challenge (response.Response);
					response = SendCommand (challenge + "\r\n", token);
				}

				if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful)
					return;

				throw new UnauthorizedAccessException ();
			}
		}

		/// <summary>
		/// Establishes a connection to the specified SMTP server.
		/// </summary>
		/// <remarks>
		/// <para>Establishes a connection to an SMTP or SMTP/S server. If the schema
		/// in the uri is "smtp", a clear-text connection is made and defaults to using
		/// port 25 if no port is specified in the URI. However, if the schema in the
		/// uri is "smtps", an SSL connection is made using the
		/// <see cref="ClientCertificates"/> and defaults to port 465 unless a port
		/// is specified in the URI.</para>
		/// <para>It should be noted that when using a clear-text SMTP connection,
		/// if the server advertizes support for the STARTTLS extension, the client
		/// will automatically switch into TLS mode before authenticating.</para>
		/// If a successful connection is made, the <see cref="AuthenticationMechanisms"/>
		/// and <see cref="Capabilities"/> properties will be populated.
		/// </remarks>
		/// <param name="uri">The server URI. The <see cref="System.Uri.Scheme"/> should either
		/// be "smtp" to make a clear-text connection or "smtps" to make an SSL connection.</param>
		/// <param name="credentials">The user's credentials or <c>null</c> if no credentials are needed.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para>The <paramref name="uri"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
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
		/// <exception cref="SmtpException">
		/// An SMTP protocol error occurred.
		/// </exception>
		public void Connect (Uri uri, ICredentials credentials, CancellationToken token)
		{
			CheckDisposed ();

			if (uri == null)
				throw new ArgumentNullException ("uri");

			if (IsConnected)
				return;

			Capabilities = SmtpCapabilities.None;
			authmechs.Clear ();
			MaxSize = 0;

			bool smtps = uri.Scheme.ToLowerInvariant () == "smtps";
			int port = uri.Port > 0 ? uri.Port : (smtps ? 465 : 25);
			var ipAddresses = Dns.GetHostAddresses (uri.DnsSafeHost);
			EndPoint localEndPoint = null;
			SmtpResponse response = null;
			Socket socket = null;

			for (int i = 0; i < ipAddresses.Length; i++) {
				socket = new Socket (ipAddresses[i].AddressFamily, SocketType.Stream, ProtocolType.Tcp);

				token.ThrowIfCancellationRequested ();

				try {
					socket.Connect (ipAddresses[i], port);
					localEndPoint = socket.LocalEndPoint;
				} catch {
					if (i + 1 == ipAddresses.Length)
						throw;
				}
			}

			if (smtps) {
				var ssl = new SslStream (new NetworkStream (socket), false, ValidateRemoteCertificate);
				ssl.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Default, true);
				stream = ssl;
			} else {
				stream = new NetworkStream (socket);
			}

			try {
				// read the greeting
				response = ReadResponse (token);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				Ehlo (localEndPoint, token);

				if (!smtps & Capabilities.HasFlag (SmtpCapabilities.StartTLS)) {
					response = SendCommand ("STARTTLS\r\n", token);
					if (response.StatusCode != SmtpStatusCode.ServiceReady)
						throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

					var tls = new SslStream (stream, false, ValidateRemoteCertificate);
					tls.AuthenticateAsClient (uri.Host, ClientCertificates, SslProtocols.Tls, true);
					stream = tls;

					// Send EHLO again and get the new list of supported extensions
					Ehlo (localEndPoint, token);
				}

				if (Capabilities.HasFlag (SmtpCapabilities.Authentication) && credentials != null)
					Authenticate (uri.Host, credentials, token);

				IsConnected = true;
			} catch {
				stream.Dispose ();
				stream = null;
				throw;
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
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		public void Disconnect (bool quit, CancellationToken token)
		{
			CheckDisposed ();

			if (!IsConnected)
				return;

			if (quit) {
				try {
					SendCommand ("QUIT\r\n", token);
				} catch (OperationCanceledException) {
				} catch (SmtpException) {
				} catch (IOException) {
				}
			}

			Disconnect ();
		}

		/// <summary>
		/// Pings the SMTP server to keep the connection alive.
		/// </summary>
		/// <remarks>Mail servers, if left idle for too long, will automatically drop the connection.</remarks>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="SmtpClient"/> is not connected.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="SmtpException">
		/// The SMTP server returned an unexpected status code.
		/// </exception>
		public void NoOp (CancellationToken token)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The SmtpClient is not connected.");

			var response = SendCommand ("NOOP\r\n", token);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
		}

		void Disconnect ()
		{
			IsConnected = false;

			if (stream != null) {
				stream.Dispose ();
				stream = null;
			}
		}

		#endregion

		#region IMessageTransport implementation

		static MailboxAddress GetMessageSender (MimeMessage message)
		{
			if (message.ResentSender != null)
				return message.ResentSender;

			if (message.ResentFrom.Count > 0)
				return message.ResentFrom.Mailboxes.FirstOrDefault ();

			if (message.Sender != null)
				return message.Sender;

			if (message.From.Count > 0)
				return message.From.Mailboxes.FirstOrDefault ();

			return null;
		}

		static IList<MailboxAddress> GetMessageRecipients (MimeMessage message)
		{
			var recipients = new List<MailboxAddress> ();

			if (message.ResentSender != null || message.ResentFrom.Count > 0) {
				recipients.AddRange (message.ResentTo.Mailboxes);
				recipients.AddRange (message.ResentCc.Mailboxes);
				recipients.AddRange (message.ResentBcc.Mailboxes);
			} else {
				recipients.AddRange (message.To.Mailboxes);
				recipients.AddRange (message.Cc.Mailboxes);
				recipients.AddRange (message.Bcc.Mailboxes);
			}

			return recipients;
		}

		[Flags]
		enum SmtpExtension {
			None         = 0,
			EightBitMime = 1 << 0,
			BinaryMime   = 1 << 1
		}

		SmtpExtension GetSmtpExtensions (MimeEntity entity)
		{
			if (entity is MessagePart) {
				var message = ((MessagePart) entity).Message;
				return GetSmtpExtensions (message);
			}

			if (entity is Multipart) {
				var extensions = SmtpExtension.None;
				var multipart = (Multipart) entity;

				foreach (var child in multipart)
					extensions |= GetSmtpExtensions (child);

				return extensions;
			}

			var part = (MimePart) entity;

			switch (part.ContentTransferEncoding) {
			case ContentEncoding.Default:
				if (Capabilities.HasFlag (SmtpCapabilities.BinaryMime))
					return SmtpExtension.BinaryMime;
				if (Capabilities.HasFlag (SmtpCapabilities.EightBitMime))
					return SmtpExtension.EightBitMime;
				return SmtpExtension.None;
			case ContentEncoding.EightBit:
				return SmtpExtension.EightBitMime;
			case ContentEncoding.Binary:
				return SmtpExtension.BinaryMime;
			default:
				return SmtpExtension.None;
			}
		}

		SmtpExtension GetSmtpExtensions (MimeMessage message)
		{
			if (message.Body == null)
				throw new ArgumentException ("Message does not contain a body.");

			return GetSmtpExtensions (message.Body);
		}

		void MailFrom (MailboxAddress mailbox, SmtpExtension extensions, CancellationToken token)
		{
			string command;

			if (Capabilities.HasFlag (SmtpCapabilities.BinaryMime) && extensions.HasFlag (SmtpExtension.BinaryMime)) {
				command = string.Format ("MAIL FROM:<{0}> BODY=BINARYMIME\r\n", mailbox.Address);
			} else if (Capabilities.HasFlag (SmtpCapabilities.EightBitMime) && extensions.HasFlag (SmtpExtension.EightBitMime)) {
				command = string.Format ("MAIL FROM:<{0}> BODY=8BITMIME\r\n", mailbox.Address);
			} else {
				command = string.Format ("MAIL FROM:<{0}>\r\n", mailbox.Address);
			}

			var response = SendCommand (command, token);

			switch (response.StatusCode) {
			case SmtpStatusCode.Ok:
				break;
			case SmtpStatusCode.MailboxNameNotAllowed:
			case SmtpStatusCode.MailboxUnavailable:
				throw new SmtpException (SmtpErrorCode.SenderNotAccepted, response.StatusCode, mailbox, response.Response);
			default:
				throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			}
		}

		void RcptTo (MailboxAddress mailbox, CancellationToken token)
		{
			var command = string.Format ("RCPT TO:<{0}>\r\n", mailbox.Address);
			var response = SendCommand (command, token);

			switch (response.StatusCode) {
			case SmtpStatusCode.UserNotLocalWillForward:
			case SmtpStatusCode.Ok:
				break;
			case SmtpStatusCode.UserNotLocalTryAlternatePath:
			case SmtpStatusCode.MailboxNameNotAllowed:
			case SmtpStatusCode.MailboxUnavailable:
			case SmtpStatusCode.MailboxBusy:
				throw new SmtpException (SmtpErrorCode.RecipientNotAccepted, response.StatusCode, mailbox, response.Response);
			default:
				throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);
			}
		}

		static void WriteData (Stream stream, Stream data, CancellationToken token)
		{
			var buffer = new byte[4096];
			int nread;

			do {
				if ((nread = data.Read (buffer, 0, buffer.Length)) > 0) {
					token.ThrowIfCancellationRequested ();
					stream.Write (buffer, 0, nread);
				}
			} while (nread > 0);
		}

		void Data (MimeMessage message, CancellationToken token)
		{
			var response = SendCommand ("DATA\r\n", token);

			if (response.StatusCode != SmtpStatusCode.StartMailInput)
				throw new SmtpException (SmtpErrorCode.UnexpectedStatusCode, response.StatusCode, response.Response);

			EncodingConstraint constraint;

			if (Capabilities.HasFlag (SmtpCapabilities.BinaryMime))
				constraint = EncodingConstraint.None;
			else if (Capabilities.HasFlag (SmtpCapabilities.EightBitMime))
				constraint = EncodingConstraint.EightBit;
			else
				constraint = EncodingConstraint.SevenBit;

			var options = FormatOptions.Default.Clone ();
			options.NewLineFormat = NewLineFormat.Dos;
			options.EncodingConstraint = constraint;

			options.HiddenHeaders.Add (HeaderId.ContentLength);
			options.HiddenHeaders.Add (HeaderId.ResentBcc);
			options.HiddenHeaders.Add (HeaderId.Bcc);

			using (var filtered = new FilteredStream (stream)) {
				filtered.Add (new SmtpDataFilter ());
				message.WriteTo (options, filtered, token);
				filtered.Flush ();
			}

			stream.Write (EndData, 0, EndData.Length);

			response = ReadResponse (token);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpException (SmtpErrorCode.MessageNotAccepted, response.StatusCode, response.Response);
		}

		void Reset ()
		{
			try {
				var response = SendCommand ("RSET\r\n", CancellationToken.None);
				if (response.StatusCode != SmtpStatusCode.Ok)
					Disconnect (false, CancellationToken.None);
			} catch (SmtpException ex) {
				if (ex.ErrorCode == SmtpErrorCode.ProtocolError)
					Disconnect ();
			} catch {
				Disconnect ();
			}
		}

		/// <summary>
		/// Send the specified message.
		/// </summary>
		/// <remarks>
		/// Sends the message by uploading it to an SMTP server.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="token">A cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="SmtpClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="SmtpClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>A sender has not been specified.</para>
		/// <para>-or-</para>
		/// <para>No recipients have been specified.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation has been canceled.
		/// </exception>
		public void Send (MimeMessage message, CancellationToken token)
		{
			CheckDisposed ();

			if (!IsConnected)
				throw new InvalidOperationException ("The SmtpClient is not connected.");

			if (message == null)
				throw new ArgumentNullException ("message");

			var recipients = GetMessageRecipients (message);
			var sender = GetMessageSender (message);

			if (sender == null)
				throw new InvalidOperationException ("No sender has been specified.");

			if (recipients.Count == 0)
				throw new InvalidOperationException ("No recipients have been specified.");

			var extensions = GetSmtpExtensions (message);

			try {
				// TODO: support pipelining
				MailFrom (sender, extensions, token);
				foreach (var recipient in recipients)
					RcptTo (recipient, token);
				Data (message, token);
			} catch (SmtpException ex) {
				if (ex.ErrorCode == SmtpErrorCode.ProtocolError)
					Disconnect ();
				else
					Reset ();
				throw;
			} catch (OperationCanceledException) {
				// irrecoverable
				Disconnect ();
				throw;
			}
		}

		#endregion

		#region IDisposable

		/// <summary>
		/// Releases all resource used by the <see cref="MailKit.Net.Smtp.SmtpClient"/> object.
		/// </summary>
		/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="MailKit.Net.Smtp.SmtpClient"/>. The
		/// <see cref="Dispose"/> method leaves the <see cref="MailKit.Net.Smtp.SmtpClient"/> in an unusable state. After
		/// calling <see cref="Dispose"/>, you must release all references to the <see cref="MailKit.Net.Smtp.SmtpClient"/> so
		/// the garbage collector can reclaim the memory that the <see cref="MailKit.Net.Smtp.SmtpClient"/> was occupying.</remarks>
		public void Dispose ()
		{
			if (!disposed) {
				disposed = true;
				Disconnect ();
			}
		}

		#endregion
	}
}

