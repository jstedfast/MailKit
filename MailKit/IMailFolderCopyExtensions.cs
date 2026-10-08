//
// IMailFolderCopyExtensions.cs
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
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MailKit {
	public static partial class IMailFolderExtensions
	{
		#region Copy Extensions

		/// <summary>
		/// Copy the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified message to the destination folder.
		/// </remarks>
		/// <returns>The UID of the message in the destination folder, if available; otherwise, <see langword="null" />.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The mail store does not support the UIDPLUS extension.
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
		public static UniqueId? CopyTo (this IMailFolder folder, UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			var map = folder.CopyTo (new [] { uid }, new CopyRequest (destination), cancellationToken);

			return map.Destination.Count > 0 ? map.Destination[0] : null;
		}

		/// <summary>
		/// Asynchronously copy the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously copies the specified message to the destination folder.
		/// </remarks>
		/// <returns>The UID of the message in the destination folder, if available; otherwise, <see langword="null" />.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The mail store does not support the UIDPLUS extension.
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
		public static async Task<UniqueId?> CopyToAsync (this IMailFolder folder, UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			var map = await folder.CopyToAsync (new [] { uid }, new CopyRequest (destination), cancellationToken).ConfigureAwait (false);

			return map.Destination.Count > 0 ? map.Destination[0] : null;
		}

		/// <summary>
		/// Copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UID mapping of the messages in the destination folder, if available; otherwise an empty mapping.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The mail store does not support the UIDPLUS extension.
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
		public static UniqueIdMap CopyTo (this IMailFolder folder, IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			return folder.CopyTo (uids, new CopyRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Asynchronously copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UID mapping of the messages in the destination folder, if available; otherwise an empty mapping.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The mail store does not support the UIDPLUS extension.
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
		public static Task<UniqueIdMap> CopyToAsync (this IMailFolder folder, IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			return folder.CopyToAsync (uids, new CopyRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Copy the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified message to the destination folder.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> does not refer to a valid message index.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The destination folder does not belong to the <see cref="IMailStore"/>.
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
		public static void CopyTo (this IMailFolder folder, int index, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			if (index < 0 || index >= folder.Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			folder.CopyTo (new [] { index }, new CopyRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Asynchronously copy the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously copies the specified message to the destination folder.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The indexes of the message to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> does not refer to a valid message index.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The destination folder does not belong to the <see cref="IMailStore"/>.
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
		public static Task CopyToAsync (this IMailFolder folder, int index, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			if (index < 0 || index >= folder.Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			return folder.CopyToAsync (new [] { index }, new CopyRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		public static void CopyTo (this IMailFolder folder, IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			folder.CopyTo (indexes, new CopyRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Asynchronously copy the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		public static Task CopyToAsync (this IMailFolder folder, IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			return folder.CopyToAsync (indexes, new CopyRequest (destination), cancellationToken);
		}

		#endregion Copy Extensions

		#region Move Extensions

		/// <summary>
		/// Move the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Moves the specified message to the destination folder.
		/// </remarks>
		/// <returns>The UID of the message in the destination folder, if available; otherwise, <see langword="null" />.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// The mail store does not support the UIDPLUS extension.
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
		public static UniqueId? MoveTo (this IMailFolder folder, UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			var map = folder.MoveTo (new [] { uid }, new MoveRequest (destination), cancellationToken);

			return map.Destination.Count > 0 ? map.Destination[0] : null;
		}

		/// <summary>
		/// Asynchronously move the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously moves the specified message to the destination folder.
		/// </remarks>
		/// <returns>The UID of the message in the destination folder, if available; otherwise, <see langword="null" />.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uid"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// The mail store does not support the UIDPLUS extension.
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
		public static async Task<UniqueId?> MoveToAsync (this IMailFolder folder, UniqueId uid, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			var map = await folder.MoveToAsync (new [] { uid }, new MoveRequest (destination), cancellationToken).ConfigureAwait (false);

			return map.Destination.Count > 0 ? map.Destination[0] : null;
		}

		/// <summary>
		/// Move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Moves the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UID mapping of the messages in the destination folder, if available; otherwise an empty mapping.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// The mail store does not support the UIDPLUS extension.
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
		public static UniqueIdMap MoveTo (this IMailFolder folder, IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			return folder.MoveTo (uids, new MoveRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Asynchronously move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously moves the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UID mapping of the messages in the destination folder, if available; otherwise an empty mapping.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		/// The mail store does not support the UIDPLUS extension.
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
		public static Task<UniqueIdMap> MoveToAsync (this IMailFolder folder, IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			return folder.MoveToAsync (uids, new MoveRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Move the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Moves the specified message to the destination folder.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> does not refer to a valid message index.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The destination folder does not belong to the <see cref="IMailStore"/>.
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
		public static void MoveTo (this IMailFolder folder, int index, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			if (index < 0 || index >= folder.Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			folder.MoveTo (new [] { index }, new MoveRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Asynchronously move the specified message to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously moves the specified message to the destination folder.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="destination"/> is <see langword="null" />.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> does not refer to a valid message index.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The destination folder does not belong to the <see cref="IMailStore"/>.
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
		public static Task MoveToAsync (this IMailFolder folder, int index, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			if (index < 0 || index >= folder.Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			return folder.MoveToAsync (new [] { index }, new MoveRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Moves the specified messages to the destination folder.
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		public static void MoveTo (this IMailFolder folder, IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			folder.MoveTo (indexes, new MoveRequest (destination), cancellationToken);
		}

		/// <summary>
		/// Asynchronously move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously moves the specified messages to the destination folder.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="indexes">The indexes of the messages to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="IMailStore"/>.</para>
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
		public static Task MoveToAsync (this IMailFolder folder, IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default)
		{
			return folder.MoveToAsync (indexes, new MoveRequest (destination), cancellationToken);
		}

		#endregion Move Extensions
	}
}
