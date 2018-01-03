//
// BodyPartCollection.cs
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using MimeKit.Utils;

namespace MailKit {
	/// <summary>
	/// A <see cref="BodyPart"/> collection.
	/// </summary>
	/// <remarks>
	/// A <see cref="BodyPart"/> collection.
	/// </remarks>
	public class BodyPartCollection : ICollection<BodyPart>
	{
		readonly List<BodyPart> collection = new List<BodyPart> ();

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.BodyPartCollection"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.BodyPartCollection"/>.
		/// </remarks>
		public BodyPartCollection ()
		{
		}

		/// <summary>
		/// Get the number of body parts in the collection.
		/// </summary>
		/// <remarks>
		/// Gets the number of body parts in the collection.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return collection.Count; }
		}

		/// <summary>
		/// Get whether or not this body part collection is read only.
		/// </summary>
		/// <remarks>
		/// Gets whether or not this body part collection is read only.
		/// </remarks>
		/// <value><c>true</c> if this collection is read only; otherwise, <c>false</c>.</value>
		public bool IsReadOnly {
			get { return false; }
		}

		/// <summary>
		/// Add the specified body part to the collection.
		/// </summary>
		/// <remarks>
		/// Adds the specified body part to the collection.
		/// </remarks>
		/// <param name="part">The body part.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		public void Add (BodyPart part)
		{
			if (part == null)
				throw new ArgumentNullException (nameof (part));

			collection.Add (part);
		}

		/// <summary>
		/// Clears the body part collection.
		/// </summary>
		/// <remarks>
		/// Removes all of the body parts from the collection.
		/// </remarks>
		public void Clear ()
		{
			collection.Clear ();
		}

		/// <summary>
		/// Checks if the collection contains the specified body part.
		/// </summary>
		/// <remarks>
		/// Determines whether or not the collection contains the specified body part.
		/// </remarks>
		/// <returns><value>true</value> if the specified body part exists; otherwise <value>false</value>.</returns>
		/// <param name="part">The body part.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		public bool Contains (BodyPart part)
		{
			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return collection.Contains (part);
		}

		/// <summary>
		/// Copies all of the body parts in the collection to the specified array.
		/// </summary>
		/// <remarks>
		/// Copies all of the body parts within the collection into the array,
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
		public void CopyTo (BodyPart[] array, int arrayIndex)
		{
			if (array == null)
				throw new ArgumentNullException (nameof (array));

			if (arrayIndex < 0 || arrayIndex + Count > array.Length)
				throw new ArgumentOutOfRangeException (nameof (arrayIndex));

			collection.CopyTo (array, arrayIndex);
		}

		/// <summary>
		/// Removes the specified body part.
		/// </summary>
		/// <remarks>
		/// Removes the specified body part.
		/// </remarks>
		/// <returns><value>true</value> if the body part was removed; otherwise <value>false</value>.</returns>
		/// <param name="part">The body part.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		public bool Remove (BodyPart part)
		{
			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return collection.Remove (part);
		}

		/// <summary>
		/// Get the body part at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the body part at the specified index.
		/// </remarks>
		/// <value>The body part at the specified index.</value>
		/// <param name="index">The index.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		public BodyPart this [int index] {
			get {
				if (index < 0 || index >= collection.Count)
					throw new ArgumentOutOfRangeException (nameof (index));

				return collection[index];
			}
		}

		/// <summary>
		/// Gets the index of the body part matching the specified URI.
		/// </summary>
		/// <remarks>
		/// <para>Finds the index of the body part matching the specified URI, if it exists.</para>
		/// <para>If the URI scheme is <c>"cid"</c>, then matching is performed based on the Content-Id header
		/// values, otherwise the Content-Location headers are used. If the provided URI is absolute and a child
		/// part's Content-Location is relative, then then the child part's Content-Location URI will be combined
		/// with the value of its Content-Base header, if available, otherwise it will be combined with the
		/// multipart/related part's Content-Base header in order to produce an absolute URI that can be
		/// compared with the provided absolute URI.</para>
		/// </remarks>
		/// <returns>The index of the part matching the specified URI if found; otherwise <c>-1</c>.</returns>
		/// <param name="uri">The URI of the body part.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uri"/> is <c>null</c>.
		/// </exception>
		public int IndexOf (Uri uri)
		{
			if (uri == null)
				throw new ArgumentNullException (nameof (uri));

			bool cid = uri.IsAbsoluteUri && uri.Scheme.ToLowerInvariant () == "cid";

			for (int index = 0; index < Count; index++) {
				var bodyPart = this[index] as BodyPartBasic;

				if (bodyPart == null)
					continue;

				if (uri.IsAbsoluteUri) {
					if (cid) {
						if (!string.IsNullOrEmpty (bodyPart.ContentId)) {
							var id = MimeUtils.EnumerateReferences (bodyPart.ContentId).FirstOrDefault ();

							if (id == uri.AbsolutePath)
								return index;
						}
					} else if (bodyPart.ContentLocation != null) {
						Uri absolute;

						if (!bodyPart.ContentLocation.IsAbsoluteUri)
							continue;

						absolute = bodyPart.ContentLocation;

						if (absolute == uri)
							return index;
					}
				} else if (bodyPart.ContentLocation == uri) {
					return index;
				}
			}

			return -1;
		}

		#region IEnumerable implementation

		/// <summary>
		/// Get the body part enumerator.
		/// </summary>
		/// <remarks>
		/// Gets the body part enumerator.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		public IEnumerator<BodyPart> GetEnumerator ()
		{
			return collection.GetEnumerator ();
		}

		#endregion

		#region IEnumerable implementation

		/// <summary>
		/// Get the body part enumerator.
		/// </summary>
		/// <remarks>
		/// Gets the body part enumerator.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		#endregion
	}
}
