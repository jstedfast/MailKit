//
// UniqueIdRange.cs
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
using System.Collections;
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A range of <see cref="UniqueId"/> items.
	/// </summary>
	/// <remarks>
	/// When dealing with a large range, it is more efficient to use a
	/// <see cref="UniqueIdRange"/> than a typical
	/// IList&lt;<see cref="UniqueId"/>&gt;.
	/// </remarks>
	public class UniqueIdRange : IList<UniqueId>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdRange"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new range of unique ids.
		/// </remarks>
		/// <param name="min">The first <see cref="UniqueId"/> in the range.</param>
		/// <param name="max">The last <see cref="UniqueId"/> in the range.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="min"/> is greater than <paramref name="max"/>.
		/// </exception>
		public UniqueIdRange (UniqueId min, UniqueId max)
		{
			if (min.Id > max.Id)
				throw new ArgumentException ();

			Min = min;
			Max = max;
		}

		/// <summary>
		/// Gets the minimum unique id in the range.
		/// </summary>
		/// <remarks>
		/// Gets the minimum unique id in the range.
		/// </remarks>
		/// <value>The minimum unique id.</value>
		public UniqueId Min {
			get; internal set;
		}

		/// <summary>
		/// Gets the maximum unique id in the range.
		/// </summary>
		/// <remarks>
		/// Gets the maximum unique id in the range.
		/// </remarks>
		/// <value>The maximum unique id.</value>
		public UniqueId Max {
			get; internal set;
		}

		#region ICollection implementation

		/// <summary>
		/// Get the number of unique ids in the range.
		/// </summary>
		/// <remarks>
		/// Gets the number of unique ids in the range.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return (int) (Max.Id - Min.Id) + 1; }
		}

		/// <summary>
		/// Get whether or not the range is read only.
		/// </summary>
		/// <remarks>
		/// A <see cref="UniqueIdRange"/> is always read-only.
		/// </remarks>
		/// <value><c>true</c> if the range is read only; otherwise, <c>false</c>.</value>
		public bool IsReadOnly {
			get { return true; }
		}

		/// <summary>
		/// Adds the unique id to the range.
		/// </summary>
		/// <remarks>
		/// Since a <see cref="UniqueIdRange"/> is read-only, unique ids cannot
		/// be added to the range.
		/// </remarks>
		/// <param name="uid">The unique id to add.</param>
		/// <exception cref="System.NotSupportedException">
		/// The list does not support adding items.
		/// </exception>
		public void Add (UniqueId uid)
		{
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Clears the list.
		/// </summary>
		/// <remarks>
		/// Since a <see cref="UniqueIdRange"/> is read-only, the range cannot be cleared.
		/// </remarks>
		/// <exception cref="System.NotSupportedException">
		/// The list does not support being cleared.
		/// </exception>
		public void Clear ()
		{
			throw new NotSupportedException ();
		}

		/// <summary>
		/// Checks if the range contains the specified unique id.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the range contains the specified unique id.
		/// </remarks>
		/// <returns><value>true</value> if the specified unique id is in the range; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique id.</param>
		public bool Contains (UniqueId uid)
		{
			return uid.Id >= Min.Id && uid.Id <= Max.Id;
		}

		/// <summary>
		/// Copies all of the unique ids in the range to the specified array.
		/// </summary>
		/// <remarks>
		/// Copies all of the unique ids within the range into the array,
		/// starting at the specified array index.
		/// </remarks>
		/// <param name="array">The array to copy the unique ids to.</param>
		/// <param name="arrayIndex">The index into the array.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="array"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="arrayIndex"/> is out of range.
		/// </exception>
		public void CopyTo (UniqueId[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException ("array");

			if (arrayIndex < 0 || arrayIndex > (array.Length - Count))
				throw new ArgumentOutOfRangeException ("arrayIndex");

			int index = arrayIndex;

			for (uint uid = Min.Id; uid <= Max.Id; uid++, index++)
				array[index] = new UniqueId (uid);
		}

		/// <summary>
		/// Removes the unique id from the range.
		/// </summary>
		/// <remarks>
		/// Since a <see cref="UniqueIdRange"/> is read-only, unique ids cannot be removed.
		/// </remarks>
		/// <returns><value>true</value> if the unique id was removed; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique id to remove.</param>
		/// <exception cref="System.NotSupportedException">
		/// The list does not support removing items.
		/// </exception>
		public bool Remove (UniqueId uid)
		{
			throw new NotSupportedException ();
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
			if (uid.Id < Min.Id || uid.Id > Max.Id)
				return -1;

			return (int) (uid.Id - Min.Id);
		}

		/// <summary>
		/// Inserts the specified unique id at the given index.
		/// </summary>
		/// <remarks>
		/// Inserts the unique id at the specified index in the range.
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
		/// <exception cref="System.NotSupportedException">
		/// The list does not support removing items.
		/// </exception>
		public void RemoveAt (int index)
		{
			throw new NotSupportedException ();
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
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");

				return new UniqueId (Min.Id + (uint) index);
			}
			set {
				throw new NotSupportedException ();
			}
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets an enumerator for the range of unique ids.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the range of unique ids.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<UniqueId> GetEnumerator ()
		{
			for (uint uid = Min.Id; uid <= Max.Id; uid++)
				yield return new UniqueId (uid);

			yield break;
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets an enumerator for the range of unique ids.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the range of unique ids.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		#endregion

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="UniqueIdRange"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="UniqueIdRange"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="UniqueIdRange"/>.</returns>
		public override string ToString ()
		{
			if (Min == Max)
				return Min.ToString ();

			if (Max == UniqueId.MaxValue)
				return string.Format ("{0}:*", Min);

			return string.Format ("{0}:{1}", Min, Max);
		}
	}
}
