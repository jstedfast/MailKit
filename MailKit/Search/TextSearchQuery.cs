//
// TextSearchQuery.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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

namespace MailKit.Search
{
	/// <summary>
	/// A text-based search query.
	/// </summary>
	/// <remarks>
	/// A text-based search query.
	/// </remarks>
	public class TextSearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.TextSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new text-based search query.
		/// </remarks>
		/// <param name="term">The search term.</param>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public TextSearchQuery (SearchTerm term, string text) : base (term)
		{
			if (text == null)
				throw new ArgumentNullException (nameof (text));

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", nameof (text));

			Text = text;
		}

		/// <summary>
		/// Gets the text to match against.
		/// </summary>
		/// <remarks>
		/// Gets the text to match against.
		/// </remarks>
		/// <value>The text.</value>
		public string Text {
			get; private set;
		}
	}
}
