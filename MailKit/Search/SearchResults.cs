//
// SearchResults.cs
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

using System.Collections.Generic;

namespace MailKit.Search {
	/// <summary>
	/// The results of a search.
	/// </summary>
	/// <remarks>
	/// The results of a search.
	/// </remarks>
	public class SearchResults
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Search.SearchResults"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SearchResults"/>.
		/// </remarks>
		public SearchResults ()
		{
			UniqueIds = new UniqueId[0];
		}

		/// <summary>
		/// Get or set the unique identifiers of the messages that matched the search query.
		/// </summary>
		/// <remarks>
		/// Gets or sets the unique identifiers of the messages that matched the search query.
		/// </remarks>
		/// <value>The unique identifiers.</value>
		public IList<UniqueId> UniqueIds {
			get; set;
		}

		/// <summary>
		/// Get or set the number of messages that matched the search query.
		/// </summary>
		/// <remarks>
		/// Gets or sets the number of messages that matched the search query.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get; set;
		}

		/// <summary>
		/// Get or set the minimum unique identifier that matched the search query.
		/// </summary>
		/// <remarks>
		/// Gets or sets the minimum unique identifier that matched the search query.
		/// </remarks>
		/// <value>The minimum unique identifier.</value>
		public UniqueId? Min {
			get; set;
		}

		/// <summary>
		/// Get or set the maximum unique identifier that matched the search query.
		/// </summary>
		/// <remarks>
		/// Gets or sets the maximum unique identifier that matched the search query.
		/// </remarks>
		/// <value>The maximum unique identifier.</value>
		public UniqueId? Max {
			get; set;
		}

		/// <summary>
		/// Gets or sets the mod-sequence identifier of the messages that matched the search query.
		/// </summary>
		/// <remarks>
		/// Gets or sets the mod-sequence identifier of the messages that matched the search query.
		/// </remarks>
		/// <value>The mod-sequence identifier.</value>
		public ulong? ModSeq {
			get; set;
		}

		/// <summary>
		/// Gets or sets the relevancy scores of the messages that matched the search query.
		/// </summary>
		/// <remarks>
		/// Gets or sets the relevancy scores of the messages that matched the search query.
		/// </remarks>
		/// <value>The relevancy scores.</value>
		public IList<byte> Relevancy {
			get; set;
		}
	}
}
