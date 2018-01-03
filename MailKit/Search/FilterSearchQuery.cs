//
// FilterSearchQuery.cs
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
	/// A filter-based search query.
	/// </summary>
	/// <remarks>
	/// A filter-based search query.
	/// </remarks>
	public class FilterSearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.FilterSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// A search query that references a predefined filter.
		/// </remarks>
		/// <param name="name">The name of the filter.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="name"/> is empty.
		/// </exception>
		public FilterSearchQuery (string name) : base (SearchTerm.Filter)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (name.Length == 0)
				throw new ArgumentException ("The filter name cannot be empty.", nameof (name));

			Name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.FilterSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// A search query that references a predefined filter.
		/// </remarks>
		/// <param name="filter">The metadata tag representing the filter.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="filter"/> does not reference a valid filter.
		/// </exception>
		public FilterSearchQuery (MetadataTag filter) : base (SearchTerm.Filter)
		{
			if (filter.Id.StartsWith ("/private/filters/values/", StringComparison.Ordinal))
				Name = filter.Id.Substring ("/private/filters/values/".Length);
			else if (filter.Id.StartsWith ("/shared/filters/values/", StringComparison.Ordinal))
				Name = filter.Id.Substring ("/shared/filters/values/".Length);
			else
				throw new ArgumentException ("Metadata tag does not reference a valid filter.", nameof (filter));
		}

		/// <summary>
		/// Get the name of the filter.
		/// </summary>
		/// <remarks>
		/// Gets the name of the filter.
		/// </remarks>
		/// <value>The name of the filter.</value>
		public string Name {
			get; private set;
		}
	}
}
