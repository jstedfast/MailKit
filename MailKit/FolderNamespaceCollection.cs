//
// FolderNamespaceCollection.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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
	/// A read-only collection of folder namespaces.
	/// </summary>
	public class FolderNamespaceCollection : IEnumerable<FolderNamespace>
	{
		readonly List<FolderNamespace> collection;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNamespaceCollection"/> class.
		/// </summary>
		internal FolderNamespaceCollection ()
		{
			collection = new List<FolderNamespace> ();
		}

		#region ICollection implementation

		/// <summary>
		/// Gets the number of folder namespaces contained in the collection.
		/// </summary>
		/// <value>The count.</value>
		public int Count {
			get { return collection.Count; }
		}

		/// <summary>
		/// Adds the specified namespace.
		/// </summary>
		/// <param name="namespace">The namespace to add.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		internal void Add (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException ("namespace");

			collection.Add (@namespace);
		}

		/// <summary>
		/// Removes all namespaces from the collection.
		/// </summary>
		internal void Clear ()
		{
			collection.Clear ();
		}

		/// <summary>
		/// Checks if the collection contains the specified namespace.
		/// </summary>
		/// <returns><value>true</value> if the specified namespace exists;
		/// otherwise <value>false</value>.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		public bool Contains (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException ("namespace");

			return collection.Contains (@namespace);
		}

		/// <summary>
		/// Removes the first occurance of the specified namespace.
		/// </summary>
		/// <returns><value>true</value> if the frst occurance of the specified
		/// namespace was removed; otherwise <value>false</value>.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		internal bool Remove (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException ("namespace");

			return collection.Remove (@namespace);
		}

		/// <summary>
		/// Gets the <see cref="MailKit.FolderNamespace"/> at the specified index.
		/// </summary>
		/// <param name="index">The index.</param>
		public FolderNamespace this [int index] {
			get {
				if (index < 0 || index > collection.Count)
					throw new ArgumentOutOfRangeException ("index");

				return collection[index];
			}
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets the enumerator.
		/// </summary>
		/// <returns>The enumerator.</returns>
		public IEnumerator<FolderNamespace> GetEnumerator ()
		{
			return collection.GetEnumerator ();
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets the enumerator.
		/// </summary>
		/// 
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return collection.GetEnumerator ();
		}

		#endregion
	}
}
