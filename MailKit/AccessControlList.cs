//
// AccessControlList.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2026 .NET Foundation and Contributors
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
	/// An Access Control List (ACL)
	/// </summary>
	/// <remarks>
	/// An Access Control List (ACL) is a list of access controls defining the permissions
	/// various identities have available.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
	/// </example>
	public class AccessControlList : IList<AccessControl>, IReadOnlyList<AccessControl>
	{
		readonly List<AccessControl> list;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControlList"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControlList"/>.
		/// </remarks>
		/// <param name="controls">The list of access controls.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="controls"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="controls"/> is <see langword="null" />.</para>
		/// </exception>
		public AccessControlList (IEnumerable<AccessControl> controls)
		{
			list = new List<AccessControl> ();
			AddRange (controls);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AccessControlList"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.AccessControlList"/>.
		/// </remarks>
		public AccessControlList ()
		{
			list = new List<AccessControl> ();
		}

		/// <summary>
		/// Get the number of access controls in the list.
		/// </summary>
		/// <remarks>
		/// Gets the number of access controls in the list.
		/// </remarks>
		/// <value>The number of access controls.</value>
		public int Count {
			get { return list.Count; }
		}

		/// <summary>
		/// Get whether or not the list is read only.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the list is read only.
		/// </remarks>
		/// <value><see langword="true" /> if the list is read only; otherwise, <see langword="false" />.</value>
		public bool IsReadOnly {
			get { return false; }
		}

		/// <summary>
		/// Get or set the access control at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets or sets the access control at the specified index.
		/// </remarks>
		/// <value>The access control at the specified index.</value>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="value"/> is <see langword="null" />.
		/// </exception>
		public AccessControl this [int index] {
			get {
				if (index < 0 || index >= list.Count)
					throw new ArgumentOutOfRangeException (nameof (index));

				return list[index];
			}
			set {
				if (index < 0 || index >= list.Count)
					throw new ArgumentOutOfRangeException (nameof (index));

				if (value == null)
					throw new ArgumentNullException (nameof (value));

				list[index] = value;
			}
		}

		/// <summary>
		/// Add the specified access control.
		/// </summary>
		/// <remarks>
		/// Adds the specified access control to the end of the list.
		/// </remarks>
		/// <param name="control">The access control.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="control"/> is <see langword="null" />.
		/// </exception>
		public void Add (AccessControl control)
		{
			if (control == null)
				throw new ArgumentNullException (nameof (control));

			list.Add (control);
		}

		/// <summary>
		/// Add the specified access controls.
		/// </summary>
		/// <remarks>
		/// Adds the specified access controls to the end of the list.
		/// </remarks>
		/// <param name="controls">The access controls.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="controls"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="controls"/> is <see langword="null" />.</para>
		/// </exception>
		public void AddRange (IEnumerable<AccessControl> controls)
		{
			if (controls == null)
				throw new ArgumentNullException (nameof (controls));

			var items = new List<AccessControl> (controls);

			for (int i = 0; i < items.Count; i++) {
				if (items[i] == null)
					throw new ArgumentNullException (nameof (controls));
			}

			list.AddRange (items);
		}

		/// <summary>
		/// Insert the specified access control at the specified index.
		/// </summary>
		/// <remarks>
		/// Inserts the specified access control at the specified index.
		/// </remarks>
		/// <param name="index">The index.</param>
		/// <param name="control">The access control.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="control"/> is <see langword="null" />.
		/// </exception>
		public void Insert (int index, AccessControl control)
		{
			if (index < 0 || index > list.Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (control == null)
				throw new ArgumentNullException (nameof (control));

			list.Insert (index, control);
		}

		/// <summary>
		/// Clear the list of access controls.
		/// </summary>
		/// <remarks>
		/// Removes all of the access controls from the list.
		/// </remarks>
		public void Clear ()
		{
			list.Clear ();
		}

		/// <summary>
		/// Check if the list contains the specified access control.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the list contains the specified access control.
		/// </remarks>
		/// <returns><see langword="true" /> if the list contains the specified access control; otherwise, <see langword="false" />.</returns>
		/// <param name="control">The access control.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="control"/> is <see langword="null" />.
		/// </exception>
		public bool Contains (AccessControl control)
		{
			if (control == null)
				throw new ArgumentNullException (nameof (control));

			return list.Contains (control);
		}

		/// <summary>
		/// Get the index of the specified access control.
		/// </summary>
		/// <remarks>
		/// Gets the index of the specified access control.
		/// </remarks>
		/// <returns>The index of the access control if found; otherwise, <c>-1</c>.</returns>
		/// <param name="control">The access control.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="control"/> is <see langword="null" />.
		/// </exception>
		public int IndexOf (AccessControl control)
		{
			if (control == null)
				throw new ArgumentNullException (nameof (control));

			return list.IndexOf (control);
		}

		/// <summary>
		/// Remove the specified access control.
		/// </summary>
		/// <remarks>
		/// Removes the specified access control.
		/// </remarks>
		/// <returns><see langword="true" /> if the access control was removed; otherwise, <see langword="false" />.</returns>
		/// <param name="control">The access control.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="control"/> is <see langword="null" />.
		/// </exception>
		public bool Remove (AccessControl control)
		{
			if (control == null)
				throw new ArgumentNullException (nameof (control));

			return list.Remove (control);
		}

		/// <summary>
		/// Remove the access control at the specified index.
		/// </summary>
		/// <remarks>
		/// Removes the access control at the specified index.
		/// </remarks>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public void RemoveAt (int index)
		{
			if (index < 0 || index >= list.Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			list.RemoveAt (index);
		}

		/// <summary>
		/// Copy all of the access controls to the specified array.
		/// </summary>
		/// <remarks>
		/// Copies all of the access controls into the array, starting at the specified array index.
		/// </remarks>
		/// <param name="array">The array.</param>
		/// <param name="arrayIndex">The array index.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="array"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="arrayIndex"/> is out of range.
		/// </exception>
		public void CopyTo (AccessControl[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));

			if (arrayIndex < 0 || arrayIndex > array.Length - list.Count)
				throw new ArgumentOutOfRangeException (nameof (arrayIndex));

			list.CopyTo (array, arrayIndex);
		}

		/// <summary>
		/// Get an enumerator for the list of access controls.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the list of access controls.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<AccessControl> GetEnumerator ()
		{
			return list.GetEnumerator ();
		}

		/// <summary>
		/// Get an enumerator for the list of access controls.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the list of access controls.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return list.GetEnumerator ();
		}
	}
}