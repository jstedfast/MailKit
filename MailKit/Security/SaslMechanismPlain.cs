//
// SaslMechanismPlain.cs
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
using System.Text;

namespace MailKit.Security {
	public class SaslMechanismPlain : SaslMechanism
	{
		public SaslMechanismPlain (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		public override string MechanismName {
			get { return "PLAIN"; }
		}

		public override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			if (IsAuthenticated)
				throw new InvalidOperationException ();

			var cred = Credentials.GetCredential (Uri, MechanismName);
			var userName = Encoding.UTF8.GetBytes (cred.UserName);
			var password = Encoding.UTF8.GetBytes (cred.Password);
			var buffer = new byte[userName.Length + password.Length + 2];
			int offset = 0;

			buffer[offset++] = 0;
			for (int i = 0; i < userName.Length; i++)
				buffer[offset++] = userName[i];

			buffer[offset++] = 0;
			for (int i = 0; i < password.Length; i++)
				buffer[offset++] = password[i];

			IsAuthenticated = true;

			return buffer;
		}
	}
}
