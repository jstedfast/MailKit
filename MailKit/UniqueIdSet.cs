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
	/// A set of <see cref="UniqueId"/> items.
	/// </summary>
	/// <remarks>
	/// When dealing with a large number of unique ids, it may be more efficient to use a
	/// <see cref="UniqueIdSet"/> than a typical IList&lt;<see cref="UniqueId"/>&gt;.
	/// </remarks>
	public class UniqueIdSet : IList<UniqueId>
	{
		readonly List<UniqueIdRange> ranges;
		int count;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique id set.
		/// </remarks>
		public UniqueIdSet ()
		{
			ranges = new List<UniqueIdRange> ();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdSet"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unique id set containing the specified uids.
		/// </remarks>
		/// <param name="uids">An initial set of unique ids.</param>
		public UniqueIdSet (IEnumerable<UniqueId> uids)
		{
			ranges = new List<UniqueIdRange> ();

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
			get { return count; }
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

		int BinarySearch (UniqueId uid)
		{
			int min = 0, max = ranges.Count;

			if (max == 0)
				return -1;

			do {
				int i = min + ((max - min) / 2);

				if (uid >= ranges[i].Min) {
					if (uid <= ranges[i].Max)
						return i;

					min = i + 1;
				} else {
					max = i;
				}
			} while (min < max);

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

				if (uid >= ranges[i].Min) {
					if (uid <= ranges[i].Max)
						return;

					if (uid.Id == ranges[i].Max.Id + 1) {
						if (i + 1 < ranges.Count && uid.Id + 1 >= ranges[i + 1].Min.Id) {
							// merge the 2 ranges together
							ranges[i].Max = ranges[i + 1].Max;
							ranges.RemoveAt (i + 1);
							count++;
							return;
						}

						ranges[i].Max = uid;
						count++;
						return;
					}

					min = i + 1;
					i = min;
				} else {
					if (uid.Id == ranges[i].Min.Id - 1) {
						if (i > 0 && uid.Id - 1 <= ranges[i - 1].Max.Id) {
							// merge the 2 ranges together
							ranges[i - 1].Max = ranges[i].Max;
							ranges.RemoveAt (i);
							count++;
							return;
						}

						ranges[i].Min = uid;
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

		/// <summary>
		/// Adds the unique id to the set.
		/// </summary>
		/// <remarks>
		/// Adds the unique id to the set.
		/// </remarks>
		/// <param name="uid">The unique id to add.</param>
		public void Add (UniqueId uid)
		{
			BinaryInsert (uid);
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
				throw new ArgumentNullException ("uids");

			foreach (var uid in uids)
				BinaryInsert (uid);
		}

		/// <summary>
		/// Clears the list.
		/// </summary>
		/// <remarks>
		/// Clears the list.
		/// </remarks>
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
		/// <returns><value>true</value> if the specified unique id is in the set; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique id.</param>
		public bool Contains (UniqueId uid)
		{
			return BinarySearch (uid) != -1;
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
				for (uint uid = ranges[i].Min.Id; uid <= ranges[i].Max.Id; uid++, index++)
					array[index] = new UniqueId (uid);
			}
		}

		void Remove (int index, UniqueId uid)
		{
			if (uid == ranges[index].Min) {
				// remove the first item in the range
				if (ranges[index].Min < ranges[index].Max)
					ranges[index].Min = new UniqueId (uid.Id + 1);
				else
					ranges.RemoveAt (index);
			} else if (uid == ranges[index].Max) {
				// remove the last item in the range
				ranges[index].Max = new UniqueId (uid.Id - 1);
			} else {
				// remove a uid from the middle of the range
				var min = new UniqueId (uid.Id + 1);
				var max = new UniqueId (uid.Id - 1);

				var range = new UniqueIdRange (ranges[index].Min, max);
				ranges.Insert (index, range);
				ranges[index + 1].Min = min;
			}

			count--;
		}

		/// <summary>
		/// Removes the unique id from the set.
		/// </summary>
		/// <remarks>
		/// Removes the unique id from the set.
		/// </remarks>
		/// <returns><value>true</value> if the unique id was removed; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique id to remove.</param>
		public bool Remove (UniqueId uid)
		{
			int index = BinarySearch (uid);

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
				if (uid >= ranges[i].Min && uid <= ranges[i].Max)
					return index + ranges[i].IndexOf (uid);

				index += ranges[i].Count;
			}

			return -1;
		}

		/// <summary>
		/// Inserts the specified unique id at the given index.
		/// </summary>
		/// <remarks>
		/// Inserts the unique id at the specified index in the set.
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
		/// Removes the unique id at the specified index.
		/// </summary>
		/// <remarks>
		/// Removes the unique id at the specified index.
		/// </remarks>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public void RemoveAt (int index)
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException ("index");

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

			throw new ArgumentOutOfRangeException ("index");
		}

		/// <summary>
		/// Gets or sets the unique id at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets or sets the unique id at the specified index.
		/// </remarks>
		/// <value>The unique id at the specified index.</value>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The list does not support setting items.
		/// </exception>
		public UniqueId this [int index] {
			get {
				if (index < 0)
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
				for (uint uid = ranges[i].Min.Id; uid <= ranges[i].Max.Id; uid++)
					yield return new UniqueId (uid);
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

			var ranges = token.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			var uidset = new UniqueIdSet ();

			uids = null;

			for (int i = 0; i < ranges.Length; i++) {
				var minmax = ranges[i].Split (':');
				uint min;

				if (!uint.TryParse (minmax[0], out min) || min == 0)
					return false;

				if (minmax.Length == 2) {
					uint max;

					if (!uint.TryParse (minmax[1], out max) || max == 0)
						return false;

					var uid0 = new UniqueId (validity, min < max ? min : max);
					var uid1 = new UniqueId (validity, min < max ? max : min);

					uidset.ranges.Add (new UniqueIdRange (uid0, uid1));
				} else if (minmax.Length == 1) {
					var uid = new UniqueId (validity, min);

					uidset.ranges.Add (new UniqueIdRange (uid, uid));
				} else {
					return false;
				}
			}

			uids = uidset;

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
