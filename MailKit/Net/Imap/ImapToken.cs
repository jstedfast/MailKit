//
// ImapToken.cs
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

namespace MailKit.Net.Imap {
	enum ImapTokenType {
		NoData        = -7,
		Error         = -6,
		Nil           = -5,
		Atom          = -4,
		Flag          = -3,
		QString       = -2,
		Literal       = -1,

		// character tokens:
		Eoln          = (int) '\n',
		OpenParen     = (int) '(',
		CloseParen    = (int) ')',
		Asterisk      = (int) '*',
		OpenBracket   = (int) '[',
		CloseBracket  = (int) ']',
	}

	class ImapToken
	{
		public readonly ImapTokenType Type;
		public readonly object Value;

		public ImapToken (ImapTokenType type, object value = null)
		{
			Value = value;
			Type = type;

			//System.Console.WriteLine ("token: {0}", this);
		}

		public override string ToString ()
		{
			switch (Type) {
			case ImapTokenType.NoData:       return "<no data>";
			case ImapTokenType.Nil:          return "NIL";
			case ImapTokenType.Atom:         return "[atom: " + (string) Value + "]";
			case ImapTokenType.Flag:         return "[flag: " + (string) Value + "]";
			case ImapTokenType.QString:      return "[qstring: \"" + (string) Value + "\"]";
			case ImapTokenType.Literal:      return "{" + (int) Value + "}";
			case ImapTokenType.Eoln:         return "'\\n'";
			case ImapTokenType.OpenParen:    return "'('";
			case ImapTokenType.CloseParen:   return "')'";
			case ImapTokenType.Asterisk:     return "'*'";
			case ImapTokenType.OpenBracket:  return "'['";
			case ImapTokenType.CloseBracket: return "']'";
			default:                         return string.Format ("[{0}: '{1}']", Type, Value);
			}
		}
	}
}
