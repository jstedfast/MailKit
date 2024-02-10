//
// ReplaceRequest.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// A request for replacing a message in a folder.
	/// </summary>
	/// <remarks>
	/// A request for replacing a message in a folder.
	/// </remarks>
	public class ReplaceRequest : AppendRequest, IReplaceRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ReplaceRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ReplaceRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		public ReplaceRequest (MimeMessage message, MessageFlags flags = MessageFlags.None) : base (message, flags)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ReplaceRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ReplaceRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="keywords">The message keywords.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="keywords"/> is <c>null</c>.</para>
		/// </exception>
		public ReplaceRequest (MimeMessage message, MessageFlags flags, IEnumerable<string> keywords) : base (message, flags, keywords)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ReplaceRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ReplaceRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="internalDate">The internal date of the message.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		public ReplaceRequest (MimeMessage message, MessageFlags flags, DateTimeOffset internalDate) : base (message, flags, internalDate)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ReplaceRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="ReplaceRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="keywords">The message keywords.</param>
		/// <param name="internalDate">The internal date of the message.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="keywords"/> is <c>null</c>.</para>
		/// </exception>
		public ReplaceRequest (MimeMessage message, MessageFlags flags, IEnumerable<string> keywords, DateTimeOffset internalDate) : base (message, flags, keywords, internalDate)
		{
		}

		/// <summary>
		/// Get or set the folder where the replacement message should be appended.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the folder where the replacement message should be appended.</para>
		/// <para>If no destination folder is specified, then the replacement message will be
		/// appended to the original folder.</para>
		/// </remarks>
		/// <value>The destination folder.</value>
		public IMailFolder Destination {
			get; set;
		}
	}
}
