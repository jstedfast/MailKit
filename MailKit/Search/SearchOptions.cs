//
// SearchOptions.cs
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
	/// Advanced search options.
	/// </summary>
	/// <remarks>
	/// Advanced search options.
	/// </remarks>
	[Flags]
	public enum SearchOptions {
		/// <summary>
		/// No options specified.
		/// </summary>
		None      = 0,

		/// <summary>
		/// Returns all of the matching unique identifiers.
		/// </summary>
		All       = 1 << 0,

		/// <summary>
		/// Returns the number of messages that match the search query.
		/// </summary>
		Count     = 1 << 1,

		/// <summary>
		/// Returns the minimum unique identifier of the messages that match the search query.
		/// </summary>
		Min       = 1 << 2,

		/// <summary>
		/// Returns the maximum unique identifier of the messages that match the search query.
		/// </summary>
		Max       = 1 << 3,

		/// <summary>
		/// Returns the relevancy scores of the messages that match the query. Can only be used
		/// when using FUZZY search.
		/// </summary>
		Relevancy = 1 << 4
	}
}
