//
// HeaderSearchQuery.cs
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

namespace MailKit.Search {
	/// <summary>
	/// A header-based search query.
	/// </summary>
	/// <remarks>
	/// A header-based search query.
	/// </remarks>
	public class HeaderSearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.HeaderSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new header search query.
		/// </remarks>
		/// <param name="field">The header field name.</param>
		/// <param name="value">The value to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="field"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="value"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="field"/> is empty.
		/// </exception>
		public HeaderSearchQuery (string field, string value) : base (SearchTerm.HeaderContains)
		{
			if (field == null)
				throw new ArgumentNullException (nameof (field));

			if (field.Length == 0)
				throw new ArgumentException ("Cannot search an empty header field name.", nameof (field));

			if (value == null)
				throw new ArgumentNullException (nameof (value));

			Field = field;
			Value = value;
		}

		/// <summary>
		/// Gets the header field name.
		/// </summary>
		/// <remarks>
		/// Gets the header field name.
		/// </remarks>
		/// <value>The header field.</value>
		public string Field {
			get; private set;
		}

		/// <summary>
		/// Gets the value to match against.
		/// </summary>
		/// <remarks>
		/// Gets the value to match against.
		/// </remarks>
		/// <value>The value.</value>
		public string Value {
			get; private set;
		}
	}
}
