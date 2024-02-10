//
// IMailFolderAppendExtensions.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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
	public static partial class IMailFolderExtensions
	{
		#region Append Extensions

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static UniqueId? Append (this IMailFolder folder, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Append (folder, FormatOptions.Default, message, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static Task<UniqueId?> AppendAsync (this IMailFolder folder, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return AppendAsync (folder, FormatOptions.Default, message, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static UniqueId? Append (this IMailFolder folder, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Append (folder, FormatOptions.Default, message, flags, date, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static Task<UniqueId?> AppendAsync (this IMailFolder folder, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return AppendAsync (folder, FormatOptions.Default, message, flags, date, cancellationToken, progress);
		}

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="annotations">The message annotations.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// One or more <paramref name="annotations"/> does not define any properties.
		/// </exception>"
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
		public static UniqueId? Append (this IMailFolder folder, MimeMessage message, MessageFlags flags, DateTimeOffset? date, IList<Annotation> annotations, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Append (folder, FormatOptions.Default, message, flags, date, annotations, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="annotations">The message annotations.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// One or more <paramref name="annotations"/> does not define any properties.
		/// </exception>"
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
		public static Task<UniqueId?> AppendAsync (this IMailFolder folder, MimeMessage message, MessageFlags flags, DateTimeOffset? date, IList<Annotation> annotations, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return AppendAsync (folder, FormatOptions.Default, message, flags, date, annotations, cancellationToken, progress);
		}

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static UniqueId? Append (this IMailFolder folder, FormatOptions options, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new AppendRequest (message, flags) {
				TransferProgress = progress
			};

			return folder.Append (options, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static Task<UniqueId?> AppendAsync (this IMailFolder folder, FormatOptions options, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new AppendRequest (message, flags) {
				TransferProgress = progress
			};

			return folder.AppendAsync (options, request, cancellationToken);
		}

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static UniqueId? Append (this IMailFolder folder, FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new AppendRequest (message, flags, date) {
				TransferProgress = progress
			};

			return folder.Append (options, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static Task<UniqueId?> AppendAsync (this IMailFolder folder, FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new AppendRequest (message, flags, date) {
				TransferProgress = progress
			};

			return folder.AppendAsync (options, request, cancellationToken);
		}

		/// <summary>
		/// Append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="annotations">The message annotations.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static UniqueId? Append (this IMailFolder folder, FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset? date, IList<Annotation> annotations, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new AppendRequest (message, flags) {
				TransferProgress = progress,
				Annotations = annotations,
				InternalDate = date
			};

			return folder.Append (options, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="annotations">The message annotations.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static Task<UniqueId?> AppendAsync (this IMailFolder folder, FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset? date, IList<Annotation> annotations, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new AppendRequest (message, flags) {
				TransferProgress = progress,
				Annotations = annotations,
				InternalDate = date
			};

			return folder.AppendAsync (options, request, cancellationToken);
		}

		/// <summary>
		/// Append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages does not match the number of flags.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static IList<UniqueId> Append (this IMailFolder folder, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Append (folder, FormatOptions.Default, messages, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages does not match the number of flags.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static Task<IList<UniqueId>> AppendAsync (this IMailFolder folder, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return AppendAsync (folder, FormatOptions.Default, messages, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="dates"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages, flags, and dates do not match.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static IList<UniqueId> Append (this IMailFolder folder, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Append (folder, FormatOptions.Default, messages, flags, dates, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="dates"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages, flags, and dates do not match.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
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
		public static Task<IList<UniqueId>> AppendAsync (this IMailFolder folder, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return AppendAsync (folder, FormatOptions.Default, messages, flags, dates, cancellationToken, progress);
		}

		/// <summary>
		/// Append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages does not match the number of flags.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static IList<UniqueId> Append (this IMailFolder folder, FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (messages == null)
				throw new ArgumentNullException (nameof (messages));

			for (int i = 0; i < messages.Count; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException (nameof (flags));

			if (messages.Count != flags.Count)
				throw new ArgumentException ("The number of messages and the number of flags must be equal.");

			var requests = new AppendRequest[messages.Count];
			for (int i = 0; i < messages.Count; i++) {
				requests[i] = new AppendRequest (messages[i], flags[i]) {
					TransferProgress = progress
				};
			}

			return folder.Append (options, requests, cancellationToken);
		}

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages does not match the number of flags.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static Task<IList<UniqueId>> AppendAsync (this IMailFolder folder, FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (messages == null)
				throw new ArgumentNullException (nameof (messages));

			for (int i = 0; i < messages.Count; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException (nameof (flags));

			if (messages.Count != flags.Count)
				throw new ArgumentException ("The number of messages and the number of flags must be equal.");

			var requests = new AppendRequest[messages.Count];
			for (int i = 0; i < messages.Count; i++) {
				requests[i] = new AppendRequest (messages[i], flags[i]) {
					TransferProgress = progress
				};
			}

			return folder.AppendAsync (options, requests, cancellationToken);
		}

		/// <summary>
		/// Append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="dates"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages, flags, and dates do not match.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static IList<UniqueId> Append (this IMailFolder folder, FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (messages == null)
				throw new ArgumentNullException (nameof (messages));

			for (int i = 0; i < messages.Count; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException (nameof (flags));

			if (messages.Count != flags.Count)
				throw new ArgumentException ("The number of messages and the number of flags must be equal.");

			if (dates == null)
				throw new ArgumentNullException (nameof (dates));

			if (messages.Count != dates.Count)
				throw new ArgumentException ("The number of messages and the number of dates must be equal.");

			var requests = new AppendRequest[messages.Count];
			for (int i = 0; i < messages.Count; i++) {
				requests[i] = new AppendRequest (messages[i], flags[i], dates[i]) {
					TransferProgress = progress
				};
			}

			return folder.Append (options, requests, cancellationToken);
		}

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="messages"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="flags"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="dates"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="messages"/> is null.</para>
		/// <para>-or-</para>
		/// <para>The number of messages, flags, and dates do not match.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// Internationalized formatting was requested but is not supported by the server.
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
		public static Task<IList<UniqueId>> AppendAsync (this IMailFolder folder, FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (messages == null)
				throw new ArgumentNullException (nameof (messages));

			for (int i = 0; i < messages.Count; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException (nameof (flags));

			if (messages.Count != flags.Count)
				throw new ArgumentException ("The number of messages and the number of flags must be equal.");

			if (dates == null)
				throw new ArgumentNullException (nameof (dates));

			if (messages.Count != dates.Count)
				throw new ArgumentException ("The number of messages and the number of dates must be equal.");

			var requests = new AppendRequest[messages.Count];
			for (int i = 0; i < messages.Count; i++) {
				requests[i] = new AppendRequest (messages[i], flags[i], dates[i]) {
					TransferProgress = progress
				};
			}

			return folder.AppendAsync (options, requests, cancellationToken);
		}

		#endregion Append Extensions

		#region Replace Extensions

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, UniqueId uid, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Replace (folder, FormatOptions.Default, uid, message, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, UniqueId uid, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return ReplaceAsync (folder, FormatOptions.Default, uid, message, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, UniqueId uid, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Replace (folder, FormatOptions.Default, uid, message, flags, date, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, UniqueId uid, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return ReplaceAsync (folder, FormatOptions.Default, uid, message, flags, date, cancellationToken, progress);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, FormatOptions options, UniqueId uid, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags) {
				TransferProgress = progress
			};

			return folder.Replace (options, uid, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, FormatOptions options, UniqueId uid, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags) {
				TransferProgress = progress
			};

			return folder.ReplaceAsync (options, uid, request, cancellationToken);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, FormatOptions options, UniqueId uid, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags, date) {
				TransferProgress = progress
			};

			return folder.Replace (options, uid, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="uid">The UID of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, FormatOptions options, UniqueId uid, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags, date) {
				TransferProgress = progress
			};

			return folder.ReplaceAsync (options, uid, request, cancellationToken);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, int index, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Replace (folder, FormatOptions.Default, index, message, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, int index, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return ReplaceAsync (folder, FormatOptions.Default, index, message, flags, cancellationToken, progress);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, int index, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return Replace (folder, FormatOptions.Default, index, message, flags, date, cancellationToken, progress);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
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
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, int index, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			return ReplaceAsync (folder, FormatOptions.Default, index, message, flags, date, cancellationToken, progress);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, FormatOptions options, int index, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags) {
				TransferProgress = progress
			};

			return folder.Replace (options, index, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Asynchronously replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, FormatOptions options, int index, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags) {
				TransferProgress = progress
			};

			return folder.ReplaceAsync (options, index, request, cancellationToken);
		}

		/// <summary>
		/// Replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static UniqueId? Replace (this IMailFolder folder, FormatOptions options, int index, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags, date) {
				TransferProgress = progress
			};

			return folder.Replace (options, index, request, cancellationToken);
		}

		/// <summary>
		/// Asynchronously replace a message in the folder.
		/// </summary>
		/// <remarks>
		/// Replaces the specified message in the folder and returns the UniqueId assigned to the new message.
		/// </remarks>
		/// <returns>The UID of the new message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="folder">The folder.</param>
		/// <param name="options">The formatting options.</param>
		/// <param name="index">The index of the message to be replaced.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="MailFolder"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="MailFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>Internationalized formatting was requested but is not supported by the server.</para>
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="CommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public static Task<UniqueId?> ReplaceAsync (this IMailFolder folder, FormatOptions options, int index, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default, ITransferProgress progress = null)
		{
			var request = new ReplaceRequest (message, flags, date) {
				TransferProgress = progress
			};

			return folder.ReplaceAsync (options, index, request, cancellationToken);
		}

		#endregion Replace Extensions
	}
}
