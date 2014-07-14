//
// BodyPartCollection.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
	/// A <see cref="BodyPart"/> collection.
	/// </summary>
	/// <remarks>
	/// A <see cref="BodyPart"/> collection.
	/// </remarks>
	public class BodyPartCollection : IEnumerable<BodyPart>
	{
		readonly List<BodyPart> collection = new List<BodyPart> ();

		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.BodyPartCollection"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="MailKit.BodyPartCollection"/>.
		/// </remarks>
		internal BodyPartCollection ()
		{
		}

		/// <summary>
		/// Adds the specified body part to the collection.
		/// </summary>
		/// <remarks>
		/// Adds the specified body part to the collection.
		/// </remarks>
		/// <param name="part">The body part.</param>
		internal void Add (BodyPart part)
		{
			if (part == null)
				throw new ArgumentNullException ("part");

			collection.Add (part);
		}

		/// <summary>
		/// Gets the number of body parts in the collection.
		/// </summary>
		/// <remarks>
		/// Gets the number of body parts in the collection.
		/// </remarks>
		/// <value>The count.</value>
		public int Count {
			get { return collection.Count; }
		}

		/// <summary>
		/// Gets the <see cref="MailKit.BodyPart"/> at the specified index.
		/// </summary>
		/// <remarks>
		/// Gets the <see cref="MailKit.BodyPart"/> at the specified index.
		/// </remarks>
		/// <value>THe body part at the specified index.</value>
		/// <param name="index">The index.</param>
		public BodyPart this [int index] {
			get {
				if (index < 0 || index >= collection.Count)
					throw new ArgumentOutOfRangeException ("index");

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
				throw new ArgumentNullException ("uri");

			bool cid = uri.IsAbsoluteUri && uri.Scheme.ToLowerInvariant () == "cid";

			for (int index = 0; index < Count; index++) {
				var bodyPart = this[index] as BodyPartBasic;

				if (bodyPart == null)
					continue;

				if (uri.IsAbsoluteUri) {
					if (cid) {
						if (bodyPart.ContentId == uri.AbsolutePath)
							return index;
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
		/// Gets the body part enumerator.
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
		/// Gets the body part enumerator.
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
