//
// AnnotationSearchQuery.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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

namespace MailKit.Search
{
	/// <summary>
	/// An annotation-based search query.
	/// </summary>
	/// <remarks>
	/// An annotation-based search query.
	/// </remarks>
	public class AnnotationSearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.AnnotationSearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new annotation-based search query.
		/// </remarks>
		/// <param name="entry">The annotation entry.</param>
		/// <param name="attribute">The annotation attribute.</param>
		/// <param name="value">The annotation attribute value.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="entry"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="attribute"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="attribute"/> is not a valid attribute for searching.
		/// </exception>
		public AnnotationSearchQuery (AnnotationEntry entry, AnnotationAttribute attribute, string value) : base (SearchTerm.Annotation)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));

			if (attribute == null)
				throw new ArgumentNullException (nameof (attribute));

			if (attribute.Name != "value")
				throw new ArgumentException ("Only the \"value\", \"value.priv\", and \"value.shared\" attributes can be searched.", nameof (attribute));

			Attribute = attribute;
			Entry = entry;
			Value = value;
		}

		/// <summary>
		/// Get the annotation entry.
		/// </summary>
		/// <remarks>
		/// Gets the annotation entry.
		/// </remarks>
		/// <value>The annotation entry.</value>
		public AnnotationEntry Entry {
			get; private set;
		}

		/// <summary>
		/// Get the annotation attribute.
		/// </summary>
		/// <remarks>
		/// Gets the annotation attribute.
		/// </remarks>
		/// <value>The annotation attribute.</value>
		public AnnotationAttribute Attribute {
			get; private set;
		}

		/// <summary>
		/// Get the annotation attribute value.
		/// </summary>
		/// <remarks>
		/// Gets the annotation attribute value.
		/// </remarks>
		/// <value>The annotation attribute value.</value>
		public string Value {
			get; private set;
		}
	}
}
