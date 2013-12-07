//
// SaslMechanism.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
	public abstract class SaslMechanism
	{
		protected SaslMechanism (Uri uri, ICredentials credentials)
		{
			Credentials = credentials;
			Uri = uri;
		}

		public abstract string MechanismName {
			get;
		}

		public ICredentials Credentials {
			get; private set;
		}

		public bool IsAuthenticated {
			get; protected set;
		}

		public Uri Uri {
			get; protected set;
		}

		public abstract byte[] Challenge (byte[] token, int startIndex, int length);

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

		public virtual void Reset ()
		{
			IsAuthenticated = false;
		}

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
			default:            return false;
			}
		}

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
			//case "NTLM":        return null;
			default:            return null;
			}
		}
	}
}
