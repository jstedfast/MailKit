//
// NumericSearchQuery.cs
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

namespace MailKit.Search {
	/// <summary>
	/// A numeric search query.
	/// </summary>
	/// <remarks>
	/// A numeric search query.
	/// </remarks>
	public class NumericSearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.NumericSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new numeric search query.
		/// </remarks>
		/// <param name="term">The search term.</param>
		/// <param name="value">The numeric value.</param>
		public NumericSearchQuery (SearchTerm term, ulong value) : base (term)
		{
			Value = value;
		}

		/// <summary>
		/// Gets the numeric value to match against.
		/// </summary>
		/// <remarks>
		/// Gets the numeric value to match against.
		/// </remarks>
		/// <value>The numeric value.</value>
		public ulong Value {
			get; private set;
		}
	}
}
