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
using System.Net.NetworkInformation;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MimeKit;
using MimeKit.IO;

using MailKit.Security;

namespace MailKit.Net.Smtp {
	public class SmtpClient : IMessageTransport
	{
		static readonly byte[] EndData = Encoding.ASCII.GetBytes ("\r\n.\r\n");
		readonly HashSet<string> authmechs = new HashSet<string> ();
		readonly byte[] buffer = new byte[2048];
		Stream stream;
		bool disposed;

		public SmtpClient ()
		{
		}

		public X509CertificateCollection ClientCertificates {
			get; set;
		}

		public HashSet<string> AuthenticationMechanisms {
			get { return authmechs; }
		}

		public SmtpCapabilities Capabilities {
			get; private set;
		}

		public uint MaxSize {
			get; private set;
		}

		void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException ("SmtpClient");
		}

		#region IMessageService implementation

		public bool IsConnected {
			get; private set;
		}

		bool ValidateRemoteCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			if (ServicePointManager.ServerCertificateValidationCallback != null)
				return ServicePointManager.ServerCertificateValidationCallback (sender, certificate, chain, errors);

			//if (errors != SslPolicyErrors.None)
			//	throw new InvalidOperationException ("SSL certificate error: " + errors);

			// FIXME: what more should we do?

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

		static SmtpResponse ReadResponse (Stream stream, byte[] buffer, CancellationToken token)
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
						throw new SmtpException (SmtpStatusCode.GeneralFailure, "Incomplete response.");

					complete = false;
					index = 0;

					do {
						int startIndex = index;

						if (newLine && index < endIndex) {
							int value;

							if (!TryParseInt32 (buffer, ref index, endIndex, out value))
								throw new SmtpException (SmtpStatusCode.GeneralFailure, "Unable to parse status code.");

							if (index == endIndex) {
								index = startIndex;
								break;
							}

							if (code == 0) {
								code = value;
							} else if (value != code) {
								throw new SmtpException (SmtpStatusCode.GeneralFailure, "Mismatched status codes.");
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

				// FIXME: use MimeKit.Utils.Charset.ConvertToUnicode()?
				var message = Encoding.ASCII.GetString (memory.GetBuffer (), 0, (int) memory.Length);

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

			return ReadResponse (stream, buffer, token);
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
					throw new SmtpException (response.StatusCode, response.Response);
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
						throw new SmtpException (response.StatusCode, response.Response);

					challenge = sasl.Challenge (response.Response);
					response = SendCommand (challenge + "\r\n", token);
				}

				if (response.StatusCode == SmtpStatusCode.AuthenticationSuccessful)
					return;

				throw new UnauthorizedAccessException ();
			}
		}

		public void Connect (Uri uri, ICredentials credentials, CancellationToken token)
		{
			CheckDisposed ();

			if (IsConnected)
				return;

			Capabilities = SmtpCapabilities.None;
			authmechs.Clear ();
			MaxSize = 0;

			bool smtps = uri.Scheme.ToLowerInvariant () == "smtps";
			int port = uri.Port != 0 ? uri.Port : (smtps ? 465 : 25);
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
				} catch (Exception) {
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
				response = ReadResponse (stream, buffer, token);

				if (response.StatusCode != SmtpStatusCode.ServiceReady)
					throw new SmtpException (response.StatusCode, response.Response);

				// Send EHLO and get a list of supported extensions
				Ehlo (localEndPoint, token);

				if (!smtps & Capabilities.HasFlag (SmtpCapabilities.StartTLS)) {
					response = SendCommand ("STARTTLS\r\n", token);
					if (response.StatusCode != SmtpStatusCode.Ok)
						throw new SmtpException (response.StatusCode, response.Response);

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

		public void Disconnect (bool quit, CancellationToken token)
		{
			CheckDisposed ();

			if (stream == null)
				throw new InvalidOperationException ("The SmtpClient has not been connected.");

			if (quit) {
				try {
					SendCommand ("QUIT\r\n", token);
				} catch (OperationCanceledException) {
					Disconnect ();
					throw;
				} catch (IOException) {
				}
			}

			Disconnect ();
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

		void MailFrom (MailboxAddress sender, SmtpExtension extensions, CancellationToken token)
		{
			string command;

			if (Capabilities.HasFlag (SmtpCapabilities.BinaryMime) && extensions.HasFlag (SmtpExtension.BinaryMime)) {
				command = string.Format ("MAIL FROM:<{0}> BODY=BINARYMIME\r\n", sender.Address);
			} else if (Capabilities.HasFlag (SmtpCapabilities.EightBitMime) && extensions.HasFlag (SmtpExtension.EightBitMime)) {
				command = string.Format ("MAIL FROM:<{0}> BODY=8BITMIME\r\n", sender.Address);
			} else {
				command = string.Format ("MAIL FROM:<{0}>\r\n", sender.Address);
			}

			var response = SendCommand (command, token);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpException (response.StatusCode, response.Response);
		}

		void RcptTo (MailboxAddress mailbox, CancellationToken token)
		{
			var command = string.Format ("RCPT TO:<{0}>\r\n", mailbox.Address);
			var response = SendCommand (command, token);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpException (response.StatusCode, response.Response);
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
				throw new SmtpException (response.StatusCode, response.Response);

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

			using (var data = new MemoryBlockStream ()) {
				using (var filtered = new FilteredStream (data)) {
					filtered.Add (new SmtpDataFilter ());
					message.WriteTo (options, filtered);
					filtered.Flush ();
				}

				data.Write (EndData, 0, EndData.Length);
				data.Position = 0;

				WriteData (stream, data, token);
			}

			response = ReadResponse (stream, buffer, token);

			if (response.StatusCode != SmtpStatusCode.Ok)
				throw new SmtpException (response.StatusCode, response.Response);
		}

		void Reset ()
		{
			using (var cancel = new CancellationTokenSource ()) {
				try {
					var response = SendCommand ("RSET\r\n", cancel.Token);
					if (response.StatusCode != SmtpStatusCode.Ok)
						Disconnect (false, cancel.Token);
				} catch (IOException) {
				}
			}
		}

		public void Send (MimeMessage message, CancellationToken token)
		{
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
			} catch (OperationCanceledException) {
				Reset ();
				throw;
			}

			try {
				Data (message, token);
			} catch (OperationCanceledException) {
				// irrecoverable
				Disconnect ();
			} finally {
				if (IsConnected)
					Reset ();
			}
		}

		#endregion

		#region IDisposable

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

