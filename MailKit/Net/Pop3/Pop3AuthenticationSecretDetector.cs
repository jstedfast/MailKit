//
// Pop3AuthenticationSecretDetector.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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
using System.Collections.Generic;

namespace MailKit.Net.Pop3 {
	class Pop3AuthenticationSecretDetector : IAuthenticationSecretDetector
	{
		static readonly IList<AuthenticationSecret> EmptyAuthSecrets;

		enum Pop3AuthCommandState
		{
			None,
			A,
			Apop,
			ApopUserName,
			ApopToken,
			ApopNewLine,
			Auth,
			AuthMechanism,
			AuthNewLine,
			AuthToken,
			User,
			UserName,
			UserNewLine,
			Pass,
			Password,
			PassNewLine,
			Error
		}

		Pop3AuthCommandState state;
		bool isAuthenticating;
		int commandIndex;

		public bool IsAuthenticating {
			get { return isAuthenticating; }
			set {
				state = Pop3AuthCommandState.None;
				isAuthenticating = value;
				commandIndex = 0;
			}
		}

		static Pop3AuthenticationSecretDetector ()
		{
#if NET45
			EmptyAuthSecrets = new AuthenticationSecret[0];
#else
			EmptyAuthSecrets = Array.Empty<AuthenticationSecret> ();
#endif
		}

		bool SkipCommand (string command, byte[] buffer, ref int index, int endIndex)
		{
			while (index < endIndex && commandIndex < command.Length) {
				if (buffer[index] != (byte) command[commandIndex]) {
					state = Pop3AuthCommandState.Error;
					break;
				}

				commandIndex++;
				index++;
			}

			return commandIndex == command.Length;
		}

		IList<AuthenticationSecret> DetectApopSecrets (byte[] buffer, int offset, int endIndex)
		{
			var secrets = new List<AuthenticationSecret> ();
			int index = offset;
			int startIndex;

			if (state == Pop3AuthCommandState.ApopNewLine)
				return EmptyAuthSecrets;

			if (state == Pop3AuthCommandState.Apop) {
				if (SkipCommand ("APOP ", buffer, ref index, endIndex))
					state = Pop3AuthCommandState.ApopUserName;

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state == Pop3AuthCommandState.ApopUserName) {
				startIndex = index;

				while (index < endIndex && buffer[index] != (byte) ' ')
					index++;

				if (index > startIndex)
					secrets.Add (new AuthenticationSecret (startIndex, index - startIndex));

				if (index < endIndex) {
					state = Pop3AuthCommandState.ApopToken;
					index++;
				}

				if (index >= endIndex)
					return secrets;
			}

			startIndex = index;

			while (index < endIndex && buffer[index] != (byte) '\r')
				index++;

			if (index < endIndex)
				state = Pop3AuthCommandState.ApopNewLine;

			if (index > startIndex)
				secrets.Add (new AuthenticationSecret (startIndex, index - startIndex));

			return secrets;
		}

		IList<AuthenticationSecret> DetectAuthSecrets (byte[] buffer, int offset, int endIndex)
		{
			int index = offset;

			if (state == Pop3AuthCommandState.Auth) {
				if (SkipCommand ("AUTH ", buffer, ref index, endIndex))
					state = Pop3AuthCommandState.AuthMechanism;

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state == Pop3AuthCommandState.AuthMechanism) {
				while (index < endIndex && buffer[index] != (byte) ' ' && buffer[index] != (byte) '\r')
					index++;

				if (index < endIndex) {
					if (buffer[index] == (byte) ' ') {
						state = Pop3AuthCommandState.AuthToken;
					} else {
						state = Pop3AuthCommandState.AuthNewLine;
					}

					index++;
				}

				if (index >= endIndex)
					return EmptyAuthSecrets;
			}

			if (state == Pop3AuthCommandState.AuthNewLine) {
				if (buffer[index] == (byte) '\n') {
					state = Pop3AuthCommandState.AuthToken;
					index++;
				} else {
					state = Pop3AuthCommandState.Error;
				}

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			int startIndex = index;
			while (index < endIndex && buffer[index] != (byte) '\r')
				index++;

			if (index < endIndex)
				state = Pop3AuthCommandState.AuthNewLine;

			if (index == startIndex)
				return EmptyAuthSecrets;

			var secret = new AuthenticationSecret (startIndex, index - startIndex);

			if (state == Pop3AuthCommandState.AuthNewLine) {
				index++;

				if (index < endIndex) {
					if (buffer[index] == (byte) '\n') {
						state = Pop3AuthCommandState.AuthToken;
					} else {
						state = Pop3AuthCommandState.Error;
					}
				}
			}

			return new AuthenticationSecret[] { secret };
		}

		IList<AuthenticationSecret> DetectUserPassSecrets (byte[] buffer, int offset, int endIndex)
		{
			var secrets = new List<AuthenticationSecret> ();
			int index = offset;

			if (state == Pop3AuthCommandState.User) {
				if (SkipCommand ("USER ", buffer, ref index, endIndex))
					state = Pop3AuthCommandState.UserName;

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state == Pop3AuthCommandState.UserName) {
				int startIndex = index;

				while (index < endIndex && buffer[index] != (byte) '\r')
					index++;

				if (index > startIndex)
					secrets.Add (new AuthenticationSecret (startIndex, index - startIndex));

				if (index < endIndex) {
					state = Pop3AuthCommandState.UserNewLine;
					index++;
				}

				if (index >= endIndex)
					return secrets;
			}

			if (state == Pop3AuthCommandState.UserNewLine) {
				if (buffer[index] == (byte) '\n') {
					state = Pop3AuthCommandState.Pass;
					commandIndex = 0;
					index++;
				} else {
					state = Pop3AuthCommandState.Error;
				}

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return secrets;
			}

			if (state == Pop3AuthCommandState.Pass) {
				if (SkipCommand ("PASS ", buffer, ref index, endIndex))
					state = Pop3AuthCommandState.Password;

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state == Pop3AuthCommandState.Password) {
				int startIndex = index;

				while (index < endIndex && buffer[index] != (byte) '\r')
					index++;

				if (index > startIndex)
					secrets.Add (new AuthenticationSecret (startIndex, index - startIndex));

				if (index < endIndex) {
					state = Pop3AuthCommandState.PassNewLine;
					index++;
				}

				if (index >= endIndex)
					return secrets;
			}

			if (state == Pop3AuthCommandState.PassNewLine) {
				if (buffer[index] == (byte) '\n') {
					state = Pop3AuthCommandState.None;
					commandIndex = 0;
					index++;
				} else {
					state = Pop3AuthCommandState.Error;
				}
			}

			return secrets;
		}

		public IList<AuthenticationSecret> DetectSecrets (byte[] buffer, int offset, int count)
		{
			if (!IsAuthenticating || state == Pop3AuthCommandState.Error || count == 0)
				return EmptyAuthSecrets;

			int endIndex = offset + count;
			int index = offset;

			if (state == Pop3AuthCommandState.None) {
				switch ((char) buffer[index]) {
				case 'A':
					state = Pop3AuthCommandState.A;
					index++;
					break;
				case 'U':
					state = Pop3AuthCommandState.User;
					commandIndex = 1;
					index++;
					break;
				default:
					state = Pop3AuthCommandState.Error;
					break;
				}

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state == Pop3AuthCommandState.A) {
				switch ((char) buffer[index]) {
				case 'P':
					state = Pop3AuthCommandState.Apop;
					commandIndex = 2;
					index++;
					break;
				case 'U':
					state = Pop3AuthCommandState.Auth;
					commandIndex = 2;
					index++;
					break;
				default:
					state = Pop3AuthCommandState.Error;
					break;
				}

				if (index >= endIndex || state == Pop3AuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state >= Pop3AuthCommandState.Apop && state <= Pop3AuthCommandState.ApopNewLine)
				return DetectApopSecrets (buffer, index, endIndex);

			if (state >= Pop3AuthCommandState.Auth && state <= Pop3AuthCommandState.AuthToken)
				return DetectAuthSecrets (buffer, index, endIndex);

			return DetectUserPassSecrets (buffer, index, endIndex);
		}
	}
}
