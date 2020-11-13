//
// SaslMechanismOAuthBearer.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
using System.Globalization;

namespace MailKit.Security {
	/// <summary>
	/// The OAuth Bearer SASL mechanism.
	/// </summary>
	/// <remarks>
	/// A SASL mechanism that makes use of a short-lived OAuth Bearer access tokens.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\OAuth2GMailExample.cs"/>
	/// <code language="c#" source="Examples\OAuth2ExchangeExample.cs"/>
	/// </example>
	public class SaslMechanismOAuthBearer : SaslMechanism
	{
		static readonly byte[] ErrorResponse = new byte[1] { 0x01 };
		const string AuthBearer = "auth=Bearer ";
		const string HostEquals = "host=";
		const string PortEquals = "port=";

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuthBearer"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new OAUTHBEARER SASL context.
		/// </remarks>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		[Obsolete ("Use SaslMechanismOAuthBearer(NetworkCredential) instead.")]
		public SaslMechanismOAuthBearer (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuthBearer"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new OAUTHBEARER SASL context.
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
		[Obsolete ("Use SaslMechanismOAuthBearer(string, string) instead.")]
		public SaslMechanismOAuthBearer (Uri uri, string userName, string auth_token) : base (uri, userName, auth_token)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuthBearer"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new OAUTHBEARER SASL context.
		/// </remarks>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="credentials"/> is <c>null</c>.
		/// </exception>
		public SaslMechanismOAuthBearer (NetworkCredential credentials) : base (credentials)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismOAuthBearer"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new OAUTHBEARER SASL context.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\OAuth2GMailExample.cs"/>
		/// <code language="c#" source="Examples\OAuth2ExchangeExample.cs"/>
		/// </example>
		/// <param name="userName">The user name.</param>
		/// <param name="auth_token">The auth token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="userName"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="auth_token"/> is <c>null</c>.</para>
		/// </exception>
		public SaslMechanismOAuthBearer (string userName, string auth_token) : base (userName, auth_token)
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
			get { return "OAUTHBEARER"; }
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

		static int CalculateBufferSize (byte[] authzid, byte[] host, string port, string token)
		{
			int length = 0;

			length += 2; // channel binding ("n,")
			length += 2; // a=
			length += authzid.Length;
			length += 1; // ','

			length++; // ^A

			length += HostEquals.Length;
			length += host.Length;
			length++; // ^A

			length += PortEquals.Length;
			length += port.Length;
			length++; // ^A

			length += AuthBearer.Length;
			length += token.Length;
			length += 2; // ^A^A

			return length;
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
				return ErrorResponse;

			var authzid = Encoding.UTF8.GetBytes (Credentials.UserName);
			var port = Uri.Port.ToString (CultureInfo.InvariantCulture);
			var host = Encoding.UTF8.GetBytes (Uri.Host);
			var authToken = Credentials.Password;

			var buf = new byte[CalculateBufferSize (authzid, host, port, authToken)];
			int index = 0;

			buf[index++] = (byte) 'n'; // channel binding not supported
			buf[index++] = (byte) ',';
			buf[index++] = (byte) 'a';
			buf[index++] = (byte) '=';
			for (int i = 0; i < authzid.Length; i++)
				buf[index++] = authzid[i];
			buf[index++] = (byte) ',';
			buf[index++] = 0x01;

			for (int i = 0; i < HostEquals.Length; i++)
				buf[index++] = (byte) HostEquals[i];
			for (int i = 0; i < host.Length; i++)
				buf[index++] = host[i];
			buf[index++] = 0x01;

			for (int i = 0; i < PortEquals.Length; i++)
				buf[index++] = (byte) PortEquals[i];
			for (int i = 0; i < port.Length; i++)
				buf[index++] = (byte) port[i];
			buf[index++] = 0x01;

			for (int i = 0; i < AuthBearer.Length; i++)
				buf[index++] = (byte) AuthBearer[i];
			for (int i = 0; i < authToken.Length; i++)
				buf[index++] = (byte) authToken[i];
			buf[index++] = 0x01;
			buf[index++] = 0x01;

			IsAuthenticated = true;

			return buf;
		}
	}
}
