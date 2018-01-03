//
// SearchTerm.cs
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

namespace MailKit.Search {
	/// <summary>
	/// A search term.
	/// </summary>
	/// <remarks>
	/// The search term as used by <see cref="SearchQuery"/>.
	/// </remarks>
	public enum SearchTerm {
		/// <summary>
		/// A search term that matches all messages.
		/// </summary>
		All,

		/// <summary>
		/// A search term that logically combines 2 or more other
		/// search expressions such that messages must match both
		/// expressions.
		/// </summary>
		And,

		/// <summary>
		/// A search term that matches answered messages.
		/// </summary>
		Answered,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within the <c>Bcc</c> header.
		/// </summary>
		BccContains,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within the body of the message.
		/// </summary>
		BodyContains,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within the <c>Cc</c> header.
		/// </summary>
		CcContains,

		/// <summary>
		/// A search term that matches deleted messages.
		/// </summary>
		Deleted,

		/// <summary>
		/// A search term that matches messages delivered after a specified date.
		/// </summary>
		DeliveredAfter,

		/// <summary>
		/// A search term that matches messages delivered before a specified date.
		/// </summary>
		DeliveredBefore,

		/// <summary>
		/// A search term that matches messages delivered on a specified date.
		/// </summary>
		DeliveredOn,

		/// <summary>
		/// A search term that matches draft messages.
		/// </summary>
		Draft,

		/// <summary>
		/// A search term that makes use of a predefined filter.
		/// </summary>
		Filter,

		/// <summary>
		/// A search term that matches flagged messages.
		/// </summary>
		Flagged,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within the <c>From</c> header.
		/// </summary>
		FromContains,

		/// <summary>
		/// A search term that modifies another search expression to allow
		/// fuzzy matching.
		/// </summary>
		Fuzzy,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within a particular header.
		/// </summary>
		HeaderContains,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// keyword.
		/// </summary>
		Keyword,

		/// <summary>
		/// A search term that matches messages that are larger than a
		/// specified number of bytes.
		/// </summary>
		LargerThan,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string anywhere within the message.
		/// </summary>
		MessageContains,

		/// <summary>
		/// A search term that matches messages that have the specified
		/// modification sequence value.
		/// </summary>
		ModSeq,

		/// <summary>
		/// A search term that matches new messages.
		/// </summary>
		New,

		/// <summary>
		/// A search term that modifies another search expression such that
		/// messages must match the logical inverse of the expression.
		/// </summary>
		Not,

		/// <summary>
		/// A search term that matches messages that have not been answered.
		/// </summary>
		NotAnswered,

		/// <summary>
		/// A search term that matches messages that have not been deleted.
		/// </summary>
		NotDeleted,

		/// <summary>
		/// A search term that matches messages that are not drafts.
		/// </summary>
		NotDraft,

		/// <summary>
		/// A search term that matches messages that have not been flagged.
		/// </summary>
		NotFlagged,

		/// <summary>
		/// A search term that matches messages that do not contain a specified
		/// keyword.
		/// </summary>
		NotKeyword,

		/// <summary>
		/// A search term that matches messages that are not recent.
		/// </summary>
		NotRecent,

		/// <summary>
		/// A search term that matches messages that have not been seen.
		/// </summary>
		NotSeen,

		/// <summary>
		/// A search term that matches messages that are older than a specified date.
		/// </summary>
		Older,

		/// <summary>
		/// A search term that logically combines 2 or more other
		/// search expressions such that messages only need to match
		/// one of the expressions.
		/// </summary>
		Or,

		/// <summary>
		/// A search term that matches messages that are recent.
		/// </summary>
		Recent,

		/// <summary>
		/// A search term that matches messages that have been seen.
		/// </summary>
		Seen,

		/// <summary>
		/// A search term that matches messages that were sent after a specified date.
		/// </summary>
		SentAfter,

		/// <summary>
		/// A search term that matches messages that were sent before a specified date.
		/// </summary>
		SentBefore,

		/// <summary>
		/// A search term that matches messages that were sent on a specified date.
		/// </summary>
		SentOn,

		/// <summary>
		/// A search term that matches messages that are smaller than a
		/// specified number of bytes.
		/// </summary>
		SmallerThan,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within the <c>Subject</c> header.
		/// </summary>
		SubjectContains,

		/// <summary>
		/// A search term that matches messages that contain a specified
		/// string within the <c>To</c> header.
		/// </summary>
		ToContains,

		/// <summary>
		/// A search term that matches messages included within a specified
		/// set of unique identifiers.
		/// </summary>
		Uid,

		/// <summary>
		/// A search term that matches messages that are younger than a specified date.
		/// </summary>
		Younger,

		// GMail SEARCH extensions

		/// <summary>
		/// A search term that matches messages with a specified GMail message identifier.
		/// </summary>
		GMailMessageId,

		/// <summary>
		/// A search term that matches messages with a specified GMail thread (conversation)
		/// identifier.
		/// </summary>
		GMailThreadId,

		/// <summary>
		/// A search term that matches messages with the specified GMail labels.
		/// </summary>
		GMailLabels,

		/// <summary>
		/// A search term that uses the GMail search syntax.
		/// </summary>
		GMailRaw,
	}
}
