//
// FetchFlags.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
	/// A bitfield of <see cref="MessageSummary"/> fields.
	/// </summary>
	/// <remarks>
	/// <see cref="MessageSummaryItems"/> are used to specify which properties
	/// of <see cref="MessageSummary"/> should be populated by calls to
	/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;UniqueId&gt;, MessageSummaryItems, System.Threading.CancellationToken)"/>,
	/// <see cref="IMailFolder.Fetch(System.Collections.Generic.IList&lt;int&gt;, MessageSummaryItems, System.Threading.CancellationToken)"/>, or
	/// <see cref="IMailFolder.Fetch(int, int, MessageSummaryItems, System.Threading.CancellationToken)"/>.
	/// </remarks>
	[Flags]
	public enum MessageSummaryItems {
		/// <summary>
		/// Don't fetch any summary items.
		/// </summary>
		None           = 0,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Body"/>.</para>
		/// <note type="note">Unlike <see cref="BodyStructure"/>, <c>Body</c> will not populate the
		/// <see cref="BodyPart.ContentType"/> parameters nor will it populate the
		/// <see cref="BodyPartBasic.ContentDisposition"/>, <see cref="BodyPartBasic.ContentLanguage"/>
		/// or <see cref="BodyPartBasic.ContentLocation"/> properties of each <see cref="BodyPartBasic"/>
		/// body part. This makes <c>Body</c> far less useful than <c>BodyStructure</c> especially when
		/// it is desirable to determine whether or not a body part is an attachment.</note>
		/// </summary>
		Body           = 1 << 0,

		/// <summary>
		/// <para>Fetch the <see cref="IMessageSummary.Body"/> (but with more details than <see cref="Body"/>).</para>
		/// <note type="note">Unlike <see cref="Body"/>, <c>BodyStructure</c> will also populate the
		/// <see cref="BodyPart.ContentType"/> parameters as well as the
		/// <see cref="BodyPartBasic.ContentDisposition"/>, <see cref="BodyPartBasic.ContentLanguage"/>
		/// and <see cref="BodyPartBasic.ContentLocation"/> properties of each <see cref="BodyPartBasic"/>
		/// body part. The <c>Content-Disposition</c> information is especially important when trying to
		/// determine whether or not a body part is an attachment, for example.</note>
		/// </summary>
		BodyStructure  = 1 << 2,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.Envelope"/>.
		/// </summary>
		Envelope       = 1 << 3,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.Flags"/>.
		/// </summary>
		Flags          = 1 << 4,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.InternalDate"/>.
		/// </summary>
		InternalDate   = 1 << 5,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.Size"/>.
		/// </summary>
		Size           = 1 << 6,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.ModSeq"/>.
		/// </summary>
		ModSeq         = 1 << 7,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.References"/>.
		/// </summary>
		References     = 1 << 8,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.UniqueId"/>.
		/// </summary>
		UniqueId       = 1 << 9,

		#region GMail extension items

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.GMailMessageId"/>.
		/// </summary>
		GMailMessageId = 1 << 10,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.GMailThreadId"/>.
		/// </summary>
		GMailThreadId  = 1 << 11,

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.GMailLabels"/>.
		/// </summary>
		GMailLabels    = 1 << 12,

		#endregion

		/// <summary>
		/// Fetch the <see cref="IMessageSummary.PreviewText"/>.
		/// </summary>
		PreviewText    = 1 << 13,

		#region Macros

		/// <summary>
		/// A macro for <see cref="Envelope"/>, <see cref="Flags"/>, <see cref="InternalDate"/>,
		/// and <see cref="Size"/>.
		/// </summary>
		All           = Envelope | Flags | InternalDate | Size,

		/// <summary>
		/// A macro for <see cref="Flags"/>, <see cref="InternalDate"/>, and <see cref="Size"/>.
		/// </summary>
		Fast          = Flags | InternalDate | Size,

		/// <summary>
		/// A macro for <see cref="Body"/>, <see cref="Envelope"/>, <see cref="Flags"/>,
		/// <see cref="InternalDate"/>, and <see cref="Size"/>.
		/// </summary>
		Full          = Body | Envelope | Flags| InternalDate | Size,

		#endregion
	}
}
