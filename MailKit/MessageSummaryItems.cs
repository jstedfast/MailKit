//
// FetchFlags.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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
	/// <see cref="IFolder.Fetch(UniqueId[], MessageSummaryItems, System.Threading.CancellationToken)"/>,
	/// <see cref="IFolder.Fetch(int[], MessageSummaryItems, System.Threading.CancellationToken)"/>, or
	/// <see cref="IFolder.Fetch(int, int, MessageSummaryItems, System.Threading.CancellationToken)"/>.
	/// </remarks>
	[Flags]
	public enum MessageSummaryItems {
		/// <summary>
		/// Don't fetch any summary items.
		/// </summary>
		None           = 0,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.Body"/>.
		/// </summary>
		Body           = 1 << 0,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.Body"/> (but with more details than <see cref="Body"/>).
		/// </summary>
		BodyStructure  = 1 << 2,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.Envelope"/>.
		/// </summary>
		Envelope       = 1 << 3,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.Flags"/>.
		/// </summary>
		Flags          = 1 << 4,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.InternalDate"/>.
		/// </summary>
		InternalDate   = 1 << 5,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.MessageSize"/>.
		/// </summary>
		MessageSize    = 1 << 6,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.Uid"/>.
		/// </summary>
		Uid            = 1 << 7,

		#region GMail extension items

		/// <summary>
		/// Fetch the <see cref="MessageSummary.GMailMessageId"/>.
		/// </summary>
		GMailMessageId = 1 << 8,

		/// <summary>
		/// Fetch the <see cref="MessageSummary.GMailThreadId"/>.
		/// </summary>
		GMailThreadId  = 1 << 9,

		#endregion

		#region Macros

		/// <summary>
		/// A macro for <see cref="Envelope"/>, <see cref="Flags"/>, <see cref="InternalDate"/>,
		/// and <see cref="MessageSize"/>.
		/// </summary>
		All           = Envelope | Flags | InternalDate | MessageSize,

		/// <summary>
		/// A macro for <see cref="Flags"/>, <see cref="InternalDate"/>, and <see cref="MessageSize"/>.
		/// </summary>
		Fast          = Flags | InternalDate | MessageSize,

		/// <summary>
		/// A macro for <see cref="Body"/>, <see cref="Envelope"/>, <see cref="Flags"/>,
		/// <see cref="InternalDate"/>, and <see cref="MessageSize"/>.
		/// </summary>
		Full          = Body | Envelope | Flags| InternalDate | MessageSize,

		#endregion
	}
}
