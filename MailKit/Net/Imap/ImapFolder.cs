//
// ImapFolder.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;
using MimeKit.IO;
using MailKit.Search;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP folder.
	/// </summary>
	/// <remarks>
	/// An IMAP folder.
	/// </remarks>
	public class ImapFolder : MailFolder
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MailKit.Net.Imap.ImapFolder"/> class.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="encodedName">The encoded name.</param>
		/// <param name="attrs">The folder attributes.</param>
		/// <param name="delim">The path delimeter.</param>
		internal ImapFolder (ImapEngine engine, string encodedName, FolderAttributes attrs, char delim)
		{
			FullName = engine.DecodeMailboxName (encodedName);
			Name = GetBaseName (FullName, delim);
			DirectorySeparator = delim;
			EncodedName = encodedName;
			Attributes = attrs;
			Engine = engine;

			engine.Disconnected += (sender, e) => {
				Access = FolderAccess.None;
			};
		}

		static string GetBaseName (string fullName, char delim)
		{
			var names = fullName.Split (new [] { delim }, StringSplitOptions.RemoveEmptyEntries);

			return names.Length > 0 ? names[names.Length - 1] : fullName;
		}

		/// <summary>
		/// Get the engine.
		/// </summary>
		/// <value>The engine.</value>
		internal ImapEngine Engine {
			get; private set;
		}

		/// <summary>
		/// Get the encoded name of the folder.
		/// </summary>
		/// <value>The encoded name.</value>
		internal string EncodedName {
			get; private set;
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the folder.
		/// </summary>
		/// <remarks>
		/// Gets an object that can be used to synchronize access to the folder.
		/// </remarks>
		/// <value>The sync root.</value>
		public override object SyncRoot {
			get { return Engine; }
		}

		void CheckState (bool open, bool rw)
		{
			if (Engine.IsDisposed)
				throw new ObjectDisposedException ("ImapClient");

			if (Engine.IsProcessingCommands)
				throw new InvalidOperationException ("The ImapClient is currently busy processing a command.");

			if (!Engine.IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (Engine.State < ImapEngineState.Authenticated)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			if (open && !IsOpen)
				throw new InvalidOperationException ("The folder is not currently open.");

			if (open && rw && Access != FolderAccess.ReadWrite)
				throw new InvalidOperationException ("The folder is not currently open in read-write mode.");
		}

		void ParentFolderRenamed (object sender, FolderRenamedEventArgs e)
		{
			var oldEncodedName = EncodedName;
			var oldFullName = FullName;

			FullName = ParentFolder.FullName + DirectorySeparator + Name;
			EncodedName = Engine.EncodeMailboxName (FullName);
			Engine.FolderCache.Remove (oldEncodedName);
			Engine.FolderCache[EncodedName] = this;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
			}

			OnRenamed (oldFullName, FullName);
		}

		internal void SetParentFolder (IMailFolder parent)
		{
			parent.Renamed += ParentFolderRenamed;
			ParentFolder = parent;
		}

		void ProcessResponseCodes (ImapCommand ic, string paramName)
		{
			bool tryCreate = false;

			foreach (var code in ic.RespCodes) {
				switch (code.Type) {
				case ImapResponseCodeType.Alert:
					Engine.OnAlert (code.Message);
					break;
				case ImapResponseCodeType.PermanentFlags:
					PermanentFlags = ((PermanentFlagsResponseCode) code).Flags;
					break;
				case ImapResponseCodeType.ReadOnly:
					Access = FolderAccess.ReadOnly;
					break;
				case ImapResponseCodeType.ReadWrite:
					Access = FolderAccess.ReadWrite;
					break;
				case ImapResponseCodeType.TryCreate:
					tryCreate = true;
					break;
				case ImapResponseCodeType.UidNext:
					UidNext = ((UidNextResponseCode) code).Uid;
					break;
				case ImapResponseCodeType.UidValidity:
					UidValidity = ((UidValidityResponseCode) code).UidValidity;
					break;
				case ImapResponseCodeType.Unseen:
					FirstUnread = ((UnseenResponseCode) code).Index;
					break;
				case ImapResponseCodeType.HighestModSeq:
					HighestModSeq = ((HighestModSeqResponseCode) code).HighestModSeq;
					SupportsModSeq = true;
					break;
				case ImapResponseCodeType.NoModSeq:
					SupportsModSeq = false;
					HighestModSeq = 0;
					break;
				}
			}

			if (tryCreate) {
				if (paramName == null)
					throw new ArgumentException ("The folder does not exist.");

				throw new ArgumentException ("The destination folder does not exist.", paramName);
			}
		}

		#region IMailFolder implementation

		/// <summary>
		/// Gets a value indicating whether the folder is currently open.
		/// </summary>
		/// <remarks>
		/// Gets a value indicating whether the folder is currently open.
		/// </remarks>
		/// <value><c>true</c> if the folder is currently open; otherwise, <c>false</c>.</value>
		public override bool IsOpen {
			get { return Engine.Selected == this; }
		}

		static string SelectOrExamine (FolderAccess access)
		{
			return access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT";
		}

		static void QResyncFetch (ImapEngine engine, ImapCommand ic, int index)
		{
			ic.Folder.OnFetch (engine, index, ic.CancellationToken);
		}

		/// <summary>
		/// Opens the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// <para>This variant of the <see cref="Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// method is meant for quick resynchronization of the folder. Before calling this method,
		/// the <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method MUST be called.</para>
		/// <para>You should also make sure to add listeners to the <see cref="MailFolder.MessagesVanished"/> and
		/// <see cref="MailFolder.MessageFlagsChanged"/> events to get notifications of changes since
		/// the last time the folder was opened.</para>
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="uidValidity">The last known <see cref="MailFolder.UidValidity"/> value.</param>
		/// <param name="highestModSeq">The last known <see cref="MailFolder.HighestModSeq"/> value.</param>
		/// <param name="uids">The last known list of unique message identifiers.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="access"/> is not a valid value.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The QRESYNC feature has not been enabled.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the QRESYNC extension.
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
		public override FolderAccess Open (FolderAccess access, UniqueId uidValidity, ulong highestModSeq, IList<UniqueId> uids, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (access != FolderAccess.ReadOnly && access != FolderAccess.ReadWrite)
				throw new ArgumentOutOfRangeException ("access");

			CheckState (false, false);

			if (IsOpen && Access == access)
				return access;

			if ((Engine.Capabilities & ImapCapabilities.QuickResync) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QRESYNC extension.");

			if (!Engine.QResyncEnabled)
				throw new InvalidOperationException ("The QRESYNC extension has not been enabled.");

			var qresync = string.Format ("(QRESYNC ({0} {1}", uidValidity.Id, highestModSeq);

			if (uids.Count > 0)
				qresync += " " + set;

			qresync += "))";

			var command = string.Format ("{0} %F {1}\r\n", SelectOrExamine (access), qresync);
			var ic = new ImapCommand (Engine, cancellationToken, this, command, this);
			ic.RegisterUntaggedHandler ("FETCH", QResyncFetch);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic);

			if (Engine.Selected != null && Engine.Selected != this)
				Engine.Selected.Access = FolderAccess.None;

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			return Access;
		}

		/// <summary>
		/// Opens the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// Opens the folder using the requested folder access.
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="access"/> is not a valid value.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public override FolderAccess Open (FolderAccess access, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (access != FolderAccess.ReadOnly && access != FolderAccess.ReadWrite)
				throw new ArgumentOutOfRangeException ("access");

			CheckState (false, false);

			if (IsOpen && Access == access)
				return access;

			var condstore = (Engine.Capabilities & ImapCapabilities.CondStore) != 0 ? " (CONDSTORE)" : string.Empty;
			var command = string.Format ("{0} %F{1}\r\n", SelectOrExamine (access), condstore);
			var ic = Engine.QueueCommand (cancellationToken, this, command, this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic);

			if (Engine.Selected != null && Engine.Selected != this)
				Engine.Selected.Access = FolderAccess.None;

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			return Access;
		}

		/// <summary>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </remarks>
		/// <param name="expunge">If set to <c>true</c>, expunge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override void Close (bool expunge = false, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (true, false);

			ImapCommand ic;

			if (expunge) {
				ic = Engine.QueueCommand (cancellationToken, this, "CLOSE\r\n");
			} else if ((Engine.Capabilities & ImapCapabilities.Unselect) != 0) {
				ic = Engine.QueueCommand (cancellationToken, this, "UNSELECT\r\n");
			} else {
				return;
			}

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create (expunge ? "CLOSE" : "UNSELECT", ic);

			Engine.State = ImapEngineState.Authenticated;
			Access = FolderAccess.None;
			Engine.Selected = null;
		}

		/// <summary>
		/// Creates a new subfolder with the given name.
		/// </summary>
		/// <remarks>
		/// Creates a new subfolder with the given name.
		/// </remarks>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="isMessageFolder"><c>true</c> if the folder will be used to contain messages; otherwise <c>false</c>.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="name"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="MailFolder.DirectorySeparator"/> is nil, and thus child folders cannot be created.</para>
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
		public override IMailFolder Create (string name, bool isMessageFolder, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name is not a legal folder name.", "name");

			CheckState (false, false);

			if (!string.IsNullOrEmpty (FullName) && DirectorySeparator == '\0')
				throw new InvalidOperationException ("Cannot create child folders.");

			var fullName = !string.IsNullOrEmpty (FullName) ? FullName + DirectorySeparator + name : name;
			var encodedName = Engine.EncodeMailboxName (fullName);
			var list = new List<ImapFolder> ();
			var createName = encodedName;
			ImapFolder folder;

			if (!isMessageFolder)
				createName += DirectorySeparator;

			var ic = Engine.QueueCommand (cancellationToken, null, "CREATE %S\r\n", createName);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("CREATE", ic);

			ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			folder = list.FirstOrDefault ();

			if (folder != null)
				folder.ParentFolder = this;

			return folder;
		}

		/// <summary>
		/// Renames the folder to exist with a new name under a new parent folder.
		/// </summary>
		/// <remarks>
		/// Renames the folder to exist with a new name under a new parent folder.
		/// </remarks>
		/// <param name="parent">The new parent folder.</param>
		/// <param name="name">The new name of the folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="parent"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="parent"/> does not belong to the <see cref="ImapClient"/>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="name"/> is not a legal folder name.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder cannot be renamed (it is either a namespace or the Inbox).</para>
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
		public override void Rename (IMailFolder parent, string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (parent == null)
				throw new ArgumentNullException ("parent");

			if (!(parent is ImapFolder) || ((ImapFolder) parent).Engine != Engine)
				throw new ArgumentException ("The parent folder does not belong to this ImapClient.", "parent");

			if (name == null)
				throw new ArgumentNullException ("name");

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name is not a legal folder name.", "name");

			if (IsNamespace || FullName.ToUpperInvariant () == "INBOX")
				throw new InvalidOperationException ("Cannot rename this folder.");

			CheckState (false, false);

			string newFullName;

			if (!string.IsNullOrEmpty (parent.FullName))
				newFullName = parent.FullName + parent.DirectorySeparator + name;
			else
				newFullName = name;

			var encodedName = Engine.EncodeMailboxName (newFullName);
			var ic = Engine.QueueCommand (cancellationToken, null, "RENAME %F %S\r\n", this, encodedName);
			var oldFullName = FullName;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("RENAME", ic);

			Engine.FolderCache.Remove (EncodedName);
			Engine.FolderCache[encodedName] = this;

			ParentFolder.Renamed -= ParentFolderRenamed;
			SetParentFolder (parent);

			FullName = Engine.DecodeMailboxName (encodedName);
			EncodedName = encodedName;
			Name = name;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
			}

			OnRenamed (oldFullName, FullName);
		}

		/// <summary>
		/// Deletes the folder on the IMAP server.
		/// </summary>
		/// <remarks>
		/// Deletes the folder on the IMAP server.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder cannot be deleted (it is either a namespace or the Inbox).</para>
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
		public override void Delete (CancellationToken cancellationToken = default (CancellationToken))
		{
			if (IsNamespace || FullName.ToUpperInvariant () == "INBOX")
				throw new InvalidOperationException ("Cannot delete this folder.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "DELETE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("DELETE", ic);

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
			}

			Attributes |= FolderAttributes.NonExistent;
			Exists = false;
			OnDeleted ();
		}

		/// <summary>
		/// Subscribes the folder.
		/// </summary>
		/// <remarks>
		/// Subscribes the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public override void Subscribe (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "SUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("SUBSCRIBE", ic);

			IsSubscribed = true;
			OnSubscribed ();
		}

		/// <summary>
		/// Unsubscribes the folder.
		/// </summary>
		/// <remarks>
		/// Unsubscribes the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public override void Unsubscribe (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "UNSUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("UNSUBSCRIBE", ic);

			IsSubscribed = false;
			OnUnsubscribed ();
		}

		/// <summary>
		/// Gets the subfolders.
		/// </summary>
		/// <remarks>
		/// Gets the subfolders.
		/// </remarks>
		/// <returns>The subfolders.</returns>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public override IEnumerable<IMailFolder> GetSubfolders (bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			var pattern = EncodedName.Length > 0 ? EncodedName + DirectorySeparator + "%" : "%";
			var command = subscribedOnly ? "LSUB" : "LIST";
			var list = new List<ImapFolder> ();

			var ic = new ImapCommand (Engine, cancellationToken, null, command + " \"\" %S\r\n", pattern);
			ic.RegisterUntaggedHandler (command, ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			foreach (var folder in list)
				folder.ParentFolder = this;

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create (subscribedOnly ? "LSUB" : "LIST", ic);

			return list;
		}

		/// <summary>
		/// Gets the specified subfolder.
		/// </summary>
		/// <remarks>
		/// Gets the specified subfolder.
		/// </remarks>
		/// <returns>The subfolder.</returns>
		/// <param name="name">The name of the subfolder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="name"/> is either an empty string or contains the <see cref="MailFolder.DirectorySeparator"/>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The requested folder could not be found.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IMailFolder GetSubfolder (string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name of the subfolder is invalid.", "name");

			CheckState (false, false);

			var fullName = FullName.Length > 0 ? FullName + DirectorySeparator + name : name;
			var encodedName = Engine.EncodeMailboxName (fullName);
			var list = new List<ImapFolder> ();
			ImapFolder subfolder;

			if (Engine.FolderCache.TryGetValue (encodedName, out subfolder))
				return subfolder;

			var ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if (list.Count == 0)
				throw new FolderNotFoundException (fullName);

			return list[0];
		}

		/// <summary>
		/// Forces the server to flush its state for the folder.
		/// </summary>
		/// <remarks>
		/// Forces the server to flush its state for the folder.
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override void Check (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (true, false);

			var ic = Engine.QueueCommand (cancellationToken, this, "CHECK\r\n");

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("CHECK", ic);
		}

		/// <summary>
		/// Updates the values of the specified items.
		/// </summary>
		/// <remarks>
		/// <para>Updates the values of the specified items.</para>
		/// <para>The <see cref="Status(StatusItems, System.Threading.CancellationToken)"/> method
		/// MUST NOT be used on a folder that is already in the opened state. Instead, other ways
		/// of getting the desired information should be used.</para>
		/// <para>For example, a common use for the <see cref="Status(StatusItems,System.Threading.CancellationToken)"/>
		/// method is to get the number of unread messages in the folder. When the folder is open, however, it is
		/// possible to use the <see cref="ImapFolder.Search(MailKit.Search.SearchQuery, System.Threading.CancellationToken)"/>
		/// method to query for the list of unread messages.</para>
		/// </remarks>
		/// <param name="items">The items to update.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the STATUS command.
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
		public override void Status (StatusItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((Engine.Capabilities & ImapCapabilities.Status) == 0)
				throw new NotSupportedException ("The IMAP server does not support the STATUS command.");

			CheckState (false, false);

			string flags = string.Empty;
			if ((items & StatusItems.Count) != 0)
				flags += "MESSAGES ";
			if ((items & StatusItems.Recent) != 0)
				flags += "RECENT ";
			if ((items & StatusItems.UidNext) != 0)
				flags += "UIDNEXT ";
			if ((items & StatusItems.UidValidity) != 0)
				flags += "UIDVALIDITY ";
			if ((items & StatusItems.Unread) != 0)
				flags += "UNSEEN ";

			if ((Engine.Capabilities & ImapCapabilities.CondStore) != 0) {
				if ((items & StatusItems.HighestModSeq) != 0)
					flags += "HIGHESTMODSEQ ";
			}

			flags = flags.TrimEnd ();

			var command = string.Format ("STATUS %F ({0})\r\n", flags);
			var ic = Engine.QueueCommand (cancellationToken, null, command, this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("STATUS", ic);
		}

		static string ReadStringToken (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.Literal: return engine.ReadLiteral (cancellationToken);
			case ImapTokenType.QString: return (string) token.Value;
			case ImapTokenType.Atom:    return (string) token.Value;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}
		}

		static void UntaggedQuotaRoot (ImapEngine engine, ImapCommand ic, int index)
		{
			// The first token should be the mailbox name
			ReadStringToken (engine, ic.CancellationToken);

			// ...followed by 0 or more quota roots
			var token = engine.PeekToken (ic.CancellationToken);

			while (token.Type != ImapTokenType.Eoln) {
				ReadStringToken (engine, ic.CancellationToken);

				token = engine.PeekToken (ic.CancellationToken);
			}
		}

		static void UntaggedQuota (ImapEngine engine, ImapCommand ic, int index)
		{
			var encodedName = ReadStringToken (engine, ic.CancellationToken);
			ImapFolder quotaRoot;
			FolderQuota quota;

			if (!engine.FolderCache.TryGetValue (encodedName, out quotaRoot)) {
				// Note: this shouldn't happen because the quota root should
				// be one of the parent folders which will all have been added
				// to the folder cache by thsi point.
			}

			ic.UserData = quota = new FolderQuota (quotaRoot);

			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			while (token.Type != ImapTokenType.CloseParen) {
				uint used, limit;
				string resource;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				resource = (string) token.Value;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out used))
					throw ImapEngine.UnexpectedToken (token, false);

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out limit))
					throw ImapEngine.UnexpectedToken (token, false);

				switch (resource.ToUpperInvariant ()) {
				case "MESSAGE":
					quota.CurrentMessageCount = used;
					quota.MessageLimit = limit;
					break;
				case "STORAGE":
					quota.CurrentStorageSize = used;
					quota.StorageLimit = limit;
					break;
				}

				token = engine.ReadToken (ic.CancellationToken);
			}
		}

		/// <summary>
		/// Get the quota information for the folder.
		/// </summary>
		/// <remarks>
		/// <para>Gets the quota information for the folder.</para>
		/// <para>To determine if a quotas are supported, check the 
		/// <see cref="ImapClient.SupportsQuotas"/> property.</para>
		/// </remarks>
		/// <returns>The folder quota.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the QUOTA extension.
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
		public override FolderQuota GetQuota (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Quota) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QUOTA extension.");

			var ic = new ImapCommand (Engine, cancellationToken, null, "GETQUOTAROOT %F\r\n", this);
			ic.RegisterUntaggedHandler ("QUOTAROOT", UntaggedQuotaRoot);
			ic.RegisterUntaggedHandler ("QUOTA", UntaggedQuota);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("GETQUOTAROOT", ic);

			if (ic.UserData == null)
				return new FolderQuota (null);

			return (FolderQuota) ic.UserData;
		}

		/// <summary>
		/// Set the quota limits for the folder.
		/// </summary>
		/// <remarks>
		/// <para>Sets the quota limits for the folder.</para>
		/// <para>To determine if a quotas are supported, check the 
		/// <see cref="ImapClient.SupportsQuotas"/> property.</para>
		/// </remarks>
		/// <returns>The folder quota.</returns>
		/// <param name="messageLimit">If not <c>null</c>, sets the maximum number of messages to allow.</param>
		/// <param name="storageLimit">If not <c>null</c>, sets the maximum storage size (in kilobytes).</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the QUOTA extension.
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
		public override FolderQuota SetQuota (uint? messageLimit, uint? storageLimit, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Quota) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QUOTA extension.");

			var command = new StringBuilder ("SETQUOTA %F (");
			if (messageLimit.HasValue)
				command.AppendFormat ("MESSAGE {0}", messageLimit.Value);
			if (storageLimit.HasValue)
				command.AppendFormat ("STORAGE {0}", storageLimit.Value);
			command.Append (")\r\n");

			var ic = new ImapCommand (Engine, cancellationToken, null, command.ToString (), this);
			ic.RegisterUntaggedHandler ("QUOTA", UntaggedQuota);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("SETQUOTA", ic);

			if (ic.UserData == null)
				return new FolderQuota (null);

			return (FolderQuota) ic.UserData;
		}

		/// <summary>
		/// Expunges the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="MailFolder.MessageExpunged"/> event will be emitted
		/// for each message that is expunged. However, if the IMAP server supports the QRESYNC
		/// extension and it has been enabled via the
		/// <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="MailFolder.MessagesVanished"/> event will be emitted rather than the
		/// <see cref="MailFolder.MessageExpunged"/> event.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void Expunge (CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (true, true);

			var ic = Engine.QueueCommand (cancellationToken, this, "EXPUNGE\r\n");

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("EXPUNGE", ic);
		}

		/// <summary>
		/// Expunges the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="MailFolder.MessageExpunged"/> event will be emitted
		/// for each message that is expunged. However, if the IMAP server supports the QRESYNC
		/// extension and it has been enabled via the
		/// <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="MailFolder.MessagesVanished"/> event will be emitted rather than the
		/// <see cref="MailFolder.MessageExpunged"/> event.</para>
		/// </remarks>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UIDPLUS extension.
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
		public override void Expunge (IList<UniqueId> uids, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			CheckState (true, true);

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UIDPLUS extension.");

			if (uids.Count == 0)
				return;

			var command = string.Format ("UID EXPUNGE {0}\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("EXPUNGE", ic);
		}

		ImapCommand QueueAppend (FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			string format = "APPEND %F";

			if ((flags & SettableFlags) != 0)
				format += " " + ImapUtils.FormatFlagsList (flags, 0);

			if (date.HasValue)
				format += " \"" + ImapUtils.FormatInternalDate (date.Value) + "\"";

			format += " %L\r\n";

			return Engine.QueueCommand (cancellationToken, null, options, format, this, message);
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>Internationalized formatting was requested but has not been enabled.</para>
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
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override UniqueId? Append (FormatOptions options, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (options == null)
				throw new ArgumentNullException ("options");

			if (message == null)
				throw new ArgumentNullException ("message");

			CheckState (false, false);

			if (options.International && (Engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8 extension.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			if ((Engine.Capabilities & ImapCapabilities.UTF8Only) == ImapCapabilities.UTF8Only)
				format.International = true;

			if (format.International && !Engine.UTF8Enabled)
				throw new InvalidOperationException ("The UTF8 extension has not been enabled.");

			var ic = QueueAppend (format, message, flags, null, cancellationToken);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("APPEND", ic);

			if (UidValidity.HasValue) {
				var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

				if (append != null && append.UidValidity == UidValidity.Value)
					return append.UidSet[0];
			}

			return null;
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="options"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="message"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>Internationalized formatting was requested but has not been enabled.</para>
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
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override UniqueId? Append (FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (options == null)
				throw new ArgumentNullException ("options");

			if (message == null)
				throw new ArgumentNullException ("message");

			CheckState (false, false);

			if (options.International && (Engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8 extension.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			if ((Engine.Capabilities & ImapCapabilities.UTF8Only) == ImapCapabilities.UTF8Only)
				format.International = true;

			if (format.International && !Engine.UTF8Enabled)
				throw new InvalidOperationException ("The UTF8 extension has not been enabled.");

			var ic = QueueAppend (format, message, flags, date, cancellationToken);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("APPEND", ic);

			if (UidValidity.HasValue) {
				var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

				if (append != null && append.UidValidity == UidValidity.Value)
					return append.UidSet[0];
			}

			return null;
		}

		ImapCommand QueueMultiAppend (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken)
		{
			var args = new List<object> ();
			string format = "APPEND %F";

			args.Add (this);

			for (int i = 0; i < messages.Count; i++) {
				if ((flags[i] & SettableFlags) != 0)
					format += " " + ImapUtils.FormatFlagsList (flags[i], 0);

				if (dates != null)
					format += " \"" + ImapUtils.FormatInternalDate (dates[i]) + "\"";

				format += " %L";

				args.Add (messages[i]);
			}

			format += "\r\n";

			return Engine.QueueCommand (cancellationToken, null, options, format, args.ToArray ());
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The list of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>Internationalized formatting was requested but has not been enabled.</para>
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
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<UniqueId> Append (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (options == null)
				throw new ArgumentNullException ("options");

			if (messages == null)
				throw new ArgumentNullException ("messages");

			for (int i = 0; i < messages.Count; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException ("flags");

			if (messages.Count != flags.Count)
				throw new ArgumentException ("The number of messages and the number of flags must be equal.");

			CheckState (false, false);

			if (options.International && (Engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8 extension.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			if ((Engine.Capabilities & ImapCapabilities.UTF8Only) == ImapCapabilities.UTF8Only)
				format.International = true;

			if (format.International && !Engine.UTF8Enabled)
				throw new InvalidOperationException ("The UTF8 extension has not been enabled.");

			if (messages.Count == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.MultiAppend) != 0) {
				var ic = QueueMultiAppend (format, messages, flags, null, cancellationToken);

				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Result != ImapCommandResult.Ok)
					throw ImapCommandException.Create ("APPEND", ic);

				if (UidValidity.HasValue) {
					var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

					if (append != null && append.UidValidity == UidValidity.Value)
						return append.UidSet;
				}

				return new UniqueId[0];
			}

			var uids = new List<UniqueId> ();

			for (int i = 0; i < messages.Count; i++) {
				var uid = Append (format, messages[i], flags[i], cancellationToken);
				if (uids != null && uid.HasValue)
					uids.Add (uid.Value);
				else
					uids = null;
			}

			if (uids == null)
				return new UniqueId[0];

			return new ReadOnlyCollection<UniqueId> (uids);
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The list of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>Internationalized formatting was requested but has not been enabled.</para>
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
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<UniqueId> Append (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (options == null)
				throw new ArgumentNullException ("options");

			if (messages == null)
				throw new ArgumentNullException ("messages");

			for (int i = 0; i < messages.Count; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException ("flags");

			if (dates == null)
				throw new ArgumentNullException ("dates");

			if (messages.Count != flags.Count || messages.Count != dates.Count)
				throw new ArgumentException ("The number of messages, the number of flags, and the number of dates must be equal.");

			CheckState (false, false);

			if (options.International && (Engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8 extension.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			if ((Engine.Capabilities & ImapCapabilities.UTF8Only) == ImapCapabilities.UTF8Only)
				format.International = true;

			if (format.International && !Engine.UTF8Enabled)
				throw new InvalidOperationException ("The UTF8 extension has not been enabled.");

			if (messages.Count == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.MultiAppend) != 0) {
				var ic = QueueMultiAppend (format, messages, flags, dates, cancellationToken);

				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Result != ImapCommandResult.Ok)
					throw ImapCommandException.Create ("APPEND", ic);

				if (UidValidity.HasValue) {
					var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

					if (append != null && append.UidValidity == UidValidity.Value)
						return append.UidSet;
				}

				return new UniqueId[0];
			}

			var uids = new List<UniqueId> ();

			for (int i = 0; i < messages.Count; i++) {
				var uid = Append (format, messages[i], flags[i], dates[i], cancellationToken);
				if (uids != null && uid.HasValue)
					uids.Add (uid.Value);
				else
					uids = null;
			}

			if (uids == null)
				return new UniqueId[0];

			return new ReadOnlyCollection<UniqueId> (uids);
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise an empty array.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="ImapClient"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UIDPLUS extension.
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
		public override IList<UniqueId> CopyTo (IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, false);

			if (uids.Count == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				var indexes = Fetch (uids, MessageSummaryItems.UniqueId, cancellationToken).Select (x => x.Index).ToList ();
				CopyTo (indexes, destination, cancellationToken);
				return new UniqueId[0];
			}

			var command = string.Format ("UID COPY {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("COPY", ic);

			if (destination.UidValidity.HasValue) {
				var copy = ic.RespCodes.OfType<CopyUidResponseCode> ().FirstOrDefault ();

				if (copy != null && copy.UidValidity == destination.UidValidity.Value)
					return copy.DestUidSet;
			}

			return new UniqueId[0];
		}

		/// <summary>
		/// Moves the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the MOVE command, then the MOVE command will be used. Otherwise,
		/// the messages will first be copied to the destination folder, then marked as \Deleted in the
		/// originating folder, and finally, if the server supports the UIDPLUS extension, expunged. Since the
		/// server could disconnect at any point between those 3 operations, it may be advisable to implement
		/// your own logic for moving messages in this case in order to better handle spontanious server
		/// disconnects and other error conditions.</para>
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise an empty array.</returns>
		/// <param name="uids">The UIDs of the messages to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="ImapClient"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the UIDPLUS extension.
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
		public override IList<UniqueId> MoveTo (IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				var copied = CopyTo (uids, destination, cancellationToken);
				AddFlags (uids, MessageFlags.Deleted, true, cancellationToken);
				if ((Engine.Capabilities & ImapCapabilities.UidPlus) != 0)
					Expunge (uids, cancellationToken);
				return copied;
			}

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				var indexes = Fetch (uids, MessageSummaryItems.UniqueId, cancellationToken).Select (x => x.Index).ToList ();
				MoveTo (indexes, destination, cancellationToken);
				return new UniqueId[0];
			}

			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, true);

			if (uids.Count == 0)
				return new UniqueId[0];

			var command = string.Format ("UID MOVE {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("MOVE", ic);

			if (destination.UidValidity.HasValue) {
				var copy = ic.RespCodes.OfType<CopyUidResponseCode> ().FirstOrDefault ();

				if (copy != null && copy.UidValidity == destination.UidValidity.Value)
					return copy.DestUidSet;
			}

			return new UniqueId[0];
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="ImapClient"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override void CopyTo (IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, false);

			if (indexes.Count == 0)
				return;

			var command = string.Format ("COPY {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("COPY", ic);
		}

		/// <summary>
		/// Moves the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the MOVE command, then the MOVE command will be used. Otherwise,
		/// the messages will first be copied to the destination folder and then marked as \Deleted in the
		/// originating folder. Since the server could disconnect at any point between those 2 operations, it
		/// may be advisable to implement your own logic for moving messages in this case in order to better
		/// handle spontanious server disconnects and other error conditions.</para>
		/// </remarks>
		/// <param name="indexes">The indexes of the messages to move.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>The destination folder does not belong to the <see cref="ImapClient"/>.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void MoveTo (IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				CopyTo (indexes, destination, cancellationToken);
				AddFlags (indexes, MessageFlags.Deleted, true, cancellationToken);
				return;
			}

			var set = ImapUtils.FormatIndexSet (indexes);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, true);

			if (indexes.Count == 0)
				return;

			var command = string.Format ("MOVE {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("MOVE", ic);
		}

		static void ReadLiteralData (ImapEngine engine, CancellationToken cancellationToken)
		{
			var buf = new byte[4096];
			int nread;

			do {
				nread = engine.Stream.Read (buf, 0, buf.Length, cancellationToken);
			} while (nread > 0);
		}

		static void FetchSummaryItems (ImapEngine engine, ImapCommand ic, int index)
		{
			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			var results = (SortedDictionary<int, IMessageSummary>) ic.UserData;
			IMessageSummary isummary;
			MessageSummary summary;

			if (!results.TryGetValue (index, out isummary)) {
				summary = new MessageSummary (index);
				results.Add (index, summary);
			} else {
				summary = (MessageSummary) isummary;
			}

			do {
				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				var atom = (string) token.Value;
				ulong value64;
				uint value;

				switch (atom) {
				case "INTERNALDATE":
					token = engine.ReadToken (ic.CancellationToken);

					switch (token.Type) {
					case ImapTokenType.QString:
					case ImapTokenType.Atom:
						summary.InternalDate = ImapUtils.ParseInternalDate ((string) token.Value);
						break;
					case ImapTokenType.Nil:
						summary.InternalDate = null;
						break;
					default:
						throw ImapEngine.UnexpectedToken (token, false);
					}
					break;
				case "RFC822.SIZE":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value))
						throw ImapEngine.UnexpectedToken (token, false);

					summary.MessageSize = value;
					break;
				case "BODYSTRUCTURE":
					summary.Body = ImapUtils.ParseBody (engine, string.Empty, ic.CancellationToken);
					break;
				case "BODY":
					token = engine.PeekToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.OpenBracket) {
						// consume the '['
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.OpenBracket)
							throw ImapEngine.UnexpectedToken (token, false);

						// References were requested...

						do {
							token = engine.ReadToken (ic.CancellationToken);

							if (token.Type == ImapTokenType.CloseBracket)
								break;

							if (token.Type == ImapTokenType.OpenParen) {
								do {
									token = engine.ReadToken (ic.CancellationToken);

									if (token.Type == ImapTokenType.CloseParen)
										break;

									if (token.Type != ImapTokenType.Atom)
										throw ImapEngine.UnexpectedToken (token, false);
								} while (true);
							} else if (token.Type != ImapTokenType.Atom) {
								throw ImapEngine.UnexpectedToken (token, false);
							}
						} while (true);

						if (token.Type != ImapTokenType.CloseBracket)
							throw ImapEngine.UnexpectedToken (token, false);

						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.Literal)
							throw ImapEngine.UnexpectedToken (token, false);

						try {
							var message = engine.ParseMessage (engine.Stream, false, ic.CancellationToken);
							summary.References = message.References;
							summary.Headers = message.Headers;
						} catch (FormatException) {
							// consume any remaining literal data...
							ReadLiteralData (engine, ic.CancellationToken);
						}
					} else {
						try {
							summary.Body = ImapUtils.ParseBody (engine, string.Empty, ic.CancellationToken);
						} catch (ImapProtocolException ex) {
							if (!ex.UnexpectedToken)
								throw;

							// Note: GMail's IMAP implementation sometimes replies with completely broken BODY values
							// (see issue #32 for examples), so to work around this nonsense, we need to drop the
							// remainder of this line.
							do {
								token = engine.PeekToken (ic.CancellationToken);

								if (token.Type == ImapTokenType.Eoln)
									break;

								token = engine.ReadToken (ic.CancellationToken);

								if (token.Type == ImapTokenType.Literal)
									ReadLiteralData (engine, ic.CancellationToken);
							} while (true);

							return;
						}
					}
					break;
				case "ENVELOPE":
					summary.Envelope = ImapUtils.ParseEnvelope (engine, ic.CancellationToken);
					break;
				case "FLAGS":
					summary.Flags = ImapUtils.ParseFlagsList (engine, summary.UserFlags, ic.CancellationToken);
					break;
				case "MODSEQ":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out value64) || value64 == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (token, false);

					summary.ModSeq = value64;
					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value) || value == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					summary.UniqueId = new UniqueId (value);
					break;
				case "X-GM-MSGID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out value64) || value64 == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					summary.GMailMessageId = value64;
					break;
				case "X-GM-THRID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out value64) || value64 == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					summary.GMailThreadId = value64;
					break;
				case "X-GM-LABELS":
					summary.GMailLabels = ImapUtils.ParseLabelsList (engine, ic.CancellationToken);
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);
		}

		string FormatSummaryItems (MessageSummaryItems items, HashSet<HeaderId> fields)
		{
			if ((items & MessageSummaryItems.BodyStructure) != 0 && (items & MessageSummaryItems.Body) != 0) {
				// don't query both the BODY and BODYSTRUCTURE, that's just dumb...
				items &= ~MessageSummaryItems.Body;
			}

			// first, eliminate the aliases...
			if (items == MessageSummaryItems.All)
				return "ALL";

			if (items == MessageSummaryItems.Full)
				return "FULL";

			if (items == MessageSummaryItems.Fast)
				return "FAST";

			var tokens = new List<string> ();

			// now add on any additional summary items...
			if ((items & MessageSummaryItems.UniqueId) != 0)
				tokens.Add ("UID");
			if ((items & MessageSummaryItems.Flags) != 0)
				tokens.Add ("FLAGS");
			if ((items & MessageSummaryItems.InternalDate) != 0)
				tokens.Add ("INTERNALDATE");
			if ((items & MessageSummaryItems.MessageSize) != 0)
				tokens.Add ("RFC822.SIZE");
			if ((items & MessageSummaryItems.Envelope) != 0)
				tokens.Add ("ENVELOPE");
			if ((items & MessageSummaryItems.BodyStructure) != 0)
				tokens.Add ("BODYSTRUCTURE");
			if ((items & MessageSummaryItems.Body) != 0)
				tokens.Add ("BODY");

			if ((Engine.Capabilities & ImapCapabilities.CondStore) != 0) {
				if ((items & MessageSummaryItems.ModSeq) != 0)
					tokens.Add ("MODSEQ");
			}

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) != 0) {
				// now for the GMail extension items
				if ((items & MessageSummaryItems.GMailMessageId) != 0)
					tokens.Add ("X-GM-MSGID");
				if ((items & MessageSummaryItems.GMailThreadId) != 0)
					tokens.Add ("X-GM-THRID");
				if ((items & MessageSummaryItems.GMailLabels) != 0)
					tokens.Add ("X-GM-LABELS");
			}

			if ((items & MessageSummaryItems.References) != 0 || fields != null) {
				var headers = new StringBuilder ("BODY.PEEK[HEADER.FIELDS (");

				if (fields != null && fields.Contains (HeaderId.References))
					items &= ~MessageSummaryItems.References;

				if ((items & MessageSummaryItems.References) != 0)
					headers.Append ("REFERENCES ");

				if (fields != null) {
					foreach (var field in fields) {
						headers.Append (field.ToHeaderName ().ToUpperInvariant ());
						headers.Append (' ');
					}
				}

				headers[headers.Length - 1] = ')';
				headers.Append (']');

				tokens.Add (headers.ToString ());
			}

			if (tokens.Count == 1)
				return tokens[0];

			return string.Format ("({0})", string.Join (" ", tokens));
		}

		static IList<IMessageSummary> AsReadOnly (ICollection<IMessageSummary> collection)
		{
			var array = new IMessageSummary[collection.Count];

			collection.CopyTo (array, 0);

			return new ReadOnlyCollection<IMessageSummary> (array);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message UIDs.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, null);
			var command = string.Format ("UID FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message UIDs.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="fields">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, MessageSummaryItems items, HashSet<HeaderId> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (fields == null)
				throw new ArgumentNullException ("fields");

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", "fields");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, fields);
			var command = string.Format ("UID FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, null);
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var command = string.Format ("UID FETCH {0} {1} (CHANGEDSINCE {2}{3})\r\n", set, query, modseq, vanished);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="fields">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, HashSet<HeaderId> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (fields == null)
				throw new ArgumentNullException ("fields");

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", "fields");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, fields);
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var command = string.Format ("UID FETCH {0} {1} (CHANGEDSINCE {2}{3})\r\n", set, query, modseq, vanished);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message indexes.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, null);
			var command = string.Format ("FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message indexes.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="fields">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, MessageSummaryItems items, HashSet<HeaderId> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (fields == null)
				throw new ArgumentNullException ("fields");

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", "fields");

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, fields);
			var command = string.Format ("FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="indexes"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, null);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", set, query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="fields">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="fields"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, ulong modseq, MessageSummaryItems items, HashSet<HeaderId> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (fields == null)
				throw new ArgumentNullException ("fields");

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", "fields");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, fields);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", set, query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		static string GetFetchRange (int min, int max)
		{
			if (min == max)
				return (min + 1).ToString ();

			var maxValue = max != -1 ? (max + 1).ToString () : "*";

			return string.Format ("{0}:{1}", min + 1, maxValue);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="items"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<IMessageSummary> Fetch (int min, int max, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (min < 0 || min > Count)
				throw new ArgumentOutOfRangeException ("min");

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException ("max");

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			if (min == Count)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, null);
			var command = string.Format ("FETCH {0} {1}\r\n", GetFetchRange (min, max), query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="fields">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="fields"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="fields"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<IMessageSummary> Fetch (int min, int max, MessageSummaryItems items, HashSet<HeaderId> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (min < 0 || min > Count)
				throw new ArgumentOutOfRangeException ("min");

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException ("max");

			if (fields == null)
				throw new ArgumentNullException ("fields");

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", "fields");

			CheckState (true, false);

			if (min == Count)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (items, fields);
			var command = string.Format ("FETCH {0} {1}\r\n", GetFetchRange (min, max), query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="items"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (min < 0 || min >= Count)
				throw new ArgumentOutOfRangeException ("min");

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException ("max");

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			var query = FormatSummaryItems (items, null);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", GetFetchRange (min, max), query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="fields">The desired header fields.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="min"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="max"/> is out of range.</para>
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="fields"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="fields"/> is empty.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<IMessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, HashSet<HeaderId> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (min < 0 || min >= Count)
				throw new ArgumentOutOfRangeException ("min");

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException ("max");

			if (fields == null)
				throw new ArgumentNullException ("fields");

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", "fields");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			var query = FormatSummaryItems (items, fields);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", GetFetchRange (min, max), query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, IMessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (results.Values);
		}

		static void FetchMessageBody (ImapEngine engine, ImapCommand ic, int index)
		{
			var streams = (Dictionary<string, Stream>) ic.UserData;
			var token = engine.ReadToken (ic.CancellationToken);
			var labels = new MessageLabelsChangedEventArgs (index);
			var flags = new MessageFlagsChangedEventArgs (index);
			bool labelsChanged = false;
			bool flagsChanged = false;
			var buf = new byte[4096];
			string specifier;
			Stream stream;
			int nread;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			do {
				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				var atom = (string) token.Value;
				ulong modseq;
				uint uid;

				switch (atom) {
				case "BODY":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenBracket)
						throw ImapEngine.UnexpectedToken (token, false);

					specifier = string.Empty;

					do {
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type == ImapTokenType.CloseBracket)
							break;

						if (token.Type == ImapTokenType.OpenParen) {
							do {
								token = engine.ReadToken (ic.CancellationToken);

								if (token.Type == ImapTokenType.CloseParen)
									break;

								if (token.Type != ImapTokenType.Atom)
									throw ImapEngine.UnexpectedToken (token, false);
							} while (true);
						} else if (token.Type != ImapTokenType.Atom) {
							throw ImapEngine.UnexpectedToken (token, false);
						} else {
							specifier += (string) token.Value;
						}
					} while (true);

					if (token.Type != ImapTokenType.CloseBracket)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.Atom) {
						var region = (string) token.Value;

						if (region[0] != '<' || region[region.Length - 1] != '>')
							throw ImapEngine.UnexpectedToken (token, false);

						token = engine.ReadToken (ic.CancellationToken);
					}

					switch (token.Type) {
					case ImapTokenType.Literal:
						stream = new MemoryBlockStream ();

						while ((nread = engine.Stream.Read (buf, 0, buf.Length, ic.CancellationToken)) > 0)
							stream.Write (buf, 0, nread);

						streams[specifier] = stream;
						stream.Position = 0;
						break;
					case ImapTokenType.QString:
					case ImapTokenType.Atom:
						stream = new MemoryStream (Encoding.UTF8.GetBytes ((string) token.Value), false);
						break;
					default:
						throw ImapEngine.UnexpectedToken (token, false);
					}

					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					labels.UniqueId = new UniqueId (uid);
					flags.UniqueId = new UniqueId (uid);
					break;
				case "MODSEQ":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq) || modseq == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (token, false);

					labels.ModSeq = modseq;
					flags.ModSeq = modseq;
					break;
				case "FLAGS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message flags.
					flags.Flags = ImapUtils.ParseFlagsList (engine, flags.UserFlags, ic.CancellationToken);
					flagsChanged = true;
					break;
				case "X-GM-LABELS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message labels.
					labels.Labels = ImapUtils.ParseLabelsList (engine, ic.CancellationToken);
					labelsChanged = true;
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			if (flagsChanged)
				ic.Folder.OnMessageFlagsChanged (flags);

			if (labelsChanged)
				ic.Folder.OnMessageLabelsChanged (labels);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override MimeMessage GetMessage (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", "uid");

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[])\r\n", uid.Id);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return Engine.ParseMessage (stream, true, cancellationToken);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <remarks>
		/// Gets the specified message.
		/// </remarks>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[])\r\n", index + 1);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return Engine.ParseMessage (stream, true, cancellationToken);
		}

		static string GetBodyPartQuery (BodyPart part, bool headersOnly, out string[] tags)
		{
			string query;

			if (headersOnly) {
				tags = new string[1];

				if (part.PartSpecifier.Length > 0) {
					query = string.Format ("BODY.PEEK[{0}.MIME]", part.PartSpecifier);
					tags[0] = part.PartSpecifier + ".MIME";
				} else {
					query = "BODY.PEEK[HEADER]";
					tags[0] = "HEADER";
				}
			} else {
				tags = new string[2];

				if (part.PartSpecifier.Length > 0) {
					tags[0] = part.PartSpecifier + ".MIME";
					tags[1] = part.PartSpecifier;
				} else {
					tags[0] = "HEADER";
					tags[1] = "TEXT";
				}

				query = string.Format ("BODY.PEEK[{0}] BODY.PEEK[{1}]", tags[0], tags[1]);
			}

			return query;
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override MimeEntity GetBodyPart (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetBodyPart (uid, part, false, cancellationToken);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override MimeEntity GetBodyPart (UniqueId uid, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", "uid");

			if (part == null)
				throw new ArgumentNullException ("part");

			CheckState (true, false);

			string[] tags;

			var command = string.Format ("UID FETCH {0} ({1})\r\n", uid.Id, GetBodyPartQuery (part, headersOnly, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			var chained = new ChainedStream ();

			foreach (var tag in tags) {
				if (!streams.TryGetValue (tag, out stream))
					return null;

				chained.Add (stream);
			}

			var entity = Engine.ParseEntity (chained, true, cancellationToken);

			if (part.PartSpecifier.Length == 0) {
				for (int i = entity.Headers.Count; i > 0; i--) {
					var header = entity.Headers[i - 1];

					if (!header.Field.StartsWith ("Content-", StringComparison.OrdinalIgnoreCase))
						entity.Headers.RemoveAt (i - 1);
				}
			}

			return entity;
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override MimeEntity GetBodyPart (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetBodyPart (index, part, false, cancellationToken);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override MimeEntity GetBodyPart (int index, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			if (part == null)
				throw new ArgumentNullException ("part");

			CheckState (true, false);

			string[] tags;

			var command = string.Format ("FETCH {0} ({1})\r\n", index + 1, GetBodyPartQuery (part, headersOnly, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			var chained = new ChainedStream ();

			foreach (var tag in tags) {
				if (!streams.TryGetValue (tag, out stream))
					return null;

				chained.Add (stream);
			}

			var entity = Engine.ParseEntity (chained, true, cancellationToken);

			if (part.PartSpecifier.Length == 0) {
				for (int i = entity.Headers.Count; i > 0; i--) {
					var header = entity.Headers[i - 1];

					if (!header.Field.StartsWith ("Content-", StringComparison.OrdinalIgnoreCase))
						entity.Headers.RemoveAt (i - 1);
				}
			}

			return entity;
		}

		/// <summary>
		/// Gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override Stream GetStream (UniqueId uid, int offset, int count, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", "uid");

			if (offset < 0)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException ("count");

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[]<%d.%d>)\r\n", uid.Id, offset, count);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the message. If the starting offset is beyond
		/// the end of the message, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the message, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="index"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override Stream GetStream (int index, int offset, int count, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			if (offset < 0)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException ("count");

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[]<%d.%d>)\r\n", index + 1, offset, count);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the body part. If the starting offset is beyond
		/// the end of the body part, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the body part, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The desired body part.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override Stream GetStream (UniqueId uid, BodyPart part, int offset, int count, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", "uid");

			if (part == null)
				throw new ArgumentNullException ("part");

			if (offset < 0)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException ("count");

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}]<{2}.{3}>)\r\n", uid.Id, part.PartSpecifier, offset, count);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (!streams.TryGetValue (part.PartSpecifier, out stream))
				return null;

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// Fetches a substream of the body part. If the starting offset is beyond
		/// the end of the body part, an empty stream is returned. If the number of
		/// bytes desired extends beyond the end of the body part, a truncated stream
		/// will be returned.
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The desired body part.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="index"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override Stream GetStream (int index, BodyPart part, int offset, int count, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			if (part == null)
				throw new ArgumentNullException ("part");

			if (offset < 0)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0)
				throw new ArgumentOutOfRangeException ("count");

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var command = string.Format ("FETCH {0} (BODY.PEEK[{1}]<{2}.{3}>)\r\n", index + 1, part.PartSpecifier, offset, count);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return stream;
		}

		IList<UniqueId> ModifyFlags (IList<UniqueId> uids, ulong? modseq, MessageFlags flags, HashSet<string> userFlags, string action, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & PermanentFlags, userFlags != null ? userFlags.Count : 0);
			var userFlagList = userFlags != null ? userFlags.ToArray () : new object[0];
			var set = ImapUtils.FormatUidSet (uids);

			if (modseq.HasValue && !SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (uids.Count == 0)
				return new UniqueId[0];

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var format = string.Format ("UID STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format, userFlagList);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			if (modseq.HasValue) {
				var modified = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (modified != null)
					return modified.UidSet;
			}

			return new UniqueId[0];
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="userFlags">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void AddFlags (IList<UniqueId> uids, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			bool emptyUserFlags = userFlags == null || userFlags.Count == 0;

			if ((flags & SettableFlags) == 0 && emptyUserFlags)
				throw new ArgumentException ("No flags were specified.", "flags");

			if ((flags & PermanentFlags) == 0 && (emptyUserFlags || (PermanentFlags & MessageFlags.UserDefined) == 0))
				return;

			ModifyFlags (uids, null, flags, userFlags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="userFlags">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void RemoveFlags (IList<UniqueId> uids, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", "flags");

			ModifyFlags (uids, null, flags, userFlags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="userFlags">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void SetFlags (IList<UniqueId> uids, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			ModifyFlags (uids, null, flags, userFlags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="userFlags">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<UniqueId> AddFlags (IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", "flags");

			return ModifyFlags (uids, modseq, flags, userFlags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="userFlags">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<UniqueId> RemoveFlags (IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", "flags");

			return ModifyFlags (uids, modseq, flags, userFlags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="userFlags">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<UniqueId> SetFlags (IList<UniqueId> uids, ulong modseq, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ModifyFlags (uids, modseq, flags, userFlags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		IList<int> ModifyFlags (IList<int> indexes, ulong? modseq, MessageFlags flags, HashSet<string> userFlags, string action, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & PermanentFlags, userFlags != null ? userFlags.Count : 0);
			var userFlagList = userFlags != null ? userFlags.ToArray () : new object[0];
			var set = ImapUtils.FormatIndexSet (indexes);

			if (modseq.HasValue && !SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (indexes.Count == 0)
				return new int[0];

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var format = string.Format ("STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format, userFlagList);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			if (modseq.HasValue) {
				var modified = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (modified != null) {
					var unmodified = new int[modified.UidSet.Count];
					for (int i = 0; i < unmodified.Length; i++)
						unmodified[i] = (int) (modified.UidSet[i].Id - 1);

					return new ReadOnlyCollection<int> (unmodified);
				}
			}

			return new int[0];
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="userFlags">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void AddFlags (IList<int> indexes, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			bool emptyUserFlags = userFlags == null || userFlags.Count == 0;

			if ((flags & SettableFlags) == 0 && emptyUserFlags)
				throw new ArgumentException ("No flags were specified.", "flags");

			if ((flags & PermanentFlags) == 0 && (emptyUserFlags || (PermanentFlags & MessageFlags.UserDefined) == 0))
				return;

			ModifyFlags (indexes, null, flags, userFlags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="userFlags">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void RemoveFlags (IList<int> indexes, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", "flags");

			ModifyFlags (indexes, null, flags, userFlags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages.
		/// </remarks>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="userFlags">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
		public override void SetFlags (IList<int> indexes, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			ModifyFlags (indexes, null, flags, userFlags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="userFlags">A set of user-defined flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<int> AddFlags (IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", "flags");

			return ModifyFlags (indexes, modseq, flags, userFlags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="userFlags">A set of user-defined flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<int> RemoveFlags (IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", "flags");

			return ModifyFlags (indexes, modseq, flags, userFlags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <remarks>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </remarks>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="userFlags">A set of user-defined flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MailFolder.MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The <see cref="ImapFolder"/> does not support mod-sequences.
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
		public override IList<int> SetFlags (IList<int> indexes, ulong modseq, MessageFlags flags, HashSet<string> userFlags, bool silent, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ModifyFlags (indexes, modseq, flags, userFlags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		static string LabelListToString (IList<string> labels, IList<object> args)
		{
			var list = new StringBuilder ("(");

			for (int i = 0; i < labels.Count; i++) {
				if (i > 0)
					list.Append (' ');

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
					args.Add (labels[i]);
					break;
				}
			}

			list.Append (')');

			return list.ToString ();
		}

		IList<UniqueId> ModifyLabels (IList<UniqueId> uids, ulong? modseq, IList<string> labels, string action, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
				throw new NotSupportedException ("The IMAP server does not support the Google Mail extensions.");

			CheckState (true, true);

			if (uids.Count == 0)
				return new UniqueId[0];

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var args = new List<object> ();
			var list = LabelListToString (labels, args);
			var format = string.Format ("UID STORE {0}{1} {2} {3}\r\n", set, @params, action, list);
			var ic = Engine.QueueCommand (cancellationToken, this, format, args.ToArray ());

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			if (modseq.HasValue) {
				var modified = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (modified != null)
					return modified.UidSet;
			}

			return new UniqueId[0];
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
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			ModifyLabels (uids, null, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			ModifyLabels (uids, null, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			ModifyLabels (uids, null, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			return ModifyLabels (uids, modseq, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			return ModifyLabels (uids, modseq, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			return ModifyLabels (uids, modseq, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", cancellationToken);
		}

		IList<int> ModifyLabels (IList<int> indexes, ulong? modseq, IList<string> labels, string action, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
				throw new NotSupportedException ("The IMAP server does not support the Google Mail extensions.");

			CheckState (true, true);

			if (indexes.Count == 0)
				return new int[0];

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var args = new List<object> ();
			var list = LabelListToString (labels, args);
			var format = string.Format ("STORE {0}{1} {2} {3}\r\n", set, @params, action, list);
			var ic = Engine.QueueCommand (cancellationToken, this, format, args.ToArray ());

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			if (modseq.HasValue) {
				var modified = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (modified != null) {
					var unmodified = new int[modified.UidSet.Count];
					for (int i = 0; i < unmodified.Length; i++)
						unmodified[i] = (int) (modified.UidSet[i].Id - 1);

					return new ReadOnlyCollection<int> (unmodified);
				}
			}

			return new int[0];
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
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			ModifyLabels (indexes, null, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			ModifyLabels (indexes, null, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			ModifyLabels (indexes, null, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No labels were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			return ModifyLabels (indexes, modseq, labels, silent ? "+X-GM-LABELS.SILENT" : "+X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para>No flags were specified.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			if (labels.Count == 0)
				throw new ArgumentException ("No labels were specified.", "labels");

			return ModifyLabels (indexes, modseq, labels, silent ? "-X-GM-LABELS.SILENT" : "-X-GM-LABELS", cancellationToken);
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
		/// <para><paramref name="indexes"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="indexes"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open in read-write mode.</para>
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
				throw new ArgumentNullException ("labels");

			return ModifyLabels (indexes, modseq, labels, silent ? "X-GM-LABELS.SILENT" : "X-GM-LABELS", cancellationToken);
		}

		static bool IsAscii (string text)
		{
			for (int i = 0; i < text.Length; i++) {
				if (text[i] > 127)
					return false;
			}

			return true;
		}

		static string FormatDateTime (DateTime date)
		{
			return date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant ();
		}

		void BuildQuery (StringBuilder builder, SearchQuery query, List<string> args, bool parens, ref bool ascii)
		{
			TextSearchQuery text = null;
			NumericSearchQuery numeric;
			HeaderSearchQuery header;
			BinarySearchQuery binary;
			UnarySearchQuery unary;
			DateSearchQuery date;
			bool isFlag;

			if (builder.Length > 0)
				builder.Append (' ');

			switch (query.Term) {
			case SearchTerm.All:
				builder.Append ("ALL");
				break;
			case SearchTerm.And:
				binary = (BinarySearchQuery) query;
				if (parens)
					builder.Append ('(');
				BuildQuery (builder, binary.Left, args, false, ref ascii);
				BuildQuery (builder, binary.Right, args, false, ref ascii);
				if (parens)
					builder.Append (')');
				break;
			case SearchTerm.Answered:
				builder.Append ("ANSWERED");
				break;
			case SearchTerm.BccContains:
				text = (TextSearchQuery) query;
				builder.Append ("BCC %S");
				args.Add (text.Text);
				break;
			case SearchTerm.BodyContains:
				text = (TextSearchQuery) query;
				builder.Append ("BODY %S");
				args.Add (text.Text);
				break;
			case SearchTerm.CcContains:
				text = (TextSearchQuery) query;
				builder.Append ("CC %S");
				args.Add (text.Text);
				break;
			case SearchTerm.Deleted:
				builder.Append ("DELETED");
				break;
			case SearchTerm.DeliveredAfter:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SINCE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.DeliveredBefore:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("BEFORE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.DeliveredOn:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("ON {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.Draft:
				builder.Append ("DRAFT");
				break;
			case SearchTerm.Flagged:
				builder.Append ("FLAGGED");
				break;
			case SearchTerm.FromContains:
				text = (TextSearchQuery) query;
				builder.Append ("FROM %S");
				args.Add (text.Text);
				break;
			case SearchTerm.Fuzzy:
				if ((Engine.Capabilities & ImapCapabilities.FuzzySearch) == 0)
					throw new NotSupportedException ("The FUZZY search term is not supported by the IMAP server.");

				builder.Append ("FUZZY");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args, true, ref ascii);
				break;
			case SearchTerm.HeaderContains:
				header = (HeaderSearchQuery) query;
				builder.AppendFormat ("HEADER {0} %S", header.Field);
				args.Add (header.Value);
				break;
			case SearchTerm.Keyword:
				text = (TextSearchQuery) query;
				builder.Append ("KEYWORD %S");
				args.Add (text.Text);
				break;
			case SearchTerm.LargerThan:
				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("LARGER {0}", numeric.Value);
				break;
			case SearchTerm.MessageContains:
				text = (TextSearchQuery) query;
				builder.Append ("TEXT %S");
				args.Add (text.Text);
				break;
			case SearchTerm.ModSeq:
				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("MODSEQ {0}", numeric.Value);
				break;
			case SearchTerm.New:
				builder.Append ("NEW");
				break;
			case SearchTerm.Not:
				builder.Append ("NOT");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args, true, ref ascii);
				break;
			case SearchTerm.NotAnswered:
				builder.Append ("UNANSWERED");
				break;
			case SearchTerm.NotDeleted:
				builder.Append ("UNDELETED");
				break;
			case SearchTerm.NotDraft:
				builder.Append ("UNDRAFT");
				break;
			case SearchTerm.NotFlagged:
				builder.Append ("UNFLAGGED");
				break;
			case SearchTerm.NotKeyword:
				text = (TextSearchQuery) query;
				builder.Append ("UNKEYWORD %S");
				args.Add (text.Text);
				break;
			case SearchTerm.NotRecent:
				builder.Append ("OLD");
				break;
			case SearchTerm.NotSeen:
				builder.Append ("UNSEEN");
				break;
			case SearchTerm.Older:
				if ((Engine.Capabilities & ImapCapabilities.Within) == 0)
					throw new NotSupportedException ("The OLDER search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("OLDER {0}", numeric.Value);
				break;
			case SearchTerm.Or:
				builder.Append ("OR");
				binary = (BinarySearchQuery) query;
				BuildQuery (builder, binary.Left, args, true, ref ascii);
				BuildQuery (builder, binary.Right, args, true, ref ascii);
				break;
			case SearchTerm.Recent:
				builder.Append ("RECENT");
				break;
			case SearchTerm.Seen:
				builder.Append ("SEEN");
				break;
			case SearchTerm.SentAfter:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTSINCE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.SentBefore:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTBEFORE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.SentOn:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTON {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.SmallerThan:
				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("SMALLER {0}", numeric.Value);
				break;
			case SearchTerm.SubjectContains:
				text = (TextSearchQuery) query;
				builder.Append ("SUBJECT %S");
				args.Add (text.Text);
				break;
			case SearchTerm.ToContains:
				text = (TextSearchQuery) query;
				builder.Append ("TO %S");
				args.Add (text.Text);
				break;
			case SearchTerm.Uid:
				break;
			case SearchTerm.Younger:
				if ((Engine.Capabilities & ImapCapabilities.Within) == 0)
					throw new NotSupportedException ("The YOUNGER search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("YOUNGER {0}", numeric.Value);
				break;
			case SearchTerm.GMailMessageId:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-MSGID search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("X-GM-MSGID {0}", numeric.Value);
				break;
			case SearchTerm.GMailThreadId:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-THRID search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("X-GM-THRID {0}", numeric.Value);
				break;
			case SearchTerm.GMailLabels:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-LABELS search term is not supported by the IMAP server.");

				text = (TextSearchQuery) query;

				if ((isFlag = text.Text[0] == '\\')) {
					for (int i = 1; i < text.Text.Length; i++) {
						if (text.Text[i] < 'A' || (text.Text[i] > 'Z' && text.Text[i] < 'a') || text.Text[i] > 'z') {
							isFlag = false;
							break;
						}
					}
				}

				if (isFlag) {
					builder.AppendFormat ("X-GM-LABELS {0}", text.Text);
				} else {
					builder.Append ("X-GM-LABELS %S");
					args.Add (text.Text);
				}
				break;
			case SearchTerm.GMailRaw:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-RAW search term is not supported by the IMAP server.");

				text = (TextSearchQuery) query;
				builder.Append ("X-GM-RAW %S");
				args.Add (text.Text);
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}

			if (text != null && !IsAscii (text.Text))
				ascii = false;
		}

		string BuildQueryExpression (SearchQuery query, List<string> args, out string charset)
		{
			var builder = new StringBuilder ();
			bool ascii = true;

			BuildQuery (builder, query, args, false, ref ascii);

			charset = ascii ? "US-ASCII" : "UTF-8";

			return builder.ToString ();
		}

		static string BuildSortOrder (IList<OrderBy> orderBy)
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			for (int i = 0; i < orderBy.Count; i++) {
				if (builder.Length > 1)
					builder.Append (' ');

				if (orderBy[i].Order == SortOrder.Descending)
					builder.Append ("REVERSE ");

				switch (orderBy [i].Type) {
				case OrderByType.Arrival: builder.Append ("ARRIVAL"); break;
				case OrderByType.Cc:      builder.Append ("CC"); break;
				case OrderByType.Date:    builder.Append ("DATE"); break;
				case OrderByType.From:    builder.Append ("FROM"); break;
				case OrderByType.Size:    builder.Append ("SIZE"); break;
				case OrderByType.Subject: builder.Append ("SUBJECT"); break;
				case OrderByType.To:      builder.Append ("TO"); break;
				default: throw new ArgumentOutOfRangeException ();
				}
			}
			builder.Append (')');

			return builder.ToString ();
		}

		static void SearchMatches (ImapEngine engine, ImapCommand ic, int index)
		{
			var uids = new List<UniqueId> ();
			ImapToken token;
			uint uid;

			do {
				token = engine.PeekToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
					throw ImapEngine.UnexpectedToken (token, false);

				uids.Add (new UniqueId (uid));
			} while (true);

			ic.UserData = uids;
		}

		static void ESearchMatches (ImapEngine engine, ImapCommand ic, int index)
		{
			var token = engine.ReadToken (ic.CancellationToken);
			IList<UniqueId> uids = null;
			uint min, max, count;
			bool uid = false;
			string atom;
			string tag;

			if (token.Type == ImapTokenType.OpenParen) {
				// optional search correlator
				do {
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (token, false);

					atom = (string) token.Value;

					if (atom == "TAG") {
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.Atom && token.Type != ImapTokenType.QString)
							throw ImapEngine.UnexpectedToken (token, false);

						tag = (string) token.Value;

						if (tag != ic.Tag)
							throw new ImapProtocolException ("Unexpected TAG value in untagged ESEARCH response: " + tag);
					}
				} while (true);

				token = engine.ReadToken (ic.CancellationToken);
			}

			if (token.Type == ImapTokenType.Atom && ((string) token.Value) == "UID") {
				token = engine.ReadToken (ic.CancellationToken);
				uid = true;
			}

			do {
				if (token.Type == ImapTokenType.Eoln) {
					// unget the eoln token
					engine.Stream.UngetToken (token);
					break;
				}

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				atom = (string) token.Value;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				switch (atom) {
				case "COUNT":
					if (!uint.TryParse ((string) token.Value, out count))
						throw ImapEngine.UnexpectedToken (token, false);
					break;
				case "MIN":
					if (!uint.TryParse ((string) token.Value, out min))
						throw ImapEngine.UnexpectedToken (token, false);
					break;
				case "MAX":
					if (!uint.TryParse ((string) token.Value, out max))
						throw ImapEngine.UnexpectedToken (token, false);
					break;
				case "ALL":
					if (!ImapUtils.TryParseUidSet ((string) token.Value, out uids))
						throw ImapEngine.UnexpectedToken (token, false);
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}

				token = engine.ReadToken (ic.CancellationToken);
			} while (true);

			if (!uid && uids != null) {
				var indexes = new int[uids.Count];
				for (int i = 0; i < uids.Count; i++)
					indexes[i] = (int) uids[i].Id - 1;

				ic.UserData = indexes;
			} else {
				ic.UserData = uids ?? new UniqueId[0];
			}
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<UniqueId> Search (SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				command += "RETURN () ";

			if (args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			else
				ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			var results = (IList<UniqueId>) ic.UserData;

			if (results == null)
				return new UniqueId[0];

			return results;
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="orderBy"/> is empty.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the SORT extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<UniqueId> Search (SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", "orderBy");

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT " + order + " ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				command += "RETURN () ";

			command += charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			else
				ic.RegisterUntaggedHandler ("SORT", SearchMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			var results = (IList<UniqueId>) ic.UserData;

			if (results == null)
				return new UniqueId[0];

			return results;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<UniqueId> Search (IList<UniqueId> uids, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true, false);

			if (uids.Count == 0)
				return new UniqueId[0];

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				command += "RETURN () ";

			if (args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			else
				ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			var results = (IList<UniqueId>) ic.UserData;

			if (results == null)
				return new UniqueId[0];

			return results;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the SORT extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<UniqueId> Search (IList<UniqueId> uids, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", "orderBy");

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			if (uids.Count == 0)
				return new UniqueId[0];

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT " + order + " ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				command += "RETURN () ";

			command += charset + " UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			else
				ic.RegisterUntaggedHandler ("SORT", SearchMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			var results = (IList<UniqueId>) ic.UserData;

			if (results == null)
				return new UniqueId[0];

			return results;
		}

		static void ThreadMatches (ImapEngine engine, ImapCommand ic, int index)
		{
			ic.UserData = ImapUtils.ParseThreads (engine, ic.CancellationToken);
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the THREAD extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<MessageThread> Thread (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			var method = algorithm.ToString ().ToUpperInvariant ();
			var args = new List<string> ();
			string charset;

			if ((Engine.Capabilities & ImapCapabilities.Thread) == 0)
				throw new NotSupportedException ("The IMAP server does not support the THREAD extension.");

			if (!Engine.ThreadingAlgorithms.Contains (algorithm))
				throw new ArgumentOutOfRangeException ("algorithm", "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ThreadMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("THREAD", ic);

			var threads = (IList<MessageThread>) ic.UserData;

			if (threads == null)
				return new MessageThread[0];

			return threads;
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the THREAD extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
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
		public override IList<MessageThread> Thread (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			var method = algorithm.ToString ().ToUpperInvariant ();
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if ((Engine.Capabilities & ImapCapabilities.Thread) == 0)
				throw new NotSupportedException ("The IMAP server does not support the THREAD extension.");

			if (!Engine.ThreadingAlgorithms.Contains (algorithm))
				throw new ArgumentOutOfRangeException ("algorithm", "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + charset + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ThreadMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw ImapCommandException.Create ("THREAD", ic);

			var threads = (IList<MessageThread>) ic.UserData;

			if (threads == null)
				return new MessageThread[0];

			return threads;
		}

		#region Untagged response handlers called by ImapEngine

		internal void OnExists (int count)
		{
			if (Count == count)
				return;

			int arrived = count - Count;

			Count = count;

			if (arrived > 0)
				OnMessagesArrived (new MessagesArrivedEventArgs (arrived));

			OnCountChanged ();
		}

		internal void OnExpunge (int index)
		{
			Count--;
			
			OnMessageExpunged (new MessageEventArgs (index));
			OnCountChanged ();
		}

		internal void OnFetch (ImapEngine engine, int index, CancellationToken cancellationToken)
		{
			var labels = new MessageLabelsChangedEventArgs (index);
			var flags = new MessageFlagsChangedEventArgs (index);
			var token = engine.ReadToken (cancellationToken);
			bool labelsChanged = false;
			bool flagsChanged = false;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				var atom = (string) token.Value;
				ulong modseq;
				uint uid;

				switch (atom) {
				case "MODSEQ":
					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq) || modseq == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (token, false);

					labels.ModSeq = modseq;
					flags.ModSeq = modseq;
					break;
				case "UID":
					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					labels.UniqueId = new UniqueId (uid);
					flags.UniqueId = new UniqueId (uid);
					break;
				case "FLAGS":
					flags.Flags = ImapUtils.ParseFlagsList (engine, flags.UserFlags, cancellationToken);
					flagsChanged = true;
					break;
				case "X-GM-LABELS":
					labels.Labels = ImapUtils.ParseLabelsList (engine, cancellationToken);
					labelsChanged = true;
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			if (flagsChanged)
				OnMessageFlagsChanged (flags);

			if (labelsChanged)
				OnMessageLabelsChanged (labels);
		}

		internal void OnRecent (int count)
		{
			if (Recent == count)
				return;

			Recent = count;

			OnRecentChanged ();
		}

		internal void OnVanished (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			IList<UniqueId> vanished;
			bool earlier = false;

			if (token.Type == ImapTokenType.OpenParen) {
				do {
					token = engine.ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (token, false);

					var atom = (string) token.Value;

					if (atom == "EARLIER")
						earlier = true;
				} while (true);

				token = engine.ReadToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.Atom || !ImapUtils.TryParseUidSet ((string) token.Value, out vanished))
				throw ImapEngine.UnexpectedToken (token, false);

			OnMessagesVanished (new MessagesVanishedEventArgs (vanished, earlier));
		}

		internal void UpdateAttributes (FolderAttributes attrs)
		{
			Attributes = attrs;
		}

		internal void UpdateAcceptedFlags (MessageFlags flags)
		{
			AcceptedFlags = flags;
		}

		internal void UpdatePermanentFlags (MessageFlags flags)
		{
			PermanentFlags = flags;
		}

		internal void UpdateIsNamespace (bool value)
		{
			IsNamespace = value;
		}

		internal void UpdateUnread (int count)
		{
			Unread = count;
		}

		internal void UpdateUidNext (UniqueId uid)
		{
			UidNext = uid;
		}

		internal void UpdateUidValidity (UniqueId uid)
		{
			if (UidValidity.HasValue && UidValidity.Value.Id == uid.Id)
				return;

			UidValidity = uid;

			OnUidValidityChanged ();
		}

		#endregion

		#endregion

		#region IEnumerable<MimeMessage> implementation

		/// <summary>
		/// Gets an enumerator for the messages in the folder.
		/// </summary>
		/// <remarks>
		/// Gets an enumerator for the messages in the folder.
		/// </remarks>
		/// <returns>The enumerator.</returns>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is not connected.</para>
		/// <para>-or-</para>
		/// <para>The <see cref="ImapClient"/> is not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder is not currently open.</para>
		/// </exception>
		public override IEnumerator<MimeMessage> GetEnumerator ()
		{
			CheckState (true, false);

			for (int i = 0; i < Count; i++)
				yield return GetMessage (i, CancellationToken.None);

			yield break;
		}

		#endregion
	}
}
