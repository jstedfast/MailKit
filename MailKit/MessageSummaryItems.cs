//
// FetchFlags.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

namespace MailKit {
	/// <summary>
	/// A bitfield of <see cref="MessageSummary"/> fields.
	/// </summary>
	/// <remarks>
	/// <see cref="MessageSummaryItems"/> are used to specify which properties
	/// of <see cref="MessageSummary"/> should be populated by calls to
	/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;, MessageSummaryItems, System.Threading.CancellationToken)"/>,
	/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;int&gt;, MessageSummaryItems, System.Threading.CancellationToken)"/>, or
	/// <see cref="IMailFolder.Fetch(int, int, MessageSummaryItems, System.Threading.CancellationToken)"/>.
	/// </remarks>
	[Flags]
	public enum MessageSummaryItems {
		/// <summary>
		/// Don't fetch any summary items.
		/// </summary>
		None           = 0,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Annotations"/>.</para>
		/// <para>Fetches all <c>ANNOATION</c> values as defined in
		/// <a href="https://tools.ietf.org/html/rfc5257">rfc5257</a>.</para>
		/// </summary>
		Annotations    = 1 << 0,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Body"/>.</para>
		/// <para>Fetches the <c>BODY</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// <note type="note">Unlike <see cref="BodyStructure"/>, <c>Body</c> will not populate the
		/// <see cref="BodyPart.ContentType"/> parameters nor will it populate the
		/// <see cref="BodyPartBasic.ContentDisposition"/>, <see cref="BodyPartBasic.ContentLanguage"/>
		/// or <see cref="BodyPartBasic.ContentLocation"/> properties of each <see cref="BodyPartBasic"/>
		/// body part. This makes <c>Body</c> far less useful than <c>BodyStructure</c> especially when
		/// it is desirable to determine whether or not a body part is an attachment.</note>
		/// </summary>
		Body           = 1 << 1,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Body"/> (but with more details than <see cref="Body"/>).</para>
		/// <para>Fetches the <c>BODYSTRUCTURE</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// <note type="note">Unlike <see cref="Body"/>, <c>BodyStructure</c> will also populate the
		/// <see cref="BodyPart.ContentType"/> parameters as well as the
		/// <see cref="BodyPartBasic.ContentDisposition"/>, <see cref="BodyPartBasic.ContentLanguage"/>
		/// and <see cref="BodyPartBasic.ContentLocation"/> properties of each <see cref="BodyPartBasic"/>
		/// body part. The <c>Content-Disposition</c> information is especially important when trying to
		/// determine whether or not a body part is an attachment, for example.</note>
		/// </summary>
		BodyStructure  = 1 << 2,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Envelope"/>.</para>
		/// <para>Fetches the <c>ENVELOPE</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		Envelope       = 1 << 3,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Flags"/>.</para>
		/// <para>Fetches the <c>FLAGS</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		Flags          = 1 << 4,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.InternalDate"/>.</para>
		/// <para>Fetches the <c>INTERNALDATE</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		InternalDate   = 1 << 5,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Size"/>.</para>
		/// <para>Fetches the <c>RFC822.SIZE</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		Size           = 1 << 6,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.ModSeq"/>.</para>
		/// <para>Fetches the <c>MODSEQ</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc4551">rfc4551</a>.</para>
		/// </summary>
		ModSeq         = 1 << 7,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.References"/>.
		/// </summary>
		References     = 1 << 8,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.UniqueId"/>.</para>
		/// <para>Fetches the <c>UID</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		UniqueId       = 1 << 9,

		/// <summary>
		/// <para></para>Fetch the <see cref="IMessageSummary.EmailId"/>.
		/// <para>Fetches the <c>EMAILID</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc8474">rfc8474</a>.</para>
		/// </summary>
		EmailId        = 1 << 10,

		/// <summary>
		/// <para></para>Fetch the <see cref="IMessageSummary.EmailId"/>.
		/// <para>Fetches the <c>EMAILID</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc8474">rfc8474</a>.</para>
		/// </summary>
		[Obsolete ("Use EmailId instead.")]
		Id             = EmailId,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.ThreadId"/>.</para>
		/// <para>Fetches the <c>THREADID</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc8474">rfc8474</a>.</para>
		/// </summary>
		ThreadId       = 1 << 11,

		#region GMail extension items

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.GMailMessageId"/>.</para>
		/// <para>Fetches the <c>X-GM-MSGID</c> value as defined in Google's
		/// <a href="https://developers.google.com/gmail/imap/imap-extensions">IMAP extensions</a>
		/// documentation.</para>
		/// </summary>
		GMailMessageId = 1 << 12,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.GMailThreadId"/>.</para>
		/// <para>Fetches the <c>X-GM-THRID</c> value as defined in Google's
		/// <a href="https://developers.google.com/gmail/imap/imap-extensions">IMAP extensions</a>
		/// documentation.</para>
		/// </summary>
		GMailThreadId  = 1 << 13,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.GMailLabels"/>.</para>
		/// <para>Fetches the <c>X-GM-LABELS</c> value as defined in Google's
		/// <a href="https://developers.google.com/gmail/imap/imap-extensions">IMAP extensions</a>
		/// documentation.</para>
		/// </summary>
		GMailLabels    = 1 << 14,

		#endregion

		/// <summary>
		/// <para>Fetch the the complete list of <see cref="IMessageSummary.Headers"/> for each message.</para>
		/// </summary>
		Headers        = 1 << 15,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.PreviewText"/>.</para>
		/// <note type="note">This property is quite expensive to calculate because it is not an
		/// item that is cached on the IMAP server. Instead, MailKit must download a hunk of the
		/// message body so that it can decode and parse it in order to generate a meaningful
		/// text snippet. This usually involves downloading the first 512 bytes for <c>text/plain</c>
		/// message bodies and the first 16 kilobytes for <c>text/html</c> message bodies. If a
		/// message contains both a <c>text/plain</c> body and a <c>text/html</c> body, then the
		/// <c>text/plain</c> content is used in order to reduce network traffic.</note>
		/// </summary>
		PreviewText    = 1 << 16,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.SaveDate"/>.</para>
		/// <para>Fetches the <c>SAVEDATE</c> value as defined in
		/// <a href="https://tools.ietf.org/html/rfc8514">rfc8514</a>.</para>
		/// </summary>
		SaveDate       = 1 << 17,

		#region Macros

		/// <summary>
		/// <para>A macro for fetching the <see cref="Envelope"/>, <see cref="Flags"/>,
		/// <see cref="InternalDate"/>, and <see cref="Size"/> values.</para>
		/// <para>This macro maps to the equivalent <c>ALL</c> macro as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		All            = Envelope | Flags | InternalDate | Size,

		/// <summary>
		/// <para>A macro for fetching the <see cref="Flags"/>, <see cref="InternalDate"/>, and
		/// <see cref="Size"/> values.</para>
		/// <para>This macro maps to the equivalent <c>FAST</c> macro as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		Fast           = Flags | InternalDate | Size,

		/// <summary>
		/// <para>A macro for fetching the <see cref="Body"/>, <see cref="Envelope"/>,
		/// <see cref="Flags"/>, <see cref="InternalDate"/>, and <see cref="Size"/> values.</para>
		/// <para>This macro maps to the equivalent <c>FULL</c> macro as defined in
		/// <a href="https://tools.ietf.org/html/rfc3501">rfc3501</a>.</para>
		/// </summary>
		Full           = Body | Envelope | Flags| InternalDate | Size,

		#endregion
	}
}
