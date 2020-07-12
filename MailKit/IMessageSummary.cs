//
// IMessageSummary.cs
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
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A summary of a message.
	/// </summary>
	/// <remarks>
	/// <para>The <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a> and
	/// <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a> methods
	/// return lists of <see cref="IMessageSummary"/> items.</para>
	/// <para>The properties of the <see cref="IMessageSummary"/> that will be available
	/// depend on the <see cref="MessageSummaryItems"/> passed to the aformentioned method.</para>
	/// </remarks>
	public interface IMessageSummary
	{
		/// <summary>
		/// Get the folder that the message belongs to.
		/// </summary>
		/// <remarks>
		/// Gets the folder that the message belongs to, if available.
		/// </remarks>
		/// <value>The folder.</value>
		IMailFolder Folder {
			get;
		}

		/// <summary>
		/// Get a bitmask of fields that have been populated.
		/// </summary>
		/// <remarks>
		/// Gets a bitmask of fields that have been populated.
		/// </remarks>
		/// <value>The fields that have been populated.</value>
		MessageSummaryItems Fields { get; }

		/// <summary>
		/// Gets the body structure of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>The body will be one of <see cref="BodyPartText"/>,
		/// <see cref="BodyPartMessage"/>, <see cref="BodyPartBasic"/>,
		/// or <see cref="BodyPartMultipart"/>.</para>
		/// <para>This property will only be set if either the
		/// <see cref="MessageSummaryItems.Body"/> flag or the
		/// <see cref="MessageSummaryItems.BodyStructure"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The body structure of the message.</value>
		BodyPart Body { get; }

		/// <summary>
		/// Gets the text body part of the message if it exists.
		/// </summary>
		/// <remarks>
		/// <para>Gets the <c>text/plain</c> body part of the message.</para>
		/// <para>This property will only be usable if the
		/// <see cref="MessageSummaryItems.BodyStructure"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <value>The text body if it exists; otherwise, <c>null</c>.</value>
		BodyPartText TextBody { get; }

		/// <summary>
		/// Gets the html body part of the message if it exists.
		/// </summary>
		/// <remarks>
		/// <para>Gets the <c>text/html</c> body part of the message.</para>
		/// <para>This property will only be usable if the
		/// <see cref="MessageSummaryItems.BodyStructure"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The html body if it exists; otherwise, <c>null</c>.</value>
		BodyPartText HtmlBody { get; }

		/// <summary>
		/// Gets the body parts of the message.
		/// </summary>
		/// <remarks>
		/// <para>Traverses over the <see cref="Body"/>, enumerating all of the
		/// <see cref="BodyPartBasic"/> objects.</para>
		/// <para>This property will only be usable if either the
		/// <see cref="MessageSummaryItems.Body"/> flag or the
		/// <see cref="MessageSummaryItems.BodyStructure"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The body parts.</value>
		IEnumerable<BodyPartBasic> BodyParts { get; }

		/// <summary>
		/// Gets the attachments.
		/// </summary>
		/// <remarks>
		/// <para>Traverses over the <see cref="Body"/>, enumerating all of the
		/// <see cref="BodyPartBasic"/> objects that have a <c>Content-Disposition</c>
		/// header set to <c>"attachment"</c>.</para>
		/// <para>This property will only be usable if the
		/// <see cref="MessageSummaryItems.BodyStructure"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <value>The attachments.</value>
		IEnumerable<BodyPartBasic> Attachments { get; }

		/// <summary>
		/// Gets the preview text of the message.
		/// </summary>
		/// <remarks>
		/// <para>The preview text is a short snippet of the beginning of the message
		/// text, typically shown in a mail client's message list to provide the user
		/// with a sense of what the message is about.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.PreviewText"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The preview text.</value>
		string PreviewText { get; }

		/// <summary>
		/// Gets the envelope of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>The envelope of a message contains information such as the
		/// date the message was sent, the subject of the message,
		/// the sender of the message, who the message was sent to,
		/// which message(s) the message may be in reply to,
		/// and the message id.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Envelope"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The envelope of the message.</value>
		Envelope Envelope { get; }

		/// <summary>
		/// Gets the normalized subject.
		/// </summary>
		/// <remarks>
		/// <para>A normalized <c>Subject</c> header value where prefixes such as <c>"Re:"</c>, <c>"Re[#]:"</c> and <c>"FWD:"</c> have been pruned.</para>
		/// <para>This property is typically used for threading messages by subject.</para>
		/// </remarks>
		/// <value>The normalized subject.</value>
		string NormalizedSubject { get; }

		/// <summary>
		/// Gets the Date header value.
		/// </summary>
		/// <remarks>
		/// Gets the Date header value. If the Date header is not present, the arrival date is used.
		/// If neither are known, <see cref="System.DateTimeOffset.MinValue"/> is returned.
		/// </remarks>
		/// <value>The date.</value>
		DateTimeOffset Date { get; }

		/// <summary>
		/// Gets whether or not the message is a reply.
		/// </summary>
		/// <remarks>
		/// This value should be based on whether the message subject contained any <c>"Re:"</c>, <c>"Re[#]:"</c> or <c>"FWD:"</c> prefixes.
		/// </remarks>
		/// <value><c>true</c> if the message is a reply; otherwise, <c>false</c>.</value>
		bool IsReply { get; }

		/// <summary>
		/// Gets the message flags, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message flags, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Flags"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The message flags.</value>
		MessageFlags? Flags { get; }

		/// <summary>
		/// Gets the user-defined message flags, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the user-defined message flags, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Flags"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The user-defined message flags.</value>
		HashSet<string> Keywords { get; }

		/// <summary>
		/// Gets the user-defined message flags, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the user-defined message flags, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Flags"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The user-defined message flags.</value>
		[Obsolete ("Use Keywords instead.")]
		HashSet<string> UserFlags { get; }

		/// <summary>
		/// Gets the message annotations, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message annotations, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Annotations"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The message annotations.</value>
		IList<Annotation> Annotations { get; }

		/// <summary>
		/// Gets the list of headers, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the list of headers, if available.</para>
		/// <para>This property will only be set if <see cref="MessageSummaryItems.Headers"/>
		/// is specified in a call to one of the
		/// <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods or specific headers are requested via a one of the Fetch or FetchAsync methods
		/// that accept list of specific headers to request for each message such as
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Collections.Generic.IEnumerable&lt;MimeKit.HeaderId&gt;,System.Threading.CancellationToken)"/>.
		/// </para>
		/// </remarks>
		/// <value>The list of headers.</value>
		HeaderList Headers { get; }

		/// <summary>
		/// Gets the internal date of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the internal date of the message (often the same date as found in the <c>Received</c> header), if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.InternalDate"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The internal date of the message.</value>
		DateTimeOffset? InternalDate { get; }

		/// <summary>
		/// Gets the date and time that the message was saved to the current mailbox, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the date and time that the message was saved to the current mailbox, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.SaveDate"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The save date of the message.</value>
		DateTimeOffset? SaveDate { get; }

		/// <summary>
		/// Gets the size of the message, in bytes, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the size of the message, in bytes, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Size"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The size of the message.</value>
		uint? Size { get; }

		/// <summary>
		/// Gets the mod-sequence value for the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the mod-sequence value for the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.ModSeq"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The mod-sequence value.</value>
		ulong? ModSeq { get; }

		/// <summary>
		/// Gets the message-ids that the message references, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message-ids that the message references, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.References"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The references.</value>
		MessageIdList References { get; }

		/// <summary>
		/// Get the globally unique identifier for the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the globally unique identifier of the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.EmailId"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// <note type="info">This property maps to the <c>EMAILID</c> value defined in the
		/// <a href="https://tools.ietf.org/html/rfc8474">OBJECTID</a> extension.</note>
		/// </remarks>
		/// <value>The globally unique message identifier.</value>
		string EmailId { get; }

		/// <summary>
		/// Get the globally unique identifier for the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the globally unique identifier of the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.EmailId"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// <note type="info">This property maps to the <c>EMAILID</c> value defined in the
		/// <a href="https://tools.ietf.org/html/rfc8474">OBJECTID</a> extension.</note>
		/// </remarks>
		/// <value>The globally unique message identifier.</value>
		[Obsolete ("Use EmailId instead.")]
		string Id { get; }

		/// <summary>
		/// Get the globally unique thread identifier for the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the globally unique thread identifier for the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.ThreadId"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// <note type="info">This property maps to the <c>THREADID</c> value defined in the
		/// <a href="https://tools.ietf.org/html/rfc8474">OBJECTID</a> extension.</note>
		/// </remarks>
		/// <value>The globally unique thread identifier.</value>
		string ThreadId { get; }

		/// <summary>
		/// Gets the unique identifier of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the unique identifier of the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.UniqueId"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The uid of the message.</value>
		UniqueId UniqueId { get; }

		/// <summary>
		/// Gets the index of the message.
		/// </summary>
		/// <remarks>
		/// <para>Gets the index of the message.</para>
		/// <para>This property is always set.</para>
		/// </remarks>
		/// <value>The index of the message.</value>
		int Index { get; }

		#region GMail extension properties

		/// <summary>
		/// Gets the GMail message identifier, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the GMail message identifier, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailMessageId"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The GMail message identifier.</value>
		ulong? GMailMessageId { get; }

		/// <summary>
		/// Gets the GMail thread identifier, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the GMail thread identifier, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailThreadId"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The GMail thread identifier.</value>
		ulong? GMailThreadId { get; }

		/// <summary>
		/// Gets the list of GMail labels, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the list of GMail labels, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailLabels"/> flag is passed to
		/// one of the <a href="Overload_MailKit_IMailFolder_Fetch.htm">Fetch</a>
		/// or <a href="Overload_MailKit_IMailFolder_FetchAsync.htm">FetchAsync</a>
		/// methods.</para>
		/// </remarks>
		/// <value>The GMail labels.</value>
		IList<string> GMailLabels { get; }

		#endregion
	}
}
