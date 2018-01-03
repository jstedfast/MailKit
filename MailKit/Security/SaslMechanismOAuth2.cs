//
// SaslMechanismOAuth2.cs
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

namespace MailKit.Security {
	/// <summary>
	/// The OAuth2 SASL mechanism.
	/// </summary>
	/// <remarks>
	/// A SASL mechanism used by Google that makes use of a short-lived
	/// OAuth 2.0 access token.
	/// </remarks>
	public class SaslMechanismOAuth2 : SaslMechanism
	{
		const string AuthBearer = "auth=Bearer ";
		const string UserEquals = "user=";

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuth2"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new XOAUTH2 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismOAuth2(NetworkCredential) instead.")]
		public SaslMechanismOAuth2 (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuth2"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new XOAUTH2 SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="userName">The user name.</param>
		/// <param name="auth_token">The auth token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="auth_token"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismOAuth2(string, string) instead.")]
		public SaslMechanismOAuth2 (Uri uri, string userName, string auth_token) : base (uri, userName, auth_token)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuth2"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new XOAUTH2 SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismOAuth2 (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuth2"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new XOAUTH2 SASL context.
		/// </remarks>
		/// <param name="userName">The user name.</param>
		/// <param name="auth_token">The auth token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="auth_token"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismOAuth2 (string userName, string auth_token) : base (userName, auth_token)
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
			get { return "XOAUTH2"; }
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

			var authToken = Credentials.Password;
			var userName = Credentials.UserName;
			int index = 0;

			var buf = new byte[UserEquals.Length + userName.Length + AuthBearer.Length + authToken.Length + 3];
			for (int i = 0; i < UserEquals.Length; i++)
				buf[index++] = (byte) UserEquals[i];
			for (int i = 0; i < userName.Length; i++)
				buf[index++] = (byte) userName[i];
			buf[index++] = 1;
			for (int i = 0; i < AuthBearer.Length; i++)
				buf[index++] = (byte) AuthBearer[i];
			for (int i = 0; i < authToken.Length; i++)
				buf[index++] = (byte) authToken[i];
			buf[index++] = 1;
			buf[index++] = 1;

			IsAuthenticated = true;

			return buf;
		}
	}
}
