//
// SaslMechanismNegotiateBase.cs
//
// Authors: Roman Konecny <rokonecn@microsoft.com>
//          Jeffrey Stedfast <jestedfa@microsoft.com>
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
using System.Buffers;
using System.Threading;
using System.Net.Security;
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
		static ReadOnlySpan<byte> SaslNoSecurityLayerToken => new byte[] { 1, 0, 0, 0 };

		NegotiateAuthentication negotiate;
		bool negotiatedChannelBinding;
		bool requestedChannelBinding;
		bool negotiatedSecurityLayer;

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
		/// Get whether or not the SASL mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not the SASL mechanism supports an initial response (SASL-IR).</para>
		/// <para>SASL mechanisms that support sending an initial client response to the server
		/// should return <see langword="true" />.</para>
		/// </remarks>
		/// <value><see langword="true" /> if the SASL mechanism supports an initial response; otherwise, <see langword="false" />.</value>
		public override bool SupportsInitialResponse {
			get { return true; }
		}

		/// <summary>
		/// Get the name of the authentication mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Gets the name of the authentication mechanism.</para>
		/// <note type="note">This value MUST be one of the following: "NTLM", "Kerberos" or "Negotiate".</note>
		/// </remarks>
		/// <value>The name of the authentication mechanism.</value>
		protected abstract string AuthMechanism {
			get;
		}

		/// <summary>
		/// Get the required protection level.
		/// </summary>
		/// <remarks>
		/// Gets the required protection level.
		/// </remarks>
		/// <value>The required protection level.</value>
		protected virtual ProtectionLevel RequiredProtectionLevel {
			get { return ProtectionLevel.None; }
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
		/// <para>Gets or sets the desired channel-binding to be negotiated by the SASL mechanism.</para>
		/// <note type="note">This value is optional.</note>
		/// </remarks>
		/// <value>The type of channel-binding.</value>
		public ChannelBindingKind DesiredChannelBinding {
			get; set;
		}

		/// <summary>
		/// Get whether or not the SASL mechanism supports negotiating a security layer.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the SASL mechanism supports negotiating a security layer.
		/// </remarks>
		/// <value><see langword="true"/> if the SASL mechanism supports negotiating a security layer; otherwise, <see langword="false"/>.</value>
		protected virtual bool SupportsSecurityLayer {
			get { return false; }
		}

		/// <summary>
		/// Get whether or not a security layer was negotiated by the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// <para>Gets whether or not a security layer has been negotiated by the SASL mechanism.</para>
		/// <note type="note">Some SASL mechanisms, such as GSSAPI, are able to negotiate security layers
		/// such as integrity and confidentiality protection.</note>
		/// </remarks>
		/// <value><see langword="true" /> if a security layer was negotiated; otherwise, <see langword="false" />.</value>
		public override bool NegotiatedSecurityLayer {
			get { return negotiatedSecurityLayer; }
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
		protected override byte[] Challenge (byte[] token, int startIndex, int length, CancellationToken cancellationToken = default)
		{
			if (!SupportsSecurityLayer && IsAuthenticated)
				return null;

			cancellationToken.ThrowIfCancellationRequested ();

			// On the first call, initialize the NegotiateAuthentication if needed.
			negotiate ??= new NegotiateAuthentication (CreateClientOptions ());

			var challenge = token != null ? token.AsSpan (startIndex, length) : ReadOnlySpan<byte>.Empty;

			if (IsAuthenticated) {
				// If auth completed and another challenge was received, then the server
				// may be doing "correct" form of GSSAPI SASL. Validate the incoming and
				// produce outgoing SASL security layer negotiate message.
				return GetSecurityLayerNegotiationResponse (challenge);
			}

			// Calculate the challenge response.
			return GetChallengeResponse (challenge);
		}

		/// <summary>
		/// Create the <see cref="NegotiateAuthenticationClientOptions"/>.
		/// </summary>
		/// <remarks>
		/// Creates the <see cref="NegotiateAuthenticationClientOptions"/>.
		/// </remarks>
		/// <returns>The client options.</returns>
		protected virtual NegotiateAuthenticationClientOptions CreateClientOptions ()
		{
			var options = new NegotiateAuthenticationClientOptions {
				RequiredProtectionLevel = RequiredProtectionLevel,
				Credential = Credentials,
				Package = AuthMechanism,
			};

			if (DesiredChannelBinding != ChannelBindingKind.Unknown && TryGetChannelBinding (DesiredChannelBinding, out var channelBinding)) {
				options.Binding = channelBinding;
				requestedChannelBinding = true;
			}

			if (!string.IsNullOrEmpty (ServicePrincipalName))
				options.TargetName = ServicePrincipalName;

			return options;
		}

		byte[] GetChallengeResponse (ReadOnlySpan<byte> challenge)
		{
			var response = negotiate.GetOutgoingBlob (challenge, out NegotiateAuthenticationStatusCode statusCode);

			switch (statusCode) {
			case NegotiateAuthenticationStatusCode.Completed:
				// Authentication is completed (but may receive a Security Layer negotiation challenge next).
				negotiatedChannelBinding = requestedChannelBinding;
				IsAuthenticated = true;
				break;
			case NegotiateAuthenticationStatusCode.ContinueNeeded:
				break;
			default:
				throw GetSaslException (MechanismName, statusCode);
			}

			return response;
		}

		// Function for SASL security layer negotiation after authorization completes.
		//
		// Returns null for failure.
		//
		// Cloned from: https://github.com/dotnet/runtime/blob/4631ecec883a90ae9c29c058eea4527f9f2cb473/src/libraries/System.Net.Mail/src/System/Net/Mail/SmtpNegotiateAuthenticationModule.cs#L107
		byte[] GetSecurityLayerNegotiationResponse (ReadOnlySpan<byte> challenge)
		{
			NegotiateAuthenticationStatusCode statusCode;
			byte[] input = challenge.ToArray ();
			Span<byte> unwrapped;

			statusCode = negotiate.UnwrapInPlace (input, out int unwrappedOffset, out int unwrappedLength, out _);
			if (statusCode != NegotiateAuthenticationStatusCode.Completed)
				return null;

			unwrapped = input.AsSpan (unwrappedOffset, unwrappedLength);

			// Per RFC 2222 Section 7.2.2:
			//   the client should then expect the server to issue a
			//   token in a subsequent challenge.  The client passes
			//   this token to GSS_Unwrap and interprets the first
			//   octet of cleartext as a bit-mask specifying the
			//   security layers supported by the server and the
			//   second through fourth octets as the maximum size
			//   output_message to send to the server.
			// Section 7.2.3
			//   The security layer and their corresponding bit-masks
			//   are as follows:
			//     1 No security layer
			//     2 Integrity protection
			//       Sender calls GSS_Wrap with conf_flag set to FALSE
			//     4 Privacy protection
			//       Sender calls GSS_Wrap with conf_flag set to TRUE
			//
			// Exchange 2007 and our client only support
			// "No security layer". We verify that the server offers
			// option to use no security layer and negotiate that if
			// possible.

			if (unwrapped.Length != 4 || (unwrapped[0] & 0x01) != 0x01)
				return null;

			// Continuing with RFC 2222 section 7.2.2:
			//   The client then constructs data, with the first octet
			//   containing the bit-mask specifying the selected security
			//   layer, the second through fourth octets containing in
			//   network byte order the maximum size output_message the client
			//   is able to receive, and the remaining octets containing the
			//   authorization identity.
			//
			// So now this constructs the "wrapped" response.

			// let MakeSignature figure out length of output
			ArrayBufferWriter<byte> writer = new ArrayBufferWriter<byte> ();
			statusCode = negotiate.Wrap (SaslNoSecurityLayerToken, writer, false, out _);
			if (statusCode != NegotiateAuthenticationStatusCode.Completed)
				return null;

			negotiatedSecurityLayer = true;

			return writer.WrittenSpan.ToArray ();
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
				negotiatedSecurityLayer = false;
				negotiate.Dispose ();
				negotiate = null;
			}

			base.Reset ();
		}

		internal static bool CheckSupported (string mechanismName)
		{
			try {
				var options = new NegotiateAuthenticationClientOptions {
					Credential = new NetworkCredential ("username", "password"),
					Package = mechanismName,
				};
				var negotiate = new NegotiateAuthentication (options);

				negotiate.GetOutgoingBlob (Array.Empty<byte> (), out NegotiateAuthenticationStatusCode statusCode);

				return statusCode == NegotiateAuthenticationStatusCode.Completed;
			} catch {
				return false;
			}
		}
	}
}

#endif // NET7_0_OR_GREATER
