//
// Annotation.cs
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
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// An annotation.
	/// </summary>
	/// <remarks>
	/// <para>An annotation.</para>
	/// <para>For more information about annotations, see
	/// <a href="https://tools.ietf.org/html/rfc5257">rfc5257</a>.</para>
	/// </remarks>
	public class Annotation
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Annotation"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="Annotation"/>.
		/// </remarks>
		/// <param name="entry">The annotation entry.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="entry"/> is <c>null</c>.
		/// </exception>
		public Annotation (AnnotationEntry entry)
		{
			if (entry == null)
				throw new ArgumentNullException (nameof (entry));

			Properties = new Dictionary<AnnotationAttribute, string> ();
			Entry = entry;
		}

		/// <summary>
		/// Get the annotation tag.
		/// </summary>
		/// <remarks>
		/// Gets the annotation tag.
		/// </remarks>
		/// <value>The annotation tag.</value>
		public AnnotationEntry Entry {
			get; private set;
		}

		/// <summary>
		/// Get the annotation properties.
		/// </summary>
		/// <remarks>
		/// Gets the annotation properties.
		/// </remarks>
		public Dictionary<AnnotationAttribute, string> Properties {
			get; private set;
		}
	}
}
