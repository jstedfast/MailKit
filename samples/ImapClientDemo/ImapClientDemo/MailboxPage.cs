//
// MailboxPage.cs
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

#nullable enable

using System;
using System.Linq;
using System.Collections.Generic;

using MailKit;

namespace ImapClientDemo
{
	// Only immutable snapshots cross from the IMAP worker to the UI. Copy the
	// displayed values rather than expose live folders or mutable message summaries.
	sealed class FolderItem
	{
		public FolderItem (string fullName, bool isInbox)
		{
			FullName = fullName;
			IsInbox = isInbox;
		}

		public string FullName { get; }
		public bool IsInbox { get; }
	}

	sealed class MessageItem
	{
		public MessageItem (IMessageSummary summary)
		{
			UniqueId = summary.UniqueId;
			Subject = string.IsNullOrEmpty (summary.Envelope?.Subject) ? "(no subject)" : summary.Envelope.Subject;
			From = summary.Envelope?.From.ToString () ?? string.Empty;
			Date = summary.Envelope?.Date?.ToLocalTime ().ToString ("g") ?? string.Empty;
			IsUnread = !summary.Flags.GetValueOrDefault ().HasFlag (MessageFlags.Seen);
		}

		public UniqueId UniqueId { get; }
		public string Subject { get; }
		public string From { get; }
		public string Date { get; }
		public bool IsUnread { get; }
	}

	sealed class MailboxPage
	{
		public const int PageSize = 100;

		public MailboxPage (string folderName, uint uidValidity, int count, int pageIndex, long changeVersion, IEnumerable<IMessageSummary> summaries)
		{
			FolderName = folderName;
			UidValidity = uidValidity;
			Count = count;
			PageIndex = ClampPage (count, pageIndex);
			ChangeVersion = changeVersion;

			var (min, max) = GetRange (count, PageIndex);

			// Servers may include unsolicited FETCH summaries outside the requested
			// range, or summaries containing only flags. Neither is a complete page row.
			// Descending UID means newest additions first, not sorting by the sender's Date header.
			Messages = summaries
				.Where (summary => summary.Index >= min && summary.Index <= max &&
					summary.UniqueId.IsValid && summary.Envelope != null)
				.OrderByDescending (summary => summary.UniqueId)
				.Select (summary => new MessageItem (summary))
				.ToArray ();
		}

		public string FolderName { get; }
		public uint UidValidity { get; }
		public int Count { get; }
		public int PageIndex { get; }
		public long ChangeVersion { get; }
		public IReadOnlyList<MessageItem> Messages { get; }
		public bool HasNewer => PageIndex > 0;
		public bool HasOlder => PageIndex < ClampPage (Count, int.MaxValue);
		public string Description => Count == 0 ? "Empty folder" :
			$"Page {PageIndex + 1:N0} of {ClampPage (Count, int.MaxValue) + 1:N0} - {Count:N0} messages";

		static int ClampPage (int count, int pageIndex)
		{
			// Expunges can remove the last page while the user is viewing it.
			return Math.Clamp (pageIndex, 0, Math.Max (0, (count - 1) / PageSize));
		}

		internal static (int Min, int Max) GetRange (int count, int pageIndex)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			// MailKit's sequence indexes are zero-based and FETCH bounds are inclusive.
			// Page zero starts at the end of the folder; older pages work backward.
			// These are windows over the current mailbox, not stable pages across changes.
			int max = count - ClampPage (count, pageIndex) * PageSize - 1;

			// For an empty folder this yields (0, -1); the caller must skip FETCH,
			// since -1 is otherwise MailKit's sentinel for the last message.
			return (Math.Max (0, max - PageSize + 1), max);
		}
	}
}
