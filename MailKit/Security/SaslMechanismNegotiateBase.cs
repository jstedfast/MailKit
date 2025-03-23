//
// SaslMechanismNegotiateBase.cs
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

using System;
using System.Net;
using System.Threading;
using System.Net.Security;
using System.Threading.Tasks;
using System.Security.Authentication.ExtendedProtection;

namespace MailKit.Security
{
	/// <summary>
	/// The base class for .NET Core's NegotiateAuthentication-based SASL mechanisms.
	/// </summary>
	/// <remarks>
	/// The base class for .NET Core's <see cref="NegotiateAuthentication"/>-based SASL mechanisms.
	/// </remarks>
	public abstract class SaslMechanismNegotiateBase : SaslMechanism
	{
		NegotiateAuthentication negotiate;
		bool negotiatedChannelBinding;
		bool requestedChannelBinding;

		static SaslException GetSaslException (string mechanismName, NegotiateAuthenticationStatusCode statusCode)
		{
			var errorCode = SaslErrorCode.InvalidChallenge;
			string message;

			switch (statusCode) {
			case NegotiateAuthenticationStatusCode.GenericFailure: message = "Operation resulted in failure but no specific error code was given."; break;
			case NegotiateAuthenticationStatusCode.BadBinding: message = "Channel binding mismatch between client and server."; break;
			case NegotiateAuthenticationStatusCode.Unsupported: message = "Unsupported authentication package was requested."; break;
			case NegotiateAuthenticationStatusCode.MessageAltered: message = "Message was altered and failed an integrity check validation."; break;
			case NegotiateAuthenticationStatusCode.ContextExpired: message = "Referenced authentication context has expired."; break;
			case NegotiateAuthenticationStatusCode.CredentialsExpired: message = "Authentication credentials have expired."; break;
			case NegotiateAuthenticationStatusCode.InvalidCredentials: message = "Consistency checks performed on the credential failed."; break;
			case NegotiateAuthenticationStatusCode.InvalidToken: message = "Checks performed on the authentication token failed."; break;
			case NegotiateAuthenticationStatusCode.UnknownCredentials: message = "The supplied credentials were not valid for context acceptance, or the credential handle did not reference any credentials."; break;
			case NegotiateAuthenticationStatusCode.QopNotSupported: message = "Requested protection level is not supported."; break;
			case NegotiateAuthenticationStatusCode.OutOfSequence: message = "Authentication token was identfied as duplicate, old, or out of expected sequence."; break;
			case NegotiateAuthenticationStatusCode.SecurityQosFailed: message = "Validation of RequiredProtectionLevel against negotiated protection level failed."; break;
			case NegotiateAuthenticationStatusCode.TargetUnknown: message = "Validation of the target name failed."; break;
			case NegotiateAuthenticationStatusCode.ImpersonationValidationFailed: message = "Validation of the impersonation level failed."; break;
			default: message = $"Failed with unknown status code {statusCode}."; break;
			}

			return new SaslException (mechanismName, errorCode, $"{mechanismName} authentication error: {message}");
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNegotiateBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NegotiateAuthentication"/>-based SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <see langword="null" />.
		/// </exception>
		protected SaslMechanismNegotiateBase (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNegotiateBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="NegotiateAuthentication"/>-based SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <see langword="null" />.</para>
		/// </exception>
		protected SaslMechanismNegotiateBase (string userName, string password) : base (userName, password)
		{
		}

		/// <summary>
		/// Get whether or not the SASL mechanism supports channel binding.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the SASL mechanism supports channel binding.
		/// </remarks>
		/// <value><see langword="true" /> if the SASL mechanism supports channel binding; otherwise, <see langword="false" />.</value>
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
		/// <value><see langword="true" /> if channel-binding was negotiated; otherwise, <see langword="false" />.</value>
		public override bool NegotiatedChannelBinding {
			get { return negotiatedChannelBinding; }
		}

		/// <summary>
		/// Get or set the desired channel-binding to be negotiated by the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Gets or sets the desired channel-binding to be negotiated by the SASL mechanism.
		/// </remarks>
		/// <value>The type of channel-binding.</value>
		public ChannelBindingKind DesiredChannelBinding {
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
			// Note: This does not need to be implemented because we override the Challenge(string) method instead.
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Decode the base64-encoded server challenge and return the next challenge response encoded in base64.
		/// </summary>
		/// <remarks>
		/// Decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </remarks>
		/// <returns>The next base64-encoded challenge response.</returns>
		/// <param name="token">The server's base64-encoded challenge token.</param>
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
		public override string Challenge (string token, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			if (IsAuthenticated)
				return string.Empty;

			if (negotiate == null) {
				var options = new NegotiateAuthenticationClientOptions {
					Credential = Credentials,
					Package = MechanismName,
				};

				if (DesiredChannelBinding != ChannelBindingKind.Unknown && TryGetChannelBinding (DesiredChannelBinding, out var channelBinding)) {
					options.Binding = channelBinding;
					requestedChannelBinding = true;
				}

				if (!string.IsNullOrEmpty (ServicePrincipalName))
					options.TargetName = ServicePrincipalName;

				negotiate = new NegotiateAuthentication (options);
			}

			var response = negotiate.GetOutgoingBlob (token, out var statusCode);

			switch (statusCode) {
			case NegotiateAuthenticationStatusCode.Completed:
				negotiatedChannelBinding = requestedChannelBinding;
				IsAuthenticated = true;
				negotiate.Dispose ();
				negotiate = null;
				break;
			case NegotiateAuthenticationStatusCode.ContinueNeeded:
				break;
			default:
				throw GetSaslException (MechanismName, statusCode);
			}

			return response;
		}

		/// <summary>
		/// Asynchronously decode the base64-encoded server challenge and return the next challenge response encoded in base64.
		/// </summary>
		/// <remarks>
		/// Asynchronously decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </remarks>
		/// <returns>The next base64-encoded challenge response.</returns>
		/// <param name="token">The server's base64-encoded challenge token.</param>
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
		public override Task<string> ChallengeAsync (string token, CancellationToken cancellationToken = default)
		{
			return Task.FromResult (Challenge (token, cancellationToken));
		}

		/// <summary>
		/// Reset the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public override void Reset ()
		{
			if (negotiate != null) {
				negotiatedChannelBinding = false;
				requestedChannelBinding = false;
				negotiate.Dispose ();
				negotiate = null;
			}

			base.Reset ();
		}
	}
}

#endif // NET7_0_OR_GREATER
