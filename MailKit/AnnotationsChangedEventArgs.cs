//
// AnnotationsChangedEventArgs.cs
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
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MailKit {
	/// <summary>
	/// Event args used when an annotation changes.
	/// </summary>
	/// <remarks>
	/// Event args used when an annotation changes.
	/// </remarks>
	public class AnnotationsChangedEventArgs : MessageEventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.AnnotationsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotationsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		internal AnnotationsChangedEventArgs (int index) : base (index)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.AnnotationsChangedEventArgs"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AnnotationsChangedEventArgs"/>.
		/// </remarks>
		/// <param name="index">The message index.</param>
		/// <param name="annotations">The annotations that changed.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="annotations"/> is <c>null</c>.
		/// </exception>
		public AnnotationsChangedEventArgs (int index, IEnumerable<Annotation> annotations) : base (index)
		{
			if (annotations == null)
				throw new ArgumentNullException (nameof (annotations));

			Annotations = new ReadOnlyCollection<Annotation> (annotations.ToArray ());
		}

		/// <summary>
		/// Get the annotations that changed.
		/// </summary>
		/// <remarks>
		/// Gets the annotations that changed.
		/// </remarks>
		/// <value>The annotation.</value>
		public IList<Annotation> Annotations {
			get; internal set;
		}

		/// <summary>
		/// Gets the updated mod-sequence value of the message, if available.
		/// </summary>
		/// <remarks>
		/// Gets the updated mod-sequence value of the message, if available.
		/// </remarks>
		/// <value>The mod-sequence value.</value>
		public ulong? ModSeq {
			get; internal set;
		}
	}
}
