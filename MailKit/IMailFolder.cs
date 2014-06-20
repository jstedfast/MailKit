//
// IMailFolder.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using MimeKit;
using MailKit.Search;

namespace MailKit {
	/// <summary>
	/// An interface for a mailbox folder as used by <see cref="IMailStore"/>.
	/// </summary>
	/// <remarks>
	/// Implemented by message stores such as <see cref="MailKit.Net.Imap.ImapClient"/>
	/// </remarks>
	public interface IMailFolder : IEnumerable<MimeMessage>
	{
		/// <summary>
		/// Get the parent folder.
		/// </summary>
		/// <remarks>
		/// Root-level folders do not have a parent folder.
		/// </remarks>
		/// <value>The parent folder.</value>
		IMailFolder ParentFolder { get; }

		/// <summary>
		/// Get the folder attributes.
		/// </summary>
		/// <remarks>
		/// Gets the folder attributes.
		/// </remarks>
		/// <value>The folder attributes.</value>
		FolderAttributes Attributes { get; }

		/// <summary>
		/// Get the permanent flags.
		/// </summary>
		/// <remarks>
		/// The permanent flags are the message flags that will persist between sessions.
		/// </remarks>
		/// <value>The permanent flags.</value>
		MessageFlags PermanentFlags { get; }

		/// <summary>
		/// Get the accepted flags.
		/// </summary>
		/// <remarks>
		/// The accepted flags are the message flags that will be accepted and persist
		/// for the current session. For the set of flags that will persist between
		/// sessions, see the <see cref="PermanentFlags"/> property.
		/// </remarks>
		/// <value>The accepted flags.</value>
		MessageFlags AcceptedFlags { get; }

		/// <summary>
		/// Get the directory separator.
		/// </summary>
		/// <remarks>
		/// Gets the directory separator.
		/// </remarks>
		/// <value>The directory separator.</value>
		char DirectorySeparator { get; }

		/// <summary>
		/// Get the read/write access of the folder.
		/// </summary>
		/// <remarks>
		/// Gets the read/write access of the folder.
		/// </remarks>
		/// <value>The read/write access.</value>
		FolderAccess Access { get; }

		/// <summary>
		/// Get whether or not the folder is a namespace folder.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the folder is a namespace folder.
		/// </remarks>
		/// <value><c>true</c> if the folder is a namespace folder; otherwise, <c>false</c>.</value>
		bool IsNamespace { get; }

		/// <summary>
		/// Get the full name of the folder.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of the full path of a file on a file system.
		/// </remarks>
		/// <value>The full name of the folder.</value>
		string FullName { get; }

		/// <summary>
		/// Get the name of the folder.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of the file name of a file on the file system.
		/// </remarks>
		/// <value>The name of the folder.</value>
		string Name { get; }

		/// <summary>
		/// Get whether or not the folder is subscribed.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the folder is subscribed.
		/// </remarks>
		/// <value><c>true</c> if the folder is subscribed; otherwise, <c>false</c>.</value>
		bool IsSubscribed { get; }

		/// <summary>
		/// Get whether or not the folder is currently open.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the folder is currently open.
		/// </remarks>
		/// <value><c>true</c> if the folder is currently open; otherwise, <c>false</c>.</value>
		bool IsOpen { get; }

		/// <summary>
		/// Get whether or not the folder exists.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the folder exists.
		/// </remarks>
		/// <value><c>true</c> if the folder exists; otherwise, <c>false</c>.</value>
		bool Exists { get; }

		/// <summary>
		/// Get whether or not the folder supports mod-sequences.
		/// </summary>
		/// <remarks>
		/// If mod-sequences are not supported by the folder, then all of the APIs that take a modseq
		/// argument will throw <see cref="System.NotSupportedException"/> and should not be used.
		/// </remarks>
		/// <value><c>true</c> if supports mod-sequences; otherwise, <c>false</c>.</value>
		bool SupportsModSeq { get; }

		/// <summary>
		/// Get the highest mod-sequence value of all messages in the mailbox.
		/// </summary>
		/// <remarks>
		/// Gets the highest mod-sequence value of all messages in the mailbox.
		/// </remarks>
		/// <value>The highest mod-sequence value.</value>
		ulong HighestModSeq { get; }

		/// <summary>
		/// Get the Unique ID validity.
		/// </summary>
		/// <remarks>
		/// <para>UIDs are only valid so long as the UID validity value remains unchanged. If and when
		/// the folder's <see cref="UidValidity"/> is changed, a client MUST discard its cache of UIDs
		/// along with any summary information that it may have and re-query the folder.</para>
		/// <para>The <see cref="UidValidity"/> will only be set after the folder has been opened.</para>
		/// </remarks>
		/// <value>The UID validity.</value>
		UniqueId? UidValidity { get; }

		/// <summary>
		/// Get the UID that the next message that is added to the folder will be assigned.
		/// </summary>
		/// <remarks>
		/// This value will only be set after the folder has been opened.
		/// </remarks>
		/// <value>The next UID.</value>
		UniqueId? UidNext { get; }

		/// <summary>
		/// Get the index of the first unread message in the folder.
		/// </summary>
		/// <remarks>
		/// This value will only be set after the folder has been opened.
		/// </remarks>
		/// <value>The index of the first unread message.</value>
		int FirstUnread { get; }

		/// <summary>
		/// Get the number of unread messages in the folder.
		/// </summary>
		/// <remarks>
		/// This value will only be set after calling <see cref="Status(StatusItems, System.Threading.CancellationToken)"/>
		/// with <see cref="StatusItems.Unread"/>.
		/// </remarks>
		/// <value>The number of unread messages.</value>
		int Unread { get; }

		/// <summary>
		/// Get the number of recently delivered messages.
		/// </summary>
		/// <remarks>
		/// Gets the number of recently delivered messages.
		/// </remarks>
		/// <value>The number of recently delivered messages.</value>
		int Recent { get; }

		/// <summary>
		/// Get the total number of messages in the folder.
		/// </summary>
		/// <remarks>
		/// Gets the total number of messages in the folder.
		/// </remarks>
		/// <value>The total number of messages.</value>
		int Count { get; }

		/// <summary>
		/// Opens the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// <para>This variant of the <see cref="Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// method is meant for quick resynchronization of the folder. Before calling this method,
		/// the <see cref="IMailStore.EnableQuickResync(CancellationToken)"/> method MUST be called.</para>
		/// <para>You should also make sure to add listeners to the <see cref="MessagesVanished"/> and
		/// <see cref="MessageFlagsChanged"/> events to get notifications of changes since
		/// the last time the folder was opened.</para>
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="uidValidity">The last known <see cref="UidValidity"/> value.</param>
		/// <param name="highestModSeq">The last known <see cref="HighestModSeq"/> value.</param>
		/// <param name="uids">The last known list of unique message identifiers.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		FolderAccess Open (FolderAccess access, UniqueId uidValidity, ulong highestModSeq, UniqueId[] uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously opens the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// <para>This variant of the <see cref="OpenAsync(FolderAccess,System.Threading.CancellationToken)"/>
		/// method is meant for quick resynchronization of the folder. Before calling this method,
		/// the <see cref="IMailStore.EnableQuickResync(CancellationToken)"/> method MUST be called.</para>
		/// <para>You should also make sure to add listeners to the <see cref="MessagesVanished"/> and
		/// <see cref="MessageFlagsChanged"/> events to get notifications of changes since
		/// the last time the folder was opened.</para>
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="uidValidity">The last known <see cref="UidValidity"/> value.</param>
		/// <param name="highestModSeq">The last known <see cref="HighestModSeq"/> value.</param>
		/// <param name="uids">The last known list of unique message identifiers.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<FolderAccess> OpenAsync (FolderAccess access, UniqueId uidValidity, ulong highestModSeq, UniqueId[] uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Open the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// Opens the folder using the requested folder access.
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		FolderAccess Open (FolderAccess access, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously open the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// Asynchronously opens the folder using the requested folder access.
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<FolderAccess> OpenAsync (FolderAccess access, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Close the folder, optionally expunging the messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </remarks>
		/// <param name="expunge">If set to <c>true</c>, expunge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Close (bool expunge = false, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously close the folder, optionally expunging the messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Asynchronously closes the folder, optionally expunging the messages marked for deletion.
		/// </remarks>
		/// <param name="expunge">If set to <c>true</c>, expunge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task CloseAsync (bool expunge = false, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Create a new subfolder with the given name.
		/// </summary>
		/// <remarks>
		/// Creates a new subfolder with the given name.
		/// </remarks>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="isMessageFolder"><c>true</c> if the folder will be used to contain messages; otherwise <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IMailFolder Create (string name, bool isMessageFolder, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously create a new subfolder with the given name.
		/// </summary>
		/// <remarks>
		/// Asynchronously creates a new subfolder with the given name.
		/// </remarks>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="isMessageFolder"><c>true</c> if the folder will be used to contain messages; otherwise <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IMailFolder> CreateAsync (string name, bool isMessageFolder, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Rename the folder.
		/// </summary>
		/// <remarks>
		/// Renames the folder.
		/// </remarks>
		/// <param name="parent">The new parent folder.</param>
		/// <param name="name">The new name of the folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Rename (IMailFolder parent, string name, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously rename the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously renames the folder.
		/// </remarks>
		/// <param name="parent">The new parent folder.</param>
		/// <param name="name">The new name of the folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task RenameAsync (IMailFolder parent, string name, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Delete the folder.
		/// </summary>
		/// <remarks>
		/// Deletes the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Delete (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously delete the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously deletes the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task DeleteAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Subscribe to the folder.
		/// </summary>
		/// <remarks>
		/// Subscribes to the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Subscribe (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously subscribe to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously subscribes to the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task SubscribeAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Unsubscribe from the folder.
		/// </summary>
		/// <remarks>
		/// Unsubscribes from the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Unsubscribe (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously unsubscribe from the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously unsubscribes from the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task UnsubscribeAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the subfolders.
		/// </summary>
		/// <remarks>
		/// Gets the subfolders.
		/// </remarks>
		/// <returns>The subfolders.</returns>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<IMailFolder> GetSubfolders (bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the subfolders.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the subfolders.
		/// </remarks>
		/// <returns>The subfolders.</returns>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<IMailFolder>> GetSubfoldersAsync (bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified subfolder.
		/// </summary>
		/// <remarks>
		/// Gets the specified subfolder.
		/// </remarks>
		/// <returns>The subfolder.</returns>
		/// <param name="name">The name of the subfolder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IMailFolder GetSubfolder (string name, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified subfolder.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified subfolder.
		/// </remarks>
		/// <returns>The subfolder.</returns>
		/// <param name="name">The name of the subfolder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IMailFolder> GetSubfolderAsync (string name, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Force the server to flush its state for the folder.
		/// </summary>
		/// <remarks>
		/// Forces the server to flush its state for the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Check (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously force the server to flush its state for the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously forces the server to flush its state for the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task CheckAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Update the values of the specified items.
		/// </summary>
		/// <remarks>
		/// <para>Updates the values of the specified items.</para>
		/// <para>The <see cref="Status(StatusItems, System.Threading.CancellationToken)"/> method
		/// MUST NOT be used on a folder that is already in the opened state. Instead, other ways
		/// of getting the desired information should be used.</para>
		/// <para>For example, a common use for the <see cref="Status(StatusItems,System.Threading.CancellationToken)"/>
		/// method is to get the number of unread messages in the folder. When the folder is open, however, it is
		/// possible to use the <see cref="IMailFolder.Search(MailKit.Search.SearchQuery, System.Threading.CancellationToken)"/>
		/// method to query for the list of unread messages.</para>
		/// </remarks>
		/// <param name="items">The items to update.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Status (StatusItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously update the values of the specified items.
		/// </summary>
		/// <remarks>
		/// <para>Updates the values of the specified items.</para>
		/// <para>The <see cref="Status(StatusItems, System.Threading.CancellationToken)"/> method
		/// MUST NOT be used on a folder that is already in the opened state. Instead, other ways
		/// of getting the desired information should be used.</para>
		/// <para>For example, a common use for the <see cref="Status(StatusItems,System.Threading.CancellationToken)"/>
		/// method is to get the number of unread messages in the folder. When the folder is open, however, it is
		/// possible to use the <see cref="IMailFolder.Search(MailKit.Search.SearchQuery, System.Threading.CancellationToken)"/>
		/// method to query for the list of unread messages.</para>
		/// </remarks>
		/// <param name="items">The items to update.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task StatusAsync (StatusItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Expunge the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="MessageExpunged"/> event will be emitted for each
		/// message that is expunged. However, if the mail store supports the quick
		/// resynchronization feature and it has been enabled via the
		/// <see cref="IMailStore.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="MessagesVanished"/> event will be emitted rather than the
		/// <see cref="MessageExpunged"/> event.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Expunge (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously expunge the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="MessageExpunged"/> event will be emitted for each
		/// message that is expunged. However, if the mail store supports the quick
		/// resynchronization feature and it has been enabled via the
		/// <see cref="IMailStore.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="MessagesVanished"/> event will be emitted rather than the
		/// <see cref="MessageExpunged"/> event.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task ExpungeAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Expunge the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="MessageExpunged"/> event will be emitted for each
		/// message that is expunged. However, if the mail store supports the quick
		/// resynchronization feature and it has been enabled via the
		/// <see cref="IMailStore.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="MessagesVanished"/> event will be emitted rather than the
		/// <see cref="MessageExpunged"/> event.</para>
		/// </remarks>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void Expunge (UniqueId[] uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously expunge the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="MessageExpunged"/> event will be emitted for each
		/// message that is expunged. However, if the mail store supports the quick
		/// resynchronization feature and it has been enabled via the
		/// <see cref="IMailStore.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="MessagesVanished"/> event will be emitted rather than the
		/// <see cref="MessageExpunged"/> event.</para>
		/// </remarks>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task ExpungeAsync (UniqueId[] uids, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId? Append (MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId?> AppendAsync (MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId? Append (MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId?> AppendAsync (MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] Append (MimeMessage[] messages, MessageFlags[] flags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified messages to the folder.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> AppendAsync (MimeMessage[] messages, MessageFlags[] flags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] Append (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified messages to the folder.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> AppendAsync (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] CopyTo (UniqueId[] uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> CopyToAsync (UniqueId[] uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Moves the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] MoveTo (UniqueId[] uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously moves the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> MoveToAsync (UniqueId[] uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void CopyTo (int[] indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously copies the specified messages to the destination folder.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task CopyToAsync (int[] indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Moves the specified messages to the destination folder.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void MoveTo (int[] indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously moves the specified messages to the destination folder.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task MoveToAsync (int[] indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message UIDs.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (UniqueId[] uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// Asynchronously fetches the message summaries for the specified message UIDs.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (UniqueId[] uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MessagesVanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (UniqueId[] uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MessagesVanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (UniqueId[] uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the messages between the two UIDs, inclusive.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two UIDs, inclusive.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID, or <c>null</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (UniqueId min, UniqueId? max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two UIDs, inclusive.
		/// </summary>
		/// <remarks>
		/// Asynchronously fetches the message summaries for the messages between the two UIDs, inclusive.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID, or <c>null</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (UniqueId min, UniqueId? max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the messages between the two UIDs (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the mail store supports the quick resynchronization feature and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MessagesVanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (UniqueId min, UniqueId? max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two UIDs (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the mail store supports the quick resynchronization feature and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MessagesVanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (UniqueId min, UniqueId? max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message indexes.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (int[] indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// Asynchronously fetches the message summaries for the specified message indexes.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (int[] indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (int[] indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Asynchronously fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (int[] indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// Asynchronously fetches the message summaries for the messages between the two indexes, inclusive.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		IEnumerable<MessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Asynchronously fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<IEnumerable<MessageSummary>> FetchAsync (int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeMessage GetMessage (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeMessage> GetMessageAsync (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeMessage> GetMessageAsync (int index, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeEntity GetBodyPart (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeEntity> GetBodyPartAsync (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeEntity GetBodyPart (UniqueId uid, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeEntity> GetBodyPartAsync (UniqueId uid, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeEntity GetBodyPart (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeEntity> GetBodyPartAsync (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MimeEntity GetBodyPart (int index, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the specified body part.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MimeEntity> GetBodyPartAsync (int index, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Gets a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Stream GetStream (UniqueId uid, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<Stream> GetStreamAsync (UniqueId uid, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Gets a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Stream GetStream (int index, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<Stream> GetStreamAsync (int index, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets a substream of the body part. If the starting offset is beyond
		/// the end of the body part, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the body part, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The desired body part.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Stream GetStream (UniqueId uid, BodyPart part, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets a substream of the body part. If the starting offset is beyond
		/// the end of the body part, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the body part, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The desired body part.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<Stream> GetStreamAsync (UniqueId uid, BodyPart part, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets a substream of the body part. If the starting offset is beyond
		/// the end of the body part, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the body part, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The desired body part.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Stream GetStream (int index, BodyPart part, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets a substream of the body part. If the starting offset is beyond
		/// the end of the body part, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the body part, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The desired body part.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<Stream> GetStreamAsync (int index, BodyPart part, int offset, int count, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void AddFlags (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task AddFlagsAsync (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void RemoveFlags (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task RemoveFlagsAsync (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void SetFlags (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task SetFlagsAsync (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] AddFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> AddFlagsAsync (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] RemoveFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> RemoveFlagsAsync (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] SetFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> SetFlagsAsync (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void AddFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task AddFlagsAsync (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void RemoveFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task RemoveFlagsAsync (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void SetFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task SetFlagsAsync (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		int[] AddFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int[]> AddFlagsAsync (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		int[] RemoveFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int[]> RemoveFlagsAsync (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		int[] SetFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<int[]> SetFlagsAsync (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] Search (SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> SearchAsync (SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Search the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] Search (SearchQuery query, OrderBy[] orderBy, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously search the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> SearchAsync (SearchQuery query, OrderBy[] orderBy, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Search the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] Search (UniqueId[] uids, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously search the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> SearchAsync (UniqueId[] uids, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Search the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		UniqueId[] Search (UniqueId[] uids, SearchQuery query, OrderBy[] orderBy, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously search the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<UniqueId[]> SearchAsync (UniqueId[] uids, SearchQuery query, OrderBy[] orderBy, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MessageThread[] Thread (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MessageThread[]> ThreadAsync (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MessageThread[] Thread (UniqueId[] uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MessageThread[]> ThreadAsync (UniqueId[] uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Occurs when the folder is deleted.
		/// </summary>
		/// <remarks>
		/// Emitted when the folder is deleted.
		/// </remarks>
		event EventHandler<EventArgs> Deleted;

		/// <summary>
		/// Occurs when the folder is renamed.
		/// </summary>
		/// <remarks>
		/// Emitted when the folder is renamed.
		/// </remarks>
		event EventHandler<FolderRenamedEventArgs> Renamed;

		/// <summary>
		/// Occurs when the folder is subscribed.
		/// </summary>
		/// <remarks>
		/// Emitted when the folder is subscribed.
		/// </remarks>
		event EventHandler<EventArgs> Subscribed;

		/// <summary>
		/// Occurs when the folder is unsubscribed.
		/// </summary>
		/// <remarks>
		/// Emitted when the folder is unsubscribed.
		/// </remarks>
		event EventHandler<EventArgs> Unsubscribed;

		/// <summary>
		/// Occurs when a message is expunged from the folder.
		/// </summary>
		/// <remarks>
		/// Emitted when a message is expunged from the folder.
		/// </remarks>
		event EventHandler<MessageEventArgs> MessageExpunged;

		/// <summary>
		/// Occurs when messages vanish from the folder.
		/// </summary>
		/// <remarks>
		/// Emitted when a messages vanish from the folder.
		/// </remarks>
		event EventHandler<MessagesVanishedEventArgs> MessagesVanished;

		/// <summary>
		/// Occurs when flags changed on a message.
		/// </summary>
		/// <remarks>
		/// Emitted when flags changed on a message.
		/// </remarks>
		event EventHandler<MessageFlagsChangedEventArgs> MessageFlagsChanged;

		/// <summary>
		/// Occurs when the UID validity changes.
		/// </summary>
		/// <remarks>
		/// Emitted when the UID validity changes.
		/// </remarks>
		event EventHandler<EventArgs> UidValidityChanged;

		/// <summary>
		/// Occurs when the message count changes.
		/// </summary>
		/// <remarks>
		/// Emitted when the message count changes.
		/// </remarks>
		event EventHandler<EventArgs> CountChanged;

		/// <summary>
		/// Occurs when the recent message count changes.
		/// </summary>
		/// <remarks>
		/// Emitted when the recent message count changes.
		/// </remarks>
		event EventHandler<EventArgs> RecentChanged;
	}
}
