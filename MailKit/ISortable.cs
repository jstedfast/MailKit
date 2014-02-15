//
// ISortable.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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
	/// An interface for message sorting.
	/// </summary>
	public interface ISortable
	{
		/// <summary>
		/// Gets a value indicating whether this instance can be sorted.
		/// </summary>
		/// <value><c>true</c> if this instance can be sorted; otherwise, <c>false</c>.</value>
		bool CanSort { get; }

		/// <summary>
		/// Gets the message index in the folder it belongs to.
		/// </summary>
		/// <value>The index.</value>
		int SortableIndex { get; }

		/// <summary>
		/// Gets the Cc header value.
		/// </summary>
		/// <value>The Cc header value.</value>
		string SortableCc { get; }

		/// <summary>
		/// Gets the Date header value.
		/// </summary>
		/// <value>The date.</value>
		DateTimeOffset SortableDate { get; }

		/// <summary>
		/// Gets the From header value.
		/// </summary>
		/// <value>The From header value.</value>
		string SortableFrom { get; }

		/// <summary>
		/// Gets the size of the message, in bytes.
		/// </summary>
		/// <value>The size of the message, in bytes.</value>
		uint SortableSize { get; }

		/// <summary>
		/// Gets the Subject header value.
		/// </summary>
		/// <value>The Subject header value.</value>
		string SortableSubject { get; }

		/// <summary>
		/// Gets the To header value.
		/// </summary>
		/// <value>The To header value.</value>
		string SortableTo { get; }
	}
}
