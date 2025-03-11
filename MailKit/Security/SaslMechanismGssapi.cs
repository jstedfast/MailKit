using System;
using System.Buffers;
using System.Net;
using System.Net.Security;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;

using MailKit.Net;

namespace MailKit.Security
{
	/// <summary>
	/// A SASL mechanism that uses the Kerberos/GSSAPI protocol.
	/// </summary>
	/// <remarks>
	/// Implements the GSSAPI for KERBEROS SASL mechanism.
	/// </remarks>
	public class SaslMechanismGssapi : SaslMechanism
	{
		private static ReadOnlySpan<byte> SaslNoSecurityLayerToken => new byte[] { 1, 0, 0, 0 };

		private NegotiateAuthentication negotiate;

		/// <summary>
		/// The name of the SASL mechanism - GSSAPI
		/// </summary>
		public override string MechanismName => "GSSAPI";

		/// <summary>
		/// GSSAPI can send an initial client response (sometimes referred to as "SASL-IR").
		/// </summary>
		public override bool SupportsInitialResponse => true;

		/// <summary>
		/// This implementation of GSSAPI does support chanell binding
		/// </summary>
		public override bool SupportsChannelBinding => true;

		/// <summary>
		/// Get or set a value indicating whether or not the GSSAPI SASL mechanism should allow channel-binding.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets a value indicating whether or not the SASL mechanism should allow channel-binding.</para>
		/// <note type="note">In the future, this option will disappear as channel-binding will become the default. For now,
		/// it is only an option because this feature has not been thoroughly tested.</note>
		/// </remarks>
		/// <value><see langword="true" /> if the GSSAPI SASL mechanism should allow channel-binding; otherwise, <see langword="false" />.</value>
		public bool AllowChannelBinding {
			get; set;
		}

		/// <summary>
		/// Target (spn). If not set default value will be computed as "SMTPSVC/{Uri.Host}"
		/// </summary>
		public string Target { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismGssapi"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SASL context using the default network credentials.
		/// </remarks>
		public SaslMechanismGssapi () : this (CredentialCache.DefaultNetworkCredentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismGssapi"/> class.
		/// </summary>
		/// <param name="credentials"></param>
		public SaslMechanismGssapi (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismGssapi"/> class.
		/// </summary>
		/// <param name="credentials"></param>
		public SaslMechanismGssapi (string userName, string password) : base (userName, password)
		{
		}

		/// <remarks>
		/// The main handshake logic. It is called ech time the SMTP server returns base64 challenge,
		/// with that data (decoded to a byte[]).
		/// We pass it into NegotiateAuthentication, get the next blob, and return it to the server.
		/// </remarks>
		/// <inheritdoc />
		protected override byte[] Challenge (byte[] token, int startIndex, int length, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested ();

			// Convert the server’s challenge token into a usable buffer.
			// If 'token' is null or empty, pass an empty buffer to "start" the handshake.
			ReadOnlySpan<byte> incoming = ReadOnlySpan<byte>.Empty;
			if (token != null && length > 0)
				incoming = new ReadOnlySpan<byte> (token, startIndex, length);

			if (!IsAuthenticated) {
				// If auth is not yet completed keep producing
				// challenge responses with GetOutgoingBlob
				return GetOutgoingBlob (incoming);
			} else {
				// If auth completed and challenge was reseaved then
				// server may be doing "correct" form of GSSAPI SASL.
				// Validate incoming and produce outgoing SASL security
				// layer negotiate message.
				return GetSecurityLayerNegotiationOutgoingBlob (incoming);
			}
		}

		private byte[] GetOutgoingBlob (ReadOnlySpan<byte> incoming)
		{
			// On the first call, initialize the NegotiateAuthentication if needed.
			if (negotiate == null) {
				ProtectionLevel protectionLevel = ProtectionLevel.Sign;
				// Workaround for https://github.com/gssapi/gss-ntlmssp/issues/77
				// GSSAPI NTLM SSP does not support gss_wrap/gss_unwrap unless confidentiality
				// is negotiated.
				if (OperatingSystem.IsLinux ()) {
					protectionLevel = ProtectionLevel.EncryptAndSign;
				}

				Target ??= $"SMTPSVC/{Uri.Host}";

				// Build the options
				var clientOptions = new NegotiateAuthenticationClientOptions {
					RequiredProtectionLevel = protectionLevel,
					Credential = Credentials,
					Binding = GetChannelBindingIfSupported(),
					TargetName = Target,
				};

				negotiate = new NegotiateAuthentication (clientOptions);
			}

			var outgoing = negotiate.GetOutgoingBlob (incoming, out NegotiateAuthenticationStatusCode status);

			if (status == NegotiateAuthenticationStatusCode.Completed) {
				// Authentication is completed, but handshake might bot be, and do cause reentrance
				IsAuthenticated = true;
			} else if (status != NegotiateAuthenticationStatusCode.ContinueNeeded) {
				// Some error occurred
				// You can throw an exception or set IsAuthenticated=false
				throw new SaslException (MechanismName, SaslErrorCode.InvalidChallenge, $"Handshake failed with status={status}.");
			}

			return outgoing; // might be null if there's nothing to send
		}

		private ChannelBinding GetChannelBindingIfSupported()
		{
			return SupportsChannelBinding && AllowChannelBinding ?
				ChannelBindingContext?.GetChannelBinding(ChannelBindingKind.Endpoint) :
				null;
		}

		// Function for SASL security layer negotiation after
		// authorization completes.
		//
		// Returns null for failure, Base64 encoded string on
		// success.
		// Cloned from: https://github.com/dotnet/runtime/blob/4631ecec883a90ae9c29c058eea4527f9f2cb473/src/libraries/System.Net.Mail/src/System/Net/Mail/SmtpNegotiateAuthenticationModule.cs#L107
		private byte[] GetSecurityLayerNegotiationOutgoingBlob (ReadOnlySpan<byte> incoming)
		{
			if (negotiate == null)
				throw new InvalidOperationException ($"this.{nameof(negotiate)} shall be already initialized");

			// must have a security layer challenge
			if (incoming == null)
				return null;

			// "unwrap" challenge
			byte[] input = incoming.ToArray();

			Span<byte> unwrappedChallenge;
			NegotiateAuthenticationStatusCode statusCode;

			statusCode = negotiate.UnwrapInPlace (input, out int newOffset, out int newLength, out _);
			if (statusCode != NegotiateAuthenticationStatusCode.Completed) {
				return null;
			}
			unwrappedChallenge = input.AsSpan (newOffset, newLength);

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

			if (unwrappedChallenge.Length != 4 || (unwrappedChallenge[0] & 1) != 1) {
				return null;
			}

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
			ArrayBufferWriter<byte> outputWriter = new ArrayBufferWriter<byte> ();
			statusCode = negotiate.Wrap (SaslNoSecurityLayerToken, outputWriter, false, out _);
			if (statusCode != NegotiateAuthenticationStatusCode.Completed) {
				return null;
			}

			return outputWriter.WrittenSpan.ToArray();
		}
	}
}
