//
// MessageThread.cs
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

using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A message thread.
	/// </summary>
	/// <remarks>
	/// A message thread.
	/// </remarks>
	public class MessageThread
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageThread"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new message thread node.
		/// </remarks>
		/// <param name="uid">The unique identifier of the message.</param>
		public MessageThread (UniqueId? uid)
		{
			Children = new List<MessageThread> ();
			UniqueId = uid;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MessageThread"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new message thread node.
		/// </remarks>
		/// <param name="message">The message summary.</param>
		public MessageThread (IMessageSummary message)
		{
			Children = new List<MessageThread> ();
			if (message != null && message.UniqueId.IsValid)
				UniqueId = message.UniqueId;
			Message = message;
		}

		/// <summary>
		/// Gets the message summary, if available.
		/// </summary>
		/// <remarks>
		/// <para>Gets the message summary, if available.</para>
		/// <note type="note">This property will only ever be set if the <see cref="MessageThread"/>
		/// was created by the <see cref="MessageThreader"/>. <see cref="MessageThread"/>s that are
		/// created by any of the
		/// <a href="Overload_MailKit_Net_Imap_ImapFolder_Thread.htm">Thread</a> or
		/// <a href="Overload_MailKit_Net_Imap_ImapFolder_ThreadAsync.htm">ThreadAsync</a>
		/// methods will always be <c>null</c>.</note>
		/// </remarks>
		/// <value>The message summary.</value>
		public IMessageSummary Message {
			get; private set;
		}

		/// <summary>
		/// Gets the unique identifier of the message.
		/// </summary>
		/// <remarks>
		/// The unique identifier may be <c>null</c> if the message is missing from the
		/// <see cref="IMailFolder"/> or from the list of messages provided to the
		/// <see cref="MessageThreader"/>.
		/// </remarks>
		/// <value>The unique identifier.</value>
		public UniqueId? UniqueId {
			// FIXME: this shouldn't be a nullable since we can just use UniqueId.Invalid
			get; private set;
		}

		/// <summary>
		/// Gets the children.
		/// </summary>
		/// <remarks>
		/// Each child represents a reply to the message referenced by <see cref="UniqueId"/>.
		/// </remarks>
		/// <value>The children.</value>
		public IList<MessageThread> Children {
			get; private set;
		}
	}
}
