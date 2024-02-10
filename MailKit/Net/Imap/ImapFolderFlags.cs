//
// ImapFolderFlags.cs
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MailKit.Net.Imap
{
	public partial class ImapFolder
	{
		static readonly IStoreFlagsRequest AddDeletedFlag = new StoreFlagsRequest (StoreAction.Add, MessageFlags.Deleted) { Silent = true };
		static readonly IStoreFlagsRequest RemoveDeletedFlag = new StoreFlagsRequest (StoreAction.Remove, MessageFlags.Deleted) { Silent = true };

		static void ProcessUnmodified (ImapCommand ic, ref UniqueIdSet uids, ulong? modseq)
		{
			if (modseq.HasValue) {
				foreach (var rc in ic.RespCodes.OfType<ModifiedResponseCode> ()) {
					if (uids != null)
						uids.AddRange (rc.UidSet);
					else
						uids = rc.UidSet;
				}
			}
		}

		void ProcessStoreResponse (ImapCommand ic)
		{
			ProcessResponseCodes (ic, null);

			ic.ThrowIfNotOk ("STORE");
		}

		static IList<int> GetUnmodified (ImapCommand ic, ulong? modseq)
		{
			if (modseq.HasValue) {
				var rc = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (rc != null) {
					var unmodified = new int[rc.UidSet.Count];
					for (int i = 0; i < unmodified.Length; i++)
						unmodified[i] = (int) (rc.UidSet[i].Id - 1);

					return unmodified;
				}
			}

			return Array.Empty<int> ();
		}

		IEnumerable<ImapCommand> QueueStoreCommands (IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (request.UnchangedSince.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (uids.Count == 0)
				return Array.Empty<ImapCommand> ();

			int numKeywords = request.Keywords != null ? request.Keywords.Count : 0;
			string action;

			switch (request.Action) {
			case StoreAction.Add:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return Array.Empty<ImapCommand> ();

				action = request.Silent ? "+FLAGS.SILENT" : "+FLAGS";
				break;
			case StoreAction.Remove:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return Array.Empty<ImapCommand> ();

				action = request.Silent ? "-FLAGS.SILENT" : "-FLAGS";
				break;
			default:
				action = request.Silent ? "FLAGS.SILENT" : "FLAGS";
				break;
			}

			var flaglist = ImapUtils.FormatFlagsList (request.Flags & PermanentFlags, request.Keywords != null ? request.Keywords.Count : 0);
			var keywordList = request.Keywords != null ? request.Keywords.ToArray () : Array.Empty<object> ();
			var @params = string.Empty;

			if (request.UnchangedSince.HasValue)
				@params = string.Format (CultureInfo.InvariantCulture, " (UNCHANGEDSINCE {0})", request.UnchangedSince.Value);

			var command = string.Format ("UID STORE %s{0} {1} {2}\r\n", @params, action, flaglist);

			return Engine.QueueCommands (cancellationToken, this, command, uids, keywordList);
		}

		/// <summary>
		/// Store message flags and keywords for a set of messages.
		/// </summary>
		/// <remarks>
		/// Updates the message flags and keywords for a set of messages.
		/// </remarks>
		/// <returns>The UIDs of the messages that were not updated.</returns>
		/// <param name="uids">The message UIDs.</param>
		/// <param name="request">The message flags and keywords to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<UniqueId> Store (IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
		{
			UniqueIdSet unmodified = null;

			foreach (var ic in QueueStoreCommands (uids, request, cancellationToken)) {
				Engine.Run (ic);

				ProcessStoreResponse (ic);

				ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		/// <summary>
		/// Asynchronously store message flags and keywords for a set of messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously updates the message flags and keywords for a set of messages.
		/// </remarks>
		/// <returns>The UIDs of the messages that were not updated.</returns>
		/// <param name="uids">The message UIDs.</param>
		/// <param name="request">The message flags and keywords to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override async Task<IList<UniqueId>> StoreAsync (IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
		{
			UniqueIdSet unmodified = null;

			foreach (var ic in QueueStoreCommands (uids, request, cancellationToken)) {
				await Engine.RunAsync (ic).ConfigureAwait (false);

				ProcessStoreResponse (ic);

				ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		ImapCommand QueueStoreCommand (IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (request.UnchangedSince.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (indexes.Count == 0)
				return null;

			int numKeywords = request.Keywords != null ? request.Keywords.Count : 0;
			string action;

			switch (request.Action) {
			case StoreAction.Add:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return null;

				action = request.Silent ? "+FLAGS.SILENT" : "+FLAGS";
				break;
			case StoreAction.Remove:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return null;

				action = request.Silent ? "-FLAGS.SILENT" : "-FLAGS";
				break;
			default:
				action = request.Silent ? "FLAGS.SILENT" : "FLAGS";
				break;
			}

			var keywordList = request.Keywords != null ? request.Keywords.ToArray () : Array.Empty<object> ();
			var command = new StringBuilder ("STORE ");
			ImapUtils.FormatIndexSet (Engine, command, indexes);
			command.Append (' ');

			if (request.UnchangedSince.HasValue) {
				command.Append ("(UNCHANGEDSINCE ");
				command.Append (request.UnchangedSince.Value.ToString (CultureInfo.InvariantCulture));
				command.Append (") ");
			}

			command.Append (action);
			command.Append (' ');
			ImapUtils.FormatFlagsList (command, request.Flags & PermanentFlags, request.Keywords != null ? request.Keywords.Count : 0);
			command.Append ("\r\n");

			return Engine.QueueCommand (cancellationToken, this, command.ToString (), keywordList);
		}

		/// <summary>
		/// Store message flags and keywords for a set of messages.
		/// </summary>
		/// <remarks>
		/// Updates the message flags and keywords for a set of message.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The message indexes.</param>
		/// <param name="request">The message flags and keywords to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<int> Store (IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, request, cancellationToken);

			if (ic == null)
				return Array.Empty<int> ();

			Engine.Run (ic);

			ProcessStoreResponse (ic);

			return GetUnmodified (ic, request.UnchangedSince);
		}

		/// <summary>
		/// Asynchronously store message flags and keywords for a set of messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously updates the message flags and keywords for a set of messages.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The message indexes.</param>
		/// <param name="request">The message flags and keywords to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override async Task<IList<int>> StoreAsync (IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, request, cancellationToken);

			if (ic == null)
				return Array.Empty<int> ();

			await Engine.RunAsync (ic).ConfigureAwait (false);

			ProcessStoreResponse (ic);

			return GetUnmodified (ic, request.UnchangedSince);
		}

		void AppendLabelList (StringBuilder command, ISet<string> labels, List<object> args)
		{
			command.Append ('(');

			if (labels != null) {
				int index = 0;

				foreach (var label in labels) {
					if (index > 0)
						command.Append (' ');

					index++;

					if (label == null) {
						command.Append ("NIL");
						continue;
					}

					switch (label) {
					case "\\AllMail":
					case "\\Drafts":
					case "\\Important":
					case "\\Inbox":
					case "\\Spam":
					case "\\Sent":
					case "\\Starred":
					case "\\Trash":
						command.Append (label);
						break;
					default:
						command.Append ("%S");
						args.Add (Engine.EncodeMailboxName (label));
						break;
					}
				}
			}

			command.Append (')');
		}

		IEnumerable<ImapCommand> QueueStoreCommands (IList<UniqueId> uids, IStoreLabelsRequest request, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
				throw new NotSupportedException ("The IMAP server does not support the Google Mail extensions.");

			CheckState (true, true);

			if (uids.Count == 0)
				return Array.Empty<ImapCommand> ();

			string action;

			switch (request.Action) {
			case StoreAction.Add:
				if (request.Labels == null || request.Labels.Count == 0)
					return Array.Empty<ImapCommand> ();

				action = request.Silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS";
				break;
			case StoreAction.Remove:
				if (request.Labels == null || request.Labels.Count == 0)
					return Array.Empty<ImapCommand> ();

				action = request.Silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS";
				break;
			default:
				action = request.Silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS";
				break;
			}

			var command = new StringBuilder ("UID STORE %s ");
			var args = new List<object> ();

			if (request.UnchangedSince.HasValue) {
				command.Append ("(UNCHANGEDSINCE ");
				command.Append (request.UnchangedSince.Value.ToString (CultureInfo.InvariantCulture));
				command.Append (") ");
			}

			command.Append (action);
			command.Append (' ');
			AppendLabelList (command, request.Labels, args);
			command.Append ("\r\n");

			return Engine.QueueCommands (cancellationToken, this, command.ToString (), uids, args.ToArray ());
		}

		/// <summary>
		/// Store GMail-style labels for a set of messages.
		/// </summary>
		/// <remarks>
		/// Updates the GMail-style labels for a set of messages.
		/// </remarks>
		/// <returns>The UIDs of the messages that were not updated.</returns>
		/// <param name="uids">The message UIDs.</param>
		/// <param name="request">The GMail-style labels to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<UniqueId> Store (IList<UniqueId> uids, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
		{
			UniqueIdSet unmodified = null;

			foreach (var ic in QueueStoreCommands (uids, request, cancellationToken)) {
				Engine.Run (ic);

				ProcessStoreResponse (ic);

				ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		/// <summary>
		/// Asynchronously store GMail-style labels for a set of messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously updates the GMail-style labels for a set of messages.
		/// </remarks>
		/// <returns>The UIDs of the messages that were not updated.</returns>
		/// <param name="uids">The message UIDs.</param>
		/// <param name="request">The GMail-style labels to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override async Task<IList<UniqueId>> StoreAsync (IList<UniqueId> uids, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
		{
			UniqueIdSet unmodified = null;

			foreach (var ic in QueueStoreCommands (uids, request, cancellationToken)) {
				await Engine.RunAsync (ic).ConfigureAwait (false);

				ProcessStoreResponse (ic);

				ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);
			}

			if (unmodified == null)
				return Array.Empty<UniqueId> ();

			return unmodified;
		}

		ImapCommand QueueStoreCommand (IList<int> indexes, IStoreLabelsRequest request, CancellationToken cancellationToken)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
				throw new NotSupportedException ("The IMAP server does not support the Google Mail extensions.");

			CheckState (true, true);

			if (indexes.Count == 0)
				return null;

			string action;

			switch (request.Action) {
			case StoreAction.Add:
				if (request.Labels == null || request.Labels.Count == 0)
					return null;

				action = request.Silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS";
				break;
			case StoreAction.Remove:
				if (request.Labels == null || request.Labels.Count == 0)
					return null;

				action = request.Silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS";
				break;
			default:
				action = request.Silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS";
				break;
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

			command.Append (action);
			command.Append (' ');
			AppendLabelList (command, request.Labels, args);
			command.Append ("\r\n");

			return Engine.QueueCommand (cancellationToken, this, command.ToString (), args.ToArray ());
		}

		/// <summary>
		/// Store GMail-style labels for a set of messages.
		/// </summary>
		/// <remarks>
		/// Updates the GMail-style labels for a set of message.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The message indexes.</param>
		/// <param name="request">The GMail-style labels to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<int> Store (IList<int> indexes, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, request, cancellationToken);

			if (ic == null)
				return Array.Empty<int> ();

			Engine.Run (ic);

			ProcessStoreResponse (ic);

			return GetUnmodified (ic, request.UnchangedSince);
		}

		/// <summary>
		/// Asynchronously store GMail-style labels for a set of messages.
		/// </summary>
		/// <remarks>
		/// Asynchronously updates the GMail-style labels for a set of messages.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The message indexes.</param>
		/// <param name="request">The GMail-style labels to store.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="request"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The <paramref name="request"/> specified an <see cref="IStoreRequest.UnchangedSince"/> value
		/// but the <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override async Task<IList<int>> StoreAsync (IList<int> indexes, IStoreLabelsRequest request, CancellationToken cancellationToken = default)
		{
			var ic = QueueStoreCommand (indexes, request, cancellationToken);

			if (ic == null)
				return Array.Empty<int> ();

			await Engine.RunAsync (ic).ConfigureAwait (false);

			ProcessStoreResponse (ic);

			return GetUnmodified (ic, request.UnchangedSince);
		}
	}
}
