//
// ConnectionExamples.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
using System.Threading;
using System.Threading.Tasks;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;
using MailKit.Net.Imap;

namespace MailKit.Examples {
	public static class ConnectionExamples
	{
		#region ConnectWithStartTls
		public static void ConnectWithStartTls ()
		{
			using (var client = new SmtpClient ()) {
				// Port 587 is the standard "submission" port. The connection starts out as plain-text
				// and is upgraded to TLS using the STARTTLS command. Using SecureSocketOptions.StartTls
				// (rather than StartTlsWhenAvailable) means that Connect() will throw if the server does
				// not support STARTTLS instead of silently continuing over an unencrypted connection.
				client.Connect ("smtp.example.com", 587, SecureSocketOptions.StartTls);

				Console.WriteLine ("Secure: {0}", client.IsSecure);
				Console.WriteLine ("Protocol: {0}", client.SslProtocol);
				Console.WriteLine ("Cipher suite: {0}", client.SslCipherSuite);

				client.Disconnect (true);
			}
		}
		#endregion

		#region ConfigureTls
		public static void ConfigureTls ()
		{
			using (var client = new ImapClient ()) {
				// Restrict the TLS protocol versions that may be negotiated.
				client.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

				// Check the server certificate against the issuer's revocation list (this is the default).
				client.CheckCertificateRevocation = true;

				// Provide a client certificate if the server requires one.
				client.ClientCertificates = new X509CertificateCollection {
					new X509Certificate2 ("client.pfx", "password")
				};

				client.Connect ("imap.example.com", 993, SecureSocketOptions.SslOnConnect);

				client.Disconnect (true);
			}
		}
		#endregion

		#region TimeoutsAndCancellation
		public static async Task ConnectWithTimeoutAsync ()
		{
			using (var client = new ImapClient ()) {
				// The Timeout property (in milliseconds) applies to each individual network read and write.
				client.Timeout = 30 * 1000;

				// A CancellationToken can be used to put an upper bound on an entire operation.
				using (var cts = new CancellationTokenSource (TimeSpan.FromMinutes (2))) {
					await client.ConnectAsync ("imap.example.com", 993, SecureSocketOptions.SslOnConnect, cts.Token);
					await client.AuthenticateAsync ("username", "password", cts.Token);

					await client.Inbox.OpenAsync (FolderAccess.ReadOnly, cts.Token);
					Console.WriteLine ("The Inbox has {0} messages.", client.Inbox.Count);

					await client.DisconnectAsync (true, cts.Token);
				}
			}
		}
		#endregion
	}
}
