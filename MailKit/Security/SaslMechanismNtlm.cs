//
// SaslMechanismNtlm.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

using MailKit.Security.Ntlm;

namespace MailKit.Security {
	/// <summary>
	/// The NTLM SASL mechanism.
	/// </summary>
	/// <remarks>
	/// A SASL mechanism based on NTLM.
	/// </remarks>
	public class SaslMechanismNtlm : SaslMechanism
	{
		enum LoginState {
			Initial,
			Challenge
		}

		LoginState state;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlm"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismNtlm(NetworkCredential) instead.")]
		public SaslMechanismNtlm (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
			Level = NtlmAuthLevel.NTLMv2_only;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlm"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismNtlm(string, string) instead.")]
		public SaslMechanismNtlm (Uri uri, string userName, string password) : base (uri, userName, password)
		{
			Level = NtlmAuthLevel.NTLMv2_only;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlm"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismNtlm (NetworkCredential credentials) : base (credentials)
		{
			Level = NtlmAuthLevel.NTLMv2_only;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlm"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new NTLM SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismNtlm (string userName, string password) : base (userName, password)
		{
			Level = NtlmAuthLevel.NTLMv2_only;
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the mechanism.
		/// </remarks>
		/// <value>The name of the mechanism.</value>
		public override string MechanismName {
			get { return "NTLM"; }
		}

		/// <summary>
		/// Gets whether or not the mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// SASL mechanisms that support sending an initial client response to the server
		/// should return <value>true</value>.
		/// </remarks>
		/// <value><c>true</c> if the mechanism supports an initial response; otherwise, <c>false</c>.</value>
		public override bool SupportsInitialResponse {
			get { return true; }
		}

		internal NtlmAuthLevel Level {
			get; set;
		}

		/// <summary>
		/// Gets or sets the Windows OS version to use in the NTLM negotiation (used for debuigging purposes).
		/// </summary>
		/// <remarks>
		/// Gets or sets the Windows OS version to use in the NTLM negotiation (used for debuigging purposes).
		/// </remarks>
		public Version OSVersion {
			get; set;
		}

		/// <summary>
		/// Gets or sets the workstation name to use for authentication.
		/// </summary>
		/// <remarks>
		/// Gets or sets the workstation name to use for authentication.
		/// </remarks>
		/// <value>The workstation name.</value>
		public string Workstation {
			get; set;
		}

		/// <summary>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </summary>
		/// <remarks>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </remarks>
		/// <returns>The next challenge response.</returns>
		/// <param name="token">The server's challenge token.</param>
		/// <param name="startIndex">The index into the token specifying where the server's challenge begins.</param>
		/// <param name="length">The length of the server's challenge.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The SASL mechanism is already authenticated.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			if (IsAuthenticated)
				return null;

			string userName = Credentials.UserName;
			string domain = Credentials.Domain;
			MessageBase message = null;

			if (string.IsNullOrEmpty (domain)) {
				int index = userName.IndexOf ('\\');
				if (index == -1)
					index = userName.IndexOf ('/');

				if (index >= 0) {
					domain = userName.Substring (0, index);
					userName = userName.Substring (index + 1);
				}
			}

			switch (state) {
			case LoginState.Initial:
				message = new Type1Message (Workstation, domain, OSVersion);
				state = LoginState.Challenge;
				break;
			case LoginState.Challenge:
				var password = Credentials.Password ?? string.Empty;
				message = GetChallengeResponse (userName, password, token, startIndex, length);
				IsAuthenticated = true;
				break;
			}

			return message?.Encode ();
		}

		MessageBase GetChallengeResponse (string userName, string password, byte[] token, int startIndex, int length)
		{
			var type2 = new Type2Message (token, startIndex, length);

			return new Type3Message (type2, OSVersion, Level, userName, password, Workstation);
		}

		/// <summary>
		/// Resets the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public override void Reset ()
		{
			state = LoginState.Initial;
			base.Reset ();
		}
	}
}
