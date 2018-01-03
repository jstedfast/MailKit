//
// MessageSorter.cs
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
using System.Linq;
using System.Collections.Generic;

using MimeKit;
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
		class MessageComparer<T> : IComparer<T> where T : IMessageSummary
		{
			readonly IList<OrderBy> orderBy;

			public MessageComparer (IList<OrderBy> orderBy)
			{
				this.orderBy = orderBy;
			}

			#region IComparer implementation

			static int CompareDisplayNames (InternetAddressList list1, InternetAddressList list2)
			{
				var m1 = list1.Mailboxes.GetEnumerator ();
				var m2 = list2.Mailboxes.GetEnumerator ();
				bool n1 = m1.MoveNext ();
				bool n2 = m2.MoveNext ();

				while (n1 && n2) {
					var name1 = m1.Current.Name ?? string.Empty;
					var name2 = m2.Current.Name ?? string.Empty;
					int cmp;

					if ((cmp = string.Compare (name1, name2, StringComparison.OrdinalIgnoreCase)) != 0)
						return cmp;

					n1 = m1.MoveNext ();
					n2 = m2.MoveNext ();
				}

				return n1 ? 1 : (n2 ? -1  : 0);
			}

			static int CompareMailboxAddresses (InternetAddressList list1, InternetAddressList list2)
			{
				var m1 = list1.Mailboxes.GetEnumerator ();
				var m2 = list2.Mailboxes.GetEnumerator ();
				bool n1 = m1.MoveNext ();
				bool n2 = m2.MoveNext ();

				while (n1 && n2) {
					int cmp;

					if ((cmp = string.Compare (m1.Current.Address, m2.Current.Address, StringComparison.OrdinalIgnoreCase)) != 0)
						return cmp;

					n1 = m1.MoveNext ();
					n2 = m2.MoveNext ();
				}

				return n1 ? 1 : (n2 ? -1  : 0);
			}

			public int Compare (T x, T y)
			{
				int cmp = 0;

				for (int i = 0; i < orderBy.Count; i++) {
					switch (orderBy[i].Type) {
					case OrderByType.Arrival:
						cmp = x.Index.CompareTo (y.Index);
						break;
					case OrderByType.Cc:
						cmp = CompareMailboxAddresses (x.Envelope.Cc, y.Envelope.Cc);
						break;
					case OrderByType.Date:
						cmp = x.Date.CompareTo (y.Date);
						break;
					case OrderByType.DisplayFrom:
						cmp = CompareDisplayNames (x.Envelope.From, y.Envelope.From);
						break;
					case OrderByType.From:
						cmp = CompareMailboxAddresses (x.Envelope.From, y.Envelope.From);
						break;
					case OrderByType.Size:
						var xsize = x.Size ?? 0;
						var ysize = y.Size ?? 0;

						cmp = xsize.CompareTo (ysize);
						break;
					case OrderByType.Subject:
						var xsubject = x.Envelope.Subject ?? string.Empty;
						var ysubject = y.Envelope.Subject ?? string.Empty;

						cmp = string.Compare (xsubject, ysubject, StringComparison.OrdinalIgnoreCase);
						break;
					case OrderByType.DisplayTo:
						cmp = CompareDisplayNames (x.Envelope.To, y.Envelope.To);
						break;
					case OrderByType.To:
						cmp = CompareMailboxAddresses (x.Envelope.To, y.Envelope.To);
						break;
					case OrderByType.ModSeq:
						var xmodseq = x.ModSeq ?? 0;
						var ymodseq = y.ModSeq ?? 0;

						cmp = xmodseq.CompareTo (ymodseq);
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

		static MessageSummaryItems GetMessageSummaryItems (IList<OrderBy> orderBy)
		{
			var items = MessageSummaryItems.None;

			for (int i = 0; i < orderBy.Count; i++) {
				switch (orderBy[i].Type) {
				case OrderByType.Arrival:
					break;
				case OrderByType.Cc:
				case OrderByType.Date:
				case OrderByType.DisplayFrom:
				case OrderByType.DisplayTo:
				case OrderByType.From:
				case OrderByType.Subject:
				case OrderByType.To:
					items |= MessageSummaryItems.Envelope;
					break;
				case OrderByType.ModSeq:
					items |= MessageSummaryItems.ModSeq;
					break;
				case OrderByType.Size:
					items |= MessageSummaryItems.Size;
					break;
				}
			}

			return items;
		}

		/// <summary>
		/// Sorts the messages by the specified ordering.
		/// </summary>
		/// <remarks>
		/// Sorts the messages by the specified ordering.
		/// </remarks>
		/// <returns>The sorted messages.</returns>
		/// <typeparam name="T">The message items must implement the <see cref="IMessageSummary"/> interface.</typeparam>
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
		public static IList<T> Sort<T> (this IEnumerable<T> messages, IList<OrderBy> orderBy) where T : IMessageSummary
		{
			if (messages == null)
				throw new ArgumentNullException (nameof (messages));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			var requiredFields = GetMessageSummaryItems (orderBy);
			var list = new List<T> ();

			foreach (var message in messages) {
				if ((message.Fields & requiredFields) != requiredFields)
					throw new ArgumentException ("One or more messages is missing information needed for sorting.", nameof (messages));

				list.Add (message);
			}

			if (list.Count < 2)
				return list;

			var comparer = new MessageComparer<T> (orderBy);

			list.Sort (comparer);

			return list;
		}

		/// <summary>
		/// Sorts the messages by the specified ordering.
		/// </summary>
		/// <remarks>
		/// Sorts the messages by the specified ordering.
		/// </remarks>
		/// <returns>The sorted messages.</returns>
		/// <typeparam name="T">The message items must implement the <see cref="IMessageSummary"/> interface.</typeparam>
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
		public static void Sort<T> (this List<T> messages, IList<OrderBy> orderBy) where T : IMessageSummary
		{
			if (messages == null)
				throw new ArgumentNullException (nameof (messages));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			var requiredFields = GetMessageSummaryItems (orderBy);

			for (int i = 0; i < messages.Count; i++) {
				if ((messages[i].Fields & requiredFields) != requiredFields)
					throw new ArgumentException ("One or more messages is missing information needed for sorting.", nameof (messages));
			}

			if (messages.Count < 2)
				return;

			var comparer = new MessageComparer<T> (orderBy);

			messages.Sort (comparer);
		}
	}
}
