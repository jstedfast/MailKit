//
// IMailFolderExpungeExtensions.cs
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
		#region Expunge Extensions

		/// <summary>
		/// Expunge the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Expunges the specified uids, permanently removing them from the folder.</para>
		/// <note type="note">Normally, an <see cref="IMailFolder.MessageExpunged"/> event will be emitted for
		/// each message that is expunged. However, if the mail store supports the quick
		/// resynchronization feature and it has been enabled via the
		/// <see cref="MailKit.Net.Imap.ImapClient.Enable(MailKit.Net.Imap.ImapFeatures,System.Threading.CancellationToken)"/> method, then
		/// the <see cref="IMailFolder.MessagesVanished"/> event will be emitted rather than the
		/// <see cref="IMailFolder.MessageExpunged"/> event.</note>
		/// </remarks>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <see langword="null" />.
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
		public static void Expunge (this IMailFolder folder, IList<UniqueId> uids, CancellationToken cancellationToken = default)
		{
			folder.Expunge (uids, new ExpungeRequest (), cancellationToken);
		}

		/// <summary>
		/// Asynchronously expunge the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Asynchronously expunges the specified uids, permanently removing them from the folder.</para>
		/// <note type="note">Normally, an <see cref="IMailFolder.MessageExpunged"/> event will be emitted for
		/// each message that is expunged. However, if the mail store supports the quick
		/// resynchronization feature and it has been enabled via the
		/// <see cref="MailKit.Net.Imap.ImapClient.Enable(MailKit.Net.Imap.ImapFeatures,System.Threading.CancellationToken)"/> method, then
		/// the <see cref="IMailFolder.MessagesVanished"/> event will be emitted rather than the
		/// <see cref="IMailFolder.MessageExpunged"/> event.</note>
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <see langword="null" />.
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
		public static Task ExpungeAsync (this IMailFolder folder, IList<UniqueId> uids, CancellationToken cancellationToken = default)
		{
			return folder.ExpungeAsync (uids, new ExpungeRequest (), cancellationToken);
		}

		#endregion Expunge Extensions
	}
}
