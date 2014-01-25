//
// MessageSummary.cs
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

namespace MailKit {
	/// <summary>
	/// A summary of a message.
	/// </summary>
	/// <remarks>
	/// A <see cref="MessageSummary"/> is returned by
	/// <see cref="IFolder.Fetch(string[], MessageAttributes, CancellationToken)"/>.
	/// The properties of the <see cref="MessageSummary"/> that will be available
	/// depend on the <see cref="MessageSummaryItems"/> passed to the aformentioned method.
	/// </remarks>
	public class MessageSummary
	{
		internal MessageSummary (int index)
		{
			Index = index;
		}

		/// <summary>
		/// Gets the body structure of the message, if available.
		/// </summary>
		/// <remarks>
		/// The body will be one of <see cref="BodyPartText"/>,
		/// <see cref="BodyPartMessage"/>, <see cref="BodyPartBasic"/>,
		/// or <see cref="BodyPartMultipart"/>.
		/// </remarks>
		/// <value>The body structure of the message.</value>
		public BodyPart Body {
			get; internal set;
		}

		/// <summary>
		/// Gets the envelope of the message, if available.
		/// </summary>
		/// <remarks>
		/// The envelope of a message contains information such as the
		/// date the message was sent, the subject of the message,
		/// the sender of the message, who the message was sent to,
		/// which message(s) the message may be in reply to,
		/// and the message id.
		/// </remarks>
		/// <value>The envelope of the message.</value>
		public Envelope Envelope {
			get; internal set;
		}

		/// <summary>
		/// Gets the message flags, if available.
		/// </summary>
		/// <value>The message flags.</value>
		public MessageFlags? Flags {
			get; internal set;
		}

		/// <summary>
		/// Gets the internal date of the message (i.e. the "received" date), if available.
		/// </summary>
		/// <value>The internal date of the message.</value>
		public DateTimeOffset? InternalDate {
			get; internal set;
		}

		/// <summary>
		/// Gets the size of the message, in bytes, if available.
		/// </summary>
		/// <value>The size of the message.</value>
		public uint? MessageSize {
			get; internal set;
		}

		/// <summary>
		/// Gets the unique ID of the message, if available.
		/// </summary>
		/// <value>The uid of the message.</value>
		public string Uid {
			get; internal set;
		}

		/// <summary>
		/// Gets the index of the message.
		/// </summary>
		/// <value>The index of the message.</value>
		public int Index {
			get; private set;
		}

		#region GMail extension properties

		/// <summary>
		/// Gets the GMail message identifier.
		/// </summary>
		/// <value>The GMail message identifier.</value>
		public ulong GMailMessageId {
			get; internal set;
		}

		/// <summary>
		/// Gets the GMail thread identifier.
		/// </summary>
		/// <value>The GMail thread identifier.</value>
		public ulong GMailThreadId {
			get; internal set;
		}

		#endregion
	}
}
