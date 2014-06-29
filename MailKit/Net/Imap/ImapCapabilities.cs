//
// ImapCapabilities.cs
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
	public enum ImapCapabilities : long {
		/// <summary>
		/// The server does not support any additional extensions.
		/// </summary>
		None           = 0,

		/// <summary>
		/// The server implements the core IMAP4 commands.
		/// </summary>
		IMAP4            = 1L << 0,

		/// <summary>
		/// The server implements the core IMAP4rev1 commands.
		/// </summary>
		IMAP4rev1        = 1L << 1,

		/// <summary>
		/// The server supports the STATUS command.
		/// </summary>
		Status           = 1L << 2,

		/// <summary>
		/// The server supports the ACL extension defined in rfc2086 and rfc4314.
		/// </summary>
		Acl              = 1L << 3,

		/// <summary>
		/// The server supports the QUOTA extension defined in rfc2087.
		/// </summary>
		Quota            = 1L << 4,

		/// <summary>
		/// The server supports the LITERAL+ extension defined in rfc2088.
		/// </summary>
		LiteralPlus      = 1L << 5,

		/// <summary>
		/// The server supports the IDLE extension defined in rfc2177.
		/// </summary>
		Idle             = 1L << 6,

		/// <summary>
		/// The server supports the MAILBOX-REFERRALS extension defined in rfc2193.
		/// </summary>
		MailboxReferrals = 1L << 7,

		/// <summary>
		/// the server supports the LOGIN-REFERRALS extension defined in rfc2221.
		/// </summary>
		LoginReferrals   = 1L << 8,

		/// <summary>
		/// The server supports the NAMESPACE extension defined in rfc2342.
		/// </summary>
		Namespace        = 1L << 9,

		/// <summary>
		/// The server supports the ID extension defined in rfc2971.
		/// </summary>
		Id               = 1L << 10,

		/// <summary>
		/// The server supports the CHILDREN extension defined in rfc3348.
		/// </summary>
		Children         = 1L << 11,

		/// <summary>
		/// The server supports the LOGINDISABLED extension defined in rfc3501.
		/// </summary>
		LoginDisabled    = 1L << 12,

		/// <summary>
		/// The server supports the StartTLS extension defined in rfc3501.
		/// </summary>
		StartTLS         = 1L << 13,

		/// <summary>
		/// The server supports the MULTIAPPEND extension defined in rfc3502.
		/// </summary>
		MultiAppend      = 1L << 14,

		/// <summary>
		/// The server supports the BINARY content extension defined in rfc3516.
		/// </summary>
		Binary           = 1L << 15,

		/// <summary>
		/// The server supports the UNSELECT extension defined in rfc3691.
		/// </summary>
		Unselect         = 1L << 16,

		/// <summary>
		/// The server supports the UIDPLUS extension defined in rfc4315.
		/// </summary>
		UidPlus          = 1L << 17,

		/// <summary>
		/// The server supports the CATENATE extension defined in rfc4469.
		/// </summary>
		Catenate         = 1L << 18,

		/// <summary>
		/// The server supports the CONDSTORE extension defined in rfc4551.
		/// </summary>
		CondStore        = 1L << 19,

		/// <summary>
		/// The server supports the ESEARCH extension defined in rfc4731.
		/// </summary>
		ESearch          = 1L << 20,

		/// <summary>
		/// The server supports the SASL-IR extension defined in rfc4959.
		/// </summary>
		SaslIR           = 1L << 21,

		/// <summary>
		/// The server supports the COMPRESS extension defined in rfc4978.
		/// </summary>
		Compress         = 1L << 22,

		/// <summary>
		/// The server supports the WITHIN extension defined in rfc5032.
		/// </summary>
		Within           = 1L << 23,

		/// <summary>
		/// The server supports the ENABLE extension defined in rfc5161.
		/// </summary>
		Enable           = 1L << 24,

		/// <summary>
		/// The server supports the QRESYNC extension defined in rfc5162.
		/// </summary>
		QuickResync      = 1L << 25,

		/// <summary>
		/// The server supports the SEARCHRES extension defined in rfc5182.
		/// </summary>
		SearchResults    = 1L << 26,

		/// <summary>
		/// The server supports the SORT extension defined in rfc5256.
		/// </summary>
		Sort             = 1L << 27,

		/// <summary>
		/// The server supports the THREAD extension defined in rfc5256.
		/// </summary>
		Thread           = 1L << 28,

		/// <summary>
		/// The server supports the LIST-EXTENDED extension defined in rfc5258.
		/// </summary>
		ListExtended     = 1L << 29,

		/// <summary>
		/// The server supports the CONVERT extension defined in rfc5259.
		/// </summary>
		Convert          = 1L << 30,

		/// <summary>
		/// The server supports the ESORT extension defined in rfc5267.
		/// </summary>
		ESort            = 1L << 31,

		/// <summary>
		/// The server supports the CONTEXT extension defined in rfc5267.
		/// </summary>
		Context          = 1L << 32,

		/// <summary>
		/// The server supports the METADATA extension defined in rfc5464.
		/// </summary>
		Metadata         = 1L << 33,

		/// <summary>
		/// The server supports the NOTIFY extension defined in rfc5465.
		/// </summary>
		Notify           = 1L << 34,

		/// <summary>
		/// The server supports the FILTERS extension defined in rfc5466.
		/// </summary>
		Filters          = 1L << 35,

		/// <summary>
		/// The server supports the LIST-STATUS extension defined in rfc5819.
		/// </summary>
		ListStatus       = 1L << 36,

		/// <summary>
		/// The server supports the SEPCIAL-USE extension defined in rfc6154.
		/// </summary>
		SpecialUse       = 1L << 37,

		/// <summary>
		/// The server supports the MULTISEARCH extension defined in rfc6237.
		/// </summary>
		MultiSearch      = 1L << 38,

		/// <summary>
		/// The server supports the MOVE extension defined in rfc6851.
		/// </summary>
		Move             = 1L << 39,

		#region GMail Extensions

		// These are documented here: https://developers.google.com/gmail/imap_extensions

		/// <summary>
		/// The server supports the XLIST extension (GMail).
		/// </summary>
		XList            = 1L << 40,

		/// <summary>
		/// The server supports the X-GM-EXT1 extension (GMail).
		/// </summary>
		GMailExt1        = 1L << 41

		#endregion
	}
}
