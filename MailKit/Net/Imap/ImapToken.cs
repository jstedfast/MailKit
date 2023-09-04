//
// ImapToken.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using System.Globalization;
using System.Collections.Generic;

using MimeKit.Utils;

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
		Plus          = (int) '+',
	}

	class ImapToken
	{
		public static readonly ImapToken Plus = new ImapToken (ImapTokenType.Plus, '+');
		public static readonly ImapToken Asterisk = new ImapToken (ImapTokenType.Asterisk, '*');
		public static readonly ImapToken OpenParen = new ImapToken (ImapTokenType.OpenParen, '(');
		public static readonly ImapToken CloseParen = new ImapToken (ImapTokenType.CloseParen, ')');
		public static readonly ImapToken OpenBracket = new ImapToken (ImapTokenType.OpenBracket, '[');
		public static readonly ImapToken CloseBracket = new ImapToken (ImapTokenType.CloseBracket, ']');
		public static readonly ImapToken Nil = new ImapToken (ImapTokenType.Nil, "NIL");
		public static readonly ImapToken Eoln = new ImapToken (ImapTokenType.Eoln, '\n');

		static readonly ImapToken[] CommonMessageFlagTokens = new ImapToken[] {
			new ImapToken (ImapTokenType.Flag, "\\Answered"),
			new ImapToken (ImapTokenType.Flag, "\\Deleted"),
			new ImapToken (ImapTokenType.Flag, "\\Draft"),
			new ImapToken (ImapTokenType.Flag, "\\Flagged"),
			new ImapToken (ImapTokenType.Flag, "\\Recent"),
			new ImapToken (ImapTokenType.Flag, "\\Seen"),
			new ImapToken (ImapTokenType.Flag, "\\*")
		};

		static readonly List<ImapToken> NilTokens = new List<ImapToken> (6) {
			Nil
		};

		static readonly ImapToken Ok = new ImapToken (ImapTokenType.Atom, "OK");
		static readonly ImapToken Fetch = new ImapToken (ImapTokenType.Atom, "FETCH");
		//static readonly ImapToken Annotation = new ImapToken (ImapTokenType.Atom, "ANNOTATION");
		static readonly ImapToken Body = new ImapToken (ImapTokenType.Atom, "BODY");
		static readonly ImapToken BodyStructure = new ImapToken (ImapTokenType.Atom, "BODYSTRUCTURE");
		//static readonly ImapToken EmailId = new ImapToken (ImapTokenType.Atom, "EMAILID");
		static readonly ImapToken Envelope = new ImapToken (ImapTokenType.Atom, "ENVELOPE");
		static readonly ImapToken Flags = new ImapToken (ImapTokenType.Atom, "FLAGS");
		//static readonly ImapToken Header = new ImapToken (ImapTokenType.Atom, "HEADER");
		//static readonly ImapToken HeaderFields = new ImapToken (ImapTokenType.Atom, "HEADER.FIELDS");
		static readonly ImapToken InternalDate = new ImapToken (ImapTokenType.Atom, "INTERNALDATE");
		static readonly ImapToken ModSeq = new ImapToken (ImapTokenType.Atom, "MODSEQ");
		static readonly ImapToken Rfc822Size = new ImapToken (ImapTokenType.Atom, "RFC822.SIZE");
		//static readonly ImapToken SaveDate = new ImapToken (ImapTokenType.Atom, "SAVEDATE");
		//static readonly ImapToken ThreadId = new ImapToken (ImapTokenType.Atom, "THREADID");
		static readonly ImapToken Uid = new ImapToken (ImapTokenType.Atom, "UID");
		static readonly ImapToken XGMLabels = new ImapToken (ImapTokenType.Atom, "X-GM-LABELS");
		static readonly ImapToken XGMMsgId = new ImapToken (ImapTokenType.Atom, "X-GM-MSGID");
		static readonly ImapToken XGMThrId = new ImapToken (ImapTokenType.Atom, "X-GM-THRID");

		static readonly ImapTokenCache Cache = new ImapTokenCache ();

		public readonly ImapTokenType Type;
		public readonly object Value;

		internal ImapToken (ImapTokenType type, object value = null)
		{
			Value = value;
			Type = type;

			//System.Console.WriteLine ("token: {0}", this);
		}

		public static ImapToken Create (ImapTokenType type, char c)
		{
			switch (type) {
			case ImapTokenType.Plus: return Plus;
			case ImapTokenType.Asterisk: return Asterisk;
			case ImapTokenType.OpenParen: return OpenParen;
			case ImapTokenType.CloseParen: return CloseParen;
			case ImapTokenType.OpenBracket: return OpenBracket;
			case ImapTokenType.CloseBracket: return CloseBracket;
			case ImapTokenType.Eoln: return Eoln;
			}

			return new ImapToken (type, c);
		}

		public static ImapToken Create (ImapTokenType type, int literalLength)
		{
			return new ImapToken (type, literalLength);
		}

		static bool IsCacheable (ByteArrayBuilder builder)
		{
			if (builder.Length < 2 || builder.Length > 32)
				return false;

			// Any atom token that starts with a digit is likely to be an integer value, so don't cache it.
			if (builder[0] >= (byte) '0' && builder[0] <= (byte) '9')
				return false;

			// Any atom token that starts with 'A'->'Z' and is followed by digits is a tag token. Ignore.
			if (builder[0] >= (byte) 'A' && builder[0] <= (byte) 'Z' && builder[1] >= (byte) '0' && builder[1] <= (byte) '9')
				return false;

			return true;
		}

		public static ImapToken Create (ImapTokenType type, ByteArrayBuilder builder)
		{
			bool cachable = false;
			string value;

			if (type == ImapTokenType.Flag) {
				foreach (var token in CommonMessageFlagTokens) {
					value = (string) token.Value;

					if (builder.Equals (value, true))
						return token;
				}

				cachable = true;
			} else if (type == ImapTokenType.Atom) {
				if (builder.Equals ("NIL", true)) {
					// Look for the cached NIL token that matches this capitalization.
					lock (NilTokens) {
						foreach (var token in NilTokens) {
							value = (string) token.Value;

							if (builder.Equals (value))
								return token;
						}

						// Add this new variation to our NIL token cache.
						var nil = new ImapToken (ImapTokenType.Nil, builder.ToString ());
						NilTokens.Add (nil);

						return nil;
					}
				}

				if (builder.Equals ("OK", false))
					return Ok;
				if (builder.Equals ("FETCH", false))
					return Fetch;
				if (builder.Equals ("BODY", false))
					return Body;
				if (builder.Equals ("BODYSTRUCTURE", false))
					return BodyStructure;
				if (builder.Equals ("ENVELOPE", false))
					return Envelope;
				if (builder.Equals ("FLAGS", false))
					return Flags;
				if (builder.Equals ("INTERNALDATE", false))
					return InternalDate;
				if (builder.Equals ("MODSEQ", false))
					return ModSeq;
				if (builder.Equals ("RFC822.SIZE", false))
					return Rfc822Size;
				if (builder.Equals ("UID", false))
					return Uid;
				if (builder.Equals ("X-GM-LABELS", false))
					return XGMLabels;
				if (builder.Equals ("X-GM-MSGID", false))
					return XGMMsgId;
				if (builder.Equals ("X-GM-THRID", false))
					return XGMThrId;

				cachable = IsCacheable (builder);
			} else if (type == ImapTokenType.QString) {
				cachable = true;
			}

			if (cachable)
				return Cache.AddOrGet (type, builder);

			value = builder.ToString ();

			return new ImapToken (type, value);
		}

		public static ImapToken CreateError (ByteArrayBuilder builder)
		{
			return new ImapToken (ImapTokenType.Error, builder.ToString ());
		}

		public override string ToString ()
		{
			switch (Type) {
			case ImapTokenType.NoData:       return "<no data>";
			case ImapTokenType.Nil:          return (string) Value;
			case ImapTokenType.Atom:         return (string) Value;
			case ImapTokenType.Flag:         return (string) Value;
			case ImapTokenType.QString:      return MimeUtils.Quote ((string) Value);
			case ImapTokenType.Literal:      return string.Format (CultureInfo.InvariantCulture, "{{{0}}}", (int) Value);
			case ImapTokenType.Eoln:         return "'\\n'";
			case ImapTokenType.Plus:         return "'+'";
			case ImapTokenType.OpenParen:    return "'('";
			case ImapTokenType.CloseParen:   return "')'";
			case ImapTokenType.Asterisk:     return "'*'";
			case ImapTokenType.OpenBracket:  return "'['";
			case ImapTokenType.CloseBracket: return "']'";
			default:                         return string.Format (CultureInfo.InvariantCulture, "[{0}: '{1}']", Type, Value);
			}
		}
	}
}
