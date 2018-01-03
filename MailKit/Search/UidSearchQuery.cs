//
// UidSearchQuery.cs
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
using System.Collections.Generic;

namespace MailKit.Search
{
	/// <summary>
	/// A unique identifier-based search query.
	/// </summary>
	/// <remarks>
	/// A unique identifier-based search query.
	/// </remarks>
	public class UidSearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.UidSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique identifier-based search query.
		/// </remarks>
		/// <param name="uids">The unique identifiers to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> is empty.
		/// </exception>
		public UidSearchQuery (IList<UniqueId> uids) : base (SearchTerm.Uid)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (uids.Count == 0)
				throw new ArgumentException ("Cannot search for an empty set of unique identifiers.", nameof (uids));

			Uids = uids;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.UidSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique identifier-based search query.
		/// </remarks>
		/// <param name="uid">The unique identifier to match against.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is an invalid unique identifier.
		/// </exception>
		public UidSearchQuery (UniqueId uid) : base (SearchTerm.Uid)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("Cannot search for an invalid unique identifier.", nameof (uid));

			Uids = new UniqueIdSet (SortOrder.Ascending);
			Uids.Add (uid);
		}

		/// <summary>
		/// Gets the unique identifiers to match against.
		/// </summary>
		/// <remarks>
		/// Gets the unique identifiers to match against.
		/// </remarks>
		/// <value>The unique identifiers.</value>
		public new IList<UniqueId> Uids {
			get; private set;
		}
	}
}
