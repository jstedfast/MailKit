//
// SaslMechanism.cs
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
	/// A SASL authentication mechanism.
	/// </summary>
	/// <remarks>
	/// Authenticating via a SASL mechanism may be a multi-step process.
	/// To determine if the mechanism has completed the necessary steps
	/// to authentication, check the <see cref="IsAuthenticated"/> after
	/// each call to <see cref="Challenge(string)"/>.
	/// </remarks>
	public abstract class SaslMechanism
	{
		/// <summary>
		/// The supported authentication mechanisms in order of strongest to weakest.
		/// </summary>
		/// <remarks>
		/// Use by the various clients when authenticating via SASL to determine
		/// which order the SASL mechanisms supported by the server should be tried.
		/// </remarks>
		public static readonly string[] AuthMechanismRank = {
#if !NETFX_CORE
			"NTLM",
#endif
			"DIGEST-MD5", "CRAM-MD5", "XOAUTH2", "PLAIN", "LOGIN"
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanism"/> class.
		/// </summary>
		/// <param name="uri">The URI of the service.</param>
		/// <param name="credentials">The user's credentials.</param>
		protected SaslMechanism (Uri uri, ICredentials credentials)
		{
			Credentials = credentials;
			Uri = uri;
		}

		/// <summary>
		/// Gets the name of the mechanism.
		/// </summary>
		/// <value>The name of the mechanism.</value>
		public abstract string MechanismName {
			get;
		}

		/// <summary>
		/// Gets the user's credentials.
		/// </summary>
		/// <value>The user's credentials.</value>
		public ICredentials Credentials {
			get; private set;
		}

		/// <summary>
		/// Gets or sets whether the SASL mechanism has finished authenticating.
		/// </summary>
		/// <value><c>true</c> if the SASL mechanism has finished authenticating; otherwise, <c>false</c>.</value>
		public bool IsAuthenticated {
			get; protected set;
		}

		/// <summary>
		/// Gets or sets the URI of the service.
		/// </summary>
		/// <value>The URI of the service.</value>
		public Uri Uri {
			get; protected set;
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
		protected abstract byte[] Challenge (byte[] token, int startIndex, int length);

		/// <summary>
		/// Decodes the base64-encoded server challenge and returns the next challenge response encoded in base64.
		/// </summary>
		/// <returns>The next base64-encoded challenge response.</returns>
		/// <param name="token">The server's base64-encoded challenge token.</param>
		/// <exception cref="SaslException">
		/// An error has occurred while parsing the server's challenge token.
		/// </exception>
		public string Challenge (string token)
		{
			byte[] decoded;
			int length;

			if (token != null) {
				decoded = Convert.FromBase64String (token);
				length = decoded.Length;
			} else {
				decoded = null;
				length = 0;
			}

			var challenge = Challenge (decoded, 0, length);

			if (challenge == null)
				return null;

			return Convert.ToBase64String (challenge);
		}

		/// <summary>
		/// Resets the state of the SASL mechanism.
		/// </summary>
		public virtual void Reset ()
		{
			IsAuthenticated = false;
		}

		/// <summary>
		/// Determines if the specified SASL mechanism is supported by MailKit.
		/// </summary>
		/// <remarks>
		/// Use this method to make sure that a SASL mechanism is supported before calling
		/// <see cref="Create"/>.
		/// </remarks>
		/// <returns><c>true</c> if the specified SASL mechanism is supported; otherwise, <c>false</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="mechanism"/> is <c>null</c>.
		/// </exception>
		public static bool IsSupported (string mechanism)
		{
			if (mechanism == null)
				throw new ArgumentNullException ("mechanism");

			switch (mechanism) {
			case "DIGEST-MD5":  return true;
			case "CRAM-MD5":    return true;
			case "XOAUTH2":     return true;
			case "PLAIN":       return true;
			case "LOGIN":       return true;
#if !NETFX_CORE
			case "NTLM":        return true;
#endif
			default:            return false;
			}
		}

		/// <summary>
		/// Create an instance of the specified SASL mechanism using the uri and credentials.
		/// </summary>
		/// <remarks>
		/// If unsure that a particular SASL mechanism is supported, you should first call
		/// <see cref="IsSupported"/>.
		/// </remarks>
		/// <returns>An instance of the requested SASL mechanism if supported; otherwise <c>null</c>.</returns>
		/// <param name="mechanism">The name of the SASL mechanism.</param>
		/// <param name="uri">The URI of the service to authenticate against.</param>
		/// <param name="credentials">The user's credentials.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="mechanism"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="uri"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="credentials"/> is <c>null</c>.</para>
		/// </exception>
		public static SaslMechanism Create (string mechanism, Uri uri, ICredentials credentials)
		{
			if (mechanism == null)
				throw new ArgumentNullException ("mechanism");

			if (uri == null)
				throw new ArgumentNullException ("uri");

			if (credentials == null)
				throw new ArgumentNullException ("credentials");

			switch (mechanism) {
			//case "KERBEROS_V4": return null;
			case "DIGEST-MD5":  return new SaslMechanismDigestMd5 (uri, credentials);
			case "CRAM-MD5":    return new SaslMechanismCramMd5 (uri, credentials);
			//case "GSSAPI":      return null;
			case "XOAUTH2":     return new SaslMechanismOAuth2 (uri, credentials);
			case "PLAIN":       return new SaslMechanismPlain (uri, credentials);
			case "LOGIN":       return new SaslMechanismLogin (uri, credentials);
#if !NETFX_CORE
			case "NTLM":        return new SaslMechanismNtlm (uri, credentials);
#endif
			default:            return null;
			}
		}
	}
}
