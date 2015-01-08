//
// IThreadable.cs
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An interface for a threading messages.
	/// </summary>
	/// <remarks>
	/// An interface for a threading messages.
	/// </remarks>
	public interface IThreadable : ISortable
	{
		/// <summary>
		/// Gets whether the message can be threaded.
		/// </summary>
		/// <remarks>
		/// Gets whether the message can be threaded.
		/// </remarks>
		/// <value><c>true</c> if the message can be threaded; otherwise, <c>false</c>.</value>
		bool CanThread { get; }

		/// <summary>
		/// Gets the threadable subject.
		/// </summary>
		/// <remarks>
		/// A normalized Subject header value where prefixes such as
		/// "Re:", "Re[#]:", etc have been pruned.
		/// </remarks>
		/// <value>The threadable subject.</value>
		string ThreadableSubject { get; }

		/// <summary>
		/// Gets a value indicating whether this instance is a reply.
		/// </summary>
		/// <remarks>
		/// This value should be based on whether the message subject contained any "Re:" or "Fwd:" prefixes.
		/// </remarks>
		/// <value><c>true</c> if this instance is a reply; otherwise, <c>false</c>.</value>
		bool IsThreadableReply { get; }

		/// <summary>
		/// Gets the threadable message identifier.
		/// </summary>
		/// <remarks>
		/// This value should be the canonicalized Message-Id header value
		/// without the angle brackets.
		/// </remarks>
		/// <value>The threadable message identifier.</value>
		string ThreadableMessageId { get; }

		/// <summary>
		/// Gets the threadable references.
		/// </summary>
		/// <remarks>
		/// This value should be the list of canonicalized Message-Ids
		/// found in the In-Reply-To and References headers.
		/// </remarks>
		/// <value>The threadable references.</value>
		MessageIdList ThreadableReferences { get; }

		/// <summary>
		/// Gets the unique identifier.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifier.
		/// </remarks>
		/// <value>The unique identifier.</value>
		UniqueId ThreadableUniqueId { get; }
	}
}
