//
// AppendRequest.cs
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
	/// A request for appending a message to a folder.
	/// </summary>
	/// <remarks>
	/// A request for appending a message to a folder.
	/// </remarks>
	public class AppendRequest : IAppendRequest
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AppendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AppendRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		public AppendRequest (MimeMessage message, MessageFlags flags = MessageFlags.None)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			Message = message;
			Flags = flags;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AppendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AppendRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="keywords">The message keywords.</param>
		/// <exception cref="ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="keywords"/> is <c>null</c>.</para>
		/// </exception>
		public AppendRequest (MimeMessage message, MessageFlags flags, IEnumerable<string> keywords)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			if (keywords == null)
				throw new ArgumentNullException (nameof (keywords));

			Keywords = keywords as ISet<string> ?? new HashSet<string> (keywords);
			Message = message;
			Flags = flags;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AppendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AppendRequest"/>.
		/// </remarks>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="internalDate">The internal date of the message.</param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		public AppendRequest (MimeMessage message, MessageFlags flags, DateTimeOffset internalDate)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			Message = message;
			Flags = flags;
			InternalDate = internalDate;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AppendRequest"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="AppendRequest"/>.
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
		public AppendRequest (MimeMessage message, MessageFlags flags, IEnumerable<string> keywords, DateTimeOffset internalDate)
		{
			if (message == null)
				throw new ArgumentNullException (nameof (message));

			if (keywords == null)
				throw new ArgumentNullException (nameof (keywords));

			Keywords = keywords as ISet<string> ?? new HashSet<string> (keywords);
			Message = message;
			Flags = flags;
			InternalDate = internalDate;
		}

		/// <summary>
		/// Get the message that should be appended to the folder.
		/// </summary>
		/// <remarks>
		/// Gets the message that should be appended to the folder.
		/// </remarks>
		/// <value>The message.</value>
		public MimeMessage Message {
			get; private set;
		}

		/// <summary>
		/// Get or set the message flags that should be set on the message.
		/// </summary>
		/// <remarks>
		/// Gets or sets the message flags that should be set on the message.
		/// </remarks>
		/// <value>The message flags.</value>
		public MessageFlags Flags {
			get; set;
		}

		/// <summary>
		/// Get or set the keywords that should be set on the message.
		/// </summary>
		/// <remarks>
		/// Gets or sets the keywords that should be set on the message.
		/// </remarks>
		/// <value>The keywords.</value>
		public ISet<string> Keywords {
			get; set;
		}

		/// <summary>
		/// Get or set the timestamp that should be used by folder as the <see cref="MessageSummaryItems.InternalDate"/>.
		/// </summary>
		/// <remarks>
		/// Gets or sets the timestamp that should be used by folder as the <see cref="MessageSummaryItems.InternalDate"/>.
		/// </remarks>
		/// <value>The date and time to use for the INTERNALDATE or <c>null</c> if it should be left up to the folder to decide.</value>
		public DateTimeOffset? InternalDate {
			get; set;
		}

		/// <summary>
		/// Get or set the list of annotations that should be set on the message.
		/// </summary>
		/// <remarks>
		/// <para>Gets or sets the list of annotations that should be set on the message.</para>
		/// <note type="note">
		/// <para>This feature is not supported by all folders.</para>
		/// <para>Use <see cref="IMailFolder.Supports(FolderFeature)"/> with the <see cref="FolderFeature.Annotations"/> enum value
		/// to determine if this feature is supported.</para>
		/// </note>
		/// </remarks>
		/// <value>The list of annotations.</value>
		public IList<Annotation> Annotations {
			get; set;
		}

		/// <summary>
		/// Get or set the transfer progress reporting mechanism.
		/// </summary>
		/// <remarks>
		/// Gets or sets the transfer progress reporting mechanism.
		/// </remarks>
		/// <value>The transfer progress mechanism.</value>
		public ITransferProgress TransferProgress {
			get; set;
		}
	}
}
