//
// UniqueIdRange.cs
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
using System.Collections;
using System.Globalization;
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
		/// A <see cref="UniqueIdRange"/> that encompases all messages in the folder.
		/// </summary>
		/// <remarks>
		/// Represents the range of messages from <see cref="UniqueId.MinValue"/> to
		/// <see cref="UniqueId.MaxValue"/>.
		/// </remarks>
		public static readonly UniqueIdRange All = new UniqueIdRange (UniqueId.MinValue, UniqueId.MaxValue);

		static readonly UniqueIdRange Invalid = new UniqueIdRange ();

		readonly uint validity;
		internal uint start;
		internal uint end;

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.UniqueIdRange"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new (invalid) range of unique identifiers.
		/// </remarks>
		UniqueIdRange ()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdRange"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new range of unique identifiers.
		/// </remarks>
		/// <param name="validity">The uid validity.</param>
		/// <param name="start">The first unique identifier in the range.</param>
		/// <param name="end">The last unique identifier in the range.</param>
		public UniqueIdRange (uint validity, uint start, uint end)
		{
			if (start == 0)
				throw new ArgumentOutOfRangeException (nameof (start));

			if (end == 0)
				throw new ArgumentOutOfRangeException (nameof (end));

			this.validity = validity;
			this.start = start;
			this.end = end;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.UniqueIdRange"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new range of unique identifiers.
		/// </remarks>
		/// <param name="start">The first <see cref="UniqueId"/> in the range.</param>
		/// <param name="end">The last <see cref="UniqueId"/> in the range.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="start"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="end"/> is invalid.</para>
		/// </exception>
		public UniqueIdRange (UniqueId start, UniqueId end)
		{
			if (!start.IsValid)
				throw new ArgumentOutOfRangeException (nameof (start));

			if (!end.IsValid)
				throw new ArgumentOutOfRangeException (nameof (end));

			this.validity = start.Validity;
			this.start = start.Id;
			this.end = end.Id;
		}

		/// <summary>
		/// Gets the validity, if non-zero.
		/// </summary>
		/// <remarks>
		/// Gets the UidValidity of the containing folder.
		/// </remarks>
		/// <value>The UidValidity of the containing folder.</value>
		public uint Validity {
			get { return validity; }
		}

		/// <summary>
		/// Gets the minimum unique identifier in the range.
		/// </summary>
		/// <remarks>
		/// Gets the minimum unique identifier in the range.
		/// </remarks>
		/// <value>The minimum unique identifier.</value>
		public UniqueId Min {
			get { return start < end ? new UniqueId (validity, start) : new UniqueId (validity, end); }
		}

		/// <summary>
		/// Gets the maximum unique identifier in the range.
		/// </summary>
		/// <remarks>
		/// Gets the maximum unique identifier in the range.
		/// </remarks>
		/// <value>The maximum unique identifier.</value>
		public UniqueId Max {
			get { return start > end ? new UniqueId (validity, start) : new UniqueId (validity, end); }
		}

		/// <summary>
		/// Get the start of the unique identifier range.
		/// </summary>
		/// <remarks>
		/// Gets the start of the unique identifier range.
		/// </remarks>
		/// <value>The start of the range.</value>
		public UniqueId Start {
			get { return new UniqueId (validity, start); }
		}

		/// <summary>
		/// Get the end of the unique identifier range.
		/// </summary>
		/// <remarks>
		/// Gets the end of the unique identifier range.
		/// </remarks>
		/// <value>The end of the range.</value>
		public UniqueId End {
			get { return new UniqueId (validity, end); }
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
			get { return (int) (start <= end ? end - start : start - end) + 1; }
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
			if (start <= end)
				return uid.Id >= start && uid.Id <= end;

			return uid.Id <= start && uid.Id >= end;
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
				throw new ArgumentNullException (nameof (array));

			if (arrayIndex < 0 || arrayIndex > (array.Length - Count))
				throw new ArgumentOutOfRangeException (nameof (arrayIndex));

			int index = arrayIndex;

			if (start <= end) {
				for (uint uid = start; uid <= end; uid++, index++)
					array[index] = new UniqueId (validity, uid);
			} else {
				for (uint uid = start; uid >= end; uid--, index++)
					array[index] = new UniqueId (validity, uid);
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
			if (start <= end) {
				if (uid.Id < start || uid.Id > end)
					return -1;

				return (int) (uid.Id - start);
			}

			if (uid.Id > start || uid.Id < end)
				return -1;

			return (int) (start - uid.Id);
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
					throw new ArgumentOutOfRangeException (nameof (index));

				uint uid = start <= end ? start + (uint) index : start - (uint) index;

				return new UniqueId (validity, uid);
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
			if (start <= end) {
				for (uint uid = start; uid <= end; uid++)
					yield return new UniqueId (validity, uid);
			} else {
				for (uint uid = start; uid >= end; uid--)
					yield return new UniqueId (validity, uid);
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
			if (end == uint.MaxValue)
				return string.Format (CultureInfo.InvariantCulture, "{0}:*", start);

			return string.Format (CultureInfo.InvariantCulture, "{0}:{1}", start, end);
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
		public static bool TryParse (string token, uint validity, out UniqueIdRange range)
		{
			if (token == null)
				throw new ArgumentNullException (nameof (token));

			uint start, end;
			int index = 0;

			if (!UniqueId.TryParse (token, ref index, out start) || index + 2 > token.Length || token[index++] != ':') {
				range = Invalid;
				return false;
			}

			if (token[index] != '*') {
				if (!UniqueId.TryParse (token, ref index, out end) || index < token.Length) {
					range = Invalid;
					return false;
				}
			} else if (index + 1 != token.Length) {
				range = Invalid;
				return false;
			} else {
				end = uint.MaxValue;
			}

			range = new UniqueIdRange (validity, start, end);

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
		public static bool TryParse (string token, out UniqueIdRange range)
		{
			return TryParse (token, 0, out range);
		}
	}
}
