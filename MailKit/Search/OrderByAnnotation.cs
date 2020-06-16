//
// OrderByAnnotation.cs
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

namespace MailKit.Search {
	/// <summary>
	/// Specifies an annotation-based sort order for search results.
	/// </summary>
	/// <remarks>
	/// You can combine multiple <see cref="OrderBy"/> rules to specify the sort
	/// order that <see cref="IMailFolder.Sort(SearchQuery,System.Collections.Generic.IList&lt;OrderBy&gt;,System.Threading.CancellationToken)"/>
	/// should return the results in.
	/// </remarks>
	public class OrderByAnnotation : OrderBy
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Search.OrderByAnnotation"/> class.
		/// </summary>
		/// <param name="entry">The annotation entry to sort by.</param>
		/// <param name="attribute">The annotation attribute to use for sorting.</param>
		/// <param name="order">The sort order.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="entry"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="attribute"/>is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="attribute"/> is not a valid attribute for sorting.
		/// </exception>
		public OrderByAnnotation (AnnotationEntry entry, AnnotationAttribute attribute, SortOrder order) : base (OrderByType.Annotation, order)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));

			if (attribute == null)
				throw new ArgumentNullException (nameof (attribute));

			if (attribute.Name != "value" || attribute.Scope == AnnotationScope.Both)
				throw new ArgumentException ("Only the \"value.priv\" and \"value.shared\" attributes can be used for sorting.", nameof (attribute));

			Entry = entry;
			Attribute = attribute;
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
	}
}
