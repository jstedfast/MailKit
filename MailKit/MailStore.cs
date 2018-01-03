//
// MailStore.cs
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
	/// An abstract mail store implementation.
	/// </summary>
	/// <remarks>
	/// An abstract mail store implementation.
	/// </remarks>
	public abstract class MailStore : MailService, IMailStore
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.MailStore"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="MailKit.MailStore"/> class.
		/// </remarks>
		/// <param name="protocolLogger">The protocol logger.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="protocolLogger"/> is <c>null</c>.
		/// </exception>
		protected MailStore (IProtocolLogger protocolLogger) : base (protocolLogger)
		{
		}

		/// <summary>
		/// Gets the personal namespaces.
		/// </summary>
		/// <remarks>
		/// The personal folder namespaces contain a user's personal mailbox folders.
		/// </remarks>
		/// <value>The personal namespaces.</value>
		public abstract FolderNamespaceCollection PersonalNamespaces {
			get;
		}

		/// <summary>
		/// Gets the shared namespaces.
		/// </summary>
		/// <remarks>
		/// The shared folder namespaces contain mailbox folders that are shared with the user.
		/// </remarks>
		/// <value>The shared namespaces.</value>
		public abstract FolderNamespaceCollection SharedNamespaces {
			get;
		}

		/// <summary>
		/// Gets the other namespaces.
		/// </summary>
		/// <remarks>
		/// The other folder namespaces contain other mailbox folders.
		/// </remarks>
		/// <value>The other namespaces.</value>
		public abstract FolderNamespaceCollection OtherNamespaces {
			get;
		}

		/// <summary>
		/// Get whether or not the mail store supports quotas.
		/// </summary>
		/// <remarks>
		/// Gets whether or not the mail store supports quotas.
		/// </remarks>
		/// <value><c>true</c> if the mail store supports quotas; otherwise, <c>false</c>.</value>
		public abstract bool SupportsQuotas {
			get;
		}

		/// <summary>
		/// Get the Inbox folder.
		/// </summary>
		/// <remarks>
		/// <para>The Inbox folder is the default folder and always exists on the mail store.</para>
		/// <note type="note">This property will only be available after the client has been authenticated.</note>
		/// </remarks>
		/// <value>The Inbox folder.</value>
		public abstract IMailFolder Inbox {
			get;
		}

		/// <summary>
		/// Enable the quick resynchronization feature.
		/// </summary>
		/// <remarks>
		/// <para>Enables quick resynchronization when a folder is opened using the
		/// <see cref="MailFolder.Open(FolderAccess,uint,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="MailFolder.MessageExpunged"/> event is replaced
		/// with the <see cref="MailFolder.MessagesVanished"/> event.</para>
		/// <para>This method needs to be called immediately after calling one of the
		/// <a href="Overload_MailKit_MailService_Authenticate.htm">Authenticate</a> methods, before
		/// opening any folders.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public abstract void EnableQuickResync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously enable the quick resynchronization feature.
		/// </summary>
		/// <remarks>
		/// <para>Enables quick resynchronization when a folder is opened using the
		/// <see cref="MailFolder.Open(FolderAccess,uint,ulong,System.Collections.Generic.IList&lt;UniqueId&gt;,System.Threading.CancellationToken)"/>
		/// method.</para>
		/// <para>If this feature is enabled, the <see cref="MailFolder.MessageExpunged"/> event is replaced
		/// with the <see cref="MailFolder.MessagesVanished"/> event.</para>
		/// <para>This method needs to be called immediately after calling one of the
		/// <a href="Overload_MailKit_MailService_Authenticate.htm">Authenticate</a> methods, before
		/// opening any folders.</para>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public abstract Task EnableQuickResyncAsync (CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Get the specified special folder.
		/// </summary>
		/// <remarks>
		/// Not all mail stores support special folders. Each implementation
		/// should provide a way to determine if special folders are supported.
		/// </remarks>
		/// <returns>The folder if available; otherwise <c>null</c>.</returns>
		/// <param name="folder">The type of special folder.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="folder"/> is out of range.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
		/// </exception>
		public abstract IMailFolder GetFolder (SpecialFolder folder);

		/// <summary>
		/// Get the folder for the specified namespace.
		/// </summary>
		/// <remarks>
		/// Gets the folder for the specified namespace.
		/// </remarks>
		/// <returns>The folder.</returns>
		/// <param name="namespace">The namespace.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="namespace"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The folder could not be found.
		/// </exception>
		public abstract IMailFolder GetFolder (FolderNamespace @namespace);

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
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public virtual IList<IMailFolder> GetFolders (FolderNamespace @namespace, bool subscribedOnly, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetFolders (@namespace, StatusItems.None, subscribedOnly, cancellationToken);
		}

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
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public virtual Task<IList<IMailFolder>> GetFoldersAsync (FolderNamespace @namespace, bool subscribedOnly, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetFoldersAsync (@namespace, StatusItems.None, subscribedOnly, cancellationToken);
		}

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
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public abstract IList<IMailFolder> GetFolders (FolderNamespace @namespace, StatusItems items = StatusItems.None, bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken));

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
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public abstract Task<IList<IMailFolder>> GetFoldersAsync (FolderNamespace @namespace, StatusItems items = StatusItems.None, bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken));

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
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public abstract IMailFolder GetFolder (string path, CancellationToken cancellationToken = default (CancellationToken));

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
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="MailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="MailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="MailStore"/> is not authenticated.
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
		public abstract Task<IMailFolder> GetFolderAsync (string path, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata value.</returns>
		/// <param name="tag">The metadata tag.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public abstract string GetMetadata (MetadataTag tag, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata value.</returns>
		/// <param name="tag">The metadata tag.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public abstract Task<string> GetMetadataAsync (MetadataTag tag, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="tags"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public virtual MetadataCollection GetMetadata (IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMetadata (new MetadataOptions (), tags, cancellationToken);
		}

		/// <summary>
		/// Asynchronously gets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously gets the specified metadata.
		/// </remarks>
		/// <returns>The requested metadata.</returns>
		/// <param name="tags">The metadata tags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="tags"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public virtual Task<MetadataCollection> GetMetadataAsync (IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMetadataAsync (new MetadataOptions (), tags, cancellationToken);
		}

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="tags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public abstract MetadataCollection GetMetadata (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken));

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
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="tags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public abstract Task<MetadataCollection> GetMetadataAsync (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Sets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
		/// <param name="metadata">The metadata.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="metadata"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public abstract void SetMetadata (MetadataCollection metadata, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Asynchronously sets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the specified metadata.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="metadata">The metadata.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="metadata"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The folder does not support metadata.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The command failed.
		/// </exception>
		public abstract Task SetMetadataAsync (MetadataCollection metadata, CancellationToken cancellationToken = default (CancellationToken));

		/// <summary>
		/// Occurs when a remote message store receives an alert message from the server.
		/// </summary>
		/// <remarks>
		/// The <see cref="Alert"/> event is raised whenever the mail server sends an
		/// alert message.
		/// </remarks>
		public event EventHandler<AlertEventArgs> Alert;

		/// <summary>
		/// Raise the alert event.
		/// </summary>
		/// <remarks>
		/// Raises the alert event.
		/// </remarks>
		/// <param name="message">The alert message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		protected virtual void OnAlert (string message)
		{
			var handler = Alert;

			if (handler != null)
				handler (this, new AlertEventArgs (message));
		}
	}
}
