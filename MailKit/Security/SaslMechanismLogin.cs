//
// SaslMechanismLogin.cs
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

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Security {
	/// <summary>
	/// The LOGIN SASL mechanism.
	/// </summary>
	/// <remarks>
	/// The LOGIN SASL mechanism provides little protection over the use
	/// of plain-text passwords by obscuring the user name and password within
	/// individual base64-encoded blobs and should be avoided unless used in
	/// combination with an SSL or TLS connection.
	/// </remarks>
	public class SaslMechanismLogin : SaslMechanism
	{
		enum LoginState {
			UserName,
			Password
		}

		readonly Encoding encoding;
		LoginState state;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismLogin(Encoding, NetworkCredential) instead.")]
		public SaslMechanismLogin (Uri uri, Encoding encoding, ICredentials credentials) : base (uri, credentials)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			this.encoding = encoding;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismLogin(Encoding, string, string) instead.")]
		public SaslMechanismLogin (Uri uri, Encoding encoding, string userName, string password) : base (uri, userName, password)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			this.encoding = encoding;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismLogin(NetworkCredential) instead.")]
		public SaslMechanismLogin (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
			encoding = Encoding.UTF8;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
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
		[Obsolete ("Use SaslMechanismLogin(string, string) instead.")]
		public SaslMechanismLogin (Uri uri, string userName, string password) : base (uri, userName, password)
		{
			encoding = Encoding.UTF8;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismLogin (Encoding encoding, NetworkCredential credentials) : base (credentials)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			this.encoding = encoding;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="encoding">The encoding to use for the user's credentials.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="encoding"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismLogin (Encoding encoding, string userName, string password) : base (userName, password)
		{
			if (encoding == null)
				throw new ArgumentNullException (nameof (encoding));

			this.encoding = encoding;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismLogin (NetworkCredential credentials) : base (credentials)
		{
			encoding = Encoding.UTF8;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismLogin"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new LOGIN SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="password">The password.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="password"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismLogin (string userName, string password) : base (userName, password)
		{
			encoding = Encoding.UTF8;
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <remarks>
		/// Gets the name of the mechanism.
		/// </remarks>
		/// <value>The name of the mechanism.</value>
		public override string MechanismName {
			get { return "LOGIN"; }
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
			get { return false; }
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
			byte[] challenge;

			if (token == null)
				throw new NotSupportedException ("LOGIN does not support SASL-IR.");

			switch (state) {
			case LoginState.UserName:
				challenge = encoding.GetBytes (Credentials.UserName);
				state = LoginState.Password;
				break;
			case LoginState.Password:
				challenge = encoding.GetBytes (Credentials.Password);
				IsAuthenticated = true;
				break;
			default:
				throw new InvalidOperationException ();
			}

			return challenge;
		}

		/// <summary>
		/// Resets the state of the SASL mechanism.
		/// </summary>
		/// <remarks>
		/// Resets the state of the SASL mechanism.
		/// </remarks>
		public override void Reset ()
		{
			state = LoginState.UserName;
			base.Reset ();
		}
	}
}
