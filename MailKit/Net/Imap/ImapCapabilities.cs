//
// ImapCapabilities.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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

namespace MailKit.Net.Imap {
	/// <summary>
	/// Capabilities supported by an IMAP server.
	/// </summary>
	/// <remarks>
	/// Capabilities are read as part of the response to the CAPABILITY command that
	/// is issued during the connection and authentication phases of the
	/// <see cref="ImapClient"/>.
	/// </remarks>
	[Flags]
	public enum ImapCapabilities {
		/// <summary>
		/// The server does not support any additional extensions.
		/// </summary>
		None           = 0,

		/// <summary>
		/// The server implements the core IMAP4 commands.
		/// </summary>
		IMAP4          = 1 << 0,

		/// <summary>
		/// The server implements the core IMAP4rev1 commands.
		/// </summary>
		IMAP4rev1      = 1 << 1,

		/// <summary>
		/// The server supports the STATUS command.
		/// </summary>
		Status         = 1 << 2,

		/// <summary>
		/// The server supports the QUOTA extension defined in rfc2087.
		/// </summary>
		Quota          = 1 << 3,

		/// <summary>
		/// The server supports the LITERAL+ extension defined in rfc2088.
		/// </summary>
		LiteralPlus    = 1 << 4,

		/// <summary>
		/// The server supports the IDLE extension defined in rfc2177.
		/// </summary>
		Idle           = 1 << 5,

		/// <summary>
		/// The server supports the NAMESPACE extension defined in rfc2342.
		/// </summary>
		Namespace      = 1 << 6,

		/// <summary>
		/// The server supports the CHILDREN extension defined in rfc3348.
		/// </summary>
		Children       = 1 << 7,

		/// <summary>
		/// The server supports the LOGINDISABLED extension defined in rfc3501.
		/// </summary>
		LoginDisabled  = 1 << 8,

		/// <summary>
		/// The server supports the StartTLS extension defined in rfc3501.
		/// </summary>
		StartTLS       = 1 << 9,

		/// <summary>
		/// The server supports the MULTIAPPEND extension defined in rfc3502.
		/// </summary>
		MultiAppend    = 1 << 10,

		/// <summary>
		/// The server supports the BINARY content extension defined in rfc3516.
		/// </summary>
		Binary         = 1 << 11,

		/// <summary>
		/// The server supports the UNSELECT extension defined in rfc3691.
		/// </summary>
		Unselect       = 1 << 12,

		/// <summary>
		/// The server supports the UIDPLUS extension defined in rfc4315.
		/// </summary>
		UidPlus        = 1 << 13,

		/// <summary>
		/// The server supports the CATENATE extension defined in rfc4469.
		/// </summary>
		Catenate       = 1 << 14,

		/// <summary>
		/// The server supports the CONDSTORE extension defined in rfc4551.
		/// </summary>
		CondStore      = 1 << 15,

		/// <summary>
		/// The server supports the ESEARCH extension defined in rfc4731.
		/// </summary>
		ESearch        = 1 << 16,

		/// <summary>
		/// The server supports the COMPRESS extension defined in rfc4978.
		/// </summary>
		Compress       = 1 << 17,

		/// <summary>
		/// The server supports the ENABLE extension defined in rfc5161.
		/// </summary>
		Enable         = 1 << 18,

		/// <summary>
		/// The server supports the LIST-EXTENDED extension defined in rfc5258.
		/// </summary>
		ListExtended   = 1 << 19,

		/// <summary>
		/// The server supports the CONVERT extension defined in rfc5259.
		/// </summary>
		Convert        = 1 << 20,

		/// <summary>
		/// The server supports the METADATA extension defined in rfc5464.
		/// </summary>
		MetaData       = 1 << 21,

		/// <summary>
		/// The server supports the FILTERS extension defined in rfc5466.
		/// </summary>
		Filters        = 1 << 22,

		/// <summary>
		/// The server supports the SEPCIAL-USE extension defined in rfc6154.
		/// </summary>
		SpecialUse     = 1 << 23,

		/// <summary>
		/// The server supports the MOVE extension defined in rfc6851.
		/// </summary>
		Move           = 1 << 24,
	}
}
