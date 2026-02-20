//
// SaslMechanismGssapi.cs
//
// Authors: Roman Konecny <rokonecn@microsoft.com>
//          Jeffrey Stedfast <jestedfa@microsoft.com>
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

#if NET7_0_OR_GREATER

using System;
using System.Net;
using System.Net.Security;

namespace MailKit.Security {
	/// <summary>
	/// A SASL mechanism that uses the Kerberos/GSSAPI protocol.
	/// </summary>
	/// <remarks>
	/// Implements the GSSAPI for KERBEROS SASL mechanism.
	/// </remarks>
	public class SaslMechanismGssapi : SaslMechanismNegotiateBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismGssapi"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new GSSAPI SASL context using the default network credentials.
		/// </remarks>
		public SaslMechanismGssapi () : this (CredentialCache.DefaultNetworkCredentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismGssapi"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new GSSAPI SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <see langword="null" />.
		/// </exception>
		public SaslMechanismGssapi (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismGssapi"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new GSSAPI SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		public SaslMechanismGssapi (string userName, string password) : base (userName, password)
		{
		}

		/// <summary>
		/// Get the name of the authentication mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Gets the name of the authentication mechanism.</para>
		/// <note type="note">This value MUST be one of the following: "NTLM", "Kerberos" or "Negotiate".</note>
		/// </remarks>
		/// <value>The name of the authentication mechanism.</value>
		protected override string AuthMechanism {
			get { return "Kerberos"; }
		}

		/// <summary>
		/// Get the name of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the SASL mechanism.
		/// </remarks>
		/// <value>The name of the SASL mechanism.</value>
		public override string MechanismName {
			get { return "GSSAPI"; }
		}

		/// <summary>
		/// Get whether or not the SASL mechanism supports negotiating a security layer.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the SASL mechanism supports negotiating a security layer.
		/// </remarks>
		/// <value><see langword="true"/> if the SASL mechanism supports negotiating a security layer; otherwise, <see langword="false"/>.</value>
		protected override bool SupportsSecurityLayer {
			get { return true; }
		}

		/// <summary>
		/// Get the required protection level.
		/// </summary>
		/// <remarks>
		/// Gets the required protection level.
		/// </remarks>
		/// <value>The required protection level.</value>
		protected override ProtectionLevel RequiredProtectionLevel {
			get {
				// Work-around for https://github.com/gssapi/gss-ntlmssp/issues/77
				// GSSAPI NTLM SSP does not support gss_wrap/gss_unwrap unless confidentiality
				// is negotiated.
				if (OperatingSystem.IsLinux ())
					return ProtectionLevel.EncryptAndSign;

				return ProtectionLevel.Sign;
			}
		}

		/// <summary>
		/// Create the <see cref="NegotiateAuthenticationClientOptions"/>.
		/// </summary>
		/// <remarks>
		/// Creates the <see cref="NegotiateAuthenticationClientOptions"/>.
		/// </remarks>
		/// <returns>The client options.</returns>
		protected override NegotiateAuthenticationClientOptions CreateClientOptions ()
		{
			var options = base.CreateClientOptions ();

			// Provide a default TargetName (the base implementation already sets the
			// TargetName to the ServicePrincipalName if the value was provided).
			options.TargetName ??= $"SMTPSVC/{Uri!.Host}";

			return options;
		}
	}
}

#endif // NET7_0_OR_GREATER
