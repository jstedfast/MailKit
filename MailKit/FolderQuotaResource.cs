//
// FolderQuotaResource.cs
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

namespace MailKit {
	/// <summary>
	/// The usage and limit of a quota resource.
	/// </summary>
	/// <remarks>
	/// <para>A <see cref="FolderQuotaResource"/> represents the current usage and the limit of a single
	/// resource (such as <c>STORAGE</c> or <c>MESSAGE</c>) that is governed by a quota root.</para>
	/// <para>See <a href="https://www.iana.org/assignments/imap-quota-resource-types/">IMAP Quota Resource Types</a>
	/// for a list of registered resource names.</para>
	/// </remarks>
	public sealed class FolderQuotaResource
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FolderQuotaResource"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderQuotaResource"/>.
		/// </remarks>
		/// <param name="name">The name of the resource.</param>
		/// <param name="usage">The current usage of the resource.</param>
		/// <param name="limit">The limit of the resource.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="name"/> is empty.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <para><paramref name="usage"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="limit"/> is negative.</para>
		/// </exception>
		public FolderQuotaResource (string name, long usage, long limit)
		{
			if (name is null)
				throw new ArgumentNullException (nameof (name));

			if (name.Length == 0)
				throw new ArgumentException ("The resource name cannot be empty.", nameof (name));

			if (usage < 0)
				throw new ArgumentOutOfRangeException (nameof (usage));

			if (limit < 0)
				throw new ArgumentOutOfRangeException (nameof (limit));

			Name = name;
			Usage = usage;
			Limit = limit;
		}

		/// <summary>
		/// Get the name of the resource.
		/// </summary>
		/// <remarks>
		/// Gets the name of the resource, such as <c>STORAGE</c>, <c>MESSAGE</c>, <c>MAILBOX</c>
		/// or <c>ANNOTATION-STORAGE</c>.
		/// </remarks>
		/// <value>The name of the resource.</value>
		public string Name {
			get; private set;
		}

		/// <summary>
		/// Get the current usage of the resource.
		/// </summary>
		/// <remarks>
		/// Gets the current usage of the resource, in the units defined by the resource type
		/// (for example, the <c>STORAGE</c> resource is measured in kilobytes).
		/// </remarks>
		/// <value>The current usage.</value>
		public long Usage {
			get; private set;
		}

		/// <summary>
		/// Get the limit of the resource.
		/// </summary>
		/// <remarks>
		/// Gets the limit of the resource, in the units defined by the resource type
		/// (for example, the <c>STORAGE</c> resource is measured in kilobytes).
		/// </remarks>
		/// <value>The limit.</value>
		public long Limit {
			get; private set;
		}
	}
}