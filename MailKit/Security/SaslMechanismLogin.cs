//
// SaslMechanismLogin.cs
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
	public class SaslMechanismLogin : SaslMechanism
	{
		enum LoginState {
			UserName,
			Password
		}

		LoginState state;

		public SaslMechanismLogin (Uri uri, ICredentials credentials) : base (uri, credentials)
		{
		}

		public override string MechanismName {
			get { return "LOGIN"; }
		}

		public override byte[] Challenge (byte[] token, int startIndex, int length)
		{
			var cred = Credentials.GetCredential (Uri, MechanismName);
			byte[] challenge;

			switch (state) {
			case LoginState.UserName:
				challenge = Encoding.UTF8.GetBytes (cred.UserName);
				state = LoginState.Password;
				break;
			case LoginState.Password:
				challenge = Encoding.UTF8.GetBytes (cred.Password);
				IsAuthenticated = true;
				break;
			default:
				throw new InvalidOperationException ();
			}

			return challenge;
		}

		public override void Reset ()
		{
			state = LoginState.UserName;
			base.Reset ();
		}
	}
}
