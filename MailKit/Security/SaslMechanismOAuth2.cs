//
// SaslMechanismOAuth2.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		public SaslMechanismOAuth2 (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <value>The name of the mechanism.</value>
		public override string MechanismName {
			get { return "XOAUTH2"; }
		}

		/// <summary>
		/// Parses the server's challenge token and returns the next challenge response.
		/// </summary>
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
			var authToken = cred.Password;
			var userName = cred.UserName;
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
