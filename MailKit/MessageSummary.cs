//
// MessageSummary.cs
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
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A summary of a message.
	/// </summary>
	/// <remarks>
	/// A <see cref="MessageSummary"/> is returned by
	/// <see cref="IFolder.Fetch(UniqueId[], MessageSummaryItems, System.Threading.CancellationToken)"/>.
	/// The properties of the <see cref="MessageSummary"/> that will be available
	/// depend on the <see cref="MessageSummaryItems"/> passed to the aformentioned method.
	/// </remarks>
	public class MessageSummary : IThreadable, ISortable
	{
		internal MessageSummary (int index)
		{
			Index = index;
		}

		/// <summary>
		/// Gets the body structure of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>The body will be one of <see cref="BodyPartText"/>,
		/// <see cref="BodyPartMessage"/>, <see cref="BodyPartBasic"/>,
		/// or <see cref="BodyPartMultipart"/>.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Body"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The body structure of the message.</value>
		public BodyPart Body {
			get; internal set;
		}

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
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The envelope of the message.</value>
		public Envelope Envelope {
			get; internal set;
		}

		/// <summary>
		/// Gets the message flags, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message flags, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Flags"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The message flags.</value>
		public MessageFlags? Flags {
			get; internal set;
		}

		/// <summary>
		/// Gets the internal date of the message (i.e. the "received" date), if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the internal date of the message (i.e. the "received" date), if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.InternalDate"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The internal date of the message.</value>
		public DateTimeOffset? InternalDate {
			get; internal set;
		}

		/// <summary>
		/// Gets the size of the message, in bytes, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the size of the message, in bytes, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.MessageSize"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The size of the message.</value>
		public uint? MessageSize {
			get; internal set;
		}

		/// <summary>
		/// Gets the mod-sequence value for the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the mod-sequence value for the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.ModSeq"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The mod-sequence value.</value>
		public ulong? ModSeq {
			get; internal set;
		}

		/// <summary>
		/// Gets the message-ids that the message references, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message-ids that the message references, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.References"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The references.</value>
		public MessageIdList References {
			get; internal set;
		}

		/// <summary>
		/// Gets the unique ID of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the unique ID of the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.UniqueId"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The uid of the message.</value>
		public UniqueId? UniqueId {
			get; internal set;
		}

		/// <summary>
		/// Gets the index of the message.
		/// </summary>
		/// <remarks>
		/// Gets the index of the message.
		/// </remarks>
		/// <value>The index of the message.</value>
		public int Index {
			get; private set;
		}

		#region GMail extension properties

		/// <summary>
		/// Gets the GMail message identifier, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the GMail message identifier, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailMessageId"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The GMail message identifier.</value>
		public ulong? GMailMessageId {
			get; internal set;
		}

		/// <summary>
		/// Gets the GMail thread identifier, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the GMail thread identifier, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailThreadId"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The GMail thread identifier.</value>
		public ulong? GMailThreadId {
			get; internal set;
		}

		/// <summary>
		/// Gets the list of GMail labels, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the list of GMail labels, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailLabels"/> flag is passed to
		/// <see cref="IFolder.Fetch(UniqueId[],MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The GMail labels.</value>
		public List<string> GMailLabels {
			get; internal set;
		}

		#endregion

		#region ISortable implementation

		/// <summary>
		/// Gets whether or not the messages can be sorted.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the messages can be sorted.
		/// </remarks>
		/// <value><c>true</c> if the messages can be sorted; otherwise, <c>false</c>.</value>
		bool ISortable.CanSort {
			get { return Envelope != null; }
		}

		/// <summary>
		/// Gets the message index in the folder it belongs to.
		/// </summary>
		/// <remarks>
		/// Gets the message index in the folder it belongs to.
		/// </remarks>
		/// <value>The index.</value>
		int ISortable.SortableIndex {
			get { return Index; }
		}

		/// <summary>
		/// Gets the Cc header value.
		/// </summary>
		/// <remarks>
		/// Gets the Cc header value.
		/// </remarks>
		/// <value>The Cc header value.</value>
		string ISortable.SortableCc {
			get { return Envelope.Cc.ToString (); }
		}

		/// <summary>
		/// Gets the Date header value.
		/// </summary>
		/// <remarks>
		/// Gets the Date header value.
		/// </remarks>
		/// <value>The date.</value>
		DateTimeOffset ISortable.SortableDate {
			get { return Envelope.Date ?? InternalDate ?? DateTimeOffset.MinValue; }
		}

		/// <summary>
		/// Gets the From header value.
		/// </summary>
		/// <remarks>
		/// Gets the From header value.
		/// </remarks>
		/// <value>The From header value.</value>
		string ISortable.SortableFrom {
			get { return Envelope.From.ToString (); }
		}

		/// <summary>
		/// Gets the size of the message, in bytes.
		/// </summary>
		/// <remarks>
		/// Gets the size of the message, in bytes.
		/// </remarks>
		/// <value>The size of the message, in bytes.</value>
		uint ISortable.SortableSize {
			get { return MessageSize ?? 0; }
		}

		/// <summary>
		/// Gets the Subject header value.
		/// </summary>
		/// <remarks>
		/// Gets the Subject header value.
		/// </remarks>
		/// <value>The Subject header value.</value>
		string ISortable.SortableSubject {
			get { return Envelope.Subject; }
		}

		/// <summary>
		/// Gets the To header value.
		/// </summary>
		/// <remarks>
		/// Gets the To header value.
		/// </remarks>
		/// <value>The To header value.</value>
		string ISortable.SortableTo {
			get { return Envelope.To.ToString (); }
		}

		#endregion

		#region IThreadable implementation

		MessageIdList threadableReferences;
		int threadableReplyDepth = -1;
		string threadableSubject;

		void UpdateThreadableSubject ()
		{
			if (threadableSubject != null)
				return;

			if (Envelope.Subject != null) {
				threadableSubject = MessageThreader.GetThreadableSubject (Envelope.Subject, out threadableReplyDepth);
			} else {
				threadableSubject = string.Empty;
				threadableReplyDepth = 0;
			}
		}

		/// <summary>
		/// Gets whether the message can be threaded.
		/// </summary>
		/// <remarks>
		/// Gets whether the message can be threaded.
		/// </remarks>
		/// <value><c>true</c> if the messages can be threaded; otherwise, <c>false</c>.</value>
		bool IThreadable.CanThread {
			get { return Envelope != null && UniqueId.HasValue; }
		}

		/// <summary>
		/// Gets the threadable subject.
		/// </summary>
		/// <remarks>
		/// A normalized Subject header value where prefixes such as
		/// "Re:", "Re[#]:", etc have been pruned.
		/// </remarks>
		/// <value>The threadable subject.</value>
		string IThreadable.ThreadableSubject {
			get {
				UpdateThreadableSubject ();

				return threadableSubject;
			}
		}

		/// <summary>
		/// Gets a value indicating whether this instance is a reply.
		/// </summary>
		/// <remarks>
		/// This value should be based on whether the message subject contained any "Re:" or "Fwd:" prefixes.
		/// </remarks>
		/// <value><c>true</c> if this instance is a reply; otherwise, <c>false</c>.</value>
		bool IThreadable.IsThreadableReply {
			get {
				UpdateThreadableSubject ();

				return threadableReplyDepth != 0;
			}
		}

		/// <summary>
		/// Gets the threadable message identifier.
		/// </summary>
		/// <remarks>
		/// This value should be the canonicalized Message-Id header value
		/// without the angle brackets.
		/// </remarks>
		/// <value>The threadable message identifier.</value>
		string IThreadable.ThreadableMessageId {
			get { return Envelope.MessageId; }
		}

		/// <summary>
		/// Gets the threadable references.
		/// </summary>
		/// <remarks>
		/// This value should be the list of canonicalized Message-Ids
		/// found in the In-Reply-To and References headers.
		/// </remarks>
		/// <value>The threadable references.</value>
		MessageIdList IThreadable.ThreadableReferences {
			get {
				if (threadableReferences == null) {
					threadableReferences = References != null ? References.Clone () : new MessageIdList ();

					if (Envelope.InReplyTo != null)
						threadableReferences.Add (Envelope.InReplyTo);
				}

				return threadableReferences;
			}
		}

		/// <summary>
		/// Gets the unique identifier.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifier.
		/// </remarks>
		/// <value>The unique identifier.</value>
		UniqueId IThreadable.ThreadableUniqueId {
			get { return UniqueId.Value; }
		}

		#endregion
	}
}
