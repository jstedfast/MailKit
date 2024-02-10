//
// SaslMechanismScramBase.cs
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

using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Authentication.ExtendedProtection;

namespace MailKit.Security {
	/// <summary>
	/// The base class for SCRAM-based SASL mechanisms.
	/// </summary>
	/// <remarks>
	/// SCRAM-based SASL mechanisms are salted challenge/response authentication mechanisms.
	/// </remarks>
	public abstract class SaslMechanismScramBase : SaslMechanism
	{
		enum LoginState {
			Initial,
			Final,
			Validate
		}

		ChannelBindingKind channelBindingKind;
		bool negotiatedChannelBinding;
		byte[] channelBindingToken;
		internal string cnonce;
		string client, server;
		byte[] salted, auth;
		LoginState state;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-based SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		protected SaslMechanismScramBase (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-based SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		protected SaslMechanismScramBase (string userName, string password) : base (userName, password)
		{
		}

		/// <summary>
		/// Get or set the authorization identifier.
		/// </summary>
		/// <remarks>
		/// The authorization identifier is the desired user account that the server should use
		/// for all accesses. This is separate from the user name used for authentication.
		/// </remarks>
		/// <value>The authorization identifier.</value>
		public string AuthorizationId {
			get; set;
		}

		/// <summary>
		/// Get whether or not the mechanism supports an initial response (SASL-IR).
		/// </summary>
		/// <remarks>
		/// <para>Get whether or not the mechanism supports an initial response (SASL-IR).</para>
		/// <para>SASL mechanisms that support sending an initial client response to the server
		/// should return <value>true</value>.</para>
		/// </remarks>
		/// <value><c>true</c> if the mechanism supports an initial response; otherwise, <c>false</c>.</value>
		public override bool SupportsInitialResponse {
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

		static string Normalize (string str)
		{
			var prepared = SaslPrep (str);

			if (prepared.Length == 0)
				return prepared;

			var builder = new StringBuilder ();

			for (int i = 0; i < prepared.Length; i++) {
				switch (prepared[i]) {
				case ',': builder.Append ("=2C"); break;
				case '=': builder.Append ("=3D"); break;
				default:
					builder.Append (prepared[i]);
					break;
				}
			}

			return builder.ToString ();
		}

		/// <summary>
		/// Create the HMAC context.
		/// </summary>
		/// <remarks>
		/// Creates the HMAC context using the secret key.
		/// </remarks>
		/// <returns>The HMAC context.</returns>
		/// <param name="key">The secret key.</param>
		protected abstract KeyedHashAlgorithm CreateHMAC (byte[] key);

		/// <summary>
		/// Apply the HMAC keyed algorithm.
		/// </summary>
		/// <remarks>
		/// HMAC(key, str): Apply the HMAC keyed hash algorithm (defined in
		/// [RFC2104]) using the octet string represented by "key" as the key
		/// and the octet string "str" as the input string.  The size of the
		/// result is the hash result size for the hash function in use.  For
		/// example, it is 20 octets for SHA-1 (see [RFC3174]).
		/// </remarks>
		/// <returns>The results of the HMAC keyed algorithm.</returns>
		/// <param name="key">The key.</param>
		/// <param name="str">The string.</param>
		byte[] HMAC (byte[] key, byte[] str)
		{
			using (var hmac = CreateHMAC (key))
				return hmac.ComputeHash (str);
		}

		/// <summary>
		/// Apply the cryptographic hash function.
		/// </summary>
		/// <remarks>
		/// H(str): Apply the cryptographic hash function to the octet string
		/// "str", producing an octet string as a result.  The size of the
		/// result depends on the hash result size for the hash function in
		/// use.
		/// </remarks>
		/// <returns>The results of the hash.</returns>
		/// <param name="str">The string.</param>
		protected abstract byte[] Hash (byte[] str);

		/// <summary>
		/// Apply the exclusive-or operation to combine two octet strings.
		/// </summary>
		/// <remarks>
		/// Apply the exclusive-or operation to combine the octet string
		/// on the left of this operator with the octet string on the right of
		/// this operator.  The length of the output and each of the two
		/// inputs will be the same for this use.
		/// </remarks>
		/// <param name="a">The alpha component.</param>
		/// <param name="b">The blue component.</param>
		static void Xor (byte[] a, byte[] b)
		{
			for (int i = 0; i < a.Length; i++)
				a[i] = (byte) (a[i] ^ b[i]);
		}

		// Hi(str, salt, i):
		//
		// U1   := HMAC(str, salt + INT(1))
		// U2   := HMAC(str, U1)
		// ...
		// Ui-1 := HMAC(str, Ui-2)
		// Ui   := HMAC(str, Ui-1)
		//
		// Hi := U1 XOR U2 XOR ... XOR Ui
		//
		// where "i" is the iteration count, "+" is the string concatenation
		// operator, and INT(g) is a 4-octet encoding of the integer g, most
		// significant octet first.
		//
		// Hi() is, essentially, PBKDF2 [RFC2898] with HMAC() as the
		// pseudorandom function (PRF) and with dkLen == output length of
		// HMAC() == output length of H().
		byte[] Hi (byte[] str, byte[] salt, int count)
		{
			using (var hmac = CreateHMAC (str)) {
				var salt1 = new byte[salt.Length + 4];
				byte[] hi, u1;

				Buffer.BlockCopy (salt, 0, salt1, 0, salt.Length);
				salt1[salt1.Length - 1] = (byte) 1;

				hi = u1 = hmac.ComputeHash (salt1);

				for (int i = 1; i < count; i++) {
					var u2 = hmac.ComputeHash (u1);
					Xor (hi, u2);
					u1 = u2;
				}

				return hi;
			}
		}

		static Dictionary<char, string> ParseServerChallenge (string challenge)
		{
			var results = new Dictionary<char, string> ();

			foreach (var pair in challenge.Split (',')) {
				if (pair.Length < 2 || pair[1] != '=')
					continue;

				results.Add (pair[0], pair.Substring (2));
			}

			return results;
		}

		static string GetChannelBindingName (ChannelBindingKind kind)
		{
			return kind == ChannelBindingKind.Endpoint ? "tls-server-end-point" : "tls-unique";
		}

		static string GetChannelBindingInput (ChannelBindingKind kind, string authzid)
		{
			string flag;

			if (kind != ChannelBindingKind.Unknown) {
				flag = "p=" + GetChannelBindingName (kind);
			} else {
				flag = "n";
			}

			if (string.IsNullOrEmpty (authzid))
				authzid = string.Empty;

			return flag + "," + Normalize (authzid) + ",";
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

			byte[] response, signature;
			string input;

			switch (state) {
			case LoginState.Initial:
				cnonce ??= GenerateEntropy (18);
				client = "n=" + Normalize (Credentials.UserName) + ",r=" + cnonce;

				// Note: RFC7677 states:
				//
				// After publication of [RFC5802], it was discovered that Transport
				// Layer Security (TLS) [RFC5246] does not have the expected properties
				// for the "tls-unique" channel binding to be secure[RFC7627].
				//
				// Based on this, we attempt to use "tls-server-end-point" instead of "tls-unique" when available.
				if (SupportsChannelBinding) {
					if (TryGetChannelBindingToken (ChannelBindingKind.Endpoint, out channelBindingToken)) {
						channelBindingKind = ChannelBindingKind.Endpoint;
					} else if (TryGetChannelBindingToken (ChannelBindingKind.Unique, out channelBindingToken)) {
						channelBindingKind = ChannelBindingKind.Unique;
					} else {
						channelBindingKind = ChannelBindingKind.Unknown;
					}
				}

				input = GetChannelBindingInput (channelBindingKind, AuthorizationId);
				response = Encoding.UTF8.GetBytes (input + client);
				state = LoginState.Final;
				break;
			case LoginState.Final:
				server = Encoding.UTF8.GetString (token, startIndex, length);
				var tokens = ParseServerChallenge (server);
				string salt, nonce, iterations;
				int count;

				if (!tokens.TryGetValue ('s', out salt))
					throw new SaslException (MechanismName, SaslErrorCode.IncompleteChallenge, "Challenge did not contain a salt.");

				if (!tokens.TryGetValue ('r', out nonce))
					throw new SaslException (MechanismName, SaslErrorCode.IncompleteChallenge, "Challenge did not contain a nonce.");

				if (!tokens.TryGetValue ('i', out iterations))
					throw new SaslException (MechanismName, SaslErrorCode.IncompleteChallenge, "Challenge did not contain an iteration count.");

				if (!nonce.StartsWith (cnonce, StringComparison.Ordinal))
					throw new SaslException (MechanismName, SaslErrorCode.InvalidChallenge, "Challenge contained an invalid nonce.");

				if (!int.TryParse (iterations, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1)
					throw new SaslException (MechanismName, SaslErrorCode.InvalidChallenge, "Challenge contained an invalid iteration count.");

				var password = Encoding.UTF8.GetBytes (SaslPrep (Credentials.Password));
				salted = Hi (password, Convert.FromBase64String (salt), count);
				Array.Clear (password, 0, password.Length);

				input = GetChannelBindingInput (channelBindingKind, AuthorizationId);
				var inputBuffer = Encoding.ASCII.GetBytes (input);
				string base64;

				if (SupportsChannelBinding && channelBindingKind != ChannelBindingKind.Unknown) {
					var binding = new byte[inputBuffer.Length + channelBindingToken.Length];

					Buffer.BlockCopy (inputBuffer, 0, binding, 0, inputBuffer.Length);
					Buffer.BlockCopy (channelBindingToken, 0, binding, inputBuffer.Length, channelBindingToken.Length);

					// Zero the channel binding token. We don't need it anymore.
					Array.Clear (channelBindingToken, 0, channelBindingToken.Length);
					channelBindingToken = null;

					base64 = Convert.ToBase64String (binding);
				} else {
					base64 = Convert.ToBase64String (inputBuffer);
				}

				var withoutProof = "c=" + base64 + ",r=" + nonce;

				auth = Encoding.UTF8.GetBytes (client + "," + server + "," + withoutProof);

				var key = HMAC (salted, Encoding.ASCII.GetBytes ("Client Key"));
				signature = HMAC (Hash (key), auth);
				Xor (key, signature);

				response = Encoding.UTF8.GetBytes (withoutProof + ",p=" + Convert.ToBase64String (key));
				state = LoginState.Validate;
				break;
			case LoginState.Validate:
				var challenge = Encoding.UTF8.GetString (token, startIndex, length);

				if (!challenge.StartsWith ("v=", StringComparison.Ordinal))
					throw new SaslException (MechanismName, SaslErrorCode.InvalidChallenge, "Challenge did not start with a signature.");

				signature = Convert.FromBase64String (challenge.Substring (2));
				var serverKey = HMAC (salted, Encoding.ASCII.GetBytes ("Server Key"));
				var calculated = HMAC (serverKey, auth);

				if (signature.Length != calculated.Length)
					throw new SaslException (MechanismName, SaslErrorCode.IncorrectHash, "Challenge contained a signature with an invalid length.");

				for (int i = 0; i < signature.Length; i++) {
					if (signature[i] != calculated[i])
						throw new SaslException (MechanismName, SaslErrorCode.IncorrectHash, $"Challenge contained an invalid signature. Expected: {Convert.ToBase64String (calculated)}");
				}

				negotiatedChannelBinding = channelBindingKind != ChannelBindingKind.Unknown;
				IsAuthenticated = true;
				response = Array.Empty<byte> ();
				break;
			default:
				throw new IndexOutOfRangeException ("state");
			}

			return response;
		}

		/// <summary>
		/// Reset the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public override void Reset ()
		{
			if (channelBindingToken != null) {
				Array.Clear (channelBindingToken, 0, channelBindingToken.Length);
				channelBindingToken = null;
			}

			channelBindingKind = ChannelBindingKind.Unknown;
			negotiatedChannelBinding = false;
			state = LoginState.Initial;
			client = null;
			server = null;
			salted = null;
			cnonce = null;
			auth = null;

			base.Reset ();
		}
	}
}
