//
// ImapCapabilities.cs
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

using System;

namespace MailKit.Net.Imap {
	/// <summary>
	/// Capabilities supported by an IMAP server.
	/// </summary>
	/// <remarks>
	/// Capabilities are read as part of the response to the <c>CAPABILITY</c> command that
	/// is issued during the connection and authentication phases of the
	/// <see cref="ImapClient"/>.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
	/// </example>
	[Flags]
	public enum ImapCapabilities : long {
		/// <summary>
		/// The server does not support any additional extensions.
		/// </summary>
		None             = 0,

		/// <summary>
		/// The server implements the core IMAP4 commands.
		/// </summary>
		IMAP4            = 1L << 0,

		/// <summary>
		/// The server implements the core IMAP4rev1 commands.
		/// </summary>
		IMAP4rev1        = 1L << 1,

		/// <summary>
		/// The server supports the <c>STATUS</c> command.
		/// </summary>
		Status           = 1L << 2,

		/// <summary>
		/// The server supports the ACL extension defined in <a href="https://tools.ietf.org/html/rfc2086">rfc2086</a>
		/// and <a href="https://tools.ietf.org/html/rfc4314">rfc4314</a>.
		/// </summary>
		Acl              = 1L << 3,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2087">QUOTA</a> extension.
		/// </summary>
		Quota            = 1L << 4,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2088">LITERAL+</a> extension.
		/// </summary>
		LiteralPlus      = 1L << 5,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2177">IDLE</a> extension.
		/// </summary>
		Idle             = 1L << 6,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2193">MAILBOX-REFERRALS</a> extension.
		/// </summary>
		MailboxReferrals = 1L << 7,

		/// <summary>
		/// the server supports the <a href="https://tools.ietf.org/html/rfc2221">LOGIN-REFERRALS</a> extension.
		/// </summary>
		LoginReferrals   = 1L << 8,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2342">NAMESPACE</a> extension.
		/// </summary>
		Namespace        = 1L << 9,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc2971">ID</a> extension.
		/// </summary>
		Id               = 1L << 10,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3348">CHILDREN</a> extension.
		/// </summary>
		Children         = 1L << 11,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3501">LOGINDISABLED</a> extension.
		/// </summary>
		LoginDisabled    = 1L << 12,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3501">STARTTLS</a> extension.
		/// </summary>
		StartTLS         = 1L << 13,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3502">MULTIAPPEND</a> extension.
		/// </summary>
		MultiAppend      = 1L << 14,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3516">BINARY</a> content extension.
		/// </summary>
		Binary           = 1L << 15,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc3691">UNSELECT</a> extension.
		/// </summary>
		Unselect         = 1L << 16,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc4315">UIDPLUS</a> extension.
		/// </summary>
		UidPlus          = 1L << 17,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc4469">CATENATE</a> extension.
		/// </summary>
		Catenate         = 1L << 18,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc4551">CONDSTORE</a> extension.
		/// </summary>
		CondStore        = 1L << 19,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc4731">ESEARCH</a> extension.
		/// </summary>
		ESearch          = 1L << 20,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc4959">SASL-IR</a> extension.
		/// </summary>
		SaslIR           = 1L << 21,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc4978">COMPRESS</a> extension.
		/// </summary>
		Compress         = 1L << 22,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5032">WITHIN</a> extension.
		/// </summary>
		Within           = 1L << 23,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5161">ENABLE</a> extension.
		/// </summary>
		Enable           = 1L << 24,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5162">QRESYNC</a> extension.
		/// </summary>
		QuickResync      = 1L << 25,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5182">SEARCHRES</a> extension.
		/// </summary>
		SearchResults    = 1L << 26,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5256">SORT</a> extension.
		/// </summary>
		Sort             = 1L << 27,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5256">THREAD</a> extension.
		/// </summary>
		Thread           = 1L << 28,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5258">LIST-EXTENDED</a> extension.
		/// </summary>
		ListExtended     = 1L << 29,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5259">CONVERT</a> extension.
		/// </summary>
		Convert          = 1L << 30,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5255">LANGUAGE</a> extension.
		/// </summary>
		Language         = 1L << 31,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5255">I18NLEVEL</a> extension.
		/// </summary>
		I18NLevel        = 1L << 32,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5267">ESORT</a> extension.
		/// </summary>
		ESort            = 1L << 33,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5267">CONTEXT</a> extension.
		/// </summary>
		Context          = 1L << 34,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5464">METADATA</a> extension.
		/// </summary>
		Metadata         = 1L << 35,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5465">NOTIFY</a> extension.
		/// </summary>
		Notify           = 1L << 36,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5466">FILTERS</a> extension.
		/// </summary>
		Filters          = 1L << 37,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5819">LIST-STATUS</a> extension.
		/// </summary>
		ListStatus       = 1L << 38,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc5957">SORT=DISPLAY</a> extension.
		/// </summary>
		SortDisplay      = 1L << 39,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6154">CREATE-SPECIAL-USE</a> extension.
		/// </summary>
		CreateSpecialUse = 1L << 40,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6154">SPECIAL-USE</a> extension.
		/// </summary>
		SpecialUse       = 1L << 41,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6203">SEARCH=FUZZY</a> extension.
		/// </summary>
		FuzzySearch      = 1L << 42,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6237">MULTISEARCH</a> extension.
		/// </summary>
		MultiSearch      = 1L << 43,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6851">MOVE</a> extension.
		/// </summary>
		Move             = 1L << 44,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6855">UTF8=ACCEPT</a> extension.
		/// </summary>
		UTF8Accept       = 1L << 45,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc6855">UTF8=ONLY</a> extension.
		/// </summary>
		UTF8Only         = 1L << 46,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc7888">LITERAL-</a> extension.
		/// </summary>
		LiteralMinus     = 1L << 47,

		/// <summary>
		/// The server supports the <a href="https://tools.ietf.org/html/rfc7889">APPENDLIMIT</a> extension.
		/// </summary>
		AppendLimit      = 1L << 48,

		#region GMail Extensions

		/// <summary>
		/// The server supports the <a href="https://developers.google.com/gmail/imap_extensions">XLIST</a> extension (GMail).
		/// </summary>
		XList            = 1L << 50,

		/// <summary>
		/// The server supports the <a href="https://developers.google.com/gmail/imap_extensions">X-GM-EXT1</a> extension (GMail).
		/// </summary>
		GMailExt1        = 1L << 51

		#endregion
	}
}
