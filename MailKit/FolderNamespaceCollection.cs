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
	public class FolderNamespaceCollection : ICollection<FolderNamespace>
	{
		readonly List<FolderNamespace> collection;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNamespaceCollection"/> class.
		/// </summary>
		/// <param name="namespaces">The folder namespaces.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespaces"/> is <c>null</c>.
		/// </exception>
		public FolderNamespaceCollection (IEnumerable<FolderNamespace> namespaces)
		{
			if (namespaces == null)
				throw new ArgumentNullException ("namespaces");

			collection = new List<FolderNamespace> (namespaces);
		}

		#region ICollection implementation

		/// <summary>
		/// Gets the number of folder namespaces contained in the collection.
		/// </summary>
		/// <value>The count.</value>
		public int Count {
			get { return collection.Count; }
		}

		public bool IsReadOnly {
			get { return false; }
		}

		public void Add (FolderNamespace item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");

			collection.Add (item);
		}

		public void Clear ()
		{
			collection.Clear ();
		}

		public bool Contains (FolderNamespace item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");

			return collection.Contains (item);
		}

		public void CopyTo (FolderNamespace[] array, int arrayIndex)
		{
			collection.CopyTo (array, arrayIndex);
		}

		public bool Remove (FolderNamespace item)
		{
			if (item == null)
				throw new ArgumentNullException ("item");

			return collection.Remove (item);
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

		IEnumerator IEnumerable.GetEnumerator ()
		{
			return collection.GetEnumerator ();
		}

		#endregion
	}
}
