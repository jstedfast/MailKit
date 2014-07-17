//
// SaslMechanismScramSha1.cs
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

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#else
using System.Security.Cryptography;
#endif

namespace MailKit.Security {
	public class SaslMechanismScramSha1 : SaslMechanismScramBase
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha1"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-1 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <param name="entropy">Random characters to act as the cnonce token.</param>
		internal SaslMechanismScramSha1 (Uri uri, ICredentials credentials, string entropy) : base (uri, credentials, entropy)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismScramSha1"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new SCRAM-SHA-1 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		public SaslMechanismScramSha1 (Uri uri, ICredentials credentials) : base (uri, credentials)
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
			get { return "SCRAM-SHA-1"; }
		}

		/// <summary>
		/// Apply the HMAC keyed algorithm.
		/// </summary>
		/// <remarks>
		/// HMAC(key, str): Apply the HMAC keyed hash algorithm (defined in
		/// [RFC2104]) using the octet string represented by "key" as the key
		/// and the octet string "str" as the input string. The size of the
		/// result is the hash result size for the hash function in use. For
		/// example, it is 20 octets for SHA-1 (see [RFC3174]).
		/// </remarks>
		/// <returns>The results of the HMAC keyed algorithm.</returns>
		/// <param name="key">The key.</param>
		/// <param name="str">The string.</param>
		protected override byte[] HMAC (byte[] key, byte[] str)
		{
			using (var hmac = new HMACSHA1 (key))
				return hmac.ComputeHash (str);
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
			using (var sha1 = new SHA1CryptoServiceProvider ())
				return sha1.ComputeHash (str);
		}
	}
}
