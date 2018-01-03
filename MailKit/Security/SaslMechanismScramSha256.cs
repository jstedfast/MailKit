//
// SaslMechanismScramSha256.cs
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

#if __MOBILE__
using SHA256CryptoServiceProvider = System.Security.Cryptography.SHA256Managed;
#endif

#if NETFX_CORE
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
#else
using System.Security.Cryptography;
#endif

namespace MailKit.Security {
	/// <summary>
	/// The SCRAM-SHA-1 SASL mechanism.
	/// </summary>
	/// <remarks>
	/// A salted challenge/response SASL mechanism that uses the HMAC SHA-256 algorithm.
	/// </remarks>
	public class SaslMechanismScramSha256 : SaslMechanismScramBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha256"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-256 SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="entropy">Random characters to act as the cnonce token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		internal SaslMechanismScramSha256 (NetworkCredential credentials, string entropy) : base (credentials, entropy)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha256"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-256 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismScramSha256(NetworkCredential) instead.")]
		public SaslMechanismScramSha256 (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha256"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-256 SASL context.
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
		[Obsolete ("Use SaslMechanismScramSha256(string, string) instead.")]
		public SaslMechanismScramSha256 (Uri uri, string userName, string password) : base (uri, userName, password)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha256"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-256 SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismScramSha256 (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha256"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-256 SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismScramSha256 (string userName, string password) : base (userName, password)
		{
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the mechanism.
		/// </remarks>
		/// <value>The name of the mechanism.</value>
		public override string MechanismName {
			get { return "SCRAM-SHA-256"; }
		}

		/// <summary>
		/// Create the HMAC context.
		/// </summary>
		/// <remarks>
		/// Creates the HMAC context using the secret key.
		/// </remarks>
		/// <returns>The HMAC context.</returns>
		/// <param name="key">The secret key.</param>
		protected override KeyedHashAlgorithm CreateHMAC (byte[] key)
		{
			return new HMACSHA256 (key);
		}

		/// <summary>
		/// Apply the cryptographic hash function.
		/// </summary>
		/// <remarks>
		/// H(str): Apply the cryptographic hash function to the octet string
		/// "str", producing an octet string as a result. The size of the
		/// result depends on the hash result size for the hash function in
		/// use.
		/// </remarks>
		/// <returns>The results of the hash.</returns>
		/// <param name="str">The string.</param>
		protected override byte[] Hash (byte[] str)
		{
#if NETFX_CORE
			var sha256 = HashAlgorithmProvider.OpenAlgorithm (HashAlgorithmNames.Sha256);
			var buf = sha256.HashData (CryptographicBuffer.CreateFromByteArray (str));
			byte[] hash;

			CryptographicBuffer.CopyToByteArray (buf, out hash);

			return hash;
#else
			using (var sha256 = SHA256.Create ())
				return sha256.ComputeHash (str);
#endif
		}
	}
}
