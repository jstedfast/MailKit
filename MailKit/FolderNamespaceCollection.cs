//
// FolderNamespaceCollection.cs
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
using System.Collections.Generic;

using MimeKit.Utils;

namespace MailKit {
	/// <summary>
	/// A read-only collection of folder namespaces.
	/// </summary>
	/// <remarks>
	/// A read-only collection of folder namespaces.
	/// </remarks>
	public class FolderNamespaceCollection : IEnumerable<FolderNamespace>
	{
		readonly List<FolderNamespace> namespaces;

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderNamespaceCollection"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderNamespaceCollection"/>.
		/// </remarks>
		public FolderNamespaceCollection ()
		{
			namespaces = new List<FolderNamespace> ();
		}

		#region ICollection implementation

		/// <summary>
		/// Gets the number of folder namespaces contained in the collection.
		/// </summary>
		/// <remarks>
		/// Gets the number of folder namespaces contained in the collection.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return namespaces.Count; }
		}

		/// <summary>
		/// Adds the specified namespace.
		/// </summary>
		/// <remarks>
		/// Adds the specified namespace.
		/// </remarks>
		/// <param name="namespace">The namespace to add.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		public void Add (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException (nameof (@namespace));

			namespaces.Add (@namespace);
		}

		/// <summary>
		/// Removes all namespaces from the collection.
		/// </summary>
		/// <remarks>
		/// Removes all namespaces from the collection.
		/// </remarks>
		public void Clear ()
		{
			namespaces.Clear ();
		}

		/// <summary>
		/// Checks if the collection contains the specified namespace.
		/// </summary>
		/// <remarks>
		/// Checks if the collection contains the specified namespace.
		/// </remarks>
		/// <returns><value>true</value> if the specified namespace exists;
		/// otherwise <value>false</value>.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		public bool Contains (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException (nameof (@namespace));

			return namespaces.Contains (@namespace);
		}

		/// <summary>
		/// Removes the first occurance of the specified namespace.
		/// </summary>
		/// <remarks>
		/// Removes the first occurance of the specified namespace.
		/// </remarks>
		/// <returns><value>true</value> if the frst occurance of the specified
		/// namespace was removed; otherwise <value>false</value>.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		public bool Remove (FolderNamespace @namespace)
		{
			if (@namespace == null)
				throw new ArgumentNullException (nameof (@namespace));

			return namespaces.Remove (@namespace);
		}

		/// <summary>
		/// Gets the <see cref="MailKit.FolderNamespace"/> at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the <see cref="MailKit.FolderNamespace"/> at the specified index.
		/// </remarks>
		/// <value>The folder namespace at the specified index.</value>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="value"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public FolderNamespace this [int index] {
			get {
				if (index < 0 || index >= namespaces.Count)
					throw new ArgumentOutOfRangeException (nameof (index));

				return namespaces[index];
			}
			set {
				if (index < 0 || index >= namespaces.Count)
					throw new ArgumentOutOfRangeException (nameof (index));

				if (value == null)
					throw new ArgumentNullException (nameof (value));

				namespaces[index] = value;
			}
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets the enumerator.
		/// </summary>
		/// <remarks>
		/// Gets the enumerator.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<FolderNamespace> GetEnumerator ()
		{
			return namespaces.GetEnumerator ();
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Gets the enumerator.
		/// </summary>
		/// <remarks>
		/// Gets the enumerator.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return namespaces.GetEnumerator ();
		}

		#endregion

		static bool Escape (char directorySeparator)
		{
			return directorySeparator == '\\' || directorySeparator == '"';
		}

		/// <summary>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:MailKit.FolderNamespaceCollection"/>.
		/// </summary>
		/// <remarks>
		/// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:MailKit.FolderNamespaceCollection"/>.
		/// </remarks>
		/// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:MailKit.FolderNamespaceCollection"/>.</returns>
		public override string ToString ()
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			for (int i = 0; i < namespaces.Count; i++) {
				builder.Append ("(\"");
				if (Escape (namespaces[i].DirectorySeparator))
					builder.Append ('\\');
				builder.Append (namespaces[i].DirectorySeparator);
				builder.Append ("\" ");
				builder.Append (MimeUtils.Quote (namespaces[i].Path));
				builder.Append (")");
			}
			builder.Append (')');

			return builder.ToString ();
		}
	}
}
