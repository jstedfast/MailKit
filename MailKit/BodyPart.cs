//
// BodyPart.cs
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
	/// A body part of a message.
	/// </summary>
	public class BodyPart
	{
		internal BodyPart ()
		{
		}

		/// <summary>
		/// Gets the Content-Type of the body part.
		/// </summary>
		/// <value>The content type.</value>
		public ContentType ContentType {
			get; internal set;
		}

		/// <summary>
		/// Gets the Content-Disposition of the body part.
		/// </summary>
		/// <value>The content disposition.</value>
		public ContentDisposition ContentDisposition {
			get; internal set;
		}

		/// <summary>
		/// Gets the Content-Transfer-Encoding of the body part.
		/// </summary>
		/// <value>The content transfer encoding.</value>
		public string ContentTransferEncoding {
			get; internal set;
		}

		/// <summary>
		/// Gets the Content-Id of the body part.
		/// </summary>
		/// <value>The content identifier.</value>
		public string ContentId {
			get; internal set;
		}

		/// <summary>
		/// Gets the Content-Description of the body part.
		/// </summary>
		/// <value>The content description.</value>
		public string ContentDescription {
			get; internal set;
		}

		/// <summary>
		/// Gets the size of the body part.
		/// </summary>
		/// <value>The number of octets.</value>
		public uint Octets {
			get; internal set;
		}
	}
}
