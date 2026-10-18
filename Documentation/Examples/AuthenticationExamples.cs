//
// AuthenticationExamples.cs
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
using System.Net;
using System.Security.Authentication.ExtendedProtection;

using MailKit;
using MailKit.Security;
using MailKit.Net.Smtp;
using MailKit.Net.Imap;

namespace MailKit.Examples {
	public static class AuthenticationExamples
	{
		#region AuthenticateWithCredentials
		public static void AuthenticateWithCredentials ()
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.example.com", 993, SecureSocketOptions.SslOnConnect);

				// The AuthenticationMechanisms property contains the SASL mechanisms advertised by the server.
				Console.WriteLine ("Supported mechanisms: {0}", string.Join (", ", client.AuthenticationMechanisms));

				// MailKit will pick the strongest mechanism supported by both the client and the server.
				client.Authenticate (new NetworkCredential ("username", "password"));

				client.Disconnect (true);
			}
		}
		#endregion

		#region RemoveAuthenticationMechanisms
		public static void RemoveAuthenticationMechanisms ()
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("smtp.example.com", 587, SecureSocketOptions.StartTls);

				// Prevent MailKit from trying mechanisms that we know are broken or undesirable
				// for this particular server. This must be done after Connect() and before
				// Authenticate().
				client.AuthenticationMechanisms.Remove ("NTLM");
				client.AuthenticationMechanisms.Remove ("CRAM-MD5");
				client.AuthenticationMechanisms.Remove ("DIGEST-MD5");

				client.Authenticate ("username", "password");

				client.Disconnect (true);
			}
		}
		#endregion

		#region AuthenticateWithSaslMechanism
		public static void AuthenticateWithSaslMechanism ()
		{
			using (var client = new ImapClient ()) {
				client.Connect ("imap.example.com", 993, SecureSocketOptions.SslOnConnect);

				var credentials = new NetworkCredential ("username", "password");
				SaslMechanism sasl;

				// Explicitly choose a mechanism rather than letting MailKit pick one.
				if (client.AuthenticationMechanisms.Contains ("SCRAM-SHA-256-PLUS"))
					sasl = new SaslMechanismScramSha256Plus (credentials);
				else if (client.AuthenticationMechanisms.Contains ("SCRAM-SHA-256"))
					sasl = new SaslMechanismScramSha256 (credentials);
				else
					throw new AuthenticationException ("The server does not support SCRAM-SHA-256.");

				client.Authenticate (sasl);

				// SCRAM-*-PLUS mechanisms bind the authentication to the underlying TLS channel.
				Console.WriteLine ("Channel binding negotiated: {0}", sasl.NegotiatedChannelBinding);

				client.Disconnect (true);
			}
		}
		#endregion

		#region AuthenticateWithNtlm
		public static void AuthenticateWithNtlm ()
		{
			using (var client = new SmtpClient ()) {
				client.Connect ("mail.corp.example.com", 587, SecureSocketOptions.StartTls);

				var ntlm = new SaslMechanismNtlm (new NetworkCredential ("username", "password", "DOMAIN")) {
					// The workstation name defaults to Environment.MachineName.
					Workstation = "MY-WORKSTATION",

					// Including the SPN in the NTLMv2 response helps protect against relay attacks.
					ServicePrincipalName = "SMTPSVC/mail.corp.example.com",

					// Channel binding ties the authentication to the TLS session, which protects against
					// man-in-the-middle attacks. It is currently opt-in for the managed NTLM implementation.
					AllowChannelBinding = true
				};

				client.Authenticate (ntlm);

				Console.WriteLine ("Channel binding negotiated: {0}", ntlm.NegotiatedChannelBinding);

				client.Disconnect (true);
			}
		}
		#endregion

#if NET8_0_OR_GREATER
		#region AuthenticateWithNtlmNative
		public static void AuthenticateWithNtlmNative ()
		{
			using (var client = new ImapClient ()) {
				client.Connect ("mail.corp.example.com", 993, SecureSocketOptions.SslOnConnect);

				// The default constructor uses CredentialCache.DefaultNetworkCredentials, which
				// means that the credentials of the currently logged-in user will be used.
				var ntlm = new SaslMechanismNtlmNative () {
					DesiredChannelBinding = ChannelBindingKind.Endpoint
				};

				client.Authenticate (ntlm);

				client.Disconnect (true);
			}
		}
		#endregion

		#region AuthenticateWithGssapi
		public static void AuthenticateWithGssapi ()
		{
			using (var client = new ImapClient ()) {
				client.Connect ("mail.corp.example.com", 993, SecureSocketOptions.SslOnConnect);

				if (client.AuthenticationMechanisms.Contains ("GSSAPI") && SaslMechanism.IsSupported ("GSSAPI")) {
					// Use the Kerberos ticket of the currently logged-in user.
					var gssapi = new SaslMechanismGssapi () {
						// The SPN identifies the service that we are authenticating to. For IMAP, this
						// is normally "imap/<fqdn>". If not set, MailKit uses "SMTPSVC/<host>".
						ServicePrincipalName = "imap/mail.corp.example.com"
					};

					client.Authenticate (gssapi);
				} else {
					client.Authenticate ("username", "password");
				}

				client.Disconnect (true);
			}
		}
		#endregion
#endif
	}
}
