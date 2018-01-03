//
// SaslMechanismCramMd5.cs
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

#if NETFX_CORE || NETSTANDARD
using MD5 = MimeKit.Cryptography.MD5;
#else
using System.Security.Cryptography;
#endif

namespace MailKit.Security {
	/// <summary>
	/// The CRAM-MD5 SASL mechanism.
	/// </summary>
	/// <remarks>
	/// A SASL mechanism based on HMAC-MD5.
	/// </remarks>
	public class SaslMechanismCramMd5 : SaslMechanism
	{
		static readonly byte[] HexAlphabet = {
			0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, // '0' -> '7'
			0x38, 0x39, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, // '8' -> 'f'
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismCramMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new CRAM-MD5 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismCramMd5(NetworkCredential) instead.")]
		public SaslMechanismCramMd5 (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismCramMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new CRAM-MD5 SASL context.
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
		[Obsolete ("Use SaslMechanismCramMd5(string, string) instead.")]
		public SaslMechanismCramMd5 (Uri uri, string userName, string password) : base (uri, userName, password)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismCramMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new CRAM-MD5 SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismCramMd5 (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismCramMd5"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new CRAM-MD5 SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismCramMd5 (string userName, string password) : base (userName, password)
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
			get { return "CRAM-MD5"; }
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
		/// <exception cref="System.NotSupportedException">
		/// The SASL mechanism does not support SASL-IR.
		/// </exception>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		protected override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			if (IsAuthenticated)
				throw new InvalidOperationException ();

			if (token == null)
				throw new NotSupportedException ("CRAM-MD5 does not support SASL-IR.");

			var userName = Encoding.UTF8.GetBytes (Credentials.UserName);
			var password = Encoding.UTF8.GetBytes (Credentials.Password);
			var ipad = new byte[64];
			var opad = new byte[64];
			byte[] digest;

			if (password.Length > 64) {
				byte[] checksum;

				using (var md5 = MD5.Create ())
					checksum = md5.ComputeHash (password);

				Array.Copy (checksum, ipad, checksum.Length);
				Array.Copy (checksum, opad, checksum.Length);
			} else {
				Array.Copy (password, ipad, password.Length);
				Array.Copy (password, opad, password.Length);
			}

			for (int i = 0; i < 64; i++) {
				ipad[i] ^= 0x36;
				opad[i] ^= 0x5c;
			}

			using (var md5 = MD5.Create ()) {
				md5.TransformBlock (ipad, 0, ipad.Length, null, 0);
				md5.TransformFinalBlock (token, startIndex, length);
				digest = md5.Hash;
			}

			using (var md5 = MD5.Create ()) {
				md5.TransformBlock (opad, 0, opad.Length, null, 0);
				md5.TransformFinalBlock (digest, 0, digest.Length);
				digest = md5.Hash;
			}

			var buffer = new byte[userName.Length + 1 + (digest.Length * 2)];
			int offset = 0;

			for (int i = 0; i < userName.Length; i++)
				buffer[offset++] = userName[i];
			buffer[offset++] = 0x20;
			for (int i = 0; i < digest.Length; i++) {
				byte c = digest[i];

				buffer[offset++] = HexAlphabet[(c >> 4) & 0x0f];
				buffer[offset++] = HexAlphabet[c & 0x0f];
			}

			IsAuthenticated = true;

			return buffer;
		}
	}
}
