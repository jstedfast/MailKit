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

using System;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace MailKit.Security {
	/// <summary>
	/// The NTLM SASL mechanism.
	/// </summary>
	/// <remarks>
	/// A SASL mechanism based on NTLM that uses .NET Core's <see cref="NegotiateAuthentication"/> class for authenticating.
	/// </remarks>
	public class SaslMechanismNtlmNative : SaslMechanism
	{
		NegotiateAuthentication negotiate;

		static string GetExceptionMessage (NegotiateAuthenticationStatusCode statusCode, out SaslErrorCode errorCode)
		{
			errorCode = SaslErrorCode.InvalidChallenge;

			switch (statusCode) {
			case NegotiateAuthenticationStatusCode.GenericFailure: return "NTLM authentication error: Operation resulted in failure but no specific error code was given.";
			case NegotiateAuthenticationStatusCode.BadBinding: return "NTLM authentication error: Channel binding mismatch between client and server.";
			case NegotiateAuthenticationStatusCode.Unsupported: return "NTLM authentication error: Unsupported authentication package was requested.";
			case NegotiateAuthenticationStatusCode.MessageAltered: return "NTLM authentication error: Message was altered and failed an integrity check validation.";
			case NegotiateAuthenticationStatusCode.ContextExpired: return "NTLM authentication error: Referenced authentication context has expired.";
			case NegotiateAuthenticationStatusCode.CredentialsExpired: return "NTLM authentication error: Authentication credentials have expired.";
			case NegotiateAuthenticationStatusCode.InvalidCredentials: return "NTLM authentication error: Consistency checks performed on the credential failed.";
			case NegotiateAuthenticationStatusCode.InvalidToken: return "NTLM authentication error: Checks performed on the authentication token failed.";
			case NegotiateAuthenticationStatusCode.UnknownCredentials: return "NTLM authentication error: The supplied credentials were not valid for context acceptance, or the credential handle did not reference any credentials.";
			case NegotiateAuthenticationStatusCode.QopNotSupported: return "NTLM authentication error: Requested protection level is not supported.";
			case NegotiateAuthenticationStatusCode.OutOfSequence: return "NTLM authentication error: Authentication token was identfied as duplicate, old, or out of expected sequence.";
			case NegotiateAuthenticationStatusCode.SecurityQosFailed: return "NTLM authentication error: Validation of RequiredProtectionLevel against negotiated protection level failed.";
			case NegotiateAuthenticationStatusCode.TargetUnknown: return "NTLM authentication error: Validation of the target name failed.";
			case NegotiateAuthenticationStatusCode.ImpersonationValidationFailed: return "NTLM authentication error: Validation of the impersonation level failed.";
			default: return $"NTLM authentication error: Failed with unknown status code {statusCode}.";
			}
		}

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
		/// Get whether or not the mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the mechanism supports an initial response (SASL-IR).</para>
		/// <para>SASL mechanisms that support sending an initial client response to the server
		/// should return <see langword="true" />.</para>
		/// </remarks>
		/// <value><see langword="true" /> if the mechanism supports an initial response; otherwise, <see langword="false" />.</value>
		public override bool SupportsInitialResponse {
			get { return true; }
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

				negotiate = new NegotiateAuthentication (options);
			}

			var response = negotiate.GetOutgoingBlob (token, out var statusCode);

			switch (statusCode) {
			case NegotiateAuthenticationStatusCode.Completed:
				IsAuthenticated = true;
				negotiate.Dispose ();
				negotiate = null;
				break;
			case NegotiateAuthenticationStatusCode.ContinueNeeded:
				break;
			default:
				var message = GetExceptionMessage (statusCode, out var errorCode);
				throw new SaslException (MechanismName, errorCode, message);
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
				negotiate.Dispose ();
				negotiate = null;
			}

			base.Reset ();
		}
	}
}

#endif // NET7_0_OR_GREATER
