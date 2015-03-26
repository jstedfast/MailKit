//
// MessageSummary.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2015 Xamarin Inc. (www.xamarin.com)
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
using System.Linq;
using System.Collections.Generic;

using MimeKit;
using MimeKit.Utils;

namespace MailKit {
	/// <summary>
	/// A summary of a message.
	/// </summary>
	/// <remarks>
	/// A <see cref="MessageSummary"/> is returned by
	/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;, MessageSummaryItems, System.Threading.CancellationToken)"/>.
	/// The properties of the <see cref="MessageSummary"/> that will be available
	/// depend on the <see cref="MessageSummaryItems"/> passed to the aformentioned method.
	/// </remarks>
	public class MessageSummary : IMessageSummary
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageSummary"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MessageSummary"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is negative.
		/// </exception>
		public MessageSummary (int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException ("index");

			UserFlags = new HashSet<string> ();
			Index = index;
		}

		/// <summary>
		/// Get a bitmask of items that have been fetched.
		/// </summary>
		/// <remarks>
		/// Gets a bitmask of items that have been fetched.
		/// </remarks>
		/// <value>The items that have been fetched.</value>
		internal MessageSummaryItems FetchedItems {
			get; set;
		}

		/// <summary>
		/// Gets the body structure of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>The body will be one of <see cref="BodyPartText"/>,
		/// <see cref="BodyPartMessage"/>, <see cref="BodyPartBasic"/>,
		/// or <see cref="BodyPartMultipart"/>.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.BodyStructure"/> or
		/// <see cref="MessageSummaryItems.Body"/> flag is used
		/// when fetching summary information from a <see cref="IMailFolder"/>.</para>
		/// </remarks>
		/// <value>The body structure of the message.</value>
		public BodyPart Body {
			get; set;
		}

		static BodyPart GetMultipartRelatedRoot (BodyPartMultipart related)
		{
			string start = related.ContentType.Parameters["start"];
			string contentId;

			if (start == null)
				return related.BodyParts.Count > 0 ? related.BodyParts[0] : null;

			if ((contentId = MimeUtils.EnumerateReferences (start).FirstOrDefault ()) == null)
				contentId = start;

			var cid = new Uri (string.Format ("cid:{0}", contentId));

			for (int i = 0; i < related.BodyParts.Count; i++) {
				var basic = related.BodyParts[i] as BodyPartBasic;

				if (basic != null && (basic.ContentId == contentId || basic.ContentLocation == cid))
					return basic;

				var multipart = related.BodyParts[i] as BodyPartMultipart;

				if (multipart != null && multipart.ContentLocation == cid)
					return multipart;
			}

			return null;
		}

		static bool TryGetMultipartAlternativeBody (BodyPartMultipart multipart, bool html, out BodyPartText body)
		{
			// walk the multipart/alternative children backwards from greatest level of faithfulness to the least faithful
			for (int i = multipart.BodyParts.Count - 1; i >= 0; i--) {
				var multi = multipart.BodyParts[i] as BodyPartMultipart;
				BodyPartText text = null;

				if (multi != null) {
					if (multi.ContentType.Matches ("multipart", "related")) {
						text = GetMultipartRelatedRoot (multi) as BodyPartText;
					} else if (multi.ContentType.Matches ("multipart", "alternative")) {
						// Note: nested multipart/alternatives make no sense... yet here we are.
						if (TryGetMultipartAlternativeBody (multi, html, out body))
							return true;
					}
				} else {
					text = multipart.BodyParts[i] as BodyPartText;
				}

				if (text != null && (html ? text.IsHtml : text.IsPlain)) {
					body = text;
					return true;
				}
			}

			body = null;

			return false;
		}

		static bool TryGetMessageBody (BodyPartMultipart multipart, bool html, out BodyPartText body)
		{
			BodyPartMultipart multi;
			BodyPartText text;

			if (multipart.ContentType.Matches ("multipart", "alternative"))
				return TryGetMultipartAlternativeBody (multipart, html, out body);

			if (!multipart.ContentType.Matches ("multipart", "related")) {
				// Note: This is probably a multipart/mixed... and if not, we can still treat it like it is.
				for (int i = 0; i < multipart.BodyParts.Count; i++) {
					multi = multipart.BodyParts[i] as BodyPartMultipart;

					// descend into nested multiparts, if there are any...
					if (multi != null) {
						if (TryGetMessageBody (multi, html, out body))
							return true;

						// The text body should never come after a multipart.
						break;
					}

					text = multipart.BodyParts[i] as BodyPartText;

					// Look for the first non-attachment text part (realistically, the body text will
					// preceed any attachments, but I'm not sure we can rely on that assumption).
					if (text != null && !text.IsAttachment) {
						if (html ? text.IsHtml : text.IsPlain) {
							body = text;
							return true;
						}

						// Note: the first text/* part in a multipart/mixed is the text body.
						// If it's not in the format we're looking for, then it doesn't exist.
						break;
					}
				}
			} else {
				// Note: If the multipart/related root document is HTML, then this is the droid we are looking for.
				var root = GetMultipartRelatedRoot (multipart);

				text = root as BodyPartText;

				if (text != null) {
					body = (html ? text.IsHtml : text.IsPlain) ? text : null;
					return body != null;
				}

				// maybe the root is another multipart (like multipart/alternative)?
				multi = root as BodyPartMultipart;

				if (multi != null)
					return TryGetMessageBody (multi, html, out body);
			}

			body = null;

			return false;
		}

		/// <summary>
		/// Gets the text body part of the message if it exists.
		/// </summary>
		/// <remarks>
		/// <para>Gets the text/plain body part of the message.</para>
		/// <para>In order for this to work properly, it is necessary to include
		/// <see cref="MessageSummaryItems.BodyStructure"/> when fetching
		/// summary information from a <see cref="IMailFolder"/>.</para>
		/// </remarks>
		/// <value>The text body if it exists; otherwise, <c>null</c>.</value>
		public BodyPartText TextBody {
			get {
				var multipart = Body as BodyPartMultipart;

				if (multipart != null) {
					BodyPartText plain;

					if (TryGetMessageBody (multipart, false, out plain))
						return plain;
				} else {
					var text = Body as BodyPartText;

					if (text != null && text.IsPlain)
						return text;
				}

				return null;
			}
		}

		/// <summary>
		/// Gets the html body part of the message if it exists.
		/// </summary>
		/// <remarks>
		/// <para>Gets the text/html body part of the message.</para>
		/// <para>In order for this to work properly, it is necessary to include
		/// <see cref="MessageSummaryItems.BodyStructure"/> when fetching
		/// summary information from a <see cref="IMailFolder"/>.</para>
		/// </remarks>
		/// <value>The html body if it exists; otherwise, <c>null</c>.</value>
		public BodyPartText HtmlBody {
			get {
				var multipart = Body as BodyPartMultipart;

				if (multipart != null) {
					BodyPartText html;

					if (TryGetMessageBody (multipart, true, out html))
						return html;
				} else {
					var text = Body as BodyPartText;

					if (text != null && text.IsHtml)
						return text;
				}

				return null;
			}
		}

		static IEnumerable<BodyPartBasic> EnumerateBodyParts (BodyPart entity)
		{
			if (entity == null)
				yield break;

			var multipart = entity as BodyPartMultipart;

			if (multipart != null) {
				foreach (var subpart in multipart.BodyParts) {
					foreach (var part in EnumerateBodyParts (subpart))
						yield return part;
				}

				yield break;
			}

			var msgpart = entity as BodyPartMessage;

			if (msgpart != null) {
				var message = msgpart.Body;

				if (message != null) {
					foreach (var part in EnumerateBodyParts (message))
						yield return part;
				}

				yield break;
			}

			yield return (BodyPartBasic) entity;
		}

		/// <summary>
		/// Gets the body parts of the message.
		/// </summary>
		/// <remarks>
		/// <para>Traverses over the <see cref="Body"/>, enumerating all of the
		/// <see cref="BodyPartBasic"/> objects.</para>
		/// <para>In order for this to work, it is necessary to include
		/// <see cref="MessageSummaryItems.BodyStructure"/> or
		/// <see cref="MessageSummaryItems.Body"/> when fetching
		/// summary information from a <see cref="IMailFolder"/>.</para>
		/// </remarks>
		/// <value>The body parts.</value>
		public IEnumerable<BodyPartBasic> BodyParts {
			get { return EnumerateBodyParts (Body); }
		}

		/// <summary>
		/// Gets the attachments.
		/// </summary>
		/// <remarks>
		/// <para>Traverses over the <see cref="Body"/>, enumerating all of the
		/// <see cref="BodyPartBasic"/> objects that have a Content-Disposition
		/// header set to <c>"attachment"</c>.</para>
		/// <para>In order for this to work properly, it is necessary to include
		/// <see cref="MessageSummaryItems.BodyStructure"/> when fetching
		/// summary information from a <see cref="IMailFolder"/>.</para>
		/// </remarks>
		/// <value>The attachments.</value>
		public IEnumerable<BodyPartBasic> Attachments {
			get { return EnumerateBodyParts (Body).Where (part => part.IsAttachment); }
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
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The envelope of the message.</value>
		public Envelope Envelope {
			get; set;
		}

		/// <summary>
		/// Gets the message flags, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message flags, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Flags"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The message flags.</value>
		public MessageFlags? Flags {
			get; set;
		}

		/// <summary>
		/// Gets the user-defined message flags, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the user-defined message flags, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.Flags"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The user-defined message flags.</value>
		public HashSet<string> UserFlags {
			get; set;
		}

		/// <summary>
		/// Gets the list of headers, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the list of headers, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Collections.Generic.HashSet&lt;MimeKit.HeaderId&gt;,System.Threading.CancellationToken)"/>.
		/// method is used.</para>
		/// </remarks>
		/// <value>The list of headers.</value>
		public HeaderList Headers {
			get; set;
		}

		/// <summary>
		/// Gets the internal date of the message (i.e. the "received" date), if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the internal date of the message (i.e. the "received" date), if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.InternalDate"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The internal date of the message.</value>
		public DateTimeOffset? InternalDate {
			get; set;
		}

		/// <summary>
		/// Gets the size of the message, in bytes, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the size of the message, in bytes, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.MessageSize"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The size of the message.</value>
		public uint? MessageSize {
			get; set;
		}

		/// <summary>
		/// Gets the mod-sequence value for the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the mod-sequence value for the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.ModSeq"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The mod-sequence value.</value>
		public ulong? ModSeq {
			get; set;
		}

		/// <summary>
		/// Gets the message-ids that the message references, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message-ids that the message references, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.References"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The references.</value>
		public MessageIdList References {
			get; set;
		}

		/// <summary>
		/// Gets the unique ID of the message, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the unique ID of the message, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.UniqueId"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The uid of the message.</value>
		public UniqueId? UniqueId {
			get; set;
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
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The GMail message identifier.</value>
		public ulong? GMailMessageId {
			get; set;
		}

		/// <summary>
		/// Gets the GMail thread identifier, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the GMail thread identifier, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailThreadId"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The GMail thread identifier.</value>
		public ulong? GMailThreadId {
			get; set;
		}

		/// <summary>
		/// Gets the list of GMail labels, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the list of GMail labels, if available.</para>
		/// <para>This property will only be set if the
		/// <see cref="MessageSummaryItems.GMailLabels"/> flag is passed to
		/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;,MessageSummaryItems,System.Threading.CancellationToken)"/>.</para>
		/// </remarks>
		/// <value>The GMail labels.</value>
		public IList<string> GMailLabels {
			get; set;
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
