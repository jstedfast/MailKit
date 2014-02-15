//
// MessageSorter.cs
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
using System.Linq;
using System.Collections.Generic;

using MailKit.Search;

namespace MailKit {
	/// <summary>
	/// Sorts messages.
	/// </summary>
	public static class MessageSorter
	{
		class MessageComparer<T> : IComparer<T> where T : ISortable
		{
			readonly OrderBy[] orderBy;

			public MessageComparer (OrderBy[] orderBy)
			{
				this.orderBy = orderBy;
			}

			#region IComparer implementation

			public int Compare (T x, T y)
			{
				int cmp = 0;

				for (int i = 0; i < orderBy.Length; i++) {
					switch (orderBy[i].Type) {
					case OrderByType.Arrival:
						cmp = x.SortableIndex.CompareTo (y.SortableIndex);
						break;
					case OrderByType.Cc:
						cmp = string.Compare (x.SortableCc, y.SortableCc, StringComparison.InvariantCultureIgnoreCase);
						break;
					case OrderByType.Date:
						cmp = x.SortableDate.CompareTo (y.SortableDate);
						break;
					case OrderByType.From:
						cmp = string.Compare (x.SortableFrom, y.SortableFrom, StringComparison.InvariantCultureIgnoreCase);
						break;
					case OrderByType.Size:
						cmp = x.SortableSize.CompareTo (y.SortableSize);
						break;
					case OrderByType.Subject:
						cmp = string.Compare (x.SortableSubject, y.SortableSubject, StringComparison.InvariantCultureIgnoreCase);
						break;
					case OrderByType.To:
						cmp = string.Compare (x.SortableTo, y.SortableTo, StringComparison.InvariantCultureIgnoreCase);
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
		/// Sortsthe messages by the specified ordering.
		/// </summary>
		/// <param name="messages">The messages to sort.</param>
		/// <param name="orderBy">The sort ordering.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="orderBy"/> is an empty list.
		/// </exception>
		public static IList<T> Sort<T> (IEnumerable<T> messages, OrderBy[] orderBy) where T : ISortable
		{
			if (messages == null)
				throw new ArgumentNullException ("messages");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			if (orderBy.Length == 0)
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
