//
// ImapFeatures.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
	/// A bitfield of IMAP features that can be enabled using the ENABLE command.
	/// </summary>
	/// <remarks>
	/// A bitfield of IMAP features that can be enabled using the
	/// <a href="https://tools.ietf.org/html/rfc5161">ENABLE</a> command.
	/// </remarks>
	/// <seealso cref="ImapClient.Enable(ImapFeatures,System.Threading.CancellationToken)"/>
	/// <seealso cref="ImapClient.EnableAsync(ImapFeatures,System.Threading.CancellationToken)"/>
	[Flags]
	public enum ImapFeatures {
		/// <summary>
		/// No features.
		/// </summary>
		None        = 0,

		/// <summary>
		/// The <a href="https://tools.ietf.org/html/rfc7162">QRESYNC</a> (and CONDSTORE) features.
		/// </summary>
		/// <remarks>
		/// <para>The QRESYNC extension improves resynchronization performance of folders by
		/// querying the IMAP server for a list of changes when the folder is opened using the
		/// <see cref="ImapFolder.Open(FolderAccess,uint,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="MailFolder.MessageExpunged"/> event is replaced
		/// with the <see cref="MailFolder.MessagesVanished"/> event.</para>
		/// </remarks>
		QuickResync = 1 << 0,

		/// <summary>
		/// The <a href="https://tools.ietf.org/html/rfc6855">UTF8=ACCEPT</a> feature.
		/// </summary>
		UTF8Accept  = 1 << 1,

		/// <summary>
		/// The <a href="https://tools.ietf.org/html/rfc9051">IMAP4rev2</a> protocol.
		/// </summary>
		/// <remarks>
		/// <para>Servers that advertise both IMAP4rev1 and IMAP4rev2 will use IMAP4rev1 semantics unless the
		/// client explicitly enables IMAP4rev2.</para>
		/// <para>Enabling IMAP4rev2 also implies UTF-8 mailbox names and the extensions that are part of the
		/// IMAP4rev2 base protocol (such as STATUS=SIZE).</para>
		/// </remarks>
		IMAP4rev2   = 1 << 2,
	}
}
