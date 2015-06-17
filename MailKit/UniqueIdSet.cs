//
// UniqueIdSet.cs
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
using System.Text;
using System.Collections;
using System.Collections.Generic;

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
		readonly List<UniqueIdRange> ranges;
		bool isReadOnly, sorted;
		long count;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique identifier set.
		/// </remarks>
		/// <param name="sort"><c>true</c> if unique identifiers should be sorted; otherwise, <c>false</c>.</param>
		public UniqueIdSet (bool sort = false)
		{
			ranges = new List<UniqueIdRange> ();
			sorted = sort;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of unique identifier set containing the specified uids.
		/// </remarks>
		/// <param name="uids">An initial set of unique ids.</param>
		/// <param name="sort"><c>true</c> if unique identifiers should be sorted; otherwise, <c>false</c>.</param>
		public UniqueIdSet (IEnumerable<UniqueId> uids, bool sort = false)
		{
			ranges = new List<UniqueIdRange> ();
			sorted = sort;

			foreach (var uid in uids)
				Add (uid);
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
			get { return isReadOnly; }
		}

		int BinarySearch (UniqueId uid)
		{
			int min = 0, max = ranges.Count;

			if (max == 0)
				return -1;

			do {
				int i = min + ((max - min) / 2);

				if (uid >= ranges[i].Start) {
					if (uid <= ranges[i].End)
						return i;

					min = i + 1;
				} else {
					max = i;
				}
			} while (min < max);

			return -1;
		}

		int IndexOfRange (UniqueId uid)
		{
			if (sorted)
				return BinarySearch (uid);

			for (int i = 0; i < ranges.Count; i++) {
				if (ranges[i].Contains (uid))
					return i;
			}

			return -1;
		}

		void BinaryInsert (UniqueId uid)
		{
			int min = 0, max = ranges.Count;
			int i;

			if (max == 0) {
				ranges.Add (new UniqueIdRange (uid, uid));
				count++;
				return;
			}

			do {
				i = min + ((max - min) / 2);

				if (uid >= ranges[i].Start) {
					if (uid <= ranges[i].End)
						return;

					if (uid.Id == ranges[i].End.Id + 1) {
						if (i + 1 < ranges.Count && uid.Id + 1 >= ranges[i + 1].Start.Id) {
							// merge the 2 ranges together
							ranges[i].End = ranges[i + 1].End;
							ranges.RemoveAt (i + 1);
							count++;
							return;
						}

						ranges[i].End = uid;
						count++;
						return;
					}

					min = i + 1;
					i = min;
				} else {
					if (uid.Id == ranges[i].Start.Id - 1) {
						if (i > 0 && uid.Id - 1 <= ranges[i - 1].End.Id) {
							// merge the 2 ranges together
							ranges[i - 1].End = ranges[i].End;
							ranges.RemoveAt (i);
							count++;
							return;
						}

						ranges[i].Start = uid;
						count++;
						return;
					}

					max = i;
				}
			} while (min < max);

			var range = new UniqueIdRange (uid, uid);

			if (i < ranges.Count)
				ranges.Insert (i, range);
			else
				ranges.Add (range);

			count++;
		}

		void Append (UniqueId uid)
		{
			if (Contains (uid))
				return;

			count++;

			if (ranges.Count > 0) {
				var range = ranges[ranges.Count - 1];

				if (range.Start == range.End) {
					if (uid.Id == range.End.Id + 1 || uid.Id == range.End.Id - 1) {
						range.End = uid;
						return;
					}
				} else if (range.Start < range.End) {
					if (uid.Id == range.End.Id + 1) {
						range.End = uid;
						return;
					}
				} else if (range.Start > range.End) {
					if (uid.Id == range.End.Id - 1) {
						range.End = uid;
						return;
					}
				}
			}

			ranges.Add (new UniqueIdRange (uid, uid));
		}

		/// <summary>
		/// Adds the unique identifier to the set.
		/// </summary>
		/// <remarks>
		/// Adds the unique identifier to the set.
		/// </remarks>
		/// <param name="uid">The unique identifier to add.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The collection is readonly.
		/// </exception>
		public void Add (UniqueId uid)
		{
			if (IsReadOnly)
				throw new InvalidOperationException ("The collection is readonly.");

			if (sorted)
				BinaryInsert (uid);
			else
				Append (uid);
		}

		/// <summary>
		/// Adds all of the uids to the set.
		/// </summary>
		/// <remarks>
		/// Adds all of the uids to the set.
		/// </remarks>
		/// <param name="uids">The collection of uids.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The collection is readonly.
		/// </exception>
		public void AddRange (IEnumerable<UniqueId> uids)
		{
			if (uids == null)
				throw new ArgumentNullException ("uids");

			if (IsReadOnly)
				throw new InvalidOperationException ("The collection is readonly");

			foreach (var uid in uids) {
				if (sorted)
					BinaryInsert (uid);
				else
					Append (uid);
			}
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
			if (IsReadOnly)
				throw new InvalidOperationException ("The collection is readonly");

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
			return IndexOfRange (uid) != -1;
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
				throw new ArgumentNullException ("array");

			if (arrayIndex < 0 || arrayIndex > (array.Length - Count))
				throw new ArgumentOutOfRangeException ("arrayIndex");

			int index = arrayIndex;

			for (int i = 0; i < ranges.Count; i++) {
				foreach (var uid in ranges[i])
					array[index] = uid;
			}
		}

		void Remove (int index, UniqueId uid)
		{
			var range = ranges[index];

			if (uid == range.Start) {
				// remove the first item in the range
				if (range.Start != range.End) {
					if (range.Start <= range.End)
						range.Start = new UniqueId (uid.Id + 1);
					else
						range.Start = new UniqueId (uid.Id - 1);
				} else {
					ranges.RemoveAt (index);
				}
			} else if (uid == range.End) {
				// remove the last item in the range
				if (range.Start <= range.End)
					range.End = new UniqueId (uid.Id - 1);
				else
					range.End = new UniqueId (uid.Id + 1);
			} else {
				// remove a uid from the middle of the range
				if (range.Start < range.End) {
					ranges.Insert (index, new UniqueIdRange (range.Start, new UniqueId (uid.Id - 1)));
					range.Start = new UniqueId (uid.Id + 1);
				} else {
					ranges.Insert (index, new UniqueIdRange (range.Start, new UniqueId (uid.Id + 1)));
					range.Start = new UniqueId (uid.Id - 1);
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
		/// <exception cref="System.InvalidOperationException">
		/// The collection is readonly.
		/// </exception>
		public bool Remove (UniqueId uid)
		{
			if (IsReadOnly)
				throw new InvalidOperationException ("The collection is readonly");

			int index = IndexOfRange (uid);

			if (index == -1)
				return false;

			Remove (index, uid);

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
				if (ranges[i].Contains (uid))
					return index + ranges[i].IndexOf (uid);

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
		/// <exception cref="System.InvalidOperationException">
		/// The collection is readonly.
		/// </exception>
		public void RemoveAt (int index)
		{
			if (index < 0 || index >= count)
				throw new ArgumentOutOfRangeException ("index");

			if (IsReadOnly)
				throw new InvalidOperationException ("The collection is readonly");

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
					throw new ArgumentOutOfRangeException ("index");

				int offset = 0;

				for (int i = 0; i < ranges.Count; i++) {
					if (index >= offset + ranges[i].Count) {
						offset += ranges[i].Count;
						continue;
					}

					return ranges[i][index - offset];
				}

				throw new ArgumentOutOfRangeException ("index");
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
					yield return uid;
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
				throw new ArgumentNullException ("uids");

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
				if (uids[index].Id == 0)
					throw new ArgumentException ("One or more of the uids is invalid.", "uids");

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
				throw new ArgumentNullException ("token");

			uids = new UniqueIdSet { isReadOnly = true, sorted = false };

			UniqueId start, end;
			int index = 0;

			do {
				if (!UniqueId.TryParse (token, ref index, validity, out start))
					return false;

				if (index >= token.Length) {
					uids.ranges.Add (new UniqueIdRange (start, start));
					uids.count++;
					return true;
				}

				if (token[index] == ':') {
					index++;

					if (!UniqueId.TryParse (token, ref index, validity, out end))
						return false;

					var range = new UniqueIdRange (start, end);
					uids.count += range.Count;
					uids.ranges.Add (range);
				} else {
					uids.ranges.Add (new UniqueIdRange (start, start));
					uids.count++;
				}

				if (index >= token.Length)
					return true;

				if (token[index++] != ',')
					return false;
			} while (true);
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
