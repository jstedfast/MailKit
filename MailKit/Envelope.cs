//
// Envelope.cs
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A message envelope containing a brief summary of the message.
	/// </summary>
	public class Envelope
	{
		internal Envelope ()
		{
		}

		public InternetAddressList From {
			get; internal set;
		}

		public InternetAddressList Sender {
			get; internal set;
		}

		public InternetAddressList ReplyTo {
			get; internal set;
		}

		public InternetAddressList To {
			get; internal set;
		}

		public InternetAddressList Cc {
			get; internal set;
		}

		public InternetAddressList Bcc {
			get; internal set;
		}

		public MessageIdList InReplyTo {
			get; internal set;
		}

		public DateTimeOffset? Date {
			get; internal set;
		}

		public string MessageId {
			get; internal set;
		}

		public string Subject {
			get; internal set;
		}
	}
}
	