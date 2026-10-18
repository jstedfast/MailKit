//
// ImapFolderExamples.cs
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

using MimeKit;

using MailKit;
using MailKit.Search;
using MailKit.Security;
using MailKit.Net.Imap;

namespace MailKit.Examples {
	public static class ImapFolderExamples
	{
		#region ListFolders
		public static void ListFolders (ImapClient client)
		{
			// Most servers have a single personal namespace, but some have more (or none at all).
			foreach (var ns in client.PersonalNamespaces) {
				var folders = client.GetFolders (ns, StatusItems.Count | StatusItems.Unread, subscribedOnly: false);

				foreach (var folder in folders)
					Console.WriteLine ("{0}: {1} messages, {2} unread", folder.FullName, folder.Count, folder.Unread);
			}

			// Folders can also be walked as a tree, starting from the root of a namespace.
			var root = client.GetFolder (client.PersonalNamespaces[0]);

			foreach (var subfolder in root.GetSubfolders ())
				Console.WriteLine ("[{0}] {1}", subfolder.Attributes, subfolder.Name);
		}
		#endregion

		#region SpecialFolders
		public static IMailFolder? GetSentFolder (ImapClient client)
		{
			// The SPECIAL-USE and XLIST extensions let the server tell us which folder is used for Sent
			// messages (or Drafts, Junk, Trash, etc.), regardless of what it is named.
			if (client.Capabilities.Contains (ImapCapability.SpecialUse) || client.Capabilities.Contains (ImapCapability.XList))
				return client.GetFolder (SpecialFolder.Sent);

			// Otherwise, fall back to guessing based on common folder names.
			var personal = client.GetFolder (client.PersonalNamespaces[0]);

			foreach (var folder in personal.GetSubfolders ()) {
				if (folder.Name.Equals ("Sent", StringComparison.OrdinalIgnoreCase) ||
					folder.Name.Equals ("Sent Items", StringComparison.OrdinalIgnoreCase))
					return folder;
			}

			return null;
		}
		#endregion

		#region ManageFolders
		public static void ManageFolders (ImapClient client)
		{
			var personal = client.GetFolder (client.PersonalNamespaces[0]);

			// Create a new folder that can contain messages.
			var projects = personal.Create ("Projects", true);

			if (projects == null)
				return;

			projects.Subscribe ();

			// Rename it (and/or move it to a different parent folder).
			projects.Rename (personal, "Archived Projects");

			// Query the server for status information without opening (selecting) the folder.
			projects.Status (StatusItems.Count | StatusItems.Unread | StatusItems.UidNext | StatusItems.UidValidity);
			Console.WriteLine ("{0} has {1} messages ({2} unread)", projects.FullName, projects.Count, projects.Unread);

			projects.Unsubscribe ();
			projects.Delete ();
		}
		#endregion

		#region FetchSummaries
		public static void FetchSummaries (ImapClient client)
		{
			var inbox = client.Inbox;

			inbox.Open (FolderAccess.ReadOnly);

			// Request only the information that is actually needed. This is far more efficient than
			// downloading each message just to display a list of messages.
			var request = new FetchRequest (MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope |
				MessageSummaryItems.Flags | MessageSummaryItems.InternalDate | MessageSummaryItems.Size |
				MessageSummaryItems.PreviewText | MessageSummaryItems.BodyStructure,
				new [] { HeaderId.ListId, HeaderId.Importance });

			// Fetch the summaries for every message in the folder (0 to -1 means "first to last").
			foreach (var summary in inbox.Fetch (0, -1, request)) {
				Console.WriteLine ("[{0}] {1}", summary.UniqueId, summary.Envelope?.Subject);
				Console.WriteLine ("    From: {0}", summary.Envelope?.From);
				Console.WriteLine ("    Received: {0} ({1} bytes)", summary.InternalDate, summary.Size);
				Console.WriteLine ("    Flags: {0}", summary.Flags);
				Console.WriteLine ("    List-Id: {0}", summary.Headers?[HeaderId.ListId]);
				Console.WriteLine ("    Preview: {0}", summary.PreviewText);

				// The BodyStructure makes it possible to see attachments without downloading the message.
				foreach (var attachment in summary.Attachments)
					Console.WriteLine ("    Attachment: {0}", attachment.FileName);
			}

			inbox.Close ();
		}
		#endregion

		#region Flags
		public static void MarkAsRead (ImapClient client, IList<UniqueId> uids)
		{
			var inbox = client.Inbox;

			inbox.Open (FolderAccess.ReadWrite);

			// Add the \Seen flag. Setting Silent = true tells the server not to send back the
			// updated flags since we don't need them.
			inbox.Store (uids, new StoreFlagsRequest (StoreAction.Add, MessageFlags.Seen) { Silent = true });

			// Remove the \Flagged flag.
			inbox.Store (uids, new StoreFlagsRequest (StoreAction.Remove, MessageFlags.Flagged) { Silent = true });

			// Add a custom keyword (if the server allows clients to create new keywords).
			if (inbox.PermanentFlags.HasFlag (MessageFlags.UserDefined))
				inbox.Store (uids, new StoreFlagsRequest (StoreAction.Add, new [] { "$Processed" }) { Silent = true });

			// The same thing can also be done using the convenience extension methods.
			inbox.AddFlags (uids, MessageFlags.Seen, true);
			inbox.RemoveFlags (uids, MessageFlags.Flagged, true);

			inbox.Close ();
		}
		#endregion

		#region Search
		public static void Search (ImapClient client)
		{
			var inbox = client.Inbox;

			inbox.Open (FolderAccess.ReadOnly);

			// Find unread messages from a particular sender delivered in the last week.
			var query = SearchQuery.NotSeen
				.And (SearchQuery.FromContains ("alice@example.com"))
				.And (SearchQuery.DeliveredAfter (DateTime.Today.AddDays (-7)));

			IList<UniqueId> uids = inbox.Search (query);

			Console.WriteLine ("Found {0} matching messages.", uids.Count);

			// Queries can be combined using And, Or and Not.
			var important = SearchQuery.Or (SearchQuery.Flagged, SearchQuery.SubjectContains ("urgent"));
			var notJunk = SearchQuery.Not (SearchQuery.HasKeyword ("$Junk"));

			uids = inbox.Search (SearchQuery.And (important, notJunk));

			// If the server supports the ESEARCH extension, it is possible to request only some of the results.
			if (client.Capabilities.Contains (ImapCapability.ESearch)) {
				var results = inbox.Search (SearchOptions.Count | SearchOptions.Min | SearchOptions.Max, query);

				Console.WriteLine ("Count: {0}, Min: {1}, Max: {2}", results.Count, results.Min, results.Max);
			}

			inbox.Close ();
		}
		#endregion

		#region Sort
		public static void Sort (ImapClient client)
		{
			var inbox = client.Inbox;

			inbox.Open (FolderAccess.ReadOnly);

			// Server-side sorting requires the SORT extension.
			if (inbox.Supports (FolderFeature.Sorting)) {
				var orderBy = new [] { OrderBy.ReverseArrival, OrderBy.Subject };
				var uids = inbox.Sort (SearchQuery.All, orderBy);

				Console.WriteLine ("The newest message has UID {0}", uids[0]);
			}

			inbox.Close ();
		}
		#endregion

		#region AppendCopyMove
		public static void AppendCopyMove (ImapClient client, MimeMessage message)
		{
			var sent = client.GetFolder (SpecialFolder.Sent);
			var archive = client.GetFolder (SpecialFolder.Archive);

			if (sent == null || archive == null)
				return;

			// Save a copy of a message that was just sent. Append does not require the folder to be open.
			// If the server supports UIDPLUS, the UID of the new message is returned.
			UniqueId? uid = sent.Append (new AppendRequest (message, MessageFlags.Seen));

			if (uid.HasValue)
				Console.WriteLine ("The message was appended with UID {0}", uid.Value);

			// Copy and move operate on messages in the currently open folder.
			client.Inbox.Open (FolderAccess.ReadWrite);

			var uids = client.Inbox.Search (SearchQuery.Seen.And (SearchQuery.DeliveredBefore (DateTime.Today.AddYears (-1))));

			// Move the messages to the Archive folder. If the server does not support the MOVE extension,
			// MailKit emulates it using COPY, STORE and EXPUNGE. The returned UniqueIdMap maps each source
			// UID to its new UID in the destination folder (if the server supports UIDPLUS).
			UniqueIdMap map = client.Inbox.MoveTo (uids, archive);

			for (int i = 0; i < map.Source.Count; i++)
				Console.WriteLine ("{0} is now {1} in {2}", map.Source[i], map.Destination[i], archive.FullName);
		}
		#endregion

		#region DeleteAndExpunge
		public static void DeleteMessages (ImapClient client, IList<UniqueId> uids)
		{
			var inbox = client.Inbox;

			inbox.Open (FolderAccess.ReadWrite);

			// In IMAP, deleting a message is a two-step process: first the message is marked with
			// the \Deleted flag and then the folder is expunged.
			inbox.Store (uids, new StoreFlagsRequest (StoreAction.Add, MessageFlags.Deleted) { Silent = true });

			if (client.Capabilities.Contains (ImapCapability.UidPlus)) {
				// Only expunge the messages that we marked as deleted.
				inbox.Expunge (uids);
			} else {
				// Expunge *all* messages marked as \Deleted (including ones marked by other clients).
				inbox.Expunge ();
			}

			// Alternatively, closing the folder with expunge = true will also expunge deleted messages.
			inbox.Close (false);
		}
		#endregion
	}
}
