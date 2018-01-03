//
// AccessRights.cs
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
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A set of access rights.
	/// </summary>
	/// <remarks>
	/// The set of access rights for a particular identity.
	/// </remarks>
	public class AccessRights : ICollection<AccessRight>
	{
		readonly List<AccessRight> list = new List<AccessRight> ();

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRights"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of access rights.
		/// </remarks>
		/// <param name="rights">The access rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="rights"/> is <c>null</c>.
		/// </exception>
		public AccessRights (IEnumerable<AccessRight> rights)
		{
			AddRange (rights);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRights"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new set of access rights.
		/// </remarks>
		/// <param name="rights">The access rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="rights"/> is <c>null</c>.
		/// </exception>
		public AccessRights (string rights)
		{
			AddRange (rights);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessRights"/> class.
		/// </summary>
		/// <remarks>
		/// Creates an empty set of access rights.
		/// </remarks>
		public AccessRights ()
		{
		}

		/// <summary>
		/// Get the number of access rights in the collection.
		/// </summary>
		/// <remarks>
		/// Gets the number of access rights in the collection.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return list.Count; }
		}

		/// <summary>
		/// Get whether or not this set of access rights is read only.
		/// </summary>
		/// <remarks>
		/// Gets whether or not this set of access rights is read only.
		/// </remarks>
		/// <value><c>true</c> if this collection is read only; otherwise, <c>false</c>.</value>
		public bool IsReadOnly {
			get { return false; }
		}

		/// <summary>
		/// Add the specified access right.
		/// </summary>
		/// <remarks>
		/// Adds the specified access right if it is not already included.
		/// </remarks>
		/// <param name="right">The access right.</param>
		void ICollection<AccessRight>.Add (AccessRight right)
		{
			Add (right);
		}

		/// <summary>
		/// Add the specified access right.
		/// </summary>
		/// <remarks>
		/// Adds the specified access right if it is not already included.
		/// </remarks>
		/// <returns><c>true</c> if the right was added; otherwise, <c>false</c>.</returns>
		/// <param name="right">The access right.</param>
		public bool Add (AccessRight right)
		{
			if (list.Contains (right))
				return false;

			list.Add (right);

			return true;
		}

		/// <summary>
		/// Add the specified right.
		/// </summary>
		/// <remarks>
		/// Adds the right specified by the given character.
		/// </remarks>
		/// <returns><c>true</c> if the right was added; otherwise, <c>false</c>.</returns>
		/// <param name="right">The right.</param>
		public bool Add (char right)
		{
			return Add (new AccessRight (right));
		}

		/// <summary>
		/// Add the rights specified by the characters in the given string.
		/// </summary>
		/// <remarks>
		/// Adds the rights specified by the characters in the given string.
		/// </remarks>
		/// <param name="rights">The rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="rights"/> is <c>null</c>.
		/// </exception>
		public void AddRange (string rights)
		{
			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			for (int i = 0; i < rights.Length; i++)
				Add (new AccessRight (rights[i]));
		}

		/// <summary>
		/// Add the range of specified rights.
		/// </summary>
		/// <remarks>
		/// Adds the range of specified rights.
		/// </remarks>
		/// <param name="rights">The rights.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="rights"/> is <c>null</c>.
		/// </exception>
		public void AddRange (IEnumerable<AccessRight> rights)
		{
			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			foreach (var right in rights)
				Add (right);
		}

		/// <summary>
		/// Clears the access rights.
		/// </summary>
		/// <remarks>
		/// Removes all of the access rights.
		/// </remarks>
		public void Clear ()
		{
			list.Clear ();
		}

		/// <summary>
		/// Checks if the set of access rights contains the specified right.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the set of access rights already contains the specified right
		/// </remarks>
		/// <returns><value>true</value> if the specified right exists; otherwise <value>false</value>.</returns>
		/// <param name="right">The access right.</param>
		public bool Contains (AccessRight right)
		{
			return list.Contains (right);
		}

		/// <summary>
		/// Copies all of the access rights to the specified array.
		/// </summary>
		/// <remarks>
		/// Copies all of the access rights into the array,
		/// starting at the specified array index.
		/// </remarks>
		/// <param name="array">The array.</param>
		/// <param name="arrayIndex">The array index.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="array"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="arrayIndex"/> is out of range.
		/// </exception>
		public void CopyTo (AccessRight[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));

			if (arrayIndex < 0 || arrayIndex + Count > array.Length)
				throw new ArgumentOutOfRangeException (nameof (arrayIndex));

			list.CopyTo (array, arrayIndex);
		}

		/// <summary>
		/// Removes the specified access right.
		/// </summary>
		/// <remarks>
		/// Removes the specified access right.
		/// </remarks>
		/// <returns><value>true</value> if the access right was removed; otherwise <value>false</value>.</returns>
		/// <param name="right">The access right.</param>
		public bool Remove (AccessRight right)
		{
			return list.Remove (right);
		}

		/// <summary>
		/// Get the access right at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the access right at the specified index.
		/// </remarks>
		/// <value>The access right at the specified index.</value>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public AccessRight this [int index] {
			get {
				if (index < 0 || index >= list.Count)
					throw new ArgumentOutOfRangeException (nameof (index));

				return list[index];
			}
		}

		#region IEnumerable implementation

		/// <summary>
		/// Get the access rights enumerator.
		/// </summary>
		/// <remarks>
		/// Gets the access rights enumerator.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<AccessRight> GetEnumerator ()
		{
			return list.GetEnumerator ();
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Get the access rights enumerator.
		/// </summary>
		/// <remarks>
		/// Gets the access rights enumerator.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return list.GetEnumerator ();
		}

		#endregion

		/// <summary>
		/// Return a <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRights"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRights"/>.
		/// </remarks>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.AccessRights"/>.</returns>
		public override string ToString ()
		{
			var rights = new char[list.Count];

			for (int i = 0; i < list.Count; i++)
				rights[i] = list[i].Right;

			return new string (rights);
		}
	}
}
