//
// SaslMechanismScramBase.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
using System.Collections.Generic;

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#else
using System.Security.Cryptography;
#endif

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

		string client, server;
		byte[] salted, auth;
		LoginState state;
		string cnonce;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-based SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="entropy">Random characters to act as the cnonce token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		internal protected SaslMechanismScramBase (NetworkCredential credentials, string entropy) : base (credentials)
		{
			cnonce = entropy;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-based SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismScramBase(NetworkCredential) instead.")]
		protected SaslMechanismScramBase (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramBase"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-based SASL context.
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
		[Obsolete ("Use SaslMechanismScramBase(string, string) instead.")]
		protected SaslMechanismScramBase (Uri uri, string userName, string password) : base (uri, userName, password)
		{
		}

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

		static string Normalize (string str)
		{
			var builder = new StringBuilder ();
			var prepared = SaslPrep (str);

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
				throw new InvalidOperationException ();

			byte[] response, signature;

			switch (state) {
			case LoginState.Initial:
				if (string.IsNullOrEmpty (cnonce)) {
					var entropy = new byte[18];

					using (var rng = RandomNumberGenerator.Create ())
						rng.GetBytes (entropy);

					cnonce = Convert.ToBase64String (entropy);
				}

				client = "n=" + Normalize (Credentials.UserName) + ",r=" + cnonce;
				response = Encoding.UTF8.GetBytes ("n,," + client);
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

				if (!int.TryParse (iterations, out count) || count < 1)
					throw new SaslException (MechanismName, SaslErrorCode.InvalidChallenge, "Challenge contained an invalid iteration count.");

				var password = Encoding.UTF8.GetBytes (SaslPrep (Credentials.Password));
				salted = Hi (password, Convert.FromBase64String (salt), count);

				var withoutProof = "c=" + Convert.ToBase64String (Encoding.ASCII.GetBytes ("n,,")) + ",r=" + nonce;
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
						throw new SaslException (MechanismName, SaslErrorCode.IncorrectHash, "Challenge contained an invalid signatire.");
				}

				IsAuthenticated = true;
				response = new byte[0];
				break;
			default:
				throw new IndexOutOfRangeException ("state");
			}

			return response;
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
			client = null;
			server = null;
			salted = null;
			cnonce = null;
			auth = null;

			base.Reset ();
		}
	}
}
