//
// ImapFolderAnnotations.cs
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
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MailKit.Net.Imap
{
	public partial class ImapFolder
	{
		IEnumerable<ImapCommand> QueueStoreCommands (IList<UniqueId> uids, ulong? modseq, IList<Annotation> annotations, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (modseq.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			if (annotations == null)
				throw new ArgumentNullException (nameof (annotations));

			CheckState (true, true);

			if (AnnotationAccess == AnnotationAccess.None)
				throw new NotSupportedException ("The ImapFolder does not support annotations.");

			if (uids.Count == 0 || annotations.Count == 0)
				return Array.Empty<ImapCommand> ();

			var builder = new StringBuilder ("UID STORE %s ");
			var values = new List<object> ();

			if (modseq.HasValue) {
				builder.Append ("(UNCHANGEDSINCE ");
				builder.Append (modseq.Value.ToString (CultureInfo.InvariantCulture));
				builder.Append (") ");
			}

			ImapUtils.FormatAnnotations (builder, annotations, values, true);
			builder.Append ("\r\n");

			var command = builder.ToString ();
			var args = values.ToArray ();

			return Engine.QueueCommands (cancellationToken, this, command, uids, args);
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
		/// Store the annotations for the specified messages.
		/// </summary>
		/// <remarks>
		/// Stores the annotations for the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// The <see cref="ImapFolder"/> does not support annotations.
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
		public override void Store (IList<UniqueId> uids, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			foreach (var ic in QueueStoreCommands (uids, null, annotations, cancellationToken)) {
				Engine.Run (ic);

				ProcessStoreAnnotationsResponse (ic);
			}
		}

		/// <summary>
		/// Asynchronously store the annotations for the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously stores the annotations for the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// The <see cref="ImapFolder"/> does not support annotations.
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
		public override async Task StoreAsync (IList<UniqueId> uids, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			foreach (var ic in QueueStoreCommands (uids, null, annotations, cancellationToken)) {
				await Engine.RunAsync (ic).ConfigureAwait (false);

				ProcessStoreAnnotationsResponse (ic);
			}
		}

		/// <summary>
		/// Store the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Stores the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// <para>The <see cref="ImapFolder"/> does not support mod-sequences.</para>
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
		public override IList<UniqueId> Store (IList<UniqueId> uids, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			UniqueIdSet unmodified = null;

			foreach (var ic in QueueStoreCommands (uids, modseq, annotations, cancellationToken)) {
				Engine.Run (ic);

				ProcessStoreAnnotationsResponse (ic);

				ProcessUnmodified (ic, ref unmodified, modseq);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		/// <summary>
		/// Asynchronously store the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously stores the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// <para>The <see cref="ImapFolder"/> does not support mod-sequences.</para>
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
		public override async Task<IList<UniqueId>> StoreAsync (IList<UniqueId> uids, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			UniqueIdSet unmodified = null;

			foreach (var ic in QueueStoreCommands (uids, modseq, annotations, cancellationToken)) {
				await Engine.RunAsync (ic).ConfigureAwait (false);

				ProcessStoreAnnotationsResponse (ic);

				ProcessUnmodified (ic, ref unmodified, modseq);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		ImapCommand QueueStoreCommand (IList<int> indexes, ulong? modseq, IList<Annotation> annotations, CancellationToken cancellationToken)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (modseq.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			if (annotations == null)
				throw new ArgumentNullException (nameof (annotations));

			CheckState (true, true);

			if (AnnotationAccess == AnnotationAccess.None)
				throw new NotSupportedException ("The ImapFolder does not support annotations.");

			if (indexes.Count == 0 || annotations.Count == 0)
				return null;

			var command = new StringBuilder ("STORE ");
			var args = new List<object> ();

			ImapUtils.FormatIndexSet (Engine, command, indexes);
			command.Append (' ');

			if (modseq.HasValue) {
				command.Append ("(UNCHANGEDSINCE ");
				command.Append (modseq.Value.ToString (CultureInfo.InvariantCulture));
				command.Append (") ");
			}

			ImapUtils.FormatAnnotations (command, annotations, args, true);
			command.Append ("\r\n");

			return Engine.QueueCommand (cancellationToken, this, command.ToString (), args.ToArray ());
		}

		/// <summary>
		/// Store the annotations for the specified messages.
		/// </summary>
		/// <remarks>
		/// Stores the annotations for the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// The <see cref="ImapFolder"/> does not support annotations.
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
		public override void Store (IList<int> indexes, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, null, annotations, cancellationToken);

			if (ic == null)
				return;

			Engine.Run (ic);

			ProcessStoreAnnotationsResponse (ic);
		}

		/// <summary>
		/// Asynchronously store the annotations for the specified messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously stores the annotations for the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// The <see cref="ImapFolder"/> does not support annotations.
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
		public override async Task StoreAsync (IList<int> indexes, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, null, annotations, cancellationToken);

			if (ic == null)
				return;

			await Engine.RunAsync (ic).ConfigureAwait (false);

			ProcessStoreAnnotationsResponse (ic);
		}

		/// <summary>
		/// Store the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Stores the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// <para>The <see cref="ImapFolder"/> does not support mod-sequences.</para>
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
		public override IList<int> Store (IList<int> indexes, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, modseq, annotations, cancellationToken);

			if (ic == null)
				return Array.Empty<int> ();

			Engine.Run (ic);

			ProcessStoreAnnotationsResponse (ic);

			return GetUnmodified (ic, modseq);
		}

		/// <summary>
		/// Asynchronously store the annotations for the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Asynchronously stores the annotations for the specified messages only if their mod-sequence value is less than the specified value.s
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="annotations">The annotations to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="annotations"/> is <c>null</c>.</para>
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
		/// <para>The <see cref="ImapFolder"/> does not support mod-sequences.</para>
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
		public override async Task<IList<int>> StoreAsync (IList<int> indexes, ulong modseq, IList<Annotation> annotations, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, modseq, annotations, cancellationToken);

			if (ic == null)
				return Array.Empty<int> ();

			await Engine.RunAsync (ic).ConfigureAwait (false);

			ProcessStoreAnnotationsResponse (ic);

			return GetUnmodified (ic, modseq);
		}
	}
}
