//
// FolderQuota.cs
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
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// A folder quota.
	/// </summary>
	/// <remarks>
	/// A <see cref="FolderQuota"/> is returned by <see cref="IMailFolder.GetQuota(System.Threading.CancellationToken)"/>.
	/// </remarks>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
	/// </example>
	public class FolderQuota
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.FolderQuota"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="FolderQuota"/> with the specified root.
		/// </remarks>
		/// <param name="quotaRoot">The quota root.</param>
		public FolderQuota (IMailFolder? quotaRoot)
		{
			QuotaRoot = quotaRoot;
		}

		/// <summary>
		/// Get the quota root.
		/// </summary>
		/// <remarks>
		/// Gets the quota root. If the quota root is <see langword="null" />, then
		/// it suggests that the folder does not have a quota.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The quota root.</value>
		public IMailFolder? QuotaRoot {
			get; private set;
		}

		/// <summary>
		/// Get or set the message limit.
		/// </summary>
		/// <remarks>
		/// Gets or sets the message limit.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The message limit.</value>
		public long? MessageLimit {
			get; set;
		}

		/// <summary>
		/// Get or set the storage limit, in kilobytes.
		/// </summary>
		/// <remarks>
		/// Gets or sets the storage limit, in kilobytes.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The storage limit, in kilobytes.</value>
		public long? StorageLimit {
			get; set;
		}

		/// <summary>
		/// Get or set the current message count.
		/// </summary>
		/// <remarks>
		/// Gets or sets the current message count.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The current message count.</value>
		public long? CurrentMessageCount {
			get; set;
		}

		/// <summary>
		/// Gets or sets the size of the current storage, in kilobytes.
		/// </summary>
		/// <remarks>
		/// Gets or sets the size of the current storage, in kilobytes.
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="Capabilities"/>
		/// </example>
		/// <value>The size of the current storage, in kilobytes.</value>
		public long? CurrentStorageSize {
			get; set;
		}

		/// <summary>
		/// Get or set the mailbox limit.
		/// </summary>
		/// <remarks>
		/// Gets or sets the maximum number of mailboxes (folders) allowed by the quota root.
		/// </remarks>
		/// <value>The mailbox limit.</value>
		public long? MailboxLimit {
			get; set;
		}

		/// <summary>
		/// Get or set the current mailbox count.
		/// </summary>
		/// <remarks>
		/// Gets or sets the current number of mailboxes (folders) governed by the quota root.
		/// </remarks>
		/// <value>The current mailbox count.</value>
		public long? CurrentMailboxCount {
			get; set;
		}

		/// <summary>
		/// Get or set the annotation storage limit, in kilobytes.
		/// </summary>
		/// <remarks>
		/// Gets or sets the maximum size of all annotations and metadata governed by the
		/// quota root, in kilobytes.
		/// </remarks>
		/// <value>The annotation storage limit, in kilobytes.</value>
		public long? AnnotationStorageLimit {
			get; set;
		}

		/// <summary>
		/// Get or set the size of the current annotation storage, in kilobytes.
		/// </summary>
		/// <remarks>
		/// Gets or sets the current size of all annotations and metadata governed by the
		/// quota root, in kilobytes.
		/// </remarks>
		/// <value>The size of the current annotation storage, in kilobytes.</value>
		public long? CurrentAnnotationStorageSize {
			get; set;
		}

		/// <summary>
		/// Get the quota resources.
		/// </summary>
		/// <remarks>
		/// <para>Gets all of the quota resources reported by the server, keyed by resource name
		/// (case-insensitive).</para>
		/// <para>This includes the well-known <c>STORAGE</c>, <c>MESSAGE</c>, <c>MAILBOX</c> and
		/// <c>ANNOTATION-STORAGE</c> resources that are also exposed by the other properties of
		/// this class, as well as any other resource types that the server reports.</para>
		/// </remarks>
		/// <value>The quota resources.</value>
		public IDictionary<string, FolderQuotaResource> Resources {
			get;
		} = new Dictionary<string, FolderQuotaResource> (StringComparer.OrdinalIgnoreCase);
	}
}
