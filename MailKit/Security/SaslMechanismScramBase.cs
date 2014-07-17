//
// SaslMechanismScramBase.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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
using MD5 = MimeKit.Cryptography.MD5;
#else
using MD5 = System.Security.Cryptography.MD5CryptoServiceProvider;
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
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="entropy">Random characters to act as the cnonce token.</param>
		internal protected SaslMechanismScramBase (Uri uri, ICredentials credentials, string entropy) : base (uri, credentials)
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
		protected SaslMechanismScramBase (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Gets the hash size.
		/// </summary>
		/// <remarks>
		/// For SHA-1, the hash size would be 20.
		/// </remarks>
		/// <value>The hash size.</value>
		protected abstract int HashSize {
			get;
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
		protected abstract byte[] HMAC (byte[] key, byte[] str);

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
		/// <param name="str">String.</param>
		protected abstract byte[] Hash (byte[] str);

		static byte[] Xor (byte[] a, byte[] b)
		{
			var result = new byte[a.Length];

			for (int i = 0; i < a.Length; i++)
				result[i] = (byte) (a[i] ^ b[i]);

			return result;
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
			var salt1 = new byte[salt.Length + 4];
			byte[] hi, u1;

			Buffer.BlockCopy (salt, 0, salt1, 0, salt.Length);
			salt1[salt1.Length - 1] = (byte) 1;

			hi = u1 = HMAC (str, salt1);

			for (int i = 1; i <= count; i++) {
				var u2 = HMAC (str, u1);
				hi = Xor (hi, u2);
				u1 = u2;
			}

			return hi;
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
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			if (IsAuthenticated)
				throw new InvalidOperationException ();

			var cred = Credentials.GetCredential (Uri, MechanismName);
			byte[] response, signature;

			switch (state) {
			case LoginState.Initial:
				if (string.IsNullOrEmpty (cnonce)) {
					var entropy = new byte[15];

					using (var rng = RandomNumberGenerator.Create ())
						rng.GetBytes (entropy);

					cnonce = Convert.ToBase64String (entropy);
				}

				client = "n,,n=" + Normalize (cred.UserName) + ",r=" + cnonce;
				response = Encoding.UTF8.GetBytes (client);
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

				var password = Encoding.UTF8.GetBytes (SaslPrep (cred.Password));
				salted = Hi (password, Convert.FromBase64String (salt), count);

				var withoutProof = "c=" + Convert.ToBase64String (Encoding.ASCII.GetBytes ("n,,")) + ",r=" + nonce;
				auth = Encoding.UTF8.GetBytes (client + "," + server + "," + withoutProof);

				var key = HMAC (salted, Encoding.ASCII.GetBytes ("Client Key"));
				signature = HMAC (Hash (key), auth);
				var proof = Xor (key, signature);

				response = Encoding.UTF8.GetBytes (withoutProof + ",p=" + Convert.ToBase64String (proof));
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
