//
// FolderFeature.cs
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

namespace MailKit
{
	/// <summary>
	/// An optional feature that an <see cref="IMailFolder"/> may support.
	/// </summary>
	/// <remarks>
	/// An optional feature that an <see cref="IMailFolder"/> may support.
	/// </remarks>
	public enum FolderFeature
	{
		/// <summary>
		/// Indicates that the folder supports access rights.
		/// </summary>
		AccessRights,

		/// <summary>
		/// Indicates that the folder allows arbitrary annotations to be set on a message.
		/// </summary>
		Annotations,

		/// <summary>
		/// Indicates that the folder allows arbitrary metadata to be set.
		/// </summary>
		Metadata,

		/// <summary>
		/// Indicates that the folder uses modification sequences for every state change of a message.
		/// </summary>
		ModSequences,

		/// <summary>
		/// Indicates that the folder supports quick resynchronization when opening.
		/// </summary>
		QuickResync,

		/// <summary>
		/// Indicates that the folder supports quotas.
		/// </summary>
		Quotas,

		/// <summary>
		/// Indicates that the folder supports sorting messages.
		/// </summary>
		Sorting,

		/// <summary>
		/// Indicates that the folder supports threading messages.
		/// </summary>
		Threading,

		/// <summary>
		/// Indicates that the folder supports the use of UTF-8.
		/// </summary>
		UTF8,
	}
}
