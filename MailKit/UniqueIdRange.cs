//
// UniqueIdRange.cs
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
		static readonly UniqueIdRange Invalid = new UniqueIdRange (new UniqueId (0), new UniqueId (0));

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdRange"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new range of unique identifiers.
		/// </remarks>
		/// <param name="start">The first <see cref="UniqueId"/> in the range.</param>
		/// <param name="end">The last <see cref="UniqueId"/> in the range.</param>
		public UniqueIdRange (UniqueId start, UniqueId end)
		{
			Start = start;
			End = end;
		}

		/// <summary>
		/// Gets the minimum unique identifier in the range.
		/// </summary>
		/// <remarks>
		/// Gets the minimum unique identifier in the range.
		/// </remarks>
		/// <value>The minimum unique identifier.</value>
		public UniqueId Min {
			get { return Start < End ? Start : End; }
		}

		/// <summary>
		/// Gets the maximum unique identifier in the range.
		/// </summary>
		/// <remarks>
		/// Gets the maximum unique identifier in the range.
		/// </remarks>
		/// <value>The maximum unique identifier.</value>
		public UniqueId Max {
			get { return Start > End ? Start : End; }
		}

		/// <summary>
		/// Get the start of the unique identifier range.
		/// </summary>
		/// <remarks>
		/// Gets the start of the unique identifier range.
		/// </remarks>
		/// <value>The start of the range.</value>
		public UniqueId Start {
			get; internal set;
		}

		/// <summary>
		/// Get the end of the unique identifier range.
		/// </summary>
		/// <remarks>
		/// Gets the end of the unique identifier range.
		/// </remarks>
		/// <value>The end of the range.</value>
		public UniqueId End {
			get; internal set;
		}

		#region ICollection implementation

		/// <summary>
		/// Get the number of unique identifiers in the range.
		/// </summary>
		/// <remarks>
		/// Gets the number of unique identifiers in the range.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return (int) (Start <= End ? End.Id - Start.Id : Start.Id - End.Id) + 1; }
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
		/// Adds the unique identifier to the range.
		/// </summary>
		/// <remarks>
		/// Since a <see cref="UniqueIdRange"/> is read-only, unique ids cannot
		/// be added to the range.
		/// </remarks>
		/// <param name="uid">The unique identifier to add.</param>
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
		/// <returns><value>true</value> if the specified unique identifier is in the range; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique id.</param>
		public bool Contains (UniqueId uid)
		{
			if (Start.Id <= End.Id)
				return uid.Id >= Start.Id && uid.Id <= End.Id;

			return uid.Id <= Start.Id && uid.Id >= End.Id;
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

			if (Start <= End) {
				for (uint uid = Start.Id; uid <= End.Id; uid++, index++)
					array[index] = new UniqueId (uid);
			} else {
				for (uint uid = Start.Id; uid >= End.Id; uid--, index++)
					array[index] = new UniqueId (uid);
			}
		}

		/// <summary>
		/// Removes the unique identifier from the range.
		/// </summary>
		/// <remarks>
		/// Since a <see cref="UniqueIdRange"/> is read-only, unique ids cannot be removed.
		/// </remarks>
		/// <returns><value>true</value> if the unique identifier was removed; otherwise <value>false</value>.</returns>
		/// <param name="uid">The unique identifier to remove.</param>
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
			if (Start <= End) {
				if (uid.Id < Start.Id || uid.Id > End.Id)
					return -1;

				return (int) (uid.Id - Start.Id);
			}

			if (uid.Id > Start.Id || uid.Id < End.Id)
				return -1;

			return (int) (Start.Id - uid.Id);
		}

		/// <summary>
		/// Inserts the specified unique identifier at the given index.
		/// </summary>
		/// <remarks>
		/// Inserts the unique identifier at the specified index in the range.
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
		/// <exception cref="System.NotSupportedException">
		/// The list does not support removing items.
		/// </exception>
		public void RemoveAt (int index)
		{
			throw new NotSupportedException ();
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
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException ("index");

				uint uid = Start <= End ? Start.Id + (uint) index : Start.Id - (uint) index;

				return new UniqueId (uid);
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
			if (Start <= End) {
				for (uint uid = Start.Id; uid <= End.Id; uid++)
					yield return new UniqueId (uid);
			} else {
				for (uint uid = Start.Id; uid >= End.Id; uid--)
					yield return new UniqueId (uid);
			}

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
			if (Start == End)
				return Start.ToString ();

			if (Start <= End && End == UniqueId.MaxValue)
				return string.Format ("{0}:*", Start);

			return string.Format ("{0}:{1}", Start, End);
		}

		/// <summary>
		/// Attempt to parse a unique identifier range.
		/// </summary>
		/// <remarks>
		/// Attempts to parse a unique identifier range.
		/// </remarks>
		/// <returns><c>true</c> if the unique identifier range was successfully parsed; otherwise, <c>false.</c>.</returns>
		/// <param name="token">The token to parse.</param>
		/// <param name="validity">The UIDVALIDITY value.</param>
		/// <param name="range">The unique identifier range.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <c>null</c>.
		/// </exception>
		public bool TryParse (string token, uint validity, out UniqueIdRange range)
		{
			if (token == null)
				throw new ArgumentNullException ("token");

			UniqueId start, end;
			int index = 0;

			if (!UniqueId.TryParse (token, ref index, validity, out start) || index + 2 >= token.Length || token[index++] != ':') {
				range = Invalid;
				return false;
			}

			if (token[index] != '*') {
				if (!UniqueId.TryParse (token, ref index, validity, out end) || index < token.Length) {
					range = Invalid;
					return false;
				}
			} else if (index + 1 != token.Length) {
				range = Invalid;
				return false;
			} else {
				end = UniqueId.MaxValue;
			}

			range = new UniqueIdRange (start, end);

			return true;
		}

		/// <summary>
		/// Attempt to parse a unique identifier range.
		/// </summary>
		/// <remarks>
		/// Attempts to parse a unique identifier range.
		/// </remarks>
		/// <returns><c>true</c> if the unique identifier range was successfully parsed; otherwise, <c>false.</c>.</returns>
		/// <param name="token">The token to parse.</param>
		/// <param name="range">The unique identifier range.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="token"/> is <c>null</c>.
		/// </exception>
		public bool TryParse (string token, out UniqueIdRange range)
		{
			return TryParse (token, 0, out range);
		}
	}
}
