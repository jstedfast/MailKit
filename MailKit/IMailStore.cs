//
// IMailStore.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MailKit {
	/// <summary>
	/// An interface for retreiving messages from a message store such as IMAP.
	/// </summary>
	/// <remarks>
	/// Implemented by <see cref="MailKit.Net.Imap.ImapClient"/>.
	/// </remarks>
	public interface IMailStore : IMailService
	{
		/// <summary>
		/// Get the personal namespaces.
		/// </summary>
		/// <remarks>
		/// The personal folder namespaces contain a user's personal mailbox folders.
		/// </remarks>
		/// <value>The personal namespaces.</value>
		FolderNamespaceCollection PersonalNamespaces { get; }

		/// <summary>
		/// Get the shared namespaces.
		/// </summary>
		/// <remarks>
		/// The shared folder namespaces contain mailbox folders that are shared with the user.
		/// </remarks>
		/// <value>The shared namespaces.</value>
		FolderNamespaceCollection SharedNamespaces { get; }

		/// <summary>
		/// Get the other namespaces.
		/// </summary>
		/// <remarks>
		/// The other folder namespaces contain other mailbox folders.
		/// </remarks>
		/// <value>The other namespaces.</value>
		FolderNamespaceCollection OtherNamespaces { get; }

		/// <summary>
		/// Get whether or not the mail store supports quotas.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the mail store supports quotas.
		/// </remarks>
		/// <value><c>true</c> if the mail store supports quotas; otherwise, <c>false</c>.</value>
		bool SupportsQuotas { get; }

		/// <summary>
		/// Get the Inbox folder.
		/// </summary>
		/// <remarks>
		/// The Inbox folder is the default folder and is typically the folder
		/// where all new messages are delivered.
		/// </remarks>
		/// <value>The Inbox folder.</value>
		IMailFolder Inbox { get; }

		/// <summary>
		/// Enable the quick resynchronization feature.
		/// </summary>
		/// <remarks>
		/// <para>Enables quick resynchronization when a folder is opened using the
		/// <see cref="IMailFolder.Open(FolderAccess,uint,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="IMailFolder.MessageExpunged"/> event
		/// is replaced with the <see cref="IMailFolder.MessagesVanished"/> event.</para>
		/// <para>This method needs to be called immediately after
		/// <see cref="IMailService.Authenticate(System.Net.ICredentials,System.Threading.CancellationToken)"/>,
		/// before the opening of any folders.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailService"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailService"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Quick resynchronization needs to be enabled before selecting a folder.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail store does not support quick resynchronization.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		void EnableQuickResync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously enable the quick resynchronization feature.
		/// </summary>
		/// <remarks>
		/// <para>Enables quick resynchronization when a folder is opened using the
		/// <see cref="IMailFolder.Open(FolderAccess,uint,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="IMailFolder.MessageExpunged"/> event
		/// is replaced with the <see cref="IMailFolder.MessagesVanished"/> event.</para>
		/// <para>This method needs to be called immediately after
		/// <see cref="IMailService.Authenticate(System.Net.ICredentials,System.Threading.CancellationToken)"/>,
		/// before the opening of any folders.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailService"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailService"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Quick resynchronization needs to be enabled before selecting a folder.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The mail store does not support quick resynchronization.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		Task EnableQuickResyncAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified special folder.
		/// </summary>
		/// <remarks>
		/// Not all message stores support the concept of special folders,
		/// so this method may return <c>null</c>.
		/// </remarks>
		/// <returns>The folder if available; otherwise <c>null</c>.</returns>
		/// <param name="folder">The type of special folder.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="folder"/> is out of range.
		/// </exception>
		IMailFolder GetFolder (SpecialFolder folder);

		/// <summary>
		/// Get the folder for the specified namespace.
		/// </summary>
		/// <remarks>
		/// The main reason to get the toplevel folder in a namespace is
		/// to list its child folders.
		/// </remarks>
		/// <returns>The folder.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		IMailFolder GetFolder (FolderNamespace @namespace);

		/// <summary>
		/// Get all of the folders within the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets all of the folders within the specified namespace.
		/// </remarks>
		/// <returns>The folders.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailService"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailService"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The namespace folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		IList<IMailFolder> GetFolders (FolderNamespace @namespace, bool subscribedOnly, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get all of the folders within the specified namespace.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets all of the folders within the specified namespace.
		/// </remarks>
		/// <returns>The folders.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailService"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailService"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The namespace folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		Task<IList<IMailFolder>> GetFoldersAsync (FolderNamespace @namespace, bool subscribedOnly, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get all of the folders within the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets all of the folders within the specified namespace.
		/// </remarks>
		/// <returns>The folders.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <param name="items">The status items to pre-populate.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailService"/> has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailService"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailService"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The namespace folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		IList<IMailFolder> GetFolders (FolderNamespace @namespace, StatusItems items = StatusItems.None, bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get all of the folders within the specified namespace.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets all of the folders within the specified namespace.
		/// </remarks>
		/// <returns>The folders.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <param name="items">The status items to pre-populate.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailService"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailService"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The namespace folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		Task<IList<IMailFolder>> GetFoldersAsync (FolderNamespace @namespace, StatusItems items = StatusItems.None, bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the folder for the specified path.
		/// </summary>
		/// <remarks>
		/// Gets the folder for the specified path.
		/// </remarks>
		/// <returns>The folder.</returns>
		/// <param name="path">The folder path.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="path"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		IMailFolder GetFolder (string path, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously get the folder for the specified path.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the folder for the specified path.
		/// </remarks>
		/// <returns>The folder.</returns>
		/// <param name="path">The folder path.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="path"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// A protocol error occurred.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		Task<IMailFolder> GetFolderAsync (string path, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata value.</returns>
		/// <param name="tag">The metadata tag.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		string GetMetadata (MetadataTag tag, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata value.</returns>
		/// <param name="tag">The metadata tag.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<string> GetMetadataAsync (MetadataTag tag, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MetadataCollection GetMetadata (IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MetadataCollection> GetMetadataAsync (IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="options">The metadata options.</param>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		MetadataCollection GetMetadata (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="options">The metadata options.</param>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task<MetadataCollection> GetMetadataAsync (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Sets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
		/// <param name="metadata">The metadata.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		void SetMetadata (MetadataCollection metadata, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously sets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the specified metadata.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="metadata">The metadata.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		Task SetMetadataAsync (MetadataCollection metadata, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Occurs when a remote message store receives an alert message from the server.
		/// </summary>
		/// <remarks>
		/// Some implementations, such as <see cref="MailKit.Net.Imap.ImapClient"/>,
		/// will emit Alert events when they receive alert messages from the server.
		/// </remarks>
		event EventHandler<AlertEventArgs> Alert;
	}
}
