//
// Envelope.cs
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A message envelope containing a brief summary of the message.
	/// </summary>
	/// <remarks>
	/// The envelope of a message contains information such as the
	/// date the message was sent, the subject of the message,
	/// the sender of the message, who the message was sent to,
	/// which message(s) the message may be in reply to,
	/// and the message id.
	/// </remarks>
	public class Envelope
	{
		internal Envelope ()
		{
		}

		/// <summary>
		/// Gets the address(es) that the message is from.
		/// </summary>
		/// <value>The address(es) that the message is from.</value>
		public InternetAddressList From {
			get; internal set;
		}

		/// <summary>
		/// Gets the actual sender(s) of the message.
		/// </summary>
		/// <remarks>
		/// The senders may differ from the addresses in <see cref="From"/> if
		/// the message was sent by someone on behalf of someone else.
		/// </remarks>
		/// <value>The actual sender(s) of the message.</value>
		public InternetAddressList Sender {
			get; internal set;
		}

		/// <summary>
		/// Gets the address(es) that replies should be sent to.
		/// </summary>
		/// <remarks>
		/// The senders of the message may prefer that replies are sent
		/// somewhere other than the address they used to send the message.
		/// </remarks>
		/// <value>The address(es) that replies should be sent to.</value>
		public InternetAddressList ReplyTo {
			get; internal set;
		}

		/// <summary>
		/// Gets the list of addresses that the message was sent to.
		/// </summary>
		/// <value>The address(es) that the message was sent to.</value>
		public InternetAddressList To {
			get; internal set;
		}

		/// <summary>
		/// Gest the list of addresses that the message was carbon-copied to.
		/// </summary>
		/// <value>The address(es) that the message was carbon-copied to.</value>
		public InternetAddressList Cc {
			get; internal set;
		}

		/// <summary>
		/// Gets the list of addresses that the message was blind-carbon-copied to.
		/// </summary>
		/// <value>The address(es) that the message was carbon-copied to.</value>
		public InternetAddressList Bcc {
			get; internal set;
		}

		/// <summary>
		/// The Message-Id that the message is replying to.
		/// </summary>
		/// <value>The Message-Id that the message is replying to.</value>
		public string InReplyTo {
			get; internal set;
		}

		/// <summary>
		/// Gets the date that the message was sent on, if available.
		/// </summary>
		/// <value>The date the message was sent.</value>
		public DateTimeOffset? Date {
			get; internal set;
		}

		/// <summary>
		/// Gets the ID of the message.
		/// </summary>
		/// <value>The message identifier.</value>
		public string MessageId {
			get; internal set;
		}

		/// <summary>
		/// Gets the subject of the message.
		/// </summary>
		/// <value>The subject.</value>
		public string Subject {
			get; internal set;
		}
	}
}
	