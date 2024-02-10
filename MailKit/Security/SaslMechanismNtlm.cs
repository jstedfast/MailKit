//
// SaslMechanismNtlm.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

// https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-nlmp/b38c36ed-2804-4868-a9ff-8dd3182128e4

using System;
using System.Net;
using System.Threading;
using System.Security.Authentication.ExtendedProtection;

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
		static readonly Version DefaultOSVersion;

		enum LoginState {
			Negotiate,
			Challenge
		}

		NtlmNegotiateMessage negotiate;
		bool negotiatedChannelBinding;
		LoginState state;

		static SaslMechanismNtlm ()
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
				DefaultOSVersion = Environment.OSVersion.Version;
			else
				DefaultOSVersion = new Version (10, 0, 22000, 0);
		}

#if NET48_OR_GREATER || NET5_0_OR_GREATER || NETSTANDARD2_0_OR_GREATER
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlm"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context using the default network credentials.
		/// </remarks>
		public SaslMechanismNtlm () : this (CredentialCache.DefaultNetworkCredentials)
		{
		}
#endif

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
			OSVersion = DefaultOSVersion;
			Workstation = Environment.MachineName;
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
			OSVersion = DefaultOSVersion;
			Workstation = Environment.MachineName;
		}

		/// <summary>
		/// This is only used for unit testing purposes.
		/// </summary>
		internal byte[] Nonce {
			get; set;
		}

		/// <summary>
		/// This is only used for unit testing purposes.
		/// </summary>
		internal long? Timestamp {
			get; set;
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

		/// <summary>
		/// Get whether or not the SASL mechanism supports channel binding.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the SASL mechanism supports channel binding.
		/// </remarks>
		/// <value><c>true</c> if the SASL mechanism supports channel binding; otherwise, <c>false</c>.</value>
		public override bool SupportsChannelBinding {
			get { return true; }
		}

		/// <summary>
		/// Get whether or not channel-binding was negotiated by the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not channel-binding has been negotiated by the SASL mechanism.</para>
		/// <note type="note">Some SASL mechanisms, such as SCRAM-SHA1-PLUS and NTLM, are able to negotiate
		/// channel-bindings.</note>
		/// </remarks>
		/// <value><c>true</c> if channel-binding was negotiated; otherwise, <c>false</c>.</value>
		public override bool NegotiatedChannelBinding {
			get { return negotiatedChannelBinding; }
		}

		/// <summary>
		/// Get whether or not the mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the mechanism supports an initial response (SASL-IR).</para>
		/// <para>SASL mechanisms that support sending an initial client response to the server
		/// should return <value>true</value>.</para>
		/// </remarks>
		/// <value><c>true</c> if the mechanism supports an initial response; otherwise, <c>false</c>.</value>
		public override bool SupportsInitialResponse {
			get { return true; }
		}

		/// <summary>
		/// Get or set a value indicating whether or not the NTLM SASL mechanism should allow channel-binding.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a value indicating whether or not the NTLM SASL mechanism should allow channel-binding.</para>
		/// <note type="note">In the future, this option will disappear as channel-binding will become the default. For now,
		/// it is only an option because this feature has not been thoroughly tested.</note>
		/// </remarks>
		/// <value><c>true</c> if the NTLM SASL mechanism should allow channel-binding; otherwise, <c>false</c>.</value>
		public bool AllowChannelBinding {
			get; set;
		}

		/// <summary>
		/// Get or set the Windows OS version to use in the NTLM negotiation (used for debugging purposes).
		/// </summary>
		/// <remarks>
		/// Gets or sets the Windows OS version to use in the NTLM negotiation (used for debugging purposes).
		/// </remarks>
		/// <value>The Windows OS version.</value>
		public Version OSVersion {
			get; set;
		}

		/// <summary>
		/// Get or set the workstation name to use for authentication.
		/// </summary>
		/// <remarks>
		/// Gets or sets the workstation name to use for authentication.
		/// </remarks>
		/// <value>The workstation name.</value>
		public string Workstation {
			get; set;
		}

		/// <summary>
		/// Get or set the service principal name (SPN) of the service that the client wishes to authenticate with.
		/// </summary>
		/// <remarks>
		/// <para>Get or set the service principal name (SPN) of the service that the client wishes to authenticate with.</para>
		/// <note type="note">This value is optional.</note>
		/// </remarks>
		/// <value>The service principal name (SPN) of the service that the client wishes to authenticate with.</value>
		public string ServicePrincipalName {
			get; set;
		}

		/// <summary>
		/// Get or set a value indicating that the caller generated the target's SPN from an untrusted source.
		/// </summary>
		/// <remarks>
		/// Gets or sets a value indicating that the caller generated the target's SPN from an untrusted source.
		/// </remarks>
		/// <value><c>true</c> if the <see cref="ServicePrincipalName"/> is unverified; otherwise, <c>false</c>.</value>
		public bool IsUnverifiedServicePrincipalName {
			get; set;
		}

		/// <summary>
		/// Parse the server's challenge token and return the next challenge response.
		/// </summary>
		/// <remarks>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </remarks>
		/// <returns>The next challenge response.</returns>
		/// <param name="token">The server's challenge token.</param>
		/// <param name="startIndex">The index into the token specifying where the server's challenge begins.</param>
		/// <param name="length">The length of the server's challenge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.NotSupportedException">
		/// The SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected override byte[] Challenge (byte[] token, int startIndex, int length, CancellationToken cancellationToken)
		{
			if (IsAuthenticated)
				return null;

			string userName = Credentials.UserName;
			string domain = Credentials.Domain;
			NtlmMessageBase message = null;

			if (string.IsNullOrEmpty (domain)) {
				int index;

				if ((index = userName.LastIndexOf ('@')) != -1) {
					domain = userName.Substring (index + 1);
					userName = userName.Substring (0, index);
				} else {
					if ((index = userName.IndexOf ('\\')) == -1)
						index = userName.IndexOf ('/');

					if (index >= 0) {
						domain = userName.Substring (0, index);
						userName = userName.Substring (index + 1);
					}
				}
			}

			switch (state) {
			case LoginState.Negotiate:
				message = negotiate = new NtlmNegotiateMessage (domain, Workstation, OSVersion);
				state = LoginState.Challenge;
				break;
			case LoginState.Challenge:
				var password = Credentials.Password;
				message = GetChallengeResponse (domain, userName, password, token, startIndex, length);
				IsAuthenticated = true;
				break;
			}

			return message?.Encode ();
		}

		NtlmAuthenticateMessage GetChallengeResponse (string domain, string userName, string password, byte[] token, int startIndex, int length)
		{
			var challenge = new NtlmChallengeMessage (token, startIndex, length);
			var authenticate = new NtlmAuthenticateMessage (negotiate, challenge, userName, password, domain, Workstation) {
				ClientChallenge = Nonce,
				Timestamp = Timestamp
			};
			byte[] channelBindingToken = null;

			if (AllowChannelBinding && challenge.TargetInfo != null) {
				// Only bother with attempting to channel-bind if the CHALLENGE_MESSAGE's TargetInfo is not NULL.
				// Not sure which channel-binding types are supported by NTLM, but I am told that supposedly the
				// System.Net.Mail.SmtpClient uses tls-unique, so we'll go with that...
				negotiatedChannelBinding = TryGetChannelBindingToken (ChannelBindingKind.Endpoint, out channelBindingToken);
			}

			authenticate.ComputeNtlmV2 (ServicePrincipalName, IsUnverifiedServicePrincipalName, channelBindingToken);

			if (channelBindingToken != null)
				Array.Clear (channelBindingToken, 0, channelBindingToken.Length);

			negotiate = null;

			return authenticate;
		}

		/// <summary>
		/// Reset the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public override void Reset ()
		{
			negotiatedChannelBinding = false;
			state = LoginState.Negotiate;
			negotiate = null;
			base.Reset ();
		}
	}
}
