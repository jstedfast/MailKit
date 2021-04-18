//
// IMailFolderExtensions.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2021 .NET Foundation and Contributors
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

using MimeKit;

namespace MailKit {
	/// <summary>
	/// Extension methods for <see cref="IMailFolder"/> that provide backwards API compatability.
	/// </summary>
	/// <remarks>
	/// Extension methods for <see cref="IMailFolder"/> that provide backwards API compatability.
	/// </remarks>
	public static class IMailFolderExtensions
	{
		#region Fetch Extensions

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <example>
		/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
		/// </example>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<UniqueId> uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items);

			return folder.Fetch (uids, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<UniqueId> uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items);

			return folder.FetchAsync (uids, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.Fetch (uids, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.FetchAsync (uids, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.Fetch (uids, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<UniqueId> uids, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.FetchAsync (uids, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that
		/// have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items) { ChangedSince = modseq };

			return folder.Fetch (uids, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResyncAsync(CancellationToken)"/>,
		/// then this method will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that
		/// have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items) { ChangedSince = modseq };

			return folder.FetchAsync (uids, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that
		/// have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.Fetch (uids, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResyncAsync(CancellationToken)"/>,
		/// then this method will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that
		/// have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.FetchAsync (uids, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that
		/// have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.Fetch (uids, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the mail store supports quick resynchronization and the application has
		/// enabled this feature via <see cref="IMailStore.EnableQuickResyncAsync(CancellationToken)"/>,
		/// then this method will emit <see cref="IMailFolder.MessagesVanished"/> events for messages that
		/// have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.FetchAsync (uids, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<int> indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items);

			return folder.Fetch (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<int> indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items);

			return folder.FetchAsync (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<int> indexes, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.Fetch (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<int> indexes, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.FetchAsync (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<int> indexes, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.Fetch (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<int> indexes, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.FetchAsync (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items) { ChangedSince = modseq };

			return folder.Fetch (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// indexes that have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items) { ChangedSince = modseq };

			return folder.FetchAsync (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.Fetch (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// indexes that have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.FetchAsync (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.Fetch (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the specified message indexes
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the specified message
		/// indexes that have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="headers"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>One or more of the specified <paramref name="headers"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.FetchAsync (indexes, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items);

			return folder.Fetch (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the messages between
		/// the two indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items);

			return folder.FetchAsync (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, int min, int max, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.Fetch (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the messages between
		/// the two indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, int min, int max, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.FetchAsync (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, int min, int max, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.Fetch (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the messages between
		/// the two indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, int min, int max, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers);

			return folder.FetchAsync (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items) { ChangedSince = modseq };

			return folder.Fetch (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes
		/// (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the messages between
		/// the two indexes (inclusive) that have a higher mod-sequence value than the
		/// one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items) { ChangedSince = modseq };

			return folder.FetchAsync (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.Fetch (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes
		/// (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the messages between
		/// the two indexes (inclusive) that have a higher mod-sequence value than the
		/// one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the specified <paramref name="headers"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<HeaderId> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.FetchAsync (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Fetch the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static IList<IMessageSummary> Fetch (this IMailFolder folder, int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.Fetch (min, max, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously fetch the message summaries for the messages between the two indexes
		/// (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously fetches the message summaries for the messages between
		/// the two indexes (inclusive) that have a higher mod-sequence value than the
		/// one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the mail service may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="headers">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="headers"/> is <c>null</c>.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open.
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
		public static Task<IList<IMessageSummary>> FetchAsync (this IMailFolder folder, int min, int max, ulong modseq, MessageSummaryItems items, IEnumerable<string> headers, CancellationToken cancellationToken = default (CancellationToken))
		{
			var request = new FetchRequest (items, headers) { ChangedSince = modseq };

			return folder.FetchAsync (min, max, request, cancellationToken);
		}

		#endregion Fetch Extensions

		#region Store Flags Extensions

		static IStoreFlagsRequest GetStoreFlagsRequest (StoreAction action, bool silent, MessageFlags flags, HashSet<string> keywords = null, ulong? modseq = null)
		{
			if (action != StoreAction.Set && flags == MessageFlags.None && (keywords == null || keywords.Count == 0))
				throw new ArgumentException ("No flags were specified.", nameof (flags));

			if (keywords != null) {
				return new StoreFlagsRequest (action, flags, keywords) {
					UnchangedSince = modseq,
					Silent = silent
				};
			} else {
				return new StoreFlagsRequest (action, flags) {
					UnchangedSince = modseq,
					Silent = silent
				};
			}
		}

		/// <summary>
		/// Add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uid, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uid, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, UniqueId uid, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uid, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, UniqueId uid, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uid, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UIDs of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uid, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para><see cref="IMailFolder.MessageFlagsChanged"/>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uid, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UIDs of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, UniqueId uid, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uid, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, UniqueId uid, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uid, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UIDs of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uid, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="IMailStore"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="IMailStore"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="IMailStore"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, UniqueId uid, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uid, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UIDs of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, UniqueId uid, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uid, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, UniqueId uid, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uid, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> AddFlags (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> AddFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> AddFlags (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> AddFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> RemoveFlags (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> RemoveFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> RemoveFlags (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> RemoveFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> SetFlags (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> SetFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> SetFlags (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> SetFlagsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, int index, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (index, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, int index, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (index, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, int index, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (index, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, int index, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (index, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, IList<int> indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, IList<int> indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddFlags (this IMailFolder folder, IList<int> indexes, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddFlagsAsync (this IMailFolder folder, IList<int> indexes, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, int index, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (index, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, int index, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (index, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, int index, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (index, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, int index, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (index, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, IList<int> indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, IList<int> indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveFlags (this IMailFolder folder, IList<int> indexes, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveFlagsAsync (this IMailFolder folder, IList<int> indexes, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, int index, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (index, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, int index, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (index, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, int index, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (index, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, int index, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (index, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, IList<int> indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, IList<int> indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetFlags (this IMailFolder folder, IList<int> indexes, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetFlagsAsync (this IMailFolder folder, IList<int> indexes, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> AddFlags (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> AddFlagsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> AddFlags (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="keywords">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> AddFlagsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Add, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> RemoveFlags (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> RemoveFlagsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> RemoveFlags (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="keywords">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> RemoveFlagsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Remove, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> SetFlags (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> SetFlagsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags, null, modseq), cancellationToken);
		}

		/// <summary>
		/// Set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> SetFlags (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="keywords">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> SetFlagsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> keywords, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreFlagsRequest (StoreAction.Set, silent, flags, keywords, modseq), cancellationToken);
		}

		#endregion Store Flags Extensions

		#region Store Labels Extensions

		static IStoreLabelsRequest GetStoreLabelsRequest (StoreAction action, bool silent, IList<string> labels, ulong? modseq = null)
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (action != StoreAction.Set && labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return new StoreLabelsRequest (action, labels) {
				UnchangedSince = modseq,
				Silent = silent
			};
		}

		/// <summary>
		/// Add a set of labels to the specified message.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddLabels (this IMailFolder folder, UniqueId uid, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (new[] { uid }, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of labels to the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddLabelsAsync (this IMailFolder folder, UniqueId uid, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (new[] { uid }, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddLabels (this IMailFolder folder, IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of labels to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddLabelsAsync (this IMailFolder folder, IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified message.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UIDs of the message.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveLabels (this IMailFolder folder, UniqueId uid, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (new[] { uid }, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of labels from the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveLabelsAsync (this IMailFolder folder, UniqueId uid, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (new[] { uid }, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveLabels (this IMailFolder folder, IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of labels from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveLabelsAsync (this IMailFolder folder, IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified message.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UIDs of the message.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetLabels (this IMailFolder folder, UniqueId uid, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (new[] { uid }, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the labels of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the labels of the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetLabelsAsync (this IMailFolder folder, UniqueId uid, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (new[] { uid }, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetLabels (this IMailFolder folder, IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (uids, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the labels of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetLabelsAsync (this IMailFolder folder, IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> AddLabels (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreLabelsRequest (StoreAction.Add, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> AddLabelsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreLabelsRequest (StoreAction.Add, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> RemoveLabels (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreLabelsRequest (StoreAction.Remove, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> RemoveLabelsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreLabelsRequest (StoreAction.Remove, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<UniqueId> SetLabels (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (uids, GetStoreLabelsRequest (StoreAction.Set, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<UniqueId>> SetLabelsAsync (this IMailFolder folder, IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (uids, GetStoreLabelsRequest (StoreAction.Set, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified message.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddLabels (this IMailFolder folder, int index, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (new[] { index }, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of labels to the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddLabelsAsync (this IMailFolder folder, int index, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (new[] { index }, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void AddLabels (this IMailFolder folder, IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of labels to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task AddLabelsAsync (this IMailFolder folder, IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreLabelsRequest (StoreAction.Add, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified message.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveLabels (this IMailFolder folder, int index, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (new[] { index }, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of labels from the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveLabelsAsync (this IMailFolder folder, int index, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (new[] { index }, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void RemoveLabels (this IMailFolder folder, IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of labels from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task RemoveLabelsAsync (this IMailFolder folder, IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreLabelsRequest (StoreAction.Remove, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified message.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified message.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetLabels (this IMailFolder folder, int index, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (new[] { index }, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the labels of the specified message.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the labels of the specified message.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="labels"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="index"/> is invalid.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetLabelsAsync (this IMailFolder folder, int index, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (new[] { index }, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static void SetLabels (this IMailFolder folder, IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			folder.Store (indexes, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the labels of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
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
		public static Task SetLabelsAsync (this IMailFolder folder, IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreLabelsRequest (StoreAction.Set, silent, labels), cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> AddLabels (this IMailFolder folder, IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreLabelsRequest (StoreAction.Add, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> AddLabelsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreLabelsRequest (StoreAction.Add, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> RemoveLabels (this IMailFolder folder, IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreLabelsRequest (StoreAction.Remove, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> RemoveLabelsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreLabelsRequest (StoreAction.Remove, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static IList<int> SetLabels (this IMailFolder folder, IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.Store (indexes, GetStoreLabelsRequest (StoreAction.Set, silent, labels, modseq), cancellationToken);
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="IMailFolder.MessageLabelsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="labels"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
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
		/// <exception cref="FolderNotOpenException">
		/// The folder is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="MailFolder"/> does not support mod-sequences.
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
		public static Task<IList<int>> SetLabelsAsync (this IMailFolder folder, IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return folder.StoreAsync (indexes, GetStoreLabelsRequest (StoreAction.Set, silent, labels, modseq), cancellationToken);
		}

		#endregion Store Labels Extensions
	}
}
