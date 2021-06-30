//
// ImapAuthenticationSecretDetector.cs
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

namespace MailKit.Net.Imap {
	class ImapAuthenticationSecretDetector : IAuthenticationSecretDetector
	{
		static readonly IList<AuthenticationSecret> EmptyAuthSecrets;

		enum ImapAuthCommandState
		{
			None,
			Command,
			Authenticate,
			AuthMechanism,
			AuthNewLine,
			AuthToken,
			Login,
			UserName,
			Password,
			LoginNewLine,
			Error
		}

		enum ImapLoginTokenType
		{
			None,
			Atom,
			QString,
			Literal
		}

		enum ImapLiteralState
		{
			None,
			Octets,
			Plus,
			CloseBrace,
			Literal,
			Complete
		}

		enum ImapQStringState
		{
			None,
			Escaped,
			EndQuote,
			Complete
		}

		ImapAuthCommandState commandState;
		ImapLiteralState literalState;
		ImapQStringState qstringState;
		ImapLoginTokenType tokenType;
		bool isAuthenticating;
		int literalOctets;
		int literalSeen;
		int textIndex;

		public bool IsAuthenticating {
			get { return isAuthenticating; }
			set {
				commandState = ImapAuthCommandState.None;
				isAuthenticating = value;
				ClearLoginTokenState ();
				textIndex = 0;
			}
		}

		static ImapAuthenticationSecretDetector ()
		{
#if NET45
			EmptyAuthSecrets = new AuthenticationSecret[0];
#else
			EmptyAuthSecrets = Array.Empty<AuthenticationSecret> ();
#endif
		}

		void ClearLoginTokenState ()
		{
			literalState = ImapLiteralState.None;
			qstringState = ImapQStringState.None;
			tokenType = ImapLoginTokenType.None;
			literalOctets = 0;
			literalSeen = 0;
		}

		bool SkipText (string text, byte[] buffer, ref int index, int endIndex)
		{
			while (index < endIndex && textIndex < text.Length) {
				if (buffer[index] != (byte) text[textIndex]) {
					commandState = ImapAuthCommandState.Error;
					break;
				}

				textIndex++;
				index++;
			}

			return textIndex == text.Length;
		}

		IList<AuthenticationSecret> DetectAuthSecrets (byte[] buffer, int offset, int endIndex)
		{
			int index = offset;

			if (commandState == ImapAuthCommandState.Authenticate) {
				if (SkipText ("AUTHENTICATE ", buffer, ref index, endIndex))
					commandState = ImapAuthCommandState.AuthMechanism;

				if (index >= endIndex || commandState == ImapAuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (commandState == ImapAuthCommandState.AuthMechanism) {
				while (index < endIndex && buffer[index] != (byte) ' ' && buffer[index] != (byte) '\r')
					index++;

				if (index < endIndex) {
					if (buffer[index] == (byte) ' ') {
						commandState = ImapAuthCommandState.AuthToken;
					} else {
						commandState = ImapAuthCommandState.AuthNewLine;
					}

					index++;
				}

				if (index >= endIndex)
					return EmptyAuthSecrets;
			}

			if (commandState == ImapAuthCommandState.AuthNewLine) {
				if (buffer[index] == (byte) '\n') {
					commandState = ImapAuthCommandState.AuthToken;
					index++;
				} else {
					commandState = ImapAuthCommandState.Error;
				}

				if (index >= endIndex || commandState == ImapAuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			int startIndex = index;
			while (index < endIndex && buffer[index] != (byte) '\r')
				index++;

			if (index < endIndex)
				commandState = ImapAuthCommandState.AuthNewLine;

			if (index == startIndex)
				return EmptyAuthSecrets;

			var secret = new AuthenticationSecret (startIndex, index - startIndex);

			if (commandState == ImapAuthCommandState.AuthNewLine) {
				index++;

				if (index < endIndex) {
					if (buffer[index] == (byte) '\n') {
						commandState = ImapAuthCommandState.AuthToken;
					} else {
						commandState = ImapAuthCommandState.Error;
					}
				}
			}

			return new AuthenticationSecret[] { secret };
		}

		bool SkipLiteralToken (List<AuthenticationSecret> secrets, byte[] buffer, ref int index, int endIndex, byte sentinel)
		{
			if (literalState == ImapLiteralState.Octets) {
				while (index < endIndex && buffer[index] != (byte) '+' && buffer[index] != (byte) '}') {
					int digit = buffer[index] - (byte) '0';
					literalOctets = literalOctets * 10 + digit;
					index++;
				}

				if (index < endIndex) {
					if (buffer[index] == (byte) '+') {
						literalState = ImapLiteralState.Plus;
						textIndex = 0;
					} else {
						literalState = ImapLiteralState.CloseBrace;
						textIndex = 1;
					}

					index++;
				}

				if (index >= endIndex)
					return false;
			}

			if (literalState < ImapLiteralState.Literal) {
				if (SkipText ("}\r\n", buffer, ref index, endIndex))
					literalState = ImapLiteralState.Literal;
			}

			if (index >= endIndex || commandState == ImapAuthCommandState.Error)
				return false;

			if (literalState == ImapLiteralState.Literal) {
				int skip = Math.Min (literalOctets - literalSeen, endIndex - index);

				secrets.Add (new AuthenticationSecret (index, skip));

				literalSeen += skip;
				index += skip;

				if (literalSeen == literalOctets)
					literalState = ImapLiteralState.Complete;
			}

			if (literalState == ImapLiteralState.Complete && index < endIndex && buffer[index] == sentinel) {
				index++;
				return true;
			}

			return false;
		}

		bool SkipLoginToken (List<AuthenticationSecret> secrets, byte[] buffer, ref int index, int endIndex, byte sentinel)
		{
			int startIndex;

			if (tokenType == ImapLoginTokenType.None) {
				switch ((char) buffer[index]) {
				case '{':
					literalState = ImapLiteralState.Octets;
					tokenType = ImapLoginTokenType.Literal;
					index++;
					break;
				case '"':
					tokenType = ImapLoginTokenType.QString;
					index++;
					break;
				default:
					tokenType = ImapLoginTokenType.Atom;
					break;
				}
			}

			switch (tokenType) {
			case ImapLoginTokenType.Literal:
				return SkipLiteralToken (secrets, buffer, ref index, endIndex, sentinel);
			case ImapLoginTokenType.QString:
				if (qstringState != ImapQStringState.Complete) {
					startIndex = index;

					while (index < endIndex) {
						if (qstringState == ImapQStringState.Escaped) {
							qstringState = ImapQStringState.None;
						} else if (buffer[index] == (byte) '\\') {
							qstringState = ImapQStringState.Escaped;
						} else if (buffer[index] == (byte) '"') {
							qstringState = ImapQStringState.EndQuote;
							break;
						}
						index++;
					}

					if (index > startIndex)
						secrets.Add (new AuthenticationSecret (startIndex, index - startIndex));

					if (qstringState == ImapQStringState.EndQuote) {
						qstringState = ImapQStringState.Complete;
						index++;
					}
				}

				if (index >= endIndex)
					return false;

				if (buffer[index] != sentinel) {
					commandState = ImapAuthCommandState.Error;
					return false;
				}

				index++;

				return true;
			default:
				startIndex = index;

				while (index < endIndex && buffer[index] != sentinel)
					index++;

				if (index > startIndex)
					secrets.Add (new AuthenticationSecret (startIndex, index - startIndex));

				if (index >= endIndex)
					return false;
				
				index++;

				return true;
			}
		}

		IList<AuthenticationSecret> DetectLoginSecrets (byte[] buffer, int offset, int endIndex)
		{
			var secrets = new List<AuthenticationSecret> ();
			int index = offset;

			if (commandState == ImapAuthCommandState.LoginNewLine)
				return EmptyAuthSecrets;

			if (commandState == ImapAuthCommandState.Login) {
				if (SkipText ("LOGIN ", buffer, ref index, endIndex))
					commandState = ImapAuthCommandState.UserName;

				if (index >= endIndex || commandState == ImapAuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (commandState == ImapAuthCommandState.UserName) {
				if (SkipLoginToken (secrets, buffer, ref index, endIndex, (byte) ' ')) {
					commandState = ImapAuthCommandState.Password;
					ClearLoginTokenState ();
				}

				if (index >= endIndex || commandState == ImapAuthCommandState.Error)
					return secrets;
			}

			if (commandState == ImapAuthCommandState.Password) {
				if (SkipLoginToken (secrets, buffer, ref index, endIndex, (byte) '\r')) {
					commandState = ImapAuthCommandState.LoginNewLine;
					ClearLoginTokenState ();
				}
			}

			return secrets;
		}

		public IList<AuthenticationSecret> DetectSecrets (byte[] buffer, int offset, int count)
		{
			if (!IsAuthenticating || commandState == ImapAuthCommandState.Error || count == 0)
				return EmptyAuthSecrets;

			int endIndex = offset + count;
			int index = offset;

			if (commandState == ImapAuthCommandState.None) {
				// skip over the tag
				while (index < endIndex && buffer[index] != (byte) ' ')
					index++;

				if (index < endIndex) {
					commandState = ImapAuthCommandState.Command;
					index++;
				}

				if (index >= endIndex)
					return EmptyAuthSecrets;
			}

			if (commandState == ImapAuthCommandState.Command) {
				switch ((char) buffer[index]) {
				case 'A':
					commandState = ImapAuthCommandState.Authenticate;
					textIndex = 1;
					index++;
					break;
				case 'L':
					commandState = ImapAuthCommandState.Login;
					textIndex = 1;
					index++;
					break;
				default:
					commandState = ImapAuthCommandState.Error;
					break;
				}

				if (index >= endIndex || commandState == ImapAuthCommandState.Error)
					return EmptyAuthSecrets;
			}

			if (commandState >= ImapAuthCommandState.Authenticate && commandState <= ImapAuthCommandState.AuthToken)
				return DetectAuthSecrets (buffer, index, endIndex);

			return DetectLoginSecrets (buffer, index, endIndex);
		}
	}
}
