//
// ImapFolderAnnotations.cs
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
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MailKit.Net.Imap
{
	public partial class ImapFolder
	{
		IEnumerable<ImapCommand> CreateStoreCommands (IList<UniqueId> uids, IStoreAnnotationsRequest request, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (request.UnchangedSince.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (AnnotationAccess == AnnotationAccess.None)
				throw new NotSupportedException ("The ImapFolder does not support annotations.");

			var annotations = request.Annotations;

			if (uids.Count == 0 || annotations == null || annotations.Count == 0)
				return Array.Empty<ImapCommand> ();

			var builder = new StringBuilder ("UID STORE %s ");
			var values = new List<object> ();

			if (request.UnchangedSince.HasValue) {
				builder.Append ("(UNCHANGEDSINCE ");
				builder.Append (request.UnchangedSince.Value.ToString (CultureInfo.InvariantCulture));
				builder.Append (") ");
			}

			ImapUtils.FormatAnnotations (builder, annotations, values, true);
			builder.Append ("\r\n");

			var command = builder.ToString ();
			var args = values.ToArray ();

			return Engine.CreateCommands (cancellationToken, this, command, uids, args);
		}

		void ProcessStoreAnnotationsResponse (ImapCommand ic)
		{
			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				// TODO: Do something with the AnnotateResponseCode if it exists??

				throw ImapCommandException.Create ("STORE", ic);
			}
		}
		/// <summary>
		/// Store the annotations for a set of messages.
		/// </summary>
		/// <remarks>
		/// Stores the annotations for a set of messages.
		/// </remarks>
		/// <returns>The UIDs of the messages that were not updated.</returns>
		/// <param name="uids">The message UIDs.</param>
		/// <param name="request">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Cannot store annotations without any properties defined.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>The <see cref="ImapFolder"/> does not support annotations.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<UniqueId> Store (IList<UniqueId> uids, IStoreAnnotationsRequest request, CancellationToken cancellationToken = default)
		{
			UniqueIdSet? unmodified = null;

			foreach (var ic in CreateStoreCommands (uids, request, cancellationToken)) {
				var chunk = ic.UniqueIds!;

				request.OnStarted (this, chunk);

				Engine.QueueCommand (ic);
				Engine.Run (ic);

				ProcessStoreAnnotationsResponse (ic);

				var chunkUnmodified = ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);

				request.OnCompleted (this, chunk, chunkUnmodified);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		/// <summary>
		/// Asynchronously store the annotations for a set of messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously stores the annotations for a set of messages.
		/// </remarks>
		/// <returns>The UIDs of the messages that were not updated.</returns>
		/// <param name="uids">The message UIDs.</param>
		/// <param name="request">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Cannot store annotations without any properties defined.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>The <see cref="ImapFolder"/> does not support annotations.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override async Task<IList<UniqueId>> StoreAsync (IList<UniqueId> uids, IStoreAnnotationsRequest request, CancellationToken cancellationToken = default)
		{
			UniqueIdSet? unmodified = null;

			foreach (var ic in CreateStoreCommands (uids, request, cancellationToken)) {
				var chunk = ic.UniqueIds!;

				request.OnStarted (this, chunk);

				Engine.QueueCommand (ic);
				await Engine.RunAsync (ic).ConfigureAwait (false);

				ProcessStoreAnnotationsResponse (ic);

				var chunkUnmodified = ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);

				request.OnCompleted (this, chunk, chunkUnmodified);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		bool TryCreateStoreCommand (IList<int> indexes, IStoreAnnotationsRequest request, CancellationToken cancellationToken, [NotNullWhen (true)] out ImapCommand? ic)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (request.UnchangedSince.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (AnnotationAccess == AnnotationAccess.None)
				throw new NotSupportedException ("The ImapFolder does not support annotations.");

			var annotations = request.Annotations;

			if (indexes.Count == 0 || annotations == null || annotations.Count == 0) {
				ic = null;
				return false;
			}

			var command = new StringBuilder ("STORE ");
			var args = new List<object> ();

			ImapUtils.FormatIndexSet (Engine, command, indexes);
			command.Append (' ');

			if (request.UnchangedSince.HasValue) {
				command.Append ("(UNCHANGEDSINCE ");
				command.Append (request.UnchangedSince.Value.ToString (CultureInfo.InvariantCulture));
				command.Append (") ");
			}

			ImapUtils.FormatAnnotations (command, annotations, args, true);
			command.Append ("\r\n");

			ic = new ImapCommand (Engine, cancellationToken, this, command.ToString (), args.ToArray ());

			return true;
		}
		/// <summary>
		/// Store the annotations for a set of messages.
		/// </summary>
		/// <remarks>
		/// Stores the annotations for a set of messages.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The message indexes.</param>
		/// <param name="request">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Cannot store annotations without any properties defined.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>The <see cref="ImapFolder"/> does not support annotations.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<int> Store (IList<int> indexes, IStoreAnnotationsRequest request, CancellationToken cancellationToken = default)
		{
			if (!TryCreateStoreCommand (indexes, request, cancellationToken, out var ic))
				return Array.Empty<int> ();

			request.OnStarted (this, indexes);

			Engine.QueueCommand (ic);
			Engine.Run (ic);

			ProcessStoreAnnotationsResponse (ic);

			var unmodified = GetUnmodified (ic, request.UnchangedSince);

			request.OnCompleted (this, indexes, unmodified);

			return unmodified;
		}

		/// <summary>
		/// Asynchronously store the annotations for a set of messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously stores the annotations for a set of messages.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The message indexes.</param>
		/// <param name="request">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <see langword="null" />.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <see langword="null" />.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Cannot store annotations without any properties defined.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>The <see cref="ImapFolder"/> does not support annotations.</para>
		/// <para>-or-</para>
		/// <para>The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override async Task<IList<int>> StoreAsync (IList<int> indexes, IStoreAnnotationsRequest request, CancellationToken cancellationToken = default)
		{
			if (!TryCreateStoreCommand (indexes, request, cancellationToken, out var ic))
				return Array.Empty<int> ();

			request.OnStarted (this, indexes);

			Engine.QueueCommand (ic);
			await Engine.RunAsync (ic).ConfigureAwait (false);

			ProcessStoreAnnotationsResponse (ic);

			var unmodified = GetUnmodified (ic, request.UnchangedSince);

			request.OnCompleted (this, indexes, unmodified);

			return unmodified;
		}
	}
}
