//
// ImapFolder.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
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
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;
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
		/// <remarks>
		/// <para>Creates a new <see cref="ImapFolder"/>.</para>
		/// <para>If you subclass <see cref="ImapFolder"/>, you will also need to subclass
		/// <see cref="ImapClient"/> and override the
		/// <see cref="ImapClient.CreateImapFolder(ImapFolderConstructorArgs)"/>
		/// method in order to return a new instance of your ImapFolder subclass.</para>
		/// </remarks>
		/// <param name="args">The constructor arguments.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="args"/> is <c>null</c>.
		/// </exception>
		public ImapFolder (ImapFolderConstructorArgs args)
		{
			if (args == null)
				throw new ArgumentNullException (nameof (args));

			DirectorySeparator = args.DirectorySeparator;
			EncodedName = args.EncodedName;
			Attributes = args.Attributes;
			FullName = args.FullName;
			Engine = args.Engine;
			Name = args.Name;

			Engine.Disconnected += (sender, e) => {
				Access = FolderAccess.None;
			};
		}

		/// <summary>
		/// Get the IMAP command engine.
		/// </summary>
		/// <remarks>
		/// Gets the IMAP command engine.
		/// </remarks>
		/// <value>The engine.</value>
		internal ImapEngine Engine {
			get; private set;
		}

		/// <summary>
		/// Get the encoded name of the folder.
		/// </summary>
		/// <remarks>
		/// Gets the encoded name of the folder.
		/// </remarks>
		/// <value>The encoded name.</value>
		internal string EncodedName {
			get; private set;
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Gets an object that can be used to synchronize access to the IMAP server.</para>
		/// <para>When using the non-Async methods from multiple threads, it is important to lock the
		/// <see cref="SyncRoot"/> object for thread safety when using the synchronous methods.</para>
		/// </remarks>
		/// <value>The lock object.</value>
		public override object SyncRoot {
			get { return Engine; }
		}

		void CheckState (bool open, bool rw)
		{
			if (Engine.IsDisposed)
				throw new ObjectDisposedException (nameof (ImapClient));

			if (!Engine.IsConnected)
				throw new ServiceNotConnectedException ("The ImapClient is not connected.");

			if (Engine.State < ImapEngineState.Authenticated)
				throw new ServiceNotAuthenticatedException ("The ImapClient is not authenticated.");

			if (open) {
				var access = rw ? FolderAccess.ReadWrite : FolderAccess.ReadOnly;

				if (!IsOpen || Access < access)
					throw new FolderNotOpenException (FullName, access);
			}
		}

		/// <summary>
		/// Notifies the folder that a parent folder has been renamed.
		/// </summary>
		/// <remarks>
		/// Updates the <see cref="MailFolder.FullName"/> property.
		/// </remarks>
		protected override void OnParentFolderRenamed ()
		{
			var oldEncodedName = EncodedName;

			FullName = ParentFolder.FullName + DirectorySeparator + Name;
			EncodedName = Engine.EncodeMailboxName (FullName);
			Engine.FolderCache.Remove (oldEncodedName);
			Engine.FolderCache[EncodedName] = this;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
				OnClosed ();
			}
		}

		void ProcessResponseCodes (ImapCommand ic, IMailFolder folder)
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

			if (tryCreate && folder != null)
				throw new FolderNotFoundException (folder.FullName);
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The QRESYNC feature has not been enabled.
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
		public override FolderAccess Open (FolderAccess access, uint uidValidity, ulong highestModSeq, IList<UniqueId> uids, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (access != FolderAccess.ReadOnly && access != FolderAccess.ReadWrite)
				throw new ArgumentOutOfRangeException (nameof (access));

			CheckState (false, false);

			if (IsOpen && Access == access)
				return access;

			if ((Engine.Capabilities & ImapCapabilities.QuickResync) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QRESYNC extension.");

			if (!Engine.QResyncEnabled)
				throw new InvalidOperationException ("The QRESYNC extension has not been enabled.");

			var qresync = string.Format ("(QRESYNC ({0} {1}", uidValidity, highestModSeq);

			if (uids.Count > 0)
				qresync += " " + set;

			qresync += "))";

			var command = string.Format ("{0} %F {1}\r\n", SelectOrExamine (access), qresync);
			var ic = new ImapCommand (Engine, cancellationToken, this, command, this);
			ic.RegisterUntaggedHandler ("FETCH", QResyncFetch);

			if (access == FolderAccess.ReadWrite) {
				// Note: if the server does not respond with a PERMANENTFLAGS response,
				// then we need to assume all flags are permanent.
				PermanentFlags = SettableFlags | MessageFlags.UserDefined;
			} else {
				PermanentFlags = MessageFlags.None;
			}

			try {
				Engine.QueueCommand (ic);
				Engine.Wait (ic);

				ProcessResponseCodes (ic, this);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic);
			} catch {
				PermanentFlags = MessageFlags.None;
				throw;
			}

			if (Engine.Selected != null && Engine.Selected != this) {
				var folder = Engine.Selected;

				folder.PermanentFlags = MessageFlags.None;
				folder.AcceptedFlags = MessageFlags.None;
				folder.Access = FolderAccess.None;

				folder.OnClosed ();
			}

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			OnOpened ();

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
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
				throw new ArgumentOutOfRangeException (nameof (access));

			CheckState (false, false);

			if (IsOpen && Access == access)
				return access;

			var condstore = (Engine.Capabilities & ImapCapabilities.CondStore) != 0 ? " (CONDSTORE)" : string.Empty;
			var command = string.Format ("{0} %F{1}\r\n", SelectOrExamine (access), condstore);
			var ic = new ImapCommand (Engine, cancellationToken, this, command, this);

			if (access == FolderAccess.ReadWrite) {
				// Note: if the server does not respond with a PERMANENTFLAGS response,
				// then we need to assume all flags are permanent.
				PermanentFlags = SettableFlags | MessageFlags.UserDefined;
			} else {
				PermanentFlags = MessageFlags.None;
			}

			try {
				Engine.QueueCommand (ic);
				Engine.Wait (ic);

				ProcessResponseCodes (ic, this);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic);
			} catch {
				PermanentFlags = MessageFlags.None;
				throw;
			}

			if (Engine.Selected != null && Engine.Selected != this) {
				var folder = Engine.Selected;

				folder.PermanentFlags = MessageFlags.None;
				folder.AcceptedFlags = MessageFlags.None;
				folder.Access = FolderAccess.None;

				folder.OnClosed ();
			}

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			OnOpened ();

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			CheckState (true, expunge);

			ImapCommand ic;

			if (expunge) {
				ic = Engine.QueueCommand (cancellationToken, this, "CLOSE\r\n");
			} else if ((Engine.Capabilities & ImapCapabilities.Unselect) != 0) {
				ic = Engine.QueueCommand (cancellationToken, this, "UNSELECT\r\n");
			} else {
				ic = null;
			}

			if (ic != null) {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create (expunge ? "CLOSE" : "UNSELECT", ic);
			}

			Engine.State = ImapEngineState.Authenticated;
			Access = FolderAccess.None;
			Engine.Selected = null;
			OnClosed ();
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailFolder.DirectorySeparator"/> is nil, and thus child folders cannot be created.
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
				throw new ArgumentNullException (nameof (name));

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name is not a legal folder name.", nameof (name));

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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("CREATE", ic);

			ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if ((folder = list.FirstOrDefault ()) != null)
				folder.ParentFolder = this;

			return folder;
		}

		/// <summary>
		/// Creates a new subfolder with the given name.
		/// </summary>
		/// <remarks>
		/// Creates a new subfolder with the given name.
		/// </remarks>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="specialUses">A list of special uses for the folder being created.</param>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="MailFolder.DirectorySeparator"/> is nil, and thus child folders cannot be created.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the CREATE-SPECIAL-USE extension.
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
		public override IMailFolder Create (string name, IEnumerable<SpecialFolder> specialUses, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name is not a legal folder name.", nameof (name));

			CheckState (false, false);

			if (!string.IsNullOrEmpty (FullName) && DirectorySeparator == '\0')
				throw new InvalidOperationException ("Cannot create child folders.");

			if ((Engine.Capabilities & ImapCapabilities.CreateSpecialUse) == 0)
				throw new NotSupportedException ("The IMAP server does not support the CREATE-SPECIAL-USE extension.");

			var uses = new StringBuilder ();

			foreach (var use in specialUses) {
				if (uses.Length > 0)
					uses.Append (' ');

				switch (use) {
				case SpecialFolder.All:     uses.Append ("\\All"); break;
				case SpecialFolder.Archive: uses.Append ("\\Archive"); break;
				case SpecialFolder.Drafts:  uses.Append ("\\Drafts"); break;
				case SpecialFolder.Flagged: uses.Append ("\\Flagged"); break;
				case SpecialFolder.Junk:    uses.Append ("\\Junk"); break;
				case SpecialFolder.Sent:    uses.Append ("\\Sent"); break;
				case SpecialFolder.Trash:   uses.Append ("\\Trash"); break;
				default: if (uses.Length > 0) uses.Length--; break;
				}
			}

			var fullName = !string.IsNullOrEmpty (FullName) ? FullName + DirectorySeparator + name : name;
			var command = string.Format ("CREATE %s (USE ({0}))\r\n", uses);
			var encodedName = Engine.EncodeMailboxName (fullName);
			var list = new List<ImapFolder> ();
			var createName = encodedName;
			ImapFolder folder;

			var ic = Engine.QueueCommand (cancellationToken, null, command, createName);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				var useAttr = ic.RespCodes.FirstOrDefault (rc => rc.Type == ImapResponseCodeType.UseAttr);

				if (useAttr != null)
					throw new ImapCommandException (ic.Response, useAttr.Message);

				throw ImapCommandException.Create ("CREATE", ic);
			}

			ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if ((folder = list.FirstOrDefault ()) != null)
				folder.ParentFolder = this;

			Engine.AssignSpecialFolders (new [] { folder });

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The folder cannot be renamed (it is either a namespace or the Inbox).
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
				throw new ArgumentNullException (nameof (parent));

			if (!(parent is ImapFolder) || ((ImapFolder) parent).Engine != Engine)
				throw new ArgumentException ("The parent folder does not belong to this ImapClient.", nameof (parent));

			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name is not a legal folder name.", nameof (name));

			if (IsNamespace || (Attributes & FolderAttributes.Inbox) != 0)
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

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("RENAME", ic);

			Engine.FolderCache.Remove (EncodedName);
			Engine.FolderCache[encodedName] = this;

			ParentFolder = parent;

			FullName = Engine.DecodeMailboxName (encodedName);
			EncodedName = encodedName;
			Name = name;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
				OnClosed ();
			}

			OnRenamed (oldFullName, FullName);
		}

		/// <summary>
		/// Deletes the folder on the IMAP server.
		/// </summary>
		/// <remarks>
		/// <para>Deletes the folder on the IMAP server.</para>
		/// <note type="note">This method will not delete any child folders.</note>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The folder cannot be deleted (it is either a namespace or the Inbox).
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
			if (IsNamespace || (Attributes & FolderAttributes.Inbox) != 0)
				throw new InvalidOperationException ("Cannot delete this folder.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "DELETE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("DELETE", ic);

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
				OnClosed ();
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SUBSCRIBE", ic);

			Attributes |= FolderAttributes.Subscribed;

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("UNSUBSCRIBE", ic);

			Attributes &= ~FolderAttributes.Subscribed;

			OnUnsubscribed ();
		}

		/// <summary>
		/// Gets the subfolders.
		/// </summary>
		/// <remarks>
		/// Gets the subfolders.
		/// </remarks>
		/// <returns>The subfolders.</returns>
		/// <param name="items">The status items to pre-populate.</param>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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
		public override IEnumerable<IMailFolder> GetSubfolders (StatusItems items, bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			var pattern = EncodedName.Length > 0 ? EncodedName + DirectorySeparator : string.Empty;
			var children = new List<IMailFolder> ();
			var status = items != StatusItems.None;
			var list = new List<ImapFolder> ();
			var command = new StringBuilder ();
			var lsub = subscribedOnly;

			if (subscribedOnly) {
				if ((Engine.Capabilities & ImapCapabilities.ListExtended) != 0) {
					command.Append ("LIST (SUBSCRIBED)");
					lsub = false;
				} else {
					command.Append ("LSUB");
				}
			} else {
				command.Append ("LIST");
			}

			command.Append (" \"\" %S");

			if (!lsub) {
				if (items != StatusItems.None && (Engine.Capabilities & ImapCapabilities.ListStatus) != 0) {
					command.Append (" RETURN (");

					if ((Engine.Capabilities & ImapCapabilities.ListExtended) != 0) {
						if (!subscribedOnly)
							command.Append ("SUBSCRIBED ");
						command.Append ("CHILDREN ");
					}

					command.AppendFormat ("STATUS ({0})", Engine.GetStatusQuery (items));
					command.Append (')');
					status = false;
				} else if ((Engine.Capabilities & ImapCapabilities.ListExtended) != 0) {
					command.Append (" RETURN (");
					if (!subscribedOnly)
						command.Append ("SUBSCRIBED ");
					command.Append ("CHILDREN");
					command.Append (')');
				}
			}

			command.Append ("\r\n");

			var ic = new ImapCommand (Engine, cancellationToken, null, command.ToString (), pattern + "%");
			ic.RegisterUntaggedHandler (lsub ? "LSUB" : "LIST", ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			// Note: Some broken IMAP servers (*cough* SmarterMail 13.0 *cough*) return folders
			// that are not children of the folder we requested, so we need to filter those
			// folders out of the list that we'll be returning to our caller.
			//
			// See https://github.com/jstedfast/MailKit/issues/149 for more details.
			var prefix = FullName.Length > 0 ? FullName + DirectorySeparator : string.Empty;
			prefix = ImapUtils.CanonicalizeMailboxName (prefix, DirectorySeparator);
			foreach (var folder in list) {
				var canonicalFullName = ImapUtils.CanonicalizeMailboxName (folder.FullName, folder.DirectorySeparator);
				var canonicalName = ImapUtils.IsInbox (folder.FullName) ? "INBOX" : folder.Name;

				if (canonicalFullName != prefix + canonicalName)
					continue;

				if (lsub) {
					// the LSUB command does not send \Subscribed flags so we need to add them ourselves
					folder.Attributes |= FolderAttributes.Subscribed;
				}

				folder.ParentFolder = this;
				children.Add (folder);
			}

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create (lsub ? "LSUB" : "LIST", ic);

			if (status) {
				for (int i = 0; i < children.Count; i++)
					children[i].Status (items, cancellationToken);
			}

			return children;
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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
				throw new ArgumentNullException (nameof (name));

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name of the subfolder is invalid.", nameof (name));

			CheckState (false, false);

			var fullName = FullName.Length > 0 ? FullName + DirectorySeparator + name : name;
			var encodedName = Engine.EncodeMailboxName (fullName);
			List<ImapFolder> list;
			ImapFolder subfolder;

			if (Engine.GetCachedFolder (encodedName, out subfolder))
				return subfolder;

			var ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderList);
			ic.UserData = list = new List<ImapFolder> ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if (list.Count == 0)
				throw new FolderNotFoundException (fullName);

			return list[0];
		}

		/// <summary>
		/// Force the server to sync its in-memory state with its disk state.
		/// </summary>
		/// <remarks>
		/// <para>The <c>CHECK</c> command forces the IMAP server to sync its
		/// in-memory state with its disk state.</para>
		/// <para>For more information about the <c>CHECK</c> command, see
		/// <a href="https://tools.ietf.org/html/rfc3501#section-6.4.1">rfc350101</a>.</para>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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

			if (ic.Response != ImapCommandResponse.Ok)
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
		/// <para>For more information about the <c>STATUS</c> command, see
		/// <a href="https://tools.ietf.org/html/rfc3501#section-6.3.10">rfc3501</a>.</para>
		/// </remarks>
		/// <param name="items">The items to update.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
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

			if (items == StatusItems.None)
				return;

			var command = string.Format ("STATUS %F ({0})\r\n", Engine.GetStatusQuery (items));
			var ic = Engine.QueueCommand (cancellationToken, null, command, this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("STATUS", ic);
		}

		static void UntaggedAcl (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ACL", "{0}");
			var acl = (AccessControlList) ic.UserData;
			string name, rights;
			ImapToken token;

			// read the mailbox name
			ReadStringToken (engine, format, ic.CancellationToken);

			do {
				name = ReadStringToken (engine, format, ic.CancellationToken);
				rights = ReadStringToken (engine, format, ic.CancellationToken);

				acl.Add (new AccessControl (name, rights));

				token = engine.PeekToken (ic.CancellationToken);
			} while (token.Type != ImapTokenType.Eoln);
		}

		/// <summary>
		/// Get the complete access control list for the folder.
		/// </summary>
		/// <remarks>
		/// Gets the complete access control list for the folder.
		/// </remarks>
		/// <returns>The access control list.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override AccessControlList GetAccessControlList (CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = new ImapCommand (Engine, cancellationToken, null, "GETACL %F\r\n", this);
			ic.RegisterUntaggedHandler ("ACL", UntaggedAcl);
			ic.UserData = new AccessControlList ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("GETACL", ic);

			return (AccessControlList) ic.UserData;
		}

		static void UntaggedListRights (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "LISTRIGHTS", "{0}");
			var access = (AccessRights) ic.UserData;
			ImapToken token;

			// read the mailbox name
			ReadStringToken (engine, format, ic.CancellationToken);

			// read the identity name
			ReadStringToken (engine, format, ic.CancellationToken);

			do {
				var rights = ReadStringToken (engine, format, ic.CancellationToken);

				access.AddRange (rights);

				token = engine.PeekToken (ic.CancellationToken);
			} while (token.Type != ImapTokenType.Eoln);
		}

		/// <summary>
		/// Get the access rights for a particular identifier.
		/// </summary>
		/// <remarks>
		/// Gets the access rights for a particular identifier.
		/// </remarks>
		/// <returns>The access rights.</returns>
		/// <param name="name">The identifier name.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
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
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override AccessRights GetAccessRights (string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = new ImapCommand (Engine, cancellationToken, null, "LISTRIGHTS %F %S\r\n", this, name);
			ic.RegisterUntaggedHandler ("LISTRIGHTS", UntaggedListRights);
			ic.UserData = new AccessRights ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LISTRIGHTS", ic);

			return (AccessRights) ic.UserData;
		}

		static void UntaggedMyRights (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "MYRIGHTS", "{0}");
			var access = (AccessRights) ic.UserData;

			// read the mailbox name
			ReadStringToken (engine, format, ic.CancellationToken);

			// read the access rights
			access.AddRange (ReadStringToken (engine, format, ic.CancellationToken));
		}

		/// <summary>
		/// Get the access rights for the current authenticated user.
		/// </summary>
		/// <remarks>
		/// Gets the access rights for the current authenticated user.
		/// </remarks>
		/// <returns>The access rights.</returns>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override AccessRights GetMyAccessRights (CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = new ImapCommand (Engine, cancellationToken, null, "MYRIGHTS %F\r\n", this);
			ic.RegisterUntaggedHandler ("MYRIGHTS", UntaggedMyRights);
			ic.UserData = new AccessRights ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("MYRIGHTS", ic);

			return (AccessRights) ic.UserData;
		}

		void ModifyAccessRights (string name, AccessRights rights, string action, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "SETACL %F %S %S\r\n", this, name, action + rights);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SETACL", ic);
		}

		/// <summary>
		/// Add access rights for the specified identity.
		/// </summary>
		/// <remarks>
		/// Adds the given access rights for the specified identity.
		/// </remarks>
		/// <param name="name">The identity name.</param>
		/// <param name="rights">The access rights.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rights"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// No rights were specified.
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
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override void AddAccessRights (string name, AccessRights rights, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			if (rights.Count == 0)
				throw new ArgumentException ("No rights were specified.", nameof (rights));

			ModifyAccessRights (name, rights, "+", cancellationToken);
		}

		/// <summary>
		/// Remove access rights for the specified identity.
		/// </summary>
		/// <remarks>
		/// Removes the given access rights for the specified identity.
		/// </remarks>
		/// <param name="name">The identity name.</param>
		/// <param name="rights">The access rights.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rights"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// No rights were specified.
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
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override void RemoveAccessRights (string name, AccessRights rights, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			if (rights.Count == 0)
				throw new ArgumentException ("No rights were specified.", nameof (rights));

			ModifyAccessRights (name, rights, "-", cancellationToken);
		}

		/// <summary>
		/// Set the access rights for the specified identity.
		/// </summary>
		/// <remarks>
		/// Sets the access rights for the specified identity.
		/// </remarks>
		/// <param name="name">The identity name.</param>
		/// <param name="rights">The access rights.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="rights"/> is <c>null</c>.</para>
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
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override void SetAccessRights (string name, AccessRights rights, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			ModifyAccessRights (name, rights, string.Empty, cancellationToken);
		}

		/// <summary>
		/// Remove all access rights for the given identity.
		/// </summary>
		/// <remarks>
		/// Removes all access rights for the given identity.
		/// </remarks>
		/// <param name="name">The identity name.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
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
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the ACL extension.
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
		/// The command failed.
		/// </exception>
		public override void RemoveAccess (string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "DELETEACL %F %S\r\n", this, name);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("DELETEACL", ic);
		}

		static string ReadStringToken (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.Literal: return engine.ReadLiteral (cancellationToken);
			case ImapTokenType.QString: return (string) token.Value;
			case ImapTokenType.Atom:    return (string) token.Value;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		static void UntaggedQuotaRoot (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "QUOTAROOT", "{0}");

			// The first token should be the mailbox name
			ReadStringToken (engine, format, ic.CancellationToken);

			// ...followed by 0 or more quota roots
			var token = engine.PeekToken (ic.CancellationToken);

			while (token.Type != ImapTokenType.Eoln) {
				ReadStringToken (engine, format, ic.CancellationToken);

				token = engine.PeekToken (ic.CancellationToken);
			}
		}

		static void UntaggedQuota (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "QUOTA", "{0}");
			var encodedName = ReadStringToken (engine, format, ic.CancellationToken);
			ImapFolder quotaRoot;
			FolderQuota quota;

			if (!engine.GetCachedFolder (encodedName, out quotaRoot)) {
				// Note: this shouldn't happen because the quota root should
				// be one of the parent folders which will all have been added
				// to the folder cache by this point.
			}

			ic.UserData = quota = new FolderQuota (quotaRoot);

			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			while (token.Type != ImapTokenType.CloseParen) {
				uint used, limit;
				string resource;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (format, token);

				resource = (string) token.Value;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out used))
					throw ImapEngine.UnexpectedToken (format, token);

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out limit))
					throw ImapEngine.UnexpectedToken (format, token);

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

				token = engine.PeekToken (ic.CancellationToken);
			}

			// read the closing paren
			engine.ReadToken (ic.CancellationToken);
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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

			if (ic.Response != ImapCommandResponse.Ok)
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SETQUOTA", ic);

			if (ic.UserData == null)
				return new FolderQuota (null);

			return (FolderQuota) ic.UserData;
		}

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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the METADATA extension.
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
		public override string GetMetadata (MetadataTag tag, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Metadata) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA extension.");

			var ic = new ImapCommand (Engine, cancellationToken, null, "GETMETADATA %F %S\r\n", this, tag.Id);
			ic.RegisterUntaggedHandler ("METADATA", ImapUtils.ParseMetadata);
			var metadata = new MetadataCollection ();
			ic.UserData = metadata;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("GETMETADATA", ic);

			for (int i = 0; i < metadata.Count; i++) {
				if (metadata[i].Tag.Id == tag.Id)
					return metadata[i].Value;
			}

			return null;
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the METADATA extension.
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
		public override MetadataCollection GetMetadata (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (tags == null)
				throw new ArgumentNullException (nameof (tags));

			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Metadata) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA extension.");

			var command = new StringBuilder ("GETMETADATA %F");
			var args = new List<object> ();
			bool hasOptions = false;

			if (options.MaxSize.HasValue || options.Depth != 0) {
				command.Append (" (");
				if (options.MaxSize.HasValue)
					command.AppendFormat ("MAXSIZE {0} ", options.MaxSize.Value);
				if (options.Depth > 0)
					command.AppendFormat ("DEPTH {0} ", options.Depth == int.MaxValue ? "infinity" : "1");
				command[command.Length - 1] = ')';
				command.Append (' ');
				hasOptions = true;
			}

			args.Add (this);

			int startIndex = command.Length;
			foreach (var tag in tags) {
				command.Append (" %S");
				args.Add (tag.Id);
			}

			if (hasOptions) {
				command[startIndex] = '(';
				command.Append (')');
			}

			command.Append ("\r\n");

			if (args.Count == 1)
				return new MetadataCollection ();

			var ic = new ImapCommand (Engine, cancellationToken, null, command.ToString (), args.ToArray ());
			ic.RegisterUntaggedHandler ("METADATA", ImapUtils.ParseMetadata);
			ic.UserData = new MetadataCollection ();
			options.LongEntries = 0;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("GETMETADATA", ic);

			if (ic.RespCodes.Count > 0 && ic.RespCodes[ic.RespCodes.Count - 1].Type == ImapResponseCodeType.Metadata) {
				var metadata = (MetadataResponseCode) ic.RespCodes[ic.RespCodes.Count - 1];

				if (metadata.SubType == MetadataResponseCodeSubType.LongEntries)
					options.LongEntries = metadata.Value;
			}

			return (MetadataCollection) ic.UserData;
		}

		/// <summary>
		/// Sets the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
		/// <returns>The metadata.</returns>
		/// <param name="metadata">The metadata.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="metadata"/> is <c>null</c>.
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
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the METADATA extension.
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
		public override void SetMetadata (MetadataCollection metadata, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (metadata == null)
				throw new ArgumentNullException (nameof (metadata));

			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Metadata) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA extension.");

			if (metadata.Count == 0)
				return;

			var command = new StringBuilder ("SETMETADATA %F (");
			var args = new List<object> ();

			args.Add (this);

			for (int i = 0; i < metadata.Count; i++) {
				if (i > 0)
					command.Append (' ');

				if (metadata[i].Value != null) {
					command.Append ("%S %S");
					args.Add (metadata[i].Tag.Id);
					args.Add (metadata[i].Value);
				} else {
					command.Append ("%S NIL");
					args.Add (metadata[i].Tag.Id);
				}
			}
			command.Append (")\r\n");

			var ic = new ImapCommand (Engine, cancellationToken, null, command.ToString (), args.ToArray ());

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SETMETADATA", ic);
		}

		/// <summary>
		/// Expunges the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// <para>The <c>EXPUNGE</c> command permanently removes all messages in the folder
		/// that have the <see cref="MessageFlags.Deleted"/> flag set.</para>
		/// <para>For more information about the <c>EXPUNGE</c> command, see
		/// <a href="https://tools.ietf.org/html/rfc3501#section-6.4.3">rfc3501</a>.</para>
		/// <note type="note">Normally, a <see cref="MailFolder.MessageExpunged"/> event will be emitted
		/// for each message that is expunged. However, if the IMAP server supports the QRESYNC extension
		/// and it has been enabled via the <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>
		/// method, then the <see cref="MailFolder.MessagesVanished"/> event will be emitted rather than
		/// the <see cref="MailFolder.MessageExpunged"/> event.</note>
		/// </remarks>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("EXPUNGE", ic);
		}

		/// <summary>
		/// Expunge the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Expunges the specified uids, permanently removing them from the folder.</para>
		/// <para>If the IMAP server supports the UIDPLUS extension (check the
		/// <see cref="ImapClient.Capabilities"/> for the <see cref="ImapCapabilities.UidPlus"/>
		/// flag), then this operation is atomic. Otherwise, MailKit implements this operation
		/// by first searching for the full list of message uids in the folder that are marked for
		/// deletion, unmarking the set of message uids that are not within the specified list of
		/// uids to be be expunged, expunging the folder (thus expunging the requested uids), and
		/// finally restoring the deleted flag on the collection of message uids that were originally
		/// marked for deletion that were not included in the list of uids provided. For this reason,
		/// it is advisable for clients that wish to maintain state to implement this themselves when
		/// the IMAP server does not support the UIDPLUS extension.</para>
		/// <para>For more information about the <c>UID EXPUNGE</c> command, see
		/// <a href="https://tools.ietf.org/html/rfc4315#section-2.1">rfc4315</a>.</para>
		/// <note type="note">Normally, a <see cref="MailFolder.MessageExpunged"/> event will be emitted
		/// for each message that is expunged. However, if the IMAP server supports the QRESYNC extension
		/// and it has been enabled via the <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>
		/// method, then the <see cref="MailFolder.MessagesVanished"/> event will be emitted rather than
		/// the <see cref="MailFolder.MessageExpunged"/> event.</note>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		public override void Expunge (IList<UniqueId> uids, CancellationToken cancellationToken = default (CancellationToken))
		{
			CheckState (true, true);

			if (uids.Count == 0)
				return;

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				// get the list of messages marked for deletion
				var marked = Search (SearchQuery.Deleted, cancellationToken);
				var unmark = new UniqueIdSet (SortOrder.Ascending);

				// remove all uids except the ones that will be expunged
				for (int i = 0; i < marked.Count; i++) {
					if (!uids.Contains (marked[i]))
						unmark.Add (marked[i]);
				}

				if (unmark.Count > 0) {
					// clear the \Deleted flag on all messages except the ones that are to be expunged
					RemoveFlags (unmark, MessageFlags.Deleted, true, cancellationToken);
				}

				// expunge the folder
				Expunge (cancellationToken);

				if (unmark.Count > 0) {
					// restore the \Deleted flags
					AddFlags (unmark, MessageFlags.Deleted, true, cancellationToken);
				}

				return;
			}

			var set = ImapUtils.FormatUidSet (uids);
			var command = string.Format ("UID EXPUNGE {0}\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("EXPUNGE", ic);
		}

		ImapCommand QueueAppend (FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset? date, CancellationToken cancellationToken, ITransferProgress progress)
		{
			string format = "APPEND %F";

			if ((flags & SettableFlags) != 0)
				format += " " + ImapUtils.FormatFlagsList (flags, 0);

			if (date.HasValue)
				format += " \"" + ImapUtils.FormatInternalDate (date.Value) + "\"";

			format += " %L\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, null, options, format, this, message);
			ic.Progress = progress;

			Engine.QueueCommand (ic);

			return ic;
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
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
		public override UniqueId? Append (FormatOptions options, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (message == null)
				throw new ArgumentNullException (nameof (message));

			CheckState (false, false);

			if (options.International && (Engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8 extension.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			if ((Engine.Capabilities & ImapCapabilities.UTF8Only) == ImapCapabilities.UTF8Only)
				format.International = true;

			if (format.International && !Engine.UTF8Enabled)
				throw new InvalidOperationException ("The UTF8 extension has not been enabled.");

			var ic = QueueAppend (format, message, flags, null, cancellationToken, progress);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("APPEND", ic);

			var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

			if (append != null)
				return append.UidSet[0];

			return null;
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified message to the folder and returns the UniqueId assigned to the message.
		/// </remarks>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
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
		public override UniqueId? Append (FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (options == null)
				throw new ArgumentNullException (nameof (options));

			if (message == null)
				throw new ArgumentNullException (nameof (message));

			CheckState (false, false);

			if (options.International && (Engine.Capabilities & ImapCapabilities.UTF8Accept) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UTF8 extension.");

			var format = options.Clone ();
			format.NewLineFormat = NewLineFormat.Dos;

			if ((Engine.Capabilities & ImapCapabilities.UTF8Only) == ImapCapabilities.UTF8Only)
				format.International = true;

			if (format.International && !Engine.UTF8Enabled)
				throw new InvalidOperationException ("The UTF8 extension has not been enabled.");

			var ic = QueueAppend (format, message, flags, date, cancellationToken, progress);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("APPEND", ic);

			var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

			if (append != null)
				return append.UidSet[0];

			return null;
		}

		ImapCommand QueueMultiAppend (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken, ITransferProgress progress)
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

			var ic = new ImapCommand (Engine, cancellationToken, null, options, format, args.ToArray ());
			ic.Progress = progress;

			Engine.QueueCommand (ic);

			return ic;
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The list of messages to append to the folder.</param>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
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
		public override IList<UniqueId> Append (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
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
				var ic = QueueMultiAppend (format, messages, flags, null, cancellationToken, progress);

				Engine.Wait (ic);

				ProcessResponseCodes (ic, this);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("APPEND", ic);

				var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

				if (append != null)
					return append.UidSet;

				return new UniqueId[0];
			}

			// FIXME: use an aggregate progress reporter
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

			return uids;
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <remarks>
		/// Appends the specified messages to the folder and returns the UniqueIds assigned to the messages.
		/// </remarks>
		/// <returns>The UIDs of the appended messages, if available; otherwise an empty array.</returns>
		/// <param name="options">The formatting options.</param>
		/// <param name="messages">The list of messages to append to the folder.</param>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// Internationalized formatting was requested but has not been enabled.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// The <see cref="ImapFolder"/> does not exist.
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
		public override IList<UniqueId> Append (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
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

			if (dates == null)
				throw new ArgumentNullException (nameof (dates));

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
				var ic = QueueMultiAppend (format, messages, flags, dates, cancellationToken, progress);

				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("APPEND", ic);

				var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

				if (append != null)
					return append.UidSet;

				return new UniqueId[0];
			}

			// FIXME: use an aggregate progress reporter
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

			return uids;
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// Copies the specified messages to the destination folder.
		/// </remarks>
		/// <returns>The UID mapping of the messages in the destination folder, if available; otherwise an empty mapping.</returns>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// <paramref name="destination"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override UniqueIdMap CopyTo (IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException (nameof (destination));

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", nameof (destination));

			CheckState (true, false);

			if (uids.Count == 0)
				return UniqueIdMap.Empty;

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				var indexes = Fetch (uids, MessageSummaryItems.UniqueId, cancellationToken).Select (x => x.Index).ToList ();
				CopyTo (indexes, destination, cancellationToken);
				return UniqueIdMap.Empty;
			}

			var command = string.Format ("UID COPY {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("COPY", ic);

			var copy = ic.RespCodes.OfType<CopyUidResponseCode> ().FirstOrDefault ();

			if (copy != null)
				return new UniqueIdMap (copy.SrcUidSet, copy.DestUidSet);

			return UniqueIdMap.Empty;
		}

		/// <summary>
		/// Move the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// <para>Moves the specified messages to the destination folder.</para>
		/// <para>If the IMAP server supports the MOVE extension (check the <see cref="ImapClient.Capabilities"/>
		/// property for the <see cref="ImapCapabilities.Move"/> flag), then this operation will be atomic.
		/// Otherwise, MailKit implements this by first copying the messages to the destination folder, then
		/// marking them for deletion in the originating folder, and finally expunging them (see
		/// <see cref="Expunge(IList&lt;UniqueId&gt;,CancellationToken)"/> for more information about how a
		/// subset of messages are expunged). Since the server could disconnect at any point between those 3
		/// (or more) commands, it is advisable for clients to implement their own logic for moving messages when
		/// the IMAP server does not support the MOVE command in order to better handle spontanious server
		/// disconnects and other error conditions.</para>
		/// </remarks>
		/// <returns>The UID mapping of the messages in the destination folder, if available; otherwise an empty mapping.</returns>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// <paramref name="destination"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		public override UniqueIdMap MoveTo (IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				var copied = CopyTo (uids, destination, cancellationToken);
				AddFlags (uids, MessageFlags.Deleted, true, cancellationToken);
				Expunge (uids, cancellationToken);
				return copied;
			}

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				var indexes = Fetch (uids, MessageSummaryItems.UniqueId, cancellationToken).Select (x => x.Index).ToList ();
				MoveTo (indexes, destination, cancellationToken);
				Expunge (uids, cancellationToken);
				return UniqueIdMap.Empty;
			}

			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException (nameof (destination));

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", nameof (destination));

			CheckState (true, true);

			if (uids.Count == 0)
				return UniqueIdMap.Empty;

			var command = string.Format ("UID MOVE {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("MOVE", ic);

			var copy = ic.RespCodes.OfType<CopyUidResponseCode> ().FirstOrDefault ();

			if (copy != null)
				return new UniqueIdMap (copy.SrcUidSet, copy.DestUidSet);

			return UniqueIdMap.Empty;
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
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// <paramref name="destination"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentNullException (nameof (destination));

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", nameof (destination));

			CheckState (true, false);

			if (indexes.Count == 0)
				return;

			var command = string.Format ("COPY {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotFoundException">
		/// <paramref name="destination"/> does not exist.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
				throw new ArgumentNullException (nameof (destination));

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", nameof (destination));

			CheckState (true, true);

			if (indexes.Count == 0)
				return;

			var command = string.Format ("MOVE {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
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

		class FetchSummaryContext
		{
			public readonly SortedDictionary<int, IMessageSummary> Results;
			public readonly MessageSummaryItems RequestedItems;

			public FetchSummaryContext (MessageSummaryItems requestedItems)
			{
				Results = new SortedDictionary<int, IMessageSummary> ();
				RequestedItems = requestedItems;
			}
		}

		void FetchSummaryItems (ImapEngine engine, ImapCommand ic, int index)
		{
			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			var ctx = (FetchSummaryContext) ic.UserData;
			IMessageSummary isummary;
			MessageSummary summary;

			if (!ctx.Results.TryGetValue (index, out isummary)) {
				summary = new MessageSummary (index);
				ctx.Results.Add (index, summary);
			} else {
				summary = (MessageSummary) isummary;
			}

			do {
				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

				var atom = (string) token.Value;
				string format;
				ulong value64;
				uint value;
				int idx;

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
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					}

					summary.Fields |= MessageSummaryItems.InternalDate;
					break;
				case "RFC822.SIZE":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					summary.Fields |= MessageSummaryItems.MessageSize;
					summary.Size = value;
					break;
				case "BODYSTRUCTURE":
					format = string.Format (ImapEngine.GenericItemSyntaxErrorFormat, "BODYSTRUCTURE", "{0}");
					summary.Body = ImapUtils.ParseBody (engine, format, string.Empty, ic.CancellationToken);
					summary.Fields |= MessageSummaryItems.BodyStructure;
					break;
				case "BODY":
					token = engine.PeekToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.OpenBracket) {
						// consume the '['
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.OpenBracket)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

						// References and/or other headers were requested...

						do {
							token = engine.ReadToken (ic.CancellationToken);

							if (token.Type == ImapTokenType.CloseBracket)
								break;

							if (token.Type == ImapTokenType.OpenParen) {
								do {
									token = engine.ReadToken (ic.CancellationToken);

									if (token.Type == ImapTokenType.CloseParen)
										break;

									// the header field names will generally be atoms or qstrings but may also be literals
									switch (token.Type) {
									case ImapTokenType.Literal:
										engine.ReadLiteral (ic.CancellationToken);
										break;
									case ImapTokenType.QString:
									case ImapTokenType.Atom:
										break;
									default:
										throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
									}
								} while (true);
							} else if (token.Type != ImapTokenType.Atom) {
								throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
							}
						} while (true);

						if (token.Type != ImapTokenType.CloseBracket)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.Literal)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

						summary.References = new MessageIdList ();

						try {
							summary.Headers = engine.ParseHeaders (engine.Stream, ic.CancellationToken);
						} catch (FormatException) {
							// consume any remaining literal data...
							ReadLiteralData (engine, ic.CancellationToken);
							summary.Headers = new HeaderList ();
						}

						if ((idx = summary.Headers.IndexOf (HeaderId.References)) != -1) {
							var references = summary.Headers[idx];
							var rawValue = references.RawValue;

							foreach (var msgid in MimeUtils.EnumerateReferences (rawValue, 0, rawValue.Length))
								summary.References.Add (msgid);
						}

						summary.Fields |= MessageSummaryItems.References;
					} else {
						summary.Fields |= MessageSummaryItems.Body;

						try {
							format = string.Format (ImapEngine.GenericItemSyntaxErrorFormat, "BODY", "{0}");
							summary.Body = ImapUtils.ParseBody (engine, format, string.Empty, ic.CancellationToken);
						} catch (ImapProtocolException ex) {
							if (!ex.UnexpectedToken)
								throw;

							// Note: GMail's IMAP implementation sometimes replies with completely broken BODY values
							// (see issue #32 for the `BODY ("ALTERNATIVE")` example), so to work around this nonsense,
							// we need to drop the remainder of this line.
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
					summary.Fields |= MessageSummaryItems.Envelope;
					break;
				case "FLAGS":
					summary.Flags = ImapUtils.ParseFlagsList (engine, atom, summary.UserFlags, ic.CancellationToken);
					summary.Fields |= MessageSummaryItems.Flags;
					break;
				case "MODSEQ":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out value64))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					summary.Fields |= MessageSummaryItems.ModSeq;
					summary.ModSeq = value64;
					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value) || value == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					summary.UniqueId = new UniqueId (ic.Folder.UidValidity, value);
					summary.Fields |= MessageSummaryItems.UniqueId;
					break;
				case "X-GM-MSGID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out value64) || value64 == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					summary.Fields |= MessageSummaryItems.GMailMessageId;
					summary.GMailMessageId = value64;
					break;
				case "X-GM-THRID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out value64) || value64 == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					summary.Fields |= MessageSummaryItems.GMailThreadId;
					summary.GMailThreadId = value64;
					break;
				case "X-GM-LABELS":
					summary.GMailLabels = ImapUtils.ParseLabelsList (engine, ic.CancellationToken);
					summary.Fields |= MessageSummaryItems.GMailLabels;
					break;
				default:
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			if ((ctx.RequestedItems & summary.Fields) == ctx.RequestedItems)
				OnMessageSummaryFetched (summary);
		}

		static HashSet<string> GetHeaderNames (HashSet<HeaderId> fields)
		{
			if (fields == null)
				return null;

			var names = new HashSet<string> ();

			foreach (var field in fields) {
				if (field == HeaderId.Unknown)
					continue;

				names.Add (field.ToHeaderName ());
			}

			return names;
		}

		string FormatSummaryItems (ref MessageSummaryItems items, HashSet<string> fields)
		{
			if ((items & MessageSummaryItems.BodyStructure) != 0 && (items & MessageSummaryItems.Body) != 0) {
				// don't query both the BODY and BODYSTRUCTURE, that's just dumb...
				items &= ~MessageSummaryItems.Body;
			}

			if (!Engine.IsGMail) {
				// first, eliminate the aliases...
				if (items == MessageSummaryItems.All)
					return "ALL";

				if (items == MessageSummaryItems.Full)
					return "FULL";

				if (items == MessageSummaryItems.Fast)
					return "FAST";
			}

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
				bool references = false;

				if (fields != null) {
					foreach (var field in fields) {
						var name = field.ToUpperInvariant ();

						if (name == "REFERENCES")
							references = true;

						headers.Append (name);
						headers.Append (' ');
					}
				}

				if ((items & MessageSummaryItems.References) != 0 && !references)
					headers.Append ("REFERENCES ");

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
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
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
				throw new ArgumentOutOfRangeException (nameof (items));

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, null);
			var command = string.Format ("UID FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			return Fetch (uids, items, GetHeaderNames (fields), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, MessageSummaryItems items, HashSet<string> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (fields == null)
				throw new ArgumentNullException (nameof (fields));

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", nameof (fields));

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, fields);
			var command = string.Format ("UID FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (items));

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, null);
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var command = string.Format ("UID FETCH {0} {1} (CHANGEDSINCE {2}{3})\r\n", set, query, modseq, vanished);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			return Fetch (uids, modseq, items, GetHeaderNames (fields), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message UIDs that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="MailFolder.MessagesVanished"/> events for messages
		/// that have vanished since the specified mod-sequence value.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override IList<IMessageSummary> Fetch (IList<UniqueId> uids, ulong modseq, MessageSummaryItems items, HashSet<string> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (fields == null)
				throw new ArgumentNullException (nameof (fields));

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", nameof (fields));

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, fields);
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var command = string.Format ("UID FETCH {0} {1} (CHANGEDSINCE {2}{3})\r\n", set, query, modseq, vanished);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (items));

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, null);
			var command = string.Format ("FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			return Fetch (indexes, items, GetHeaderNames (fields), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, MessageSummaryItems items, HashSet<string> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (fields == null)
				throw new ArgumentNullException (nameof (fields));

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", nameof (fields));

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, fields);
			var command = string.Format ("FETCH {0} {1}\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (items));

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, null);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", set, query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			return Fetch (indexes, modseq, items, GetHeaderNames (fields), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a
		/// higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the specified message indexes that
		/// have a higher mod-sequence value than the one specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override IList<IMessageSummary> Fetch (IList<int> indexes, ulong modseq, MessageSummaryItems items, HashSet<string> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (fields == null)
				throw new ArgumentNullException (nameof (fields));

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", nameof (fields));

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (indexes.Count == 0)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, fields);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", set, query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
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
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			CheckState (true, false);

			if (min == Count)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, null);
			var command = string.Format ("FETCH {0} {1}\r\n", GetFetchRange (min, max), query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			return Fetch (min, max, items, GetHeaderNames (fields), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes, inclusive.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override IList<IMessageSummary> Fetch (int min, int max, MessageSummaryItems items, HashSet<string> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (min < 0 || min > Count)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (fields == null)
				throw new ArgumentNullException (nameof (fields));

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", nameof (fields));

			CheckState (true, false);

			if (min == Count)
				return new IMessageSummary[0];

			var query = FormatSummaryItems (ref items, fields);
			var command = string.Format ("FETCH {0} {1}\r\n", GetFetchRange (min, max), query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException (nameof (items));

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			var query = FormatSummaryItems (ref items, null);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", GetFetchRange (min, max), query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
			return Fetch (min, max, modseq, items, GetHeaderNames (fields), cancellationToken);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive)
		/// that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>Fetches the message summaries for the messages between the two
		/// indexes (inclusive) that have a higher mod-sequence value than the one
		/// specified.</para>
		/// <para>It should be noted that if another client has modified any message
		/// in the folder, the IMAP server may choose to return information that was
		/// not explicitly requested. It is therefore important to be prepared to
		/// handle both additional fields on a <see cref="IMessageSummary"/> for
		/// messages that were requested as well as summaries for messages that were
		/// not requested at all.</para>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override IList<IMessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, HashSet<string> fields, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (min < 0 || min >= Count)
				throw new ArgumentOutOfRangeException (nameof (min));

			if (max != -1 && max < min)
				throw new ArgumentOutOfRangeException (nameof (max));

			if (fields == null)
				throw new ArgumentNullException (nameof (fields));

			if (fields.Count == 0)
				throw new ArgumentException ("The set of header fields cannot be empty.", nameof (fields));

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			var query = FormatSummaryItems (ref items, fields);
			var command = string.Format ("FETCH {0} {1} (CHANGEDSINCE {2})\r\n", GetFetchRange (min, max), query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchSummaryContext (items);

			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("FETCH", ic);

			return AsReadOnly (ctx.Results.Values);
		}

		/// <summary>
		/// Create a backing stream for use with the GetMessage, GetBodyPart, and GetStream methods.
		/// </summary>
		/// <remarks>
		/// <para>Allows subclass implementations to override the type of stream
		/// created for use with the GetMessage, GetBodyPart and GetStream methods.</para>
		/// <para>This could be useful for subclass implementations that intend to implement
		/// support for caching and/or for subclass implementations that want to use
		/// temporary file streams instead of memory-based streams for larger amounts of
		/// message data.</para>
		/// <para>Subclasses that implement caching using this API should wait for
		/// <see cref="CommitStream"/> before adding the stream to their cache.</para>
		/// <para>Streams returned by this method SHOULD clean up any allocated resources
		/// such as deleting temporary files from the file system.</para>
		/// <note type="note">The <paramref name="uid"/> will not be available for the various
		/// GetMessage(), GetBodyPart() and GetStream() methods that take a message index rather
		/// than a <see cref="UniqueId"/>. It may also not be available if the IMAP server
		/// response does not specify the <c>UID</c> value prior to sending the <c>literal-string</c>
		/// token containing the message stream.</note>
		/// </remarks>
		/// <seealso cref="CommitStream"/>
		/// <returns>The stream.</returns>
		/// <param name="uid">The unique identifier of the message, if available.</param>
		/// <param name="section">The section of the message that is being fetched.</param>
		/// <param name="offset">The starting offset of the message section being fetched.</param>
		/// <param name="length">The length of the stream being fetched, measured in bytes.</param>
		protected virtual Stream CreateStream (UniqueId? uid, string section, int offset, int length)
		{
			if (length > 4096)
				return new MemoryBlockStream ();

			return new MemoryStream (length);
		}

		/// <summary>
		/// Commit a stream returned by <see cref="CreateStream"/>.
		/// </summary>
		/// <remarks>
		/// <para>Commits a stream returned by <see cref="CreateStream"/>.</para>
		/// <para>This method is called only after both the message data has successfully
		/// been written to the stream returned by <see cref="CreateStream"/> and a
		/// <see cref="UniqueId"/> has been obtained for the associated message.</para>
		/// <para>For subclasses implementing caching, this method should be used for
		/// committing the stream to their cache.</para>
		/// <note type="note">Subclass implementations may take advantage of the fact that
		/// <see cref="CommitStream"/> allows returning a new <see cref="System.IO.Stream"/>
		/// reference if they move a file on the file system and wish to return a new
		/// <see cref="System.IO.FileStream"/> based on the new path, for example.</note>
		/// </remarks>
		/// <seealso cref="CreateStream"/>
		/// <returns>The stream.</returns>
		/// <param name="stream">The stream.</param>
		/// <param name="uid">The unique identifier of the message.</param>
		protected virtual Stream CommitStream (Stream stream, UniqueId uid)
		{
			return stream;
		}

		MimeMessage ParseMessage (Stream stream, CancellationToken cancellationToken)
		{
			bool dispose = !(stream is MemoryStream || stream is MemoryBlockStream);

			try {
				return Engine.ParseMessage (stream, !dispose, cancellationToken);
			} finally {
				if (dispose)
					stream.Dispose ();
			}
		}

		MimeEntity ParseEntity (Stream stream, bool dispose, CancellationToken cancellationToken)
		{
			try {
				return Engine.ParseEntity (stream, !dispose, cancellationToken);
			} finally {
				if (dispose)
					stream.Dispose ();
			}
		}

		class FetchStreamContext : IDisposable
		{
			public readonly Dictionary<string, Stream> Sections = new Dictionary<string, Stream> (StringComparer.OrdinalIgnoreCase);
			readonly ITransferProgress Progress;

			public FetchStreamContext (ITransferProgress progress)
			{
				Progress = progress;
			}

			public void Report (long nread, long total)
			{
				if (Progress == null)
					return;

				Progress.Report (nread, total);
			}

			public void Dispose ()
			{
				foreach (var section in Sections) {
					try {
						section.Value.Dispose ();
					} catch (IOException) {
					}
				}
			}
		}

		void FetchStream (ImapEngine engine, ImapCommand ic, int index)
		{
			var token = engine.ReadToken (ic.CancellationToken);
			var labels = new MessageLabelsChangedEventArgs (index);
			var flags = new MessageFlagsChangedEventArgs (index);
			var ctx = (FetchStreamContext) ic.UserData;
			var section = new StringBuilder ();
			bool labelsChanged = false;
			bool flagsChanged = false;
			var buf = new byte[4096];
			long nread = 0, size = 0;
			UniqueId? uid = null;
			Stream stream;
			int n;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			do {
				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

				var atom = (string) token.Value;
				int offset = 0, length;
				ulong modseq;
				uint value;

				switch (atom) {
				case "BODY":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenBracket)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					section.Clear ();

					do {
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type == ImapTokenType.CloseBracket)
							break;

						if (token.Type == ImapTokenType.OpenParen) {
							section.Append (" (");

							do {
								token = engine.ReadToken (ic.CancellationToken);

								if (token.Type == ImapTokenType.CloseParen)
									break;

								// the header field names will generally be atoms or qstrings but may also be literals
								switch (token.Type) {
								case ImapTokenType.Literal:
									section.Append (engine.ReadLiteral (ic.CancellationToken));
									section.Append (' ');
									break;
								case ImapTokenType.QString:
								case ImapTokenType.Atom:
									section.Append ((string) token.Value);
									break;
								default:
									throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
								}
							} while (true);

							if (section[section.Length - 1] == ' ')
								section.Length--;

							section.Append (')');
						} else if (token.Type != ImapTokenType.Atom) {
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
						} else {
							section.Append ((string) token.Value);
						}
					} while (true);

					if (token.Type != ImapTokenType.CloseBracket)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.Atom) {
						// this might be a region ("<###>")
						var expr = (string) token.Value;

						if (expr.Length > 2 && expr[0] == '<' && expr[expr.Length - 1] == '>') {
							var region = expr.Substring (1, expr.Length - 2);
							int.TryParse (region, out offset);

							token = engine.ReadToken (ic.CancellationToken);
						}
					}

					switch (token.Type) {
					case ImapTokenType.Literal:
						length = (int) token.Value;
						size += length;

						stream = CreateStream (uid, section.ToString (), offset, length);

						try {
							while ((n = engine.Stream.Read (buf, 0, buf.Length, ic.CancellationToken)) > 0) {
								stream.Write (buf, 0, n);
								nread += n;

								ctx.Report (nread, size);
							}

							stream.Position = 0;
						} catch {
							stream.Dispose ();
							throw;
						}
						break;
					case ImapTokenType.QString:
					case ImapTokenType.Atom:
						var buffer = Encoding.UTF8.GetBytes ((string) token.Value);
						length = buffer.Length;
						nread += length;
						size += length;

						stream = CreateStream (uid, section.ToString (), offset, length);

						try {
							stream.Write (buffer, 0, length);
							ctx.Report (nread, size);
							stream.Position = 0;
						} catch {
							stream.Dispose ();
							throw;
						}
						break;
					case ImapTokenType.Nil:
						stream = CreateStream (uid, section.ToString (), offset, 0);
						break;
					default:
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					}

					if (uid.HasValue)
						ctx.Sections[section.ToString ()] = CommitStream (stream, uid.Value);
					else
						ctx.Sections[section.ToString ()] = stream;

					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value) || value == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					uid = new UniqueId (UidValidity, value);

					foreach (var key in ctx.Sections.Keys.ToArray ())
						ctx.Sections[key] = CommitStream (ctx.Sections[key], uid.Value);

					labels.UniqueId = uid.Value;
					flags.UniqueId = uid.Value;
					break;
				case "MODSEQ":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					labels.ModSeq = modseq;
					flags.ModSeq = modseq;
					break;
				case "FLAGS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message flags.
					flags.Flags = ImapUtils.ParseFlagsList (engine, atom, flags.UserFlags, ic.CancellationToken);
					flagsChanged = true;
					break;
				case "X-GM-LABELS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message labels.
					labels.Labels = ImapUtils.ParseLabelsList (engine, ic.CancellationToken);
					labelsChanged = true;
					break;
				default:
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

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
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeMessage GetMessage (UniqueId uid, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[])\r\n", uid.Id);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (string.Empty, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested message.");

				ctx.Sections.Remove (string.Empty);
			} finally {
				ctx.Dispose ();
			}

			return ParseMessage (stream, cancellationToken);
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
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeMessage GetMessage (int index, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			CheckState (true, false);

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[])\r\n", index + 1);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (string.Empty, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested message.");

				ctx.Sections.Remove (string.Empty);
			} finally {
				ctx.Dispose ();
			}

			return ParseMessage (stream, cancellationToken);
		}

		static string GetBodyPartQuery (string partSpec, bool headersOnly, out string[] tags)
		{
			string query;

			if (headersOnly) {
				tags = new string[1];

				if (partSpec.Length > 0) {
					query = string.Format ("BODY.PEEK[{0}.MIME]", partSpec);
					tags[0] = partSpec + ".MIME";
				} else {
					query = "BODY.PEEK[HEADER]";
					tags[0] = "HEADER";
				}
			} else {
				tags = new string[2];

				if (partSpec.Length > 0) {
					tags[0] = partSpec + ".MIME";
					tags[1] = partSpec;
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
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public override MimeEntity GetBodyPart (UniqueId uid, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPart (uid, part, false, cancellationToken, progress);
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
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public override MimeEntity GetBodyPart (UniqueId uid, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetBodyPart (uid, part.PartSpecifier, headersOnly, cancellationToken, progress);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public MimeEntity GetBodyPart (UniqueId uid, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPart (uid, partSpecifier, false, cancellationToken, progress);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message body.
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
		public MimeEntity GetBodyPart (UniqueId uid, string partSpecifier, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			CheckState (true, false);

			string[] tags;

			var command = string.Format ("UID FETCH {0} ({1})\r\n", uid.Id, GetBodyPartQuery (partSpecifier, headersOnly, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			ChainedStream chained;
			bool dispose = false;
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				chained = new ChainedStream ();

				foreach (var tag in tags) {
					if (!ctx.Sections.TryGetValue (tag, out stream))
						throw new MessageNotFoundException ("The IMAP server did not return the requested body part.");

					if (!(stream is MemoryStream || stream is MemoryBlockStream))
						dispose = true;

					chained.Add (stream);
				}

				foreach (var tag in tags)
					ctx.Sections.Remove (tag);
			} finally {
				ctx.Dispose ();
			}

			var entity = ParseEntity (chained, dispose, cancellationToken);

			if (partSpecifier.Length == 0) {
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
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeEntity GetBodyPart (int index, BodyPart part, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPart (index, part, false, cancellationToken, progress);
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
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="part"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public override MimeEntity GetBodyPart (int index, BodyPart part, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (part == null)
				throw new ArgumentNullException (nameof (part));

			return GetBodyPart (index, part.PartSpecifier, headersOnly, cancellationToken, progress);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public MimeEntity GetBodyPart (int index, string partSpecifier, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return GetBodyPart (index, partSpecifier, false, cancellationToken, progress);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <remarks>
		/// Gets the specified body part.
		/// </remarks>
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="partSpecifier">The body part specifier.</param>
		/// <param name="headersOnly"><c>true</c> if only the headers should be downloaded; otherwise, <c>false</c>></param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="partSpecifier"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message.
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
		public MimeEntity GetBodyPart (int index, string partSpecifier, bool headersOnly, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (partSpecifier == null)
				throw new ArgumentNullException (nameof (partSpecifier));

			CheckState (true, false);

			string[] tags;

			var command = string.Format ("FETCH {0} ({1})\r\n", index + 1, GetBodyPartQuery (partSpecifier, headersOnly, out tags));
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			ChainedStream chained;
			bool dispose = false;
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				chained = new ChainedStream ();

				foreach (var tag in tags) {
					if (!ctx.Sections.TryGetValue (tag, out stream))
						throw new MessageNotFoundException ("The IMAP server did not return the requested body part.");

					if (!(stream is MemoryStream || stream is MemoryBlockStream))
						dispose = true;

					chained.Add (stream);
				}

				foreach (var tag in tags)
					ctx.Sections.Remove (tag);
			} finally {
				ctx.Dispose ();
			}

			var entity = ParseEntity (chained, dispose, cancellationToken);

			if (partSpecifier.Length == 0) {
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
		/// <param name="progress">The progress reporting mechanism.</param>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (UniqueId uid, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[]<%d.%d>)\r\n", uid.Id, offset, count);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (string.Empty, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");

				ctx.Sections.Remove (string.Empty);
			} finally {
				ctx.Dispose ();
			}

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
		/// <param name="progress">The progress reporting mechanism.</param>
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (int index, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[]<%d.%d>)\r\n", index + 1, offset, count);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (string.Empty, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");

				ctx.Sections.Remove (string.Empty);
			} finally {
				ctx.Dispose ();
			}

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified body part.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (UniqueId uid, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			CheckState (true, false);

			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}])\r\n", uid.Id, section);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (section, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");

				ctx.Sections.Remove (section);
			} finally {
				ctx.Dispose ();
			}

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message. If the starting offset is beyond
		/// the end of the specified section of the message, an empty stream is returned. If
		/// the number of bytes desired extends beyond the end of the section, a truncated
		/// stream will be returned.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uid"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="offset"/> is negative.</para>
		/// <para>-or-</para>
		/// <para><paramref name="count"/> is negative.</para>
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (UniqueId uid, string section, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", nameof (uid));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}]<{2}.{3}>)\r\n", uid.Id, section, offset, count);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (section, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");

				ctx.Sections.Remove (section);
			} finally {
				ctx.Dispose ();
			}

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="index"/> is out of range.
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
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (int index, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			CheckState (true, false);

			var command = string.Format ("FETCH {0} (BODY.PEEK[{1}])\r\n", index + 1, section);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (section, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");

				ctx.Sections.Remove (section);
			} finally {
				ctx.Dispose ();
			}

			return stream;
		}

		/// <summary>
		/// Gets a substream of the specified message.
		/// </summary>
		/// <remarks>
		/// <para>Gets a substream of the specified message. If the starting offset is beyond
		/// the end of the specified section of the message, an empty stream is returned. If
		/// the number of bytes desired extends beyond the end of the section, a truncated
		/// stream will be returned.</para>
		/// <para>For more information about how to construct the <paramref name="section"/>,
		/// see Section 6.4.5 of RFC3501.</para>
		/// </remarks>
		/// <returns>The stream.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="section">The desired section of the message.</param>
		/// <param name="offset">The starting offset of the first desired byte.</param>
		/// <param name="count">The number of bytes desired.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="progress">The progress reporting mechanism.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="section"/> is <c>null</c>.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="MessageNotFoundException">
		/// The IMAP server did not return the requested message stream.
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
		public override Stream GetStream (int index, string section, int offset, int count, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException (nameof (index));

			if (section == null)
				throw new ArgumentNullException (nameof (section));

			if (offset < 0)
				throw new ArgumentOutOfRangeException (nameof (offset));

			if (count < 0)
				throw new ArgumentOutOfRangeException (nameof (count));

			CheckState (true, false);

			if (count == 0)
				return new MemoryStream ();

			var command = string.Format ("FETCH {0} (BODY.PEEK[{1}]<{2}.{3}>)\r\n", index + 1, section, offset, count);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var ctx = new FetchStreamContext (progress);
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchStream);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			try {
				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create ("FETCH", ic);

				if (!ctx.Sections.TryGetValue (section, out stream))
					throw new MessageNotFoundException ("The IMAP server did not return the requested stream.");

				ctx.Sections.Remove (section);
			} finally {
				ctx.Dispose ();
			}

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

			if (ic.Response != ImapCommandResponse.Ok)
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			if (modseq.HasValue) {
				var modified = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (modified != null) {
					var unmodified = new int[modified.UidSet.Count];
					for (int i = 0; i < unmodified.Length; i++)
						unmodified[i] = (int) (modified.UidSet[i].Id - 1);

					return unmodified;
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
			if ((flags & SettableFlags) == 0 && (userFlags == null || userFlags.Count == 0))
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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
				throw new ArgumentException ("No flags were specified.", nameof (flags));

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

		static string LabelListToString (IList<string> labels, ICollection<object> args)
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
					args.Add (ImapEncoding.Encode (labels[i]));
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

			if (ic.Response != ImapCommandResponse.Ok)
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("STORE", ic);

			if (modseq.HasValue) {
				var modified = ic.RespCodes.OfType<ModifiedResponseCode> ().FirstOrDefault ();

				if (modified != null) {
					var unmodified = new int[modified.UidSet.Count];
					for (int i = 0; i < unmodified.Length; i++)
						unmodified[i] = (int) (modified.UidSet[i].Id - 1);

					return unmodified;
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open in read-write mode.
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
			return date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture);
		}

		void BuildQuery (StringBuilder builder, SearchQuery query, List<string> args, bool parens, ref bool ascii)
		{
			TextSearchQuery text = null;
			NumericSearchQuery numeric;
			FilterSearchQuery filter;
			HeaderSearchQuery header;
			BinarySearchQuery binary;
			UnarySearchQuery unary;
			DateSearchQuery date;

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
			case SearchTerm.Filter:
				if ((Engine.Capabilities & ImapCapabilities.Filters) == 0)
					throw new NotSupportedException ("The FILTER search term is not supported by the IMAP server.");

				filter = (FilterSearchQuery) query;
				builder.Append ("FILTER %S");
				args.Add (filter.Name);
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
				builder.Append ("X-GM-LABELS %S");
				args.Add (text.Text);
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

			charset = ascii ? null : "UTF-8";

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
				case OrderByType.Arrival:     builder.Append ("ARRIVAL"); break;
				case OrderByType.Cc:          builder.Append ("CC"); break;
				case OrderByType.Date:        builder.Append ("DATE"); break;
				case OrderByType.DisplayFrom: builder.Append ("DISPLAYFROM"); break;
				case OrderByType.DisplayTo:   builder.Append ("DISPLAYTO"); break;
				case OrderByType.From:        builder.Append ("FROM"); break;
				case OrderByType.Size:        builder.Append ("SIZE"); break;
				case OrderByType.Subject:     builder.Append ("SUBJECT"); break;
				case OrderByType.To:          builder.Append ("TO"); break;
				default: throw new ArgumentOutOfRangeException ();
				}
			}
			builder.Append (')');

			return builder.ToString ();
		}

		static void SearchMatches (ImapEngine engine, ImapCommand ic, int index)
		{
			var uids = new UniqueIdSet (SortOrder.Ascending);
			var results = (SearchResults) ic.UserData;
			ImapToken token;
			ulong modseq;
			uint uid;

			do {
				token = engine.PeekToken (ic.CancellationToken);

				// keep reading UIDs until we get to the end of the line or until we get a "(MODSEQ ####)"
				if (token.Type == ImapTokenType.Eoln || token.Type == ImapTokenType.OpenParen)
					break;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);

				uids.Add (new UniqueId (ic.Folder.UidValidity, uid));
			} while (true);

			if (token.Type == ImapTokenType.OpenParen) {
				engine.ReadToken (ic.CancellationToken);

				do {
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);

					var atom = (string) token.Value;

					switch (atom) {
					case "MODSEQ":
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq)) {
							Debug.WriteLine ("Expected 64-bit nz-number as the MODSEQ value, but got: {0}", token);
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
						}
						break;
					}

					token = engine.PeekToken (ic.CancellationToken);
				} while (token.Type != ImapTokenType.Eoln);
			}

			results.UniqueIds = uids;
		}

		static void ESearchMatches (ImapEngine engine, ImapCommand ic, int index)
		{
			var token = engine.ReadToken (ic.CancellationToken);
			var results = (SearchResults) ic.UserData;
			UniqueIdSet uids = null;
			int parenDepth = 0;
			//bool uid = false;
			uint min, max;
			ulong modseq;
			string atom;
			string tag;
			int count;

			if (token.Type == ImapTokenType.OpenParen) {
				// optional search correlator
				do {
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					atom = (string) token.Value;

					if (atom == "TAG") {
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type != ImapTokenType.Atom && token.Type != ImapTokenType.QString)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						tag = (string) token.Value;

						if (tag != ic.Tag)
							throw new ImapProtocolException ("Unexpected TAG value in untagged ESEARCH response: " + tag);
					}
				} while (true);

				token = engine.ReadToken (ic.CancellationToken);
			}

			if (token.Type == ImapTokenType.Atom && ((string) token.Value) == "UID") {
				token = engine.ReadToken (ic.CancellationToken);
				//uid = true;
			}

			do {
				if (token.Type == ImapTokenType.CloseParen) {
					if (parenDepth == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					token = engine.ReadToken (ic.CancellationToken);
					parenDepth--;
				}

				if (token.Type == ImapTokenType.Eoln) {
					// unget the eoln token
					engine.Stream.UngetToken (token);
					break;
				}

				if (token.Type == ImapTokenType.OpenParen) {
					token = engine.ReadToken (ic.CancellationToken);
					parenDepth++;
				}

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

				atom = (string) token.Value;

				token = engine.ReadToken (ic.CancellationToken);

				switch (atom) {
				case "RELEVANCY":
					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					results.Relevancy = new List<byte> ();

					do {
						int score;

						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						if (token.Type != ImapTokenType.Atom || !int.TryParse ((string) token.Value, out score) || score < 1 || score > 100)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						results.Relevancy.Add ((byte) score);
					} while (true);
					break;
				case "MODSEQ":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!ulong.TryParse ((string) token.Value, out modseq))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.ModSeq = modseq;
					break;
				case "COUNT":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!int.TryParse ((string) token.Value, out count))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Count = count;
					break;
				case "MIN":
					if (!uint.TryParse ((string) token.Value, out min))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Min = new UniqueId (ic.Folder.UidValidity, min);
					break;
				case "MAX":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!uint.TryParse ((string) token.Value, out max))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Max = new UniqueId (ic.Folder.UidValidity, max);
					break;
				case "ALL":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!UniqueIdSet.TryParse ((string) token.Value, ic.Folder.UidValidity, out uids))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Count = uids.Count;
					break;
				default:
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);
				}

				token = engine.ReadToken (ic.CancellationToken);
			} while (true);

			results.UniqueIds = uids ?? new UniqueIdSet ();
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Sends a <c>UID SEARCH</c> command with the specified query passed directly to the IMAP server
		/// with no interpretation by MailKit. This means that the query may contain any arguments that a
		/// <c>UID SEARCH</c> command is allowed to have according to the IMAP specifications and any
		/// extensions that are supported, including <c>RETURN</c> parameters.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="query"/> is an empty string.
		/// </exception>>
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public SearchResults Search (string query, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));

			query = query.Trim ();

			if (query.Length == 0)
				throw new ArgumentException (nameof (query));

			CheckState (true, false);

			var command = "UID SEARCH " + query + "\r\n";
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Asynchronously searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Sends a <c>UID SEARCH</c> command with the specified query passed directly to the IMAP server
		/// with no interpretation by MailKit. This means that the query may contain any arguments that a
		/// <c>UID SEARCH</c> command is allowed to have according to the IMAP specifications and any
		/// extensions that are supported, including <c>RETURN</c> parameters.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="query"/> is an empty string.
		/// </exception>>
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public Task<SearchResults> SearchAsync (string query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return Task.Factory.StartNew (() => {
				lock (SyncRoot) {
					return Search (query, cancellationToken);
				}
			}, cancellationToken, TaskCreationOptions.None, TaskScheduler.Default);
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				command += "RETURN () ";

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);

			// Note: always register the untagged SEARCH handler because some servers will brokenly
			// respond with "* SEARCH ..." instead of "* ESEARCH ..." even when using the extended
			// search syntax.
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return ((SearchResults) ic.UserData).UniqueIds;
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0) {
				for (int i = 0; i < orderBy.Count; i++) {
					if (orderBy[i].Type == OrderByType.DisplayFrom || orderBy[i].Type == OrderByType.DisplayTo)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");
				}
			}

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				command += "RETURN () ";

			command += order + " " + (charset ?? "US-ASCII") + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			else
				ic.RegisterUntaggedHandler ("SORT", SearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return ((SearchResults) ic.UserData).UniqueIds;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
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
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			if (uids.Count == 0)
				return new UniqueId[0];

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				command += "RETURN () ";

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);

			// Note: always register the untagged SEARCH handler because some servers will brokenly
			// respond with "* SEARCH ..." instead of "* ESEARCH ..." even when using the extended
			// search syntax.
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return ((SearchResults) ic.UserData).UniqueIds;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0) {
				for (int i = 0; i < orderBy.Count; i++) {
					if (orderBy[i].Type == OrderByType.DisplayFrom || orderBy[i].Type == OrderByType.DisplayTo)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");
				}
			}

			if (uids.Count == 0)
				return new UniqueId[0];

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				command += "RETURN () ";

			command += order + " " + (charset ?? "US-ASCII") + " UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			else
				ic.RegisterUntaggedHandler ("SORT", SearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return ((SearchResults) ic.UserData).UniqueIds;
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query,
		/// returning only the specified search results.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESEARCH extension.</para>
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override SearchResults Search (SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.ESearch) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESEARCH extension.");

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH RETURN (";

			if (options != SearchOptions.All && options != 0) {
				if ((options & SearchOptions.All) != 0)
					command += "ALL ";
				if ((options & SearchOptions.Relevancy) != 0)
					command += "RELEVANCY ";
				if ((options & SearchOptions.Count) != 0)
					command += "COUNT ";
				if ((options & SearchOptions.Min) != 0)
					command += "MIN ";
				if ((options & SearchOptions.Max) != 0)
					command += "MAX ";
				command = command.TrimEnd ();
			}
			command += ") ";

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query and ordering,
		/// returning only the requested search results.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
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
		/// <para>The IMAP server does not support the ESORT extension.</para>
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override SearchResults Search (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.ESort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESORT extension.");

			if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0) {
				for (int i = 0; i < orderBy.Count; i++) {
					if (orderBy[i].Type == OrderByType.DisplayFrom || orderBy[i].Type == OrderByType.DisplayTo)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");
				}
			}

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);

			var command = "UID SORT RETURN (";
			if (options != SearchOptions.All && options != 0) {
				if ((options & SearchOptions.All) != 0)
					command += "ALL ";
				if ((options & SearchOptions.Relevancy) != 0)
					command += "RELEVANCY ";
				if ((options & SearchOptions.Count) != 0)
					command += "COUNT ";
				if ((options & SearchOptions.Min) != 0)
					command += "MIN ";
				if ((options & SearchOptions.Max) != 0)
					command += "MAX ";
				command = command.TrimEnd ();
			}
			command += ") ";

			command += order + " " + (charset ?? "US-ASCII") + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Searches the fsubset of UIDs in the folder for messages matching the specified query,
		/// returning only the specified search results.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the <paramref name="uids"/> is invalid.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESEARCH extension.</para>
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override SearchResults Search (SearchOptions options, IList<UniqueId> uids, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.ESearch) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESEARCH extension.");

			if (uids.Count == 0)
				return new SearchResults ();

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH RETURN (";

			if (options != SearchOptions.All && options != 0) {
				if ((options & SearchOptions.All) != 0)
					command += "ALL ";
				if ((options & SearchOptions.Relevancy) != 0)
					command += "RELEVANCY ";
				if ((options & SearchOptions.Count) != 0)
					command += "COUNT ";
				if ((options & SearchOptions.Min) != 0)
					command += "MIN ";
				if ((options & SearchOptions.Max) != 0)
					command += "MAX ";
				command = command.TrimEnd ();
			}
			command += ") ";

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query and ordering,
		/// returning only the requested search results.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
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
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is empty.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESORT extension.</para>
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
		/// The <see cref="ImapFolder"/> is not currently open.
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
		public override SearchResults Search (SearchOptions options, IList<UniqueId> uids, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.ESort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESORT extension.");

			if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0) {
				for (int i = 0; i < orderBy.Count; i++) {
					if (orderBy[i].Type == OrderByType.DisplayFrom || orderBy[i].Type == OrderByType.DisplayTo)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");
				}
			}

			if (uids.Count == 0)
				return new SearchResults ();

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT RETURN (";

			if (options != SearchOptions.All && options != 0) {
				if ((options & SearchOptions.All) != 0)
					command += "ALL ";
				if ((options & SearchOptions.Relevancy) != 0)
					command += "RELEVANCY ";
				if ((options & SearchOptions.Count) != 0)
					command += "COUNT ";
				if ((options & SearchOptions.Min) != 0)
					command += "MIN ";
				if ((options & SearchOptions.Max) != 0)
					command += "MAX ";
				command = command.TrimEnd ();
			}
			command += ") ";

			command += order + " " + (charset ?? "US-ASCII") + " UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatches);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return (SearchResults) ic.UserData;
		}

		static void ThreadMatches (ImapEngine engine, ImapCommand ic, int index)
		{
			ic.UserData = ImapUtils.ParseThreads (engine, ic.Folder.UidValidity, ic.CancellationToken);
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (algorithm), "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + (charset ?? "US-ASCII") + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ThreadMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
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
		/// The <see cref="MessageThread.UniqueId"/> can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
				throw new ArgumentOutOfRangeException (nameof (algorithm), "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + (charset ?? "US-ASCII") + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ThreadMatches);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
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
			var labelsChangedEventArgs = new MessageLabelsChangedEventArgs (index);
			var flagsChangedEventArgs = new MessageFlagsChangedEventArgs (index);
			var modSeqChangedEventArgs = new ModSeqChangedEventArgs (index);
			var token = engine.ReadToken (cancellationToken);
			bool modSeqChanged = false;
			bool labelsChanged = false;
			bool flagsChanged = false;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

				var atom = (string) token.Value;
				ulong modseq;
				uint uid;

				switch (atom) {
				case "MODSEQ":
					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					modSeqChangedEventArgs.ModSeq = modseq;
					labelsChangedEventArgs.ModSeq = modseq;
					flagsChangedEventArgs.ModSeq = modseq;
					modSeqChanged = true;
					break;
				case "UID":
					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					modSeqChangedEventArgs.UniqueId = new UniqueId (UidValidity, uid);
					labelsChangedEventArgs.UniqueId = new UniqueId (UidValidity, uid);
					flagsChangedEventArgs.UniqueId = new UniqueId (UidValidity, uid);
					break;
				case "FLAGS":
					flagsChangedEventArgs.Flags = ImapUtils.ParseFlagsList (engine, atom, flagsChangedEventArgs.UserFlags, cancellationToken);
					flagsChanged = true;
					break;
				case "X-GM-LABELS":
					labelsChangedEventArgs.Labels = ImapUtils.ParseLabelsList (engine, cancellationToken);
					labelsChanged = true;
					break;
				default:
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			if (flagsChanged)
				OnMessageFlagsChanged (flagsChangedEventArgs);

			if (labelsChanged)
				OnMessageLabelsChanged (labelsChangedEventArgs);

			if (modSeqChanged)
				OnModSeqChanged (modSeqChangedEventArgs);
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
			UniqueIdSet vanished;
			bool earlier = false;

			if (token.Type == ImapTokenType.OpenParen) {
				do {
					token = engine.ReadToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "VANISHED", token);

					var atom = (string) token.Value;

					if (atom == "EARLIER")
						earlier = true;
				} while (true);

				token = engine.ReadToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.Atom || !UniqueIdSet.TryParse ((string) token.Value, UidValidity, out vanished))
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "VANISHED", token);

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

		internal void UpdateAppendLimit (uint? limit)
		{
			AppendLimit = limit;
		}

		internal void UpdateHighestModSeq (ulong modseq)
		{
			if (HighestModSeq == modseq)
				return;

			HighestModSeq = modseq;

			OnHighestModSeqChanged ();
		}

		internal void UpdateUidValidity (uint validity)
		{
			if (UidValidity == validity)
				return;

			UidValidity = validity;

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
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
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
