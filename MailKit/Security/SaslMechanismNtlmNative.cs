//
// SaslMechanismNtlmNative.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2025 .NET Foundation and Contributors
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

using System.Net;
using System.Net.Security;

namespace MailKit.Security {
	/// <summary>
	/// The NTLM SASL mechanism.
	/// </summary>
	/// <remarks>
	/// <para>A SASL mechanism based on NTLM that uses .NET Core's <see cref="NegotiateAuthentication"/> class for authenticating.</para>
	/// <note type="warning">
	/// <para>NTLM is a legacy challenge-response authentication mechanism introduced by Microsoft
	/// in the 1990's and suffers from the following weaknesses:</para>
	/// <list type="bullet">
	/// <item>Pass-the-Hash Attacks: Stolen NTLM hashes can be reused without knowing the password.</item>
	/// <item>Relay Attacks: NTLM does not protect against credential forwarding.</item>
	/// <item>Cryptography: NTLMv1 relies on DES and MD4 which are both very weak. NTLMv2 relies on HMAC-MD5
	/// which is better but still considered very weak by modern standards.</item>
	/// </list>
	/// <para>Microsoft recommends disabling NTLM and migrating to Kerberos
	/// (<a href="T_MailKit_Security_SaslMechanismGssapi.htm">GSSAPI</a>)
	/// or modern alternatives.</para>
	/// </note>
	/// </remarks>
	public class SaslMechanismNtlmNative : SaslMechanismNegotiateBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlmNative"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context using the default network credentials.
		/// </remarks>
		public SaslMechanismNtlmNative () : this (CredentialCache.DefaultNetworkCredentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlmNative"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <see langword="null" />.
		/// </exception>
		public SaslMechanismNtlmNative (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlmNative"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		public SaslMechanismNtlmNative (string userName, string password) : base (userName, password)
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
			get { return MechanismName; }
		}

		/// <summary>
		/// Get the name of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the SASL mechanism.
		/// </remarks>
		/// <value>The name of the SASL mechanism.</value>
		public override string MechanismName {
			get { return "NTLM"; }
		}
	}
}

#endif // NET7_0_OR_GREATER
