//
// ImapFolderFlags.cs
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

		void ProcessUnmodified (ImapCommand ic, ref UniqueIdSet uids, ulong? modseq)
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

		IList<int> GetUnmodified (ImapCommand ic, ulong? modseq)
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

			return new int[0];
		}

		async Task<IList<UniqueId>> StoreAsync (IList<UniqueId> uids, IStoreFlagsRequest request, bool doAsync, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (request.UnchangedSince.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (uids.Count == 0)
				return new UniqueId[0];

			int numKeywords = request.Keywords != null ? request.Keywords.Count : 0;
			string action;

			switch (request.Action) {
			case StoreAction.Add:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return new UniqueId[0];

				action = request.Silent ? "+FLAGS.SILENT" : "+FLAGS";
				break;
			case StoreAction.Remove:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return new UniqueId[0];

				action = request.Silent ? "-FLAGS.SILENT" : "-FLAGS";
				break;
			default:
				action = request.Silent ? "FLAGS.SILENT" : "FLAGS";
				break;
			}

			var flaglist = ImapUtils.FormatFlagsList (request.Flags & PermanentFlags, request.Keywords != null ? request.Keywords.Count : 0);
			var keywordList = request.Keywords != null ? request.Keywords.ToArray () : new object[0];
			UniqueIdSet unmodified = null;
			var @params = string.Empty;

			if (request.UnchangedSince.HasValue)
				@params = string.Format (CultureInfo.InvariantCulture, " (UNCHANGEDSINCE {0})", request.UnchangedSince.Value);

			var command = string.Format ("UID STORE %s{0} {1} {2}\r\n", @params, action, flaglist);

			foreach (var ic in Engine.QueueCommands (cancellationToken, this, command, uids, keywordList)) {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("STORE", ic);

				ProcessUnmodified (ic, ref unmodified, request.UnchangedSince);
			}

			if (unmodified == null)
				return new UniqueId[0];

			return unmodified;
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
		public override IList<UniqueId> Store (IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken = default (CancellationToken))
		{
			return StoreAsync (uids, request, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override Task<IList<UniqueId>> StoreAsync (IList<UniqueId> uids, IStoreFlagsRequest request, CancellationToken cancellationToken = default (CancellationToken))
		{
			return StoreAsync (uids, request, true, cancellationToken);
		}

		async Task<IList<int>> StoreAsync (IList<int> indexes, IStoreFlagsRequest request, bool doAsync, CancellationToken cancellationToken)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if (request == null)
				throw new ArgumentNullException (nameof (request));

			if (request.UnchangedSince.HasValue && !supportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (indexes.Count == 0)
				return new int[0];

			int numKeywords = request.Keywords != null ? request.Keywords.Count : 0;
			string action;

			switch (request.Action) {
			case StoreAction.Add:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return new int[0];

				action = request.Silent ? "+FLAGS.SILENT" : "+FLAGS";
				break;
			case StoreAction.Remove:
				if ((request.Flags & SettableFlags) == 0 && numKeywords == 0)
					return new int[0];

				action = request.Silent ? "-FLAGS.SILENT" : "-FLAGS";
				break;
			default:
				action = request.Silent ? "FLAGS.SILENT" : "FLAGS";
				break;
			}

			var flaglist = ImapUtils.FormatFlagsList (request.Flags & PermanentFlags, request.Keywords != null ? request.Keywords.Count : 0);
			var keywordList = request.Keywords != null ? request.Keywords.ToArray () : new object[0];
			var set = ImapUtils.FormatIndexSet (Engine, indexes);
			var @params = string.Empty;

			if (request.UnchangedSince.HasValue)
				@params = string.Format (CultureInfo.InvariantCulture, " (UNCHANGEDSINCE {0})", request.UnchangedSince.Value);

			var format = string.Format ("STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format, keywordList);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			return GetUnmodified (ic, request.UnchangedSince);
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
		public override IList<int> Store (IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken = default (CancellationToken))
		{
			return StoreAsync (indexes, request, false, cancellationToken).GetAwaiter ().GetResult ();
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
		public override Task<IList<int>> StoreAsync (IList<int> indexes, IStoreFlagsRequest request, CancellationToken cancellationToken = default (CancellationToken))
		{
			return StoreAsync (indexes, request, true, cancellationToken);
		}

		string LabelListToString (IList<string> labels, ICollection<object> args)
		{
			var list = new StringBuilder ("(");

			for (int i = 0; i < labels.Count; i++) {
				if (i > 0)
					list.Append (' ');

				if (labels[i] == null) {
					list.Append ("NIL");
					continue;
				}

				switch (labels[i]) {
				case "\\AllMail":
				case "\\Drafts":
				case "\\Important":
				case "\\Inbox":
				case "\\Spam":
				case "\\Sent":
				case "\\Starred":
				case "\\Trash":
					list.Append (labels[i]);
					break;
				default:
					list.Append ("%S");
					args.Add (Engine.EncodeMailboxName (labels[i]));
					break;
				}
			}

			list.Append (')');

			return list.ToString ();
		}

		async Task<IList<UniqueId>> ModifyLabelsAsync (IList<UniqueId> uids, ulong? modseq, IList<string> labels, string action, bool doAsync, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
				throw new NotSupportedException ("The IMAP server does not support the Google Mail extensions.");

			CheckState (true, true);

			if (uids.Count == 0)
				return new UniqueId[0];

			var @params = string.Empty;

			if (modseq.HasValue)
				@params = string.Format (CultureInfo.InvariantCulture, " (UNCHANGEDSINCE {0})", modseq.Value);

			var args = new List<object> ();
			var list = LabelListToString (labels, args);
			var command = string.Format ("UID STORE %s{0} {1} {2}\r\n", @params, action, list);
			UniqueIdSet unmodified = null;

			foreach (var ic in Engine.QueueCommands (cancellationToken, this, command, uids, args.ToArray ())) {
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("STORE", ic);

				ProcessUnmodified (ic, ref unmodified, modseq);
			}

			if (unmodified == null)
				return new UniqueId[0];

			return unmodified;
		}

		/// <summary>
		/// Add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override void AddLabels (IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			ModifyLabelsAsync (uids, null, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task AddLabelsAsync (IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (uids, null, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override void RemoveLabels (IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			ModifyLabelsAsync (uids, null, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task RemoveLabelsAsync (IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (uids, null, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override void SetLabels (IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			ModifyLabelsAsync (uids, null, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task SetLabelsAsync (IList<UniqueId> uids, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			return ModifyLabelsAsync (uids, null, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override IList<UniqueId> AddLabels (IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (uids, modseq, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task<IList<UniqueId>> AddLabelsAsync (IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (uids, modseq, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override IList<UniqueId> RemoveLabels (IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (uids, modseq, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task<IList<UniqueId>> RemoveLabelsAsync (IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (uids, modseq, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override IList<UniqueId> SetLabels (IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			return ModifyLabelsAsync (uids, modseq, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task<IList<UniqueId>> SetLabelsAsync (IList<UniqueId> uids, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			return ModifyLabelsAsync (uids, modseq, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", true, cancellationToken);
		}

		async Task<IList<int>> ModifyLabelsAsync (IList<int> indexes, ulong? modseq, IList<string> labels, string action, bool doAsync, CancellationToken cancellationToken)
		{
			if (indexes == null)
				throw new ArgumentNullException (nameof (indexes));

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
				throw new NotSupportedException ("The IMAP server does not support the Google Mail extensions.");

			CheckState (true, true);

			if (indexes.Count == 0)
				return new int[0];

			var set = ImapUtils.FormatIndexSet (Engine, indexes);
			var @params = string.Empty;

			if (modseq.HasValue)
				@params = string.Format (CultureInfo.InvariantCulture, " (UNCHANGEDSINCE {0})", modseq.Value);

			var args = new List<object> ();
			var list = LabelListToString (labels, args);
			var format = string.Format ("STORE {0}{1} {2} {3}\r\n", set, @params, action, list);
			var ic = Engine.QueueCommand (cancellationToken, this, format, args.ToArray ());

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			return GetUnmodified (ic, modseq);
		}

		/// <summary>
		/// Add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override void AddLabels (IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			ModifyLabelsAsync (indexes, null, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task AddLabelsAsync (IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (indexes, null, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override void RemoveLabels (IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			ModifyLabelsAsync (indexes, null, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task RemoveLabelsAsync (IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (indexes, null, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Sets the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override void SetLabels (IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			ModifyLabelsAsync (indexes, null, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously sets the labels of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task SetLabelsAsync (IList<int> indexes, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			return ModifyLabelsAsync (indexes, null, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override IList<int> AddLabels (IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (indexes, modseq, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously add a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of labels to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task<IList<int>> AddLabelsAsync (IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (indexes, modseq, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override IList<int> RemoveLabels (IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (indexes, modseq, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously remove a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of labels from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task<IList<int>> RemoveLabelsAsync (IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", nameof (labels));

			return ModifyLabelsAsync (indexes, modseq, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", true, cancellationToken);
		}

		/// <summary>
		/// Set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override IList<int> SetLabels (IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			return ModifyLabelsAsync (indexes, modseq, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously set the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the labels of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="labels">The labels to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageLabelsChanged"/> events will be emitted.</param>
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
		/// The IMAP server does not support the Google Mail Extensions.
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
		public override Task<IList<int>> SetLabelsAsync (IList<int> indexes, ulong modseq, IList<string> labels, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (labels == null)
				throw new ArgumentNullException (nameof (labels));

			return ModifyLabelsAsync (indexes, modseq, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", true, cancellationToken);
		}
	}
}
