//
// MessageSorter.cs
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

using System;
using System.Collections.Generic;

using MailKit.Search;

namespace MailKit {
	/// <summary>
	/// Routines for sorting messages.
	/// </summary>
	/// <remarks>
	/// Routines for sorting messages.
	/// </remarks>
	public static class MessageSorter
	{
		class MessageComparer<T> : IComparer<T> where T : ISortable
		{
			readonly IList<OrderBy> orderBy;

			public MessageComparer (IList<OrderBy> orderBy)
			{
				this.orderBy = orderBy;
			}

			#region IComparer implementation

			public int Compare (T x, T y)
			{
				int cmp = 0;

				for (int i = 0; i < orderBy.Count; i++) {
					switch (orderBy[i].Type) {
					case OrderByType.Arrival:
						cmp = x.Index.CompareTo (y.Index);
						break;
					case OrderByType.Cc:
						cmp = string.Compare (x.Cc, y.Cc, StringComparison.OrdinalIgnoreCase);
						break;
					case OrderByType.Date:
						cmp = x.Date.CompareTo (y.Date);
						break;
					case OrderByType.From:
						cmp = string.Compare (x.From, y.From, StringComparison.OrdinalIgnoreCase);
						break;
					case OrderByType.Size:
						cmp = x.Size.CompareTo (y.Size);
						break;
					case OrderByType.Subject:
						cmp = string.Compare (x.Subject, y.Subject, StringComparison.OrdinalIgnoreCase);
						break;
					case OrderByType.To:
						cmp = string.Compare (x.To, y.To, StringComparison.OrdinalIgnoreCase);
						break;
					}

					if (cmp == 0)
						continue;

					return orderBy[i].Order == SortOrder.Descending ? cmp * -1 : cmp;
				}

				return cmp;
			}

			#endregion
		}

		/// <summary>
		/// Sorts the messages by the specified ordering.
		/// </summary>
		/// <remarks>
		/// Sorts the messages by the specified ordering.
		/// </remarks>
		/// <returns>The sorted messages.</returns>
		/// <typeparam name="T">The message items must implement the <see cref="ISortable"/> interface.</typeparam>
		/// <param name="messages">The messages to sort.</param>
		/// <param name="orderBy">The sort ordering.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="messages"/> contains one or more items that is missing information needed for sorting.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is an empty list.</para>
		/// </exception>
		public static IList<T> Sort<T> (IEnumerable<T> messages, IList<OrderBy> orderBy) where T : ISortable
		{
			if (messages == null)
				throw new ArgumentNullException ("messages");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", "orderBy");

			var list = new List<T> ();
			foreach (var message in messages) {
				if (!message.CanSort)
					throw new ArgumentException ("One or more messages is missing information needed for sorting.", "messages");

				list.Add (message);
			}

			if (list.Count < 2)
				return list;

			var comparer = new MessageComparer<T> (orderBy);

			list.Sort (comparer);

			return list;
		}
	}
}
