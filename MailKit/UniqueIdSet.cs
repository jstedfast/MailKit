//
// UniqueIdSet.cs
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
using System.Text;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

using MailKit.Search;

namespace MailKit {
	/// <summary>
	/// A set of unique identifiers.
	/// </summary>
	/// <remarks>
	/// When dealing with a large number of unique identifiers, it may be more efficient to use a
	/// <see cref="UniqueIdSet"/> than a typical IList&lt;<see cref="UniqueId"/>&gt;.
	/// </remarks>
	public class UniqueIdSet : IList<UniqueId>
	{
		struct Range
		{
			public uint Start;
			public uint End;

			public Range (uint start, uint end)
			{
				Start = start;
				End = end;
			}

			public int Count {
				get { return (int) (Start <= End ? End - Start : Start - End) + 1; }
			}

			public bool Contains (uint uid)
			{
				if (Start <= End)
					return uid >= Start && uid <= End;

				return uid <= Start && uid >= End;
			}

			public int IndexOf (uint uid)
			{
				if (Start <= End) {
					if (uid < Start || uid > End)
						return -1;

					return (int) (uid - Start);
				}

				if (uid > Start || uid < End)
					return -1;

				return (int) (Start - uid);
			}

			public uint this [int index] {
				get {
					return Start <= End ? Start + (uint) index : Start - (uint) index;
				}
			}

			public IEnumerator<uint> GetEnumerator ()
			{
				if (Start <= End) {
					for (uint uid = Start; uid <= End; uid++)
						yield return uid;
				} else {
					for (uint uid = Start; uid >= End; uid--)
						yield return uid;
				}

				yield break;
			}

			public override string ToString ()
			{
				if (Start == End)
					return Start.ToString (CultureInfo.InvariantCulture);

				if (Start <= End && End == uint.MaxValue)
					return string.Format (CultureInfo.InvariantCulture, "{0}:*", Start);

				return string.Format (CultureInfo.InvariantCulture, "{0}:{1}", Start, End);
			}
		}

		readonly List<Range> ranges = new List<Range> ();
		long count;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique identifier set.
		/// </remarks>
		/// <param name="validity">The uid validity.</param>
		/// <param name="order">The sorting order to use for the unique identifiers.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="order"/> is invalid.
		/// </exception>
		public UniqueIdSet (uint validity, SortOrder order = SortOrder.None)
		{
			switch (order) {
			case SortOrder.Descending:
			case SortOrder.Ascending:
			case SortOrder.None:
				break;
			default:
				throw new ArgumentOutOfRangeException (nameof (order));
			}

			Validity = validity;
			SortOrder = order;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique identifier set.
		/// </remarks>
		/// <param name="order">The sorting order to use for the unique identifiers.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="order"/> is invalid.
		/// </exception>
		public UniqueIdSet (SortOrder order = SortOrder.None) : this (0, order)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of unique identifier set containing the specified uids.
		/// </remarks>
		/// <param name="uids">An initial set of unique ids.</param>
		/// <param name="order">The sorting order to use for the unique identifiers.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="order"/> is invalid.
		/// </exception>
		public UniqueIdSet (IEnumerable<UniqueId> uids, SortOrder order = SortOrder.None) : this (order)
		{
			foreach (var uid in uids)
				Add (uid);
		}

		/// <summary>
		/// Gets the sort order of the unique identifiers.
		/// </summary>
		/// <remarks>
		/// Gets the sort order of the unique identifiers.
		/// </remarks>
		/// <value>The sort order.</value>
		public SortOrder SortOrder {
			get; private set;
		}

		/// <summary>
		/// Gets the validity, if non-zero.
		/// </summary>
		/// <remarks>
		/// Gets the UidValidity of the containing folder.
		/// </remarks>
		/// <value>The UidValidity of the containing folder.</value>
		public uint Validity {
			get; private set;
		}

		#region ICollection implementation

		/// <summary>
		/// Get the number of unique ids in the set.
		/// </summary>
		/// <remarks>
		/// Gets the number of unique ids in the set.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return (int) Math.Min (count, int.MaxValue); }
		}

		/// <summary>
		/// Get whether or not the set is read only.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the set is read-only.
		/// </remarks>
		/// <value><c>true</c> if the set is read only; otherwise, <c>false</c>.</value>
		public bool IsReadOnly {
			get { return false; }
		}

		int BinarySearch (uint uid)
		{
			int min = 0, max = ranges.Count;

			if (max == 0)
				return -1;

			do {
				int i = min + ((max - min) / 2);

				if (SortOrder == SortOrder.Ascending) {
					// sorted ascending: 1:3,5:7,9
					if (uid >= ranges[i].Start) {
						if (uid <= ranges[i].End)
							return i;

						min = i + 1;
					} else {
						max = i;
					}
				} else {
					// sorted descending: 9,7:5,3:1
					if (uid >= ranges[i].End) {
						if (uid <= ranges[i].Start)
							return i;

						max = i;
					} else {
						min = i + 1;
					}
				}
			} while (min < max);

			return -1;
		}

		int IndexOfRange (uint uid)
		{
			if (SortOrder != SortOrder.None)
				return BinarySearch (uid);

			for (int i = 0; i < ranges.Count; i++) {
				if (ranges[i].Contains (uid))
					return i;
			}

			return -1;
		}

		void BinaryInsertAscending (uint uid)
		{
			int min = 0, max = ranges.Count;
			int i;

			do {
				i = min + ((max - min) / 2);

				if (uid >= ranges[i].Start) {
					if (uid <= ranges[i].End)
						return;

					if (uid == ranges[i].End + 1) {
						if (i + 1 < ranges.Count && uid + 1 >= ranges[i + 1].Start) {
							// merge the 2 ranges together
							ranges[i] = new Range (ranges[i].Start, ranges[i + 1].End);
							ranges.RemoveAt (i + 1);
							count++;
							return;
						}

						ranges[i] = new Range (ranges[i].Start, uid);
						count++;
						return;
					}

					min = i + 1;
					i = min;
				} else {
					if (uid == ranges[i].Start - 1) {
						if (i > 0 && uid - 1 <= ranges[i - 1].End) {
							// merge the 2 ranges together
							ranges[i - 1] = new Range (ranges[i - 1].Start, ranges[i].End);
							ranges.RemoveAt (i);
							count++;
							return;
						}

						ranges[i] = new Range (uid, ranges[i].End);
						count++;
						return;
					}

					max = i;
				}
			} while (min < max);

			var range = new Range (uid, uid);

			if (i < ranges.Count)
				ranges.Insert (i, range);
			else
				ranges.Add (range);

			count++;
		}

		void BinaryInsertDescending (uint uid)
		{
			int min = 0, max = ranges.Count;
			int i;

			do {
				i = min + ((max - min) / 2);

				if (uid <= ranges[i].Start) {
					if (uid >= ranges[i].End)
						return;

					if (uid == ranges[i].End - 1) {
						if (i + 1 < ranges.Count && uid - 1 <= ranges[i + 1].Start) {
							// merge the 2 ranges together
							ranges[i] = new Range (ranges[i].Start, ranges[i + 1].End);
							ranges.RemoveAt (i + 1);
							count++;
							return;
						}

						ranges[i] = new Range (ranges[i].Start, uid);
						count++;
						return;
					}

					min = i + 1;
					i = min;
				} else {
					if (uid == ranges[i].Start + 1) {
						if (i > 0 && uid + 1 >= ranges[i - 1].End) {
							// merge the 2 ranges together
							ranges[i - 1] = new Range (ranges[i - 1].Start, ranges[i].End);
							ranges.RemoveAt (i);
							count++;
							return;
						}

						ranges[i] = new Range (uid, ranges[i].End);
						count++;
						return;
					}

					max = i;
				}
			} while (min < max);

			var range = new Range (uid, uid);

			if (i < ranges.Count)
				ranges.Insert (i, range);
			else
				ranges.Add (range);

			count++;
		}

		void Append (uint uid)
		{
			if (IndexOfRange (uid) != -1)
				return;

			count++;

			if (ranges.Count > 0) {
				int index = ranges.Count - 1;
				var range = ranges[index];

				if (range.Start == range.End) {
					if (uid == range.End + 1 || uid == range.End - 1) {
						ranges[index] = new Range (range.Start, uid);
						return;
					}
				} else if (range.Start < range.End) {
					if (uid == range.End + 1) {
						ranges[index] = new Range (range.Start, uid);
						return;
					}
				} else if (range.Start > range.End) {
					if (uid == range.End - 1) {
						ranges[index] = new Range (range.Start, uid);
						return;
					}
				}
			}

			ranges.Add (new Range (uid, uid));
		}

		/// <summary>
		/// Adds the unique identifier to the set.
		/// </summary>
		/// <remarks>
		/// Adds the unique identifier to the set.
		/// </remarks>
		/// <param name="uid">The unique identifier to add.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		public void Add (UniqueId uid)
		{
			if (!uid.IsValid)
				throw new ArgumentException ("Invalid unique identifier.", nameof (uid));

			if (ranges.Count == 0) {
				ranges.Add (new Range (uid.Id, uid.Id));
				count++;
				return;
			}

			switch (SortOrder) {
			case SortOrder.Descending:
				BinaryInsertDescending (uid.Id);
				break;
			case SortOrder.Ascending:
				BinaryInsertAscending (uid.Id);
				break;
			default:
				Append (uid.Id);
				break;
			}
		}

		/// <summary>
		/// Adds all of the uids to the set.
		/// </summary>
		/// <remarks>
		/// Adds all of the uids to the set.
		/// </remarks>
		/// <param name="uids">The collection of uids.</param>
		public void AddRange (IEnumerable<UniqueId> uids)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			foreach (var uid in uids)
				Add (uid);
		}

		/// <summary>
		/// Clears the list.
		/// </summary>
		/// <remarks>
		/// Clears the list.
		/// </remarks>
		/// <exception cref="System.InvalidOperationException">
		/// The collection is readonly.
		/// </exception>
		public void Clear ()
		{
			ranges.Clear ();
			count = 0;
		}

		/// <summary>
		/// Checks if the set contains the specified unique id.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the set contains the specified unique id.
		/// </remarks>
		/// <returns><value>true</value> if the specified unique identifier is in the set; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique id.</param>
		public bool Contains (UniqueId uid)
		{
			return IndexOfRange (uid.Id) != -1;
		}

		/// <summary>
		/// Copies all of the unique ids in the set to the specified array.
		/// </summary>
		/// <remarks>
		/// Copies all of the unique ids within the set into the array,
		/// starting at the specified array index.
		/// </remarks>
		/// <param name="array">The array to copy the unique ids to.</param>
		/// <param name="arrayIndex">The index into the array.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="array"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="arrayIndex"/> is out of set.
		/// </exception>
		public void CopyTo (UniqueId[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));

			if (arrayIndex < 0 || arrayIndex > (array.Length - Count))
				throw new ArgumentOutOfRangeException (nameof (arrayIndex));

			int index = arrayIndex;

			for (int i = 0; i < ranges.Count; i++) {
				foreach (var uid in ranges[i])
					array[index++] = new UniqueId (Validity, uid);
			}
		}

		void Remove (int index, uint uid)
		{
			var range = ranges[index];

			if (uid == range.Start) {
				// remove the first item in the range
				if (range.Start != range.End) {
					if (range.Start <= range.End)
						ranges[index] = new Range (uid + 1, range.End);
					else
						ranges[index] = new Range (uid - 1, range.End);
				} else {
					ranges.RemoveAt (index);
				}
			} else if (uid == range.End) {
				// remove the last item in the range
				if (range.Start <= range.End)
					ranges[index] = new Range (range.Start, uid - 1);
				else
					ranges[index] = new Range (range.Start, uid + 1);
			} else {
				// remove a uid from the middle of the range
				if (range.Start < range.End) {
					ranges.Insert (index, new Range (range.Start, uid - 1));
					ranges[index + 1] = new Range (uid + 1, range.End);
				} else {
					ranges.Insert (index, new Range (range.Start, uid + 1));
					ranges[index + 1] = new Range (uid - 1, range.End);
				}
			}

			count--;
		}

		/// <summary>
		/// Removes the unique identifier from the set.
		/// </summary>
		/// <remarks>
		/// Removes the unique identifier from the set.
		/// </remarks>
		/// <returns><value>true</value> if the unique identifier was removed; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique identifier to remove.</param>
		public bool Remove (UniqueId uid)
		{
			int index = IndexOfRange (uid.Id);

			if (index == -1)
				return false;

			Remove (index, uid.Id);

			return true;
		}

		#endregion

		#region IList implementation

		/// <summary>
		/// Gets the index of the specified unique id, if it exists.
		/// </summary>
		/// <remarks>
		/// Finds the index of the specified unique id, if it exists.
		/// </remarks>
		/// <returns>The index of the specified unique id; otherwise <value>-1</value>.</returns>
		/// <param name="uid">The unique id.</param>
		public int IndexOf (UniqueId uid)
		{
			int index = 0;

			for (int i = 0; i < ranges.Count; i++) {
				if (ranges[i].Contains (uid.Id))
					return index + ranges[i].IndexOf (uid.Id);

				index += ranges[i].Count;
			}

			return -1;
		}

		/// <summary>
		/// Inserts the specified unique identifier at the given index.
		/// </summary>
		/// <remarks>
		/// Inserts the unique identifier at the specified index in the set.
		/// </remarks>
		/// <param name="index">The index to insert the unique id.</param>
		/// <param name="uid">The unique id.</param>
		/// <exception cref="System.NotSupportedException">
		/// The list does not support inserting items.
		/// </exception>
		public void Insert (int index, UniqueId uid)
		{
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Removes the unique identifier at the specified index.
		/// </summary>
		/// <remarks>
		/// Removes the unique identifier at the specified index.
		/// </remarks>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public void RemoveAt (int index)
		{
			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException (nameof (index));

			int offset = 0;

			for (int i = 0; i < ranges.Count; i++) {
				if (index >= offset + ranges[i].Count) {
					offset += ranges[i].Count;
					continue;
				}

				var uid = ranges[i][index - offset];
				Remove (i, uid);
				return;
			}
		}

		/// <summary>
		/// Gets or sets the unique identifier at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets or sets the unique identifier at the specified index.
		/// </remarks>
		/// <value>The unique identifier at the specified index.</value>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The list does not support setting items.
		/// </exception>
		public UniqueId this [int index] {
			get {
				if (index < 0 || index >= count)
					throw new ArgumentOutOfRangeException (nameof (index));

				int offset = 0;

				for (int i = 0; i < ranges.Count; i++) {
					if (index >= offset + ranges[i].Count) {
						offset += ranges[i].Count;
						continue;
					}

					uint uid = ranges[i][index - offset];

					return new UniqueId (Validity, uid);
				}

				throw new ArgumentOutOfRangeException (nameof (index));
			}
			set {
				throw new NotSupportedException ();
			}
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets an enumerator for the set of unique ids.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the set of unique ids.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<UniqueId> GetEnumerator ()
		{
			for (int i = 0; i < ranges.Count; i++) {
				foreach (var uid in ranges[i])
					yield return new UniqueId (Validity, uid);
			}

			yield break;
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets an enumerator for the set of unique ids.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the set of unique ids.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		#endregion

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="UniqueIdSet"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="UniqueIdSet"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="UniqueIdSet"/>.</returns>
		public override string ToString ()
		{
			var builder = new StringBuilder ();

			for (int i = 0; i < ranges.Count; i++) {
				if (i > 0)
					builder.Append (',');

				builder.Append (ranges[i]);
			}

			return builder.ToString ();
		}

		/// <summary>
		/// Format a generic list of unique identifiers as a string.
		/// </summary>
		/// <remarks>
		/// Formats a generic list of unique identifiers as a string.
		/// </remarks>
		/// <returns>The string representation of the collection of unique identifiers.</returns>
		/// <param name="uids">The unique identifiers.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the unique identifiers is invalid (has a value of <c>0</c>).
		/// </exception>
		public static string ToString (IList<UniqueId> uids)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (uids.Count == 0)
				return string.Empty;

			var range = uids as UniqueIdRange;
			if (range != null)
				return range.ToString ();

			var set = uids as UniqueIdSet;
			if (set != null)
				return set.ToString ();

			var builder = new StringBuilder ();
			int index = 0;

			while (index < uids.Count) {
				if (!uids[index].IsValid)
					throw new ArgumentException ("One or more of the uids is invalid.", nameof (uids));

				uint start = uids[index].Id;
				uint end = uids[index].Id;
				int i = index + 1;

				if (i < uids.Count) {
					if (uids[i].Id == end + 1) {
						end = uids[i++].Id;

						while (i < uids.Count && uids[i].Id == end + 1) {
							end++;
							i++;
						}
					} else if (uids[i].Id == end - 1) {
						end = uids[i++].Id;

						while (i < uids.Count && uids[i].Id == end - 1) {
							end--;
							i++;
						}
					}
				}

				if (builder.Length > 0)
					builder.Append (',');

				if (start != end)
					builder.AppendFormat ("{0}:{1}", start, end);
				else
					builder.Append (start.ToString ());

				index = i;
			}

			return builder.ToString ();
		}

		/// <summary>
		/// Attempt to parse the specified token as a set of unique identifiers.
		/// </summary>
		/// <remarks>
		/// Attempts to parse the specified token as a set of unique identifiers.
		/// </remarks>
		/// <returns><c>true</c> if the set of unique identifiers were successfully parsed; otherwise, <c>false</c>.</returns>
		/// <param name="token">The token containing the set of unique identifiers.</param>
		/// <param name="validity">The UIDVALIDITY value.</param>
		/// <param name="uids">The set of unique identifiers.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <c>null</c>.
		/// </exception>
		public static bool TryParse (string token, uint validity, out UniqueIdSet uids)
		{
			if (token == null)
				throw new ArgumentNullException (nameof (token));

			uids = new UniqueIdSet (validity);

			var order = SortOrder.None;
			bool sorted = true;
			uint start, end;
			uint prev = 0;
			int index = 0;

			do {
				if (!UniqueId.TryParse (token, ref index, out start))
					return false;

				if (index < token.Length && token[index] == ':') {
					index++;

					if (!UniqueId.TryParse (token, ref index, out end))
						return false;

					var range = new Range (start, end);
					uids.count += range.Count;
					uids.ranges.Add (range);

					if (sorted) {
						switch (order) {
						default: sorted = true; order = start <= end ? SortOrder.Ascending : SortOrder.Descending; break;
						case SortOrder.Descending: sorted = start >= end && start <= prev; break;
						case SortOrder.Ascending: sorted = start <= end && start >= prev; break;
						}
					}

					prev = end;
				} else {
					uids.ranges.Add (new Range (start, start));
					uids.count++;

					if (sorted && uids.ranges.Count > 1) {
						switch (order) {
						default: sorted = true; order = start >= prev ? SortOrder.Ascending : SortOrder.Descending; break;
						case SortOrder.Descending: sorted = start <= prev; break;
						case SortOrder.Ascending: sorted = start >= prev; break;
						}
					}

					prev = start;
				}

				if (index >= token.Length)
					break;

				if (token[index++] != ',')
					return false;
			} while (true);

			uids.SortOrder = sorted ? order : SortOrder.None;

			return true;
		}

		/// <summary>
		/// Attempt to parse the specified token as a set of unique identifiers.
		/// </summary>
		/// <remarks>
		/// Attempts to parse the specified token as a set of unique identifiers.
		/// </remarks>
		/// <returns><c>true</c> if the set of unique identifiers were successfully parsed; otherwise, <c>false</c>.</returns>
		/// <param name="token">The token containing the set of unique identifiers.</param>
		/// <param name="uids">The set of unique identifiers.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <c>null</c>.
		/// </exception>
		public static bool TryParse (string token, out UniqueIdSet uids)
		{
			return TryParse (token, 0, out uids);
		}
	}
}
