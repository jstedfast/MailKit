//
// IFolder.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013 Jeffrey Stedfast
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
using System.Threading;
using System.Collections.Generic;

using MimeKit;

namespace MailKit {
	/// <summary>
	/// An interface for a mailbox folder as used by <see cref="IMessageStore"/>.
	/// </summary>
	/// <remarks>
	/// Implemented by message stores such as <see cref="MailKit.Net.Imap.ImapClient"/>
	/// </remarks>
	public interface IFolder
	{
		IFolder ParentFolder { get; }

		FolderAttributes Attributes { get; }
		MessageFlags PermanentFlags { get; }
		MessageFlags AcceptedFlags { get; }
		char DirectorySeparator { get; }
		FolderAccess Access { get; }
		string FullName { get; }
		string Name { get; }

		bool IsSubscribed { get; }
		bool IsOpen { get; }
		bool Exists { get; }

		ulong HighestModSeq { get; }
		string UidValidity { get; }
		string UidNext { get; }

		int FirstUnreadIndex { get; }
		int Recent { get; }
		int Count { get; }

		FolderAccess Open (FolderAccess access, CancellationToken cancellationToken);
		void Close (bool expunge, CancellationToken cancellationToken);

		void Rename (string newName, CancellationToken cancellationToken);
		void Create (CancellationToken cancellationToken);
		void Delete (CancellationToken cancellationToken);

		void Subscribe (CancellationToken cancellationToken);
		void Unsubscribe (CancellationToken cancellationToken);

		IEnumerable<IFolder> GetSubfolders (bool subscribedOnly, CancellationToken cancellationToken);

		void Check (CancellationToken cancellationToken);

		void Expunge (CancellationToken cancellationToken);
		void Expunge (string[] uids, CancellationToken cancellationToken);

		string Append (MimeMessage message, MessageFlags flags, CancellationToken cancellationToken);
		string Append (MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken);

		string[] Append (MimeMessage[] messages, MessageFlags[] flags, CancellationToken cancellationToken);
		string[] Append (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates, CancellationToken cancellationToken);

		string[] CopyTo (string[] uids, IFolder destination, CancellationToken cancellationToken);
		string[] MoveTo (string[] uids, IFolder destination, CancellationToken cancellationToken);

		void CopyTo (int[] indexes, IFolder destination, CancellationToken cancellationToken);
		void MoveTo (int[] indexes, IFolder destination, CancellationToken cancellationToken);

		FetchResult Fetch (string uid, MessageAttributes attributes, CancellationToken cancellationToken);
		IEnumerable<FetchResult> Fetch (string[] uids, MessageAttributes attributes, CancellationToken cancellationToken);

		FetchResult Fetch (int index, MessageAttributes attributes, CancellationToken cancellationToken);
		IEnumerable<FetchResult> Fetch (int[] indexes, MessageAttributes attributes, CancellationToken cancellationToken);

		// TODO: support fetching of individual mime parts and substreams
		MimeMessage GetMessage (string uid, CancellationToken cancellationToken);
		MimeMessage GetMessage (int index, CancellationToken cancellationToken);

		void AddFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken);
		void RemoveFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken);
		void SetFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken);

		void AddFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken);
		void RemoveFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken);
		void SetFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken);

		event EventHandler<EventArgs> Deleted;
		event EventHandler<FolderRenamedEventArgs> Renamed;

		event EventHandler<EventArgs> Subscribed;
		event EventHandler<EventArgs> Unsubscribed;

		event EventHandler<MessageEventArgs> Expunged;
		event EventHandler<FlagsChangedEventArgs> FlagsChanged;
		event EventHandler<EventArgs> UidValidityChanged;
		event EventHandler<EventArgs> CountChanged;
		event EventHandler<EventArgs> RecentChanged;
	}
}
