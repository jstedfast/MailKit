//
// SmtpAuthenticationSecretDetector.cs
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

namespace MailKit.Net.Smtp {
	class SmtpAuthenticationSecretDetector : IAuthenticationSecretDetector
	{
		static readonly IList<AuthenticationSecret> EmptyAuthSecrets;

		enum SmtpAuthCommandState
		{
			Auth,
			AuthMechanism,
			AuthNewLine,
			AuthToken,
			Error
		}

		SmtpAuthCommandState state;
		bool isAuthenticating;
		int commandIndex;

		public bool IsAuthenticating {
			get { return isAuthenticating; }
			set {
				state = SmtpAuthCommandState.Auth;
				isAuthenticating = value;
				commandIndex = 0;
			}
		}

		static SmtpAuthenticationSecretDetector ()
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
					state = SmtpAuthCommandState.Error;
					break;
				}

				commandIndex++;
				index++;
			}

			return commandIndex == command.Length;
		}

		public IList<AuthenticationSecret> DetectSecrets (byte[] buffer, int offset, int count)
		{
			if (!IsAuthenticating || state == SmtpAuthCommandState.Error || count == 0)
				return EmptyAuthSecrets;

			int endIndex = offset + count;
			int index = offset;

			if (state == SmtpAuthCommandState.Auth) {
				if (SkipCommand ("AUTH ", buffer, ref index, endIndex))
					state = SmtpAuthCommandState.AuthMechanism;

				if (index >= endIndex || state == SmtpAuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (state == SmtpAuthCommandState.AuthMechanism) {
				while (index < endIndex && buffer[index] != (byte) ' ' && buffer[index] != (byte) '\r')
					index++;

				if (index < endIndex) {
					if (buffer[index] == (byte) ' ') {
						state = SmtpAuthCommandState.AuthToken;
					} else {
						state = SmtpAuthCommandState.AuthNewLine;
					}

					index++;
				}

				if (index >= endIndex)
					return EmptyAuthSecrets;
			}

			if (state == SmtpAuthCommandState.AuthNewLine) {
				if (buffer[index] == (byte) '\n') {
					state = SmtpAuthCommandState.AuthToken;
					index++;
				} else {
					state = SmtpAuthCommandState.Error;
				}

				if (index >= endIndex || state == SmtpAuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			int startIndex = index;
			while (index < endIndex && buffer[index] != (byte) '\r')
				index++;

			if (index < endIndex)
				state = SmtpAuthCommandState.AuthNewLine;

			if (index == startIndex)
				return EmptyAuthSecrets;

			var secret = new AuthenticationSecret (startIndex, index - startIndex);

			if (state == SmtpAuthCommandState.AuthNewLine) {
				index++;

				if (index < endIndex) {
					if (buffer[index] == (byte) '\n') {
						state = SmtpAuthCommandState.AuthToken;
					} else {
						state = SmtpAuthCommandState.Error;
					}
				}
			}

			return new AuthenticationSecret[] { secret };
		}
	}
}
