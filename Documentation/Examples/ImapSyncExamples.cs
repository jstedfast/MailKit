//
// ImapSyncExamples.cs
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
using System.Linq;
using System.Collections.Generic;

using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;

namespace MailKit.Examples {
	#region CacheState
	// The information that needs to be persisted (e.g. in a database) for each folder between sessions.
	public class CachedMessage
	{
		public UniqueId UniqueId { get; set; }
		public MessageFlags Flags { get; set; }
		public HashSet<string> Keywords { get; set; } = new HashSet<string> ();
		public ulong ModSeq { get; set; }
		public string? Subject { get; set; }
		public string? From { get; set; }
		public DateTimeOffset? InternalDate { get; set; }
	}

	public class FolderCache
	{
		// The UIDVALIDITY value of the folder at the time the cache was last synchronized.
		// If the server's UIDVALIDITY ever differs, all cached UIDs are invalid.
		public uint UidValidity { get; set; }

		// The UIDNEXT value of the folder at the time the cache was last synchronized.
		// Any message with a UID greater than or equal to this value is new.
		public uint UidNext { get; set; }

		// The HIGHESTMODSEQ value of the folder at the time the cache was last synchronized.
		// Only used if the server supports CONDSTORE.
		public ulong HighestModSeq { get; set; }

		public Dictionary<uint, CachedMessage> Messages { get; } = new Dictionary<uint, CachedMessage> ();

		public IList<UniqueId> GetCachedUids ()
		{
			return Messages.Keys.OrderBy (id => id).Select (id => new UniqueId (UidValidity, id)).ToList ();
		}

		public void Clear (uint uidValidity)
		{
			Messages.Clear ();
			UidValidity = uidValidity;
			HighestModSeq = 0;
			UidNext = 0;
		}

		public void Update (IMessageSummary summary)
		{
			if (!Messages.TryGetValue (summary.UniqueId.Id, out var message)) {
				message = new CachedMessage { UniqueId = summary.UniqueId };
				Messages.Add (summary.UniqueId.Id, message);
			}

			if (summary.Flags.HasValue)
				message.Flags = summary.Flags.Value;

			if (summary.Keywords != null)
				message.Keywords = new HashSet<string> (summary.Keywords);

			if (summary.ModSeq.HasValue)
				message.ModSeq = summary.ModSeq.Value;

			if (summary.Envelope != null) {
				message.Subject = summary.Envelope.Subject;
				message.From = summary.Envelope.From.ToString ();
			}

			if (summary.InternalDate.HasValue)
				message.InternalDate = summary.InternalDate;
		}
	}
	#endregion

	public static class ImapSyncExamples
	{
		const MessageSummaryItems NewMessageItems = MessageSummaryItems.UniqueId | MessageSummaryItems.Flags |
			MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate | MessageSummaryItems.ModSeq;

		#region FetchNewMessages
		static void FetchNewMessages (IMailFolder folder, FolderCache cache)
		{
			IList<UniqueId> range;

			if (cache.UidNext > 0) {
				// Only fetch messages with a UID >= the UIDNEXT value from the last sync.
				range = new UniqueIdRange (new UniqueId (folder.UidValidity, cache.UidNext), UniqueId.MaxValue);
			} else {
				range = UniqueIdRange.All;
			}

			foreach (var summary in folder.Fetch (range, new FetchRequest (NewMessageItems))) {
				// Note: Per the IMAP specification, a UID range of "N:*" always matches at least the
				// last message in the folder, even if its UID is less than N, so filter those out.
				if (summary.UniqueId.Id >= cache.UidNext)
					cache.Update (summary);
			}

			if (folder.UidNext.HasValue)
				cache.UidNext = folder.UidNext.Value.Id;
			else if (cache.Messages.Count > 0)
				cache.UidNext = cache.Messages.Keys.Max () + 1;
		}
		#endregion

		#region BasicSync
		// Synchronizes the cache with any IMAP server, without relying on any extensions.
		public static void BasicSync (ImapClient client, IMailFolder folder, FolderCache cache)
		{
			folder.Open (FolderAccess.ReadOnly);

			// Step 1: If the UIDVALIDITY has changed, all of our cached UIDs are invalid and the cache must be flushed.
			if (folder.UidValidity != cache.UidValidity)
				cache.Clear (folder.UidValidity);

			if (cache.Messages.Count > 0) {
				// Step 2: Find out which of our cached messages have been expunged from the server.
				var existing = new HashSet<uint> (folder.Search (SearchQuery.All).Select (uid => uid.Id));

				foreach (var id in cache.Messages.Keys.Where (id => !existing.Contains (id)).ToList ())
					cache.Messages.Remove (id);

				// Step 3: Re-fetch the flags for each of the remaining cached messages.
				var cached = cache.GetCachedUids ();

				if (cached.Count > 0) {
					foreach (var summary in folder.Fetch (cached, MessageSummaryItems.UniqueId | MessageSummaryItems.Flags))
						cache.Update (summary);
				}
			}

			// Step 4: Fetch the summary information for any new messages.
			FetchNewMessages (folder, cache);

			folder.Close ();
		}
		#endregion

		#region CondStoreSync
		// Synchronizes the cache using the CONDSTORE extension so that only the flags that have
		// actually changed since the last sync need to be downloaded.
		public static void CondStoreSync (ImapClient client, IMailFolder folder, FolderCache cache)
		{
			folder.Open (FolderAccess.ReadOnly);

			if (!folder.Supports (FolderFeature.ModSequences)) {
				folder.Close ();
				BasicSync (client, folder, cache);
				return;
			}

			if (folder.UidValidity != cache.UidValidity)
				cache.Clear (folder.UidValidity);

			if (cache.Messages.Count > 0) {
				// Expunged messages still need to be detected by comparing UIDs.
				var existing = new HashSet<uint> (folder.Search (SearchQuery.All).Select (uid => uid.Id));

				foreach (var id in cache.Messages.Keys.Where (id => !existing.Contains (id)).ToList ())
					cache.Messages.Remove (id);

				// Only fetch the flags for messages whose mod-sequence value has changed since our last sync.
				if (cache.HighestModSeq > 0 && folder.HighestModSeq != cache.HighestModSeq) {
					var request = new FetchRequest (MessageSummaryItems.UniqueId | MessageSummaryItems.Flags | MessageSummaryItems.ModSeq) {
						ChangedSince = cache.HighestModSeq
					};

					foreach (var summary in folder.Fetch (cache.GetCachedUids (), request))
						cache.Update (summary);
				}
			}

			FetchNewMessages (folder, cache);

			cache.HighestModSeq = folder.HighestModSeq;

			folder.Close ();
		}
		#endregion

		#region QuickResyncSync
		// Must be called once per connection, after authenticating but before opening any folders.
		public static bool EnableQuickResync (ImapClient client)
		{
			if (!client.Capabilities.Contains (ImapCapability.QuickResync))
				return false;

			client.Enable (ImapFeatures.QuickResync);

			return true;
		}

		// Synchronizes the cache using the QRESYNC extension. The server reports which messages have
		// been expunged and which flags have changed as part of opening the folder.
		public static void QuickResyncSync (ImapClient client, IMailFolder folder, FolderCache cache)
		{
			void OnMessagesVanished (object? sender, MessagesVanishedEventArgs e)
			{
				foreach (var uid in e.UniqueIds)
					cache.Messages.Remove (uid.Id);
			}

			void OnMessageFlagsChanged (object? sender, MessageFlagsChangedEventArgs e)
			{
				if (e.UniqueId.HasValue && cache.Messages.TryGetValue (e.UniqueId.Value.Id, out var message)) {
					message.Flags = e.Flags;
					message.Keywords = new HashSet<string> (e.Keywords);

					if (e.ModSeq.HasValue)
						message.ModSeq = e.ModSeq.Value;
				}
			}

			folder.MessagesVanished += OnMessagesVanished;
			folder.MessageFlagsChanged += OnMessageFlagsChanged;

			try {
				if (cache.UidValidity != 0 && cache.HighestModSeq > 0) {
					// Pass the state from our last sync to the server. If the UIDVALIDITY still matches,
					// the server will emit VANISHED (EARLIER) and FETCH responses for everything that
					// changed while we were away, which raise the events above.
					folder.Open (FolderAccess.ReadOnly, cache.UidValidity, cache.HighestModSeq, cache.GetCachedUids ());
				} else {
					folder.Open (FolderAccess.ReadOnly);
				}

				if (folder.UidValidity != cache.UidValidity) {
					// The server ignores the QRESYNC parameters if the UIDVALIDITY doesn't match.
					cache.Clear (folder.UidValidity);
				}

				FetchNewMessages (folder, cache);

				cache.HighestModSeq = folder.HighestModSeq;

				folder.Close ();
			} finally {
				folder.MessagesVanished -= OnMessagesVanished;
				folder.MessageFlagsChanged -= OnMessageFlagsChanged;
			}
		}
		#endregion
	}
}
