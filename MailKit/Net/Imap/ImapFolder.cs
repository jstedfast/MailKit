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
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

using MimeKit;
using MimeKit.IO;
using MailKit.Search;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP folder.
	/// </summary>
	public class ImapFolder : IFolder
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
			FullName = ImapEncoding.Decode (encodedName);
			Name = GetBaseName (FullName, delim);
			DirectorySeparator = delim;
			EncodedName = encodedName;
			Attributes = attrs;
			Engine = engine;
		}

		static string GetBaseName (string fullName, char delim)
		{
			var names = fullName.Split (new [] { delim }, StringSplitOptions.RemoveEmptyEntries);

			return names.Length > 0 ? names[names.Length - 1] : fullName;
		}

		/// <summary>
		/// Gets the engine.
		/// </summary>
		/// <value>The engine.</value>
		internal ImapEngine Engine {
			get; private set;
		}

		/// <summary>
		/// Gets the encoded name of the folder.
		/// </summary>
		/// <value>The encoded name.</value>
		internal string EncodedName {
			get; private set;
		}

		void CheckState (bool open, bool rw)
		{
			if (Engine.IsDisposed)
				throw new ObjectDisposedException ("ImapClient");

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
			EncodedName = ImapEncoding.Encode (FullName);
			Engine.FolderCache.Remove (oldEncodedName);
			Engine.FolderCache[EncodedName] = this;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Access = FolderAccess.None;
				Engine.Selected = null;
			}

			OnRenamed (oldFullName, FullName);
		}

		internal void SetParentFolder (IFolder parent)
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
					PermanentFlags = code.Flags;
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
					UidNext = code.Uid;
					break;
				case ImapResponseCodeType.UidValidity:
					UidValidity = code.UidValidity;
					break;
				case ImapResponseCodeType.Unseen:
					FirstUnread = code.Index;
					break;
				case ImapResponseCodeType.HighestModSeq:
					HighestModSeq = code.HighestModSeq;
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

		#region IFolder implementation

		/// <summary>
		/// Gets the parent folder.
		/// </summary>
		/// <remarks>
		/// Root-level folders do not have a parent folder.
		/// </remarks>
		/// <value>The parent folder.</value>
		public IFolder ParentFolder {
			get; private set;
		}

		/// <summary>
		/// Gets the folder attributes.
		/// </summary>
		/// <value>The folder attributes.</value>
		public FolderAttributes Attributes {
			get; internal set;
		}

		/// <summary>
		/// Gets the permanent flags.
		/// </summary>
		/// <remarks>
		/// The permanent flags are the message flags that will persist between sessions.
		/// </remarks>
		/// <value>The permanent flags.</value>
		public MessageFlags PermanentFlags {
			get; internal set;
		}

		/// <summary>
		/// Gets the accepted flags.
		/// </summary>
		/// <remarks>
		/// The accepted flags are the message flags that will be accepted and persist
		/// for the current session. For the set of flags that will persist between
		/// sessions, see the <see cref="PermanentFlags"/> property.
		/// </remarks>
		/// <value>The accepted flags.</value>
		public MessageFlags AcceptedFlags {
			get; internal set;
		}

		/// <summary>
		/// Gets the directory separator.
		/// </summary>
		/// <value>The directory separator.</value>
		public char DirectorySeparator { 
			get; private set;
		}

		/// <summary>
		/// Gets the read/write access of the folder.
		/// </summary>
		/// <value>The read/write access.</value>
		public FolderAccess Access {
			get; internal set;
		}

		/// <summary>
		/// Gets whether or not the folder is a namespace folder.
		/// </summary>
		/// <value><c>true</c> if the folder is a namespace folder; otherwise, <c>false</c>.</value>
		public bool IsNamespace {
			get; internal set;
		}

		/// <summary>
		/// Gets the full name of the folder.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of the full path of a file on a file system.
		/// </remarks>
		/// <value>The full name of the folder.</value>
		public string FullName {
			get; private set;
		}

		/// <summary>
		/// Gets the name of the folder.
		/// </summary>
		/// <remarks>
		/// This is the equivalent of the file name of a file on the file system.
		/// </remarks>
		/// <value>The name of the folder.</value>
		public string Name {
			get; private set;
		}

		/// <summary>
		/// Gets a value indicating whether the folder is subscribed.
		/// </summary>
		/// <value><c>true</c> if the folder is subscribed; otherwise, <c>false</c>.</value>
		public bool IsSubscribed {
			get; private set;
		}

		/// <summary>
		/// Gets a value indicating whether the folder is currently open.
		/// </summary>
		/// <value><c>true</c> if the folder is currently open; otherwise, <c>false</c>.</value>
		public bool IsOpen {
			get { return Engine.Selected == this; }
		}

		/// <summary>
		/// Gets a value indicating whether the folder exists.
		/// </summary>
		/// <value><c>true</c> if the folder exists; otherwise, <c>false</c>.</value>
		public bool Exists {
			get; internal set;
		}

		/// <summary>
		/// Gets whether or not the folder supports mod-sequences.
		/// </summary>
		/// <remarks>
		/// <para>If mod-sequences are not supported by the folder, then all of the APIs that take a modseq
		/// argument will throw <see cref="System.NotSupportedException"/> and should not be used.</para>
		/// <para>There are two reasons that a <see cref="ImapFolder"/> might not support mod-sequences:
		/// <list type="bullet">
		/// <item>The IMAP server does not support the CONDSTORE extension (<see cref="ImapCapabilities.CondStore"/>).</item>
		/// <item>The SELECT or EXAMINE command returned the NOMODSEQ response code.</item>
		/// </list></para>
		/// </remarks>
		/// <value><c>true</c> if supports mod-sequences; otherwise, <c>false</c>.</value>
		public bool SupportsModSeq {
			get; private set;
		}

		/// <summary>
		/// Gets the highest mod-sequence value of all messages in the mailbox.
		/// </summary>
		/// <remarks>
		/// This property is only available if the IMAP server supports the CONDSTORE extension.
		/// </remarks>
		/// <value>The highest mod-sequence value.</value>
		public ulong HighestModSeq {
			get; private set;
		}

		/// <summary>
		/// Gets the UID validity.
		/// </summary>
		/// <remarks>
		/// <para>UIDs are only valid so long as the UID validity value remains unchanged. If and when
		/// the folder's <see cref="UidValidity"/> is changed, a client MUST discard its cache of UIDs
		/// along with any summary information that it may have and re-query the folder.</para>
		/// <para>This value will only be set after the folder has been opened.</para>
		/// </remarks>
		/// <value>The UID validity.</value>
		public UniqueId? UidValidity {
			get; private set;
		}

		/// <summary>
		/// Gets the UID that the folder will assign to the next message that is added.
		/// </summary>
		/// <remarks>
		/// This value will only be set after the folder has been opened.
		/// </remarks>
		/// <value>The next UID.</value>
		public UniqueId? UidNext {
			get; private set;
		}

		/// <summary>
		/// Gets the index of the first unread message in the folder.
		/// </summary>
		/// <remarks>
		/// This value will only be set after the folder has been opened.
		/// </remarks>
		/// <value>The index of the first unread message.</value>
		public int FirstUnread {
			get; private set;
		}

		/// <summary>
		/// Gets the number of recently added messages.
		/// </summary>
		/// <value>The number of recently added messages.</value>
		public int Recent {
			get; private set;
		}

		/// <summary>
		/// Gets the total number of messages in the folder.
		/// </summary>
		/// <value>The total number of messages.</value>
		public int Count {
			get; private set;
		}

		static string SelectOrExamine (FolderAccess access)
		{
			return access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT";
		}

		static void QResyncFetch (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
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
		/// <para>You should also make sure to add listeners to the <see cref="Vanished"/> and
		/// <see cref="MessageFlagsChanged"/> events to get notifications of changes since
		/// the last time the folder was opened.</para>
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="uidValidity">The last known <see cref="UidValidity"/> value.</param>
		/// <param name="highestModSeq">The last known <see cref="HighestModSeq"/> value.</param>
		/// <param name="uids">The last known list of unique message identifiers.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public FolderAccess Open (FolderAccess access, UniqueId uidValidity, ulong highestModSeq, UniqueId[] uids)
		{
			return Open (access, uidValidity, highestModSeq, uids, CancellationToken.None);
		}

		/// <summary>
		/// Opens the folder using the requested folder access.
		/// </summary>
		/// <remarks>
		/// <para>This variant of the <see cref="Open(FolderAccess,System.Threading.CancellationToken)"/>
		/// method is meant for quick resynchronization of the folder. Before calling this method,
		/// the <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method MUST be called.</para>
		/// <para>You should also make sure to add listeners to the <see cref="Vanished"/> and
		/// <see cref="MessageFlagsChanged"/> events to get notifications of changes since
		/// the last time the folder was opened.</para>
		/// </remarks>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <param name="uidValidity">The last known <see cref="UidValidity"/> value.</param>
		/// <param name="highestModSeq">The last known <see cref="HighestModSeq"/> value.</param>
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
		public FolderAccess Open (FolderAccess access, UniqueId uidValidity, ulong highestModSeq, UniqueId[] uids, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (access != FolderAccess.ReadOnly && access != FolderAccess.ReadWrite)
				throw new ArgumentOutOfRangeException ("access");

			CheckState (false, false);

			if (IsOpen && Access == access)
				return access;

			if ((Engine.Capabilities & ImapCapabilities.QuickResync) == 0)
				throw new NotSupportedException ();

			if (!Engine.QResyncEnabled)
				throw new InvalidOperationException ("The QRESYNC feature is not enabled.");

			var qresync = string.Format ("(QRESYNC ({0} {1}", uidValidity.Id, highestModSeq);

			if (uids.Length > 0)
				qresync += " " + set;

			qresync += "))";

			var command = string.Format ("{0} %F {1}\r\n", SelectOrExamine (access), qresync);
			var ic = new ImapCommand (Engine, cancellationToken, this, command, this);
			ic.RegisterUntaggedHandler ("FETCH", QResyncFetch);

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic.Result);

			if (Engine.Selected != null)
				Engine.Selected.Access = FolderAccess.None;

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			return Access;
		}

		/// <summary>
		/// Opens the folder using the requested folder access.
		/// </summary>
		/// <returns>The <see cref="FolderAccess"/> state of the folder.</returns>
		/// <param name="access">The requested folder access.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="access"/> is not a valid value.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public FolderAccess Open (FolderAccess access)
		{
			return Open (access, CancellationToken.None);
		}

		/// <summary>
		/// Opens the folder using the requested folder access.
		/// </summary>
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
		public FolderAccess Open (FolderAccess access, CancellationToken cancellationToken)
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
				throw new ImapCommandException (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic.Result);

			if (Engine.Selected != null)
				Engine.Selected.Access = FolderAccess.None;

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			return Access;
		}

		/// <summary>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </summary>
		/// <param name="expunge">If set to <c>true</c>, expunge.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void Close (bool expunge)
		{
			Close (expunge, CancellationToken.None);
		}

		/// <summary>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </summary>
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
		public void Close (bool expunge, CancellationToken cancellationToken)
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
				throw new ImapCommandException (expunge ? "CLOSE" : "UNSELECT", ic.Result);

			Engine.State = ImapEngineState.Authenticated;
			Access = FolderAccess.None;
			Engine.Selected = null;
		}

		/// <summary>
		/// Creates a new subfolder with the given name.
		/// </summary>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="isMessageFolder"><c>true</c> if the folder will be used to contain messages; otherwise <c>false</c>.</param>
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
		/// <para>The <see cref="DirectorySeparator"/> is nil, and thus child folders cannot be created.</para>
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
		public IFolder Create (string name, bool isMessageFolder)
		{
			return Create (name, isMessageFolder, CancellationToken.None);
		}

		/// <summary>
		/// Creates a new subfolder with the given name.
		/// </summary>
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
		/// <para>The <see cref="DirectorySeparator"/> is nil, and thus child folders cannot be created.</para>
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
		public IFolder Create (string name, bool isMessageFolder, CancellationToken cancellationToken)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (string.IsNullOrEmpty (name) || name.IndexOf (DirectorySeparator) != -1)
				throw new ArgumentException ("The name is not a legal folder name.", "name");

			CheckState (false, false);

			if (!string.IsNullOrEmpty (FullName) && DirectorySeparator == '\0')
				throw new InvalidOperationException ("Cannot create child folders.");

			var fullName = !string.IsNullOrEmpty (FullName) ? FullName + DirectorySeparator + name : name;
			var encodedName = ImapEncoding.Encode (fullName);
			var list = new List<ImapFolder> ();
			var createName = encodedName;
			ImapFolder folder;

			if (!isMessageFolder)
				createName += DirectorySeparator;

			var ic = Engine.QueueCommand (cancellationToken, null, "CREATE %S\r\n", createName);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("CREATE", ic.Result);

			ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderList);
			ic.UserData = list;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("LIST", ic.Result);

			folder = list.FirstOrDefault ();

			if (folder != null)
				folder.ParentFolder = this;

			return folder;
		}

		/// <summary>
		/// Renames the folder to exist with a new name under a new parent folder.
		/// </summary>
		/// <param name="parent">The new parent folder.</param>
		/// <param name="name">The new name of the folder.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void Rename (IFolder parent, string name)
		{
			Rename (parent, name, CancellationToken.None);
		}

		/// <summary>
		/// Renames the folder to exist with a new name under a new parent folder.
		/// </summary>
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
		public void Rename (IFolder parent, string name, CancellationToken cancellationToken)
		{
			if (parent == null)
				throw new ArgumentNullException ("parent");

			if (!(parent is ImapFolder) || ((ImapFolder) parent).Engine != Engine)
				throw new ArgumentException ("The parent folder does not belong to this ImapClient.", "parent");

			if (name == null)
				throw new ArgumentNullException ("name");

			if (string.IsNullOrEmpty (name) || name.IndexOf (parent.DirectorySeparator) != -1)
				throw new ArgumentException ("The name is not a legal folder name.", "name");

			if (IsNamespace || FullName.ToUpperInvariant () == "INBOX")
				throw new InvalidOperationException ("Cannot rename this folder.");

			CheckState (false, false);

			var encodedName = ImapEncoding.Encode (parent.FullName + parent.DirectorySeparator + name);
			var ic = Engine.QueueCommand (cancellationToken, null, "RENAME %F %S\r\n", this, encodedName);
			var oldFullName = FullName;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("RENAME", ic.Result);

			Engine.FolderCache.Remove (EncodedName);
			Engine.FolderCache[encodedName] = this;

			ParentFolder.Renamed -= ParentFolderRenamed;
			SetParentFolder (parent);

			FullName = ImapEncoding.Decode (encodedName);
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
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// <para>The <see cref="ImapClient"/> is either not connected or not authenticated.</para>
		/// <para>-or-</para>
		/// <para>The folder cannot be deleted (it is either a namespace or the Inbox).</para>
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
		public void Delete ()
		{
			Delete (CancellationToken.None);
		}

		/// <summary>
		/// Deletes the folder on the IMAP server.
		/// </summary>
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
		public void Delete (CancellationToken cancellationToken)
		{
			if (IsNamespace || FullName.ToUpperInvariant () == "INBOX")
				throw new InvalidOperationException ("Cannot rename this folder.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "DELETE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("DELETE", ic.Result);

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
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public void Subscribe ()
		{
			Subscribe (CancellationToken.None);
		}

		/// <summary>
		/// Subscribes the folder.
		/// </summary>
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
		public void Subscribe (CancellationToken cancellationToken)
		{
			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "SUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("SUBSCRIBE", ic.Result);

			IsSubscribed = true;
			OnSubscribed ();
		}

		/// <summary>
		/// Unsubscribes the folder.
		/// </summary>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public void Unsubscribe ()
		{
			Unsubscribe (CancellationToken.None);
		}

		/// <summary>
		/// Unsubscribes the folder.
		/// </summary>
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
		public void Unsubscribe (CancellationToken cancellationToken)
		{
			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "UNSUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("UNSUBSCRIBE", ic.Result);

			IsSubscribed = false;
			OnUnsubscribed ();
		}

		/// <summary>
		/// Gets the subfolders.
		/// </summary>
		/// <returns>The subfolders.</returns>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public IEnumerable<IFolder> GetSubfolders (bool subscribedOnly)
		{
			return GetSubfolders (subscribedOnly, CancellationToken.None);
		}

		/// <summary>
		/// Gets the subfolders.
		/// </summary>
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
		public IEnumerable<IFolder> GetSubfolders (bool subscribedOnly, CancellationToken cancellationToken)
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

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException (subscribedOnly ? "LSUB" : "LIST", ic.Result);

			return list;
		}

		/// <summary>
		/// Gets the specified subfolder.
		/// </summary>
		/// <returns>The subfolder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="name">The name of the subfolder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="name"/> is either an empty string or contains the <see cref="DirectorySeparator"/>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public IFolder GetSubfolder (string name)
		{
			return GetSubfolder (name, CancellationToken.None);
		}

		/// <summary>
		/// Gets the specified subfolder.
		/// </summary>
		/// <returns>The subfolder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="name">The name of the subfolder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="name"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="name"/> is either an empty string or contains the <see cref="DirectorySeparator"/>.
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
		public IFolder GetSubfolder (string name, CancellationToken cancellationToken)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (name.Length == 0 || name.IndexOf (DirectorySeparator) != -1)
				throw new ArgumentException ("The name of the subfolder is invalid.", "name");

			CheckState (false, false);

			var fullName = FullName.Length > 0 ? FullName + DirectorySeparator + name : name;
			var encodedName = ImapEncoding.Encode (fullName);
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
				throw new ImapCommandException ("LIST", ic.Result);

			if (list.Count == 0)
				throw new FolderNotFoundException (fullName);

			return list[0];
		}

		/// <summary>
		/// Forces the server to flush its state for the folder.
		/// </summary>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void Check ()
		{
			Check (CancellationToken.None);
		}

		/// <summary>
		/// Forces the server to flush its state for the folder.
		/// </summary>
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
		public void Check (CancellationToken cancellationToken)
		{
			CheckState (true, false);

			var ic = Engine.QueueCommand (cancellationToken, this, "CHECK\r\n");

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("CHECK", ic.Result);
		}

		/// <summary>
		/// Updates the values of the specified items.
		/// </summary>
		/// <param name="items">The items to update.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void Status (StatusItems items)
		{
			Status (items, CancellationToken.None);
		}

		/// <summary>
		/// Updates the values of the specified items.
		/// </summary>
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
		public void Status (StatusItems items, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Status) != 0)
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
			if ((items & StatusItems.FirstUnread) != 0)
				flags += "UNSEEN ";

			if ((Engine.Capabilities & ImapCapabilities.CondStore) != 0) {
				if ((items & StatusItems.HighestModSeq) != 0)
					flags += "HIGHESTMODSEQ ";
			}

			flags = flags.TrimEnd ();

			var ic = Engine.QueueCommand (cancellationToken, null, "STATUS %F (%s)\r\n", this, flags);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STATUS", ic.Result);
		}

		/// <summary>
		/// Expunges the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="Expunged"/> event will be emitted for each
		/// message that is expunged. However, if the IMAP server supports the QRESYNC
		/// extension and it has been enabled via the
		/// <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="Vanished"/> event will be emitted rather than the
		/// <see cref="Expunged"/> event.</para>
		/// </remarks>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void Expunge ()
		{
			Expunge (CancellationToken.None);
		}

		/// <summary>
		/// Expunges the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="Expunged"/> event will be emitted for each
		/// message that is expunged. However, if the IMAP server supports the QRESYNC
		/// extension and it has been enabled via the
		/// <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="Vanished"/> event will be emitted rather than the
		/// <see cref="Expunged"/> event.</para>
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
		public void Expunge (CancellationToken cancellationToken)
		{
			CheckState (true, true);

			var ic = Engine.QueueCommand (cancellationToken, this, "EXPUNGE\r\n");

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("EXPUNGE", ic.Result);
		}

		/// <summary>
		/// Expunges the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="Expunged"/> event will be emitted for each
		/// message that is expunged. However, if the IMAP server supports the QRESYNC
		/// extension and it has been enabled via the
		/// <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="Vanished"/> event will be emitted rather than the
		/// <see cref="Expunged"/> event.</para>
		/// </remarks>
		/// <param name="uids">The message uids.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The list of uids contained one or more invalid values.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void Expunge (UniqueId[] uids)
		{
			Expunge (uids, CancellationToken.None);
		}

		/// <summary>
		/// Expunges the specified uids, permanently removing them from the folder.
		/// </summary>
		/// <remarks>
		/// <para>Normally, an <see cref="Expunged"/> event will be emitted for each
		/// message that is expunged. However, if the IMAP server supports the QRESYNC
		/// extension and it has been enabled via the
		/// <see cref="ImapClient.EnableQuickResync(CancellationToken)"/> method, then
		/// the <see cref="Vanished"/> event will be emitted rather than the
		/// <see cref="Expunged"/> event.</para>
		/// </remarks>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// The list of uids contained one or more invalid values.
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
		public void Expunge (UniqueId[] uids, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			CheckState (true, true);

			if (uids.Length == 0)
				return;

			var ic = Engine.QueueCommand (cancellationToken, this, "UID EXPUNGE %s\r\n", set);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("EXPUNGE", ic.Result);
		}

		ImapCommand QueueAppend (MimeMessage message, MessageFlags flags, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			string format = string.Empty;

			// Note: GMail claims to support UIDPLUS, but does not accept "UID APPEND"
			if (!Engine.IsGMail) {
				if ((Engine.Capabilities & ImapCapabilities.UidPlus) != 0)
					format = "UID ";
			}

			format += "APPEND %F";

			if (flags != MessageFlags.None)
				format += " " + ImapUtils.FormatFlagsList (flags);

			if (date.HasValue)
				format += " \"" + ImapUtils.FormatInternalDate (date.Value) + "\"";

			format += " %L\r\n";

			return Engine.QueueCommand (cancellationToken, null, format, this, message);
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public UniqueId? Append (MimeMessage message, MessageFlags flags)
		{
			return Append (message, flags, CancellationToken.None);
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		public UniqueId? Append (MimeMessage message, MessageFlags flags, CancellationToken cancellationToken)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			CheckState (false, false);

			var ic = QueueAppend (message, flags, null, cancellationToken);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("APPEND", ic.Result);

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.AppendUid)
					return code.DestUidSet[0];
			}

			return null;
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public UniqueId? Append (MimeMessage message, MessageFlags flags, DateTimeOffset date)
		{
			return Append (message, flags, date, CancellationToken.None);
		}

		/// <summary>
		/// Appends the specified message to the folder.
		/// </summary>
		/// <returns>The UID of the appended message, if available; otherwise, <c>null</c>.</returns>
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
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
		public UniqueId? Append (MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			CheckState (false, false);

			var ic = QueueAppend (message, flags, date, cancellationToken);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("APPEND", ic.Result);

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.AppendUid)
					return code.DestUidSet[0];
			}

			return null;
		}

		ImapCommand QueueMultiAppend (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates, CancellationToken cancellationToken)
		{
			var args = new List<object> ();
			string format = string.Empty;

			// Note: GMail claims to support UIDPLUS, but does not accept "UID APPEND"
			if (!Engine.IsGMail) {
				if ((Engine.Capabilities & ImapCapabilities.UidPlus) != 0)
					format = "UID ";
			}

			format += "APPEND %F";
			args.Add (this);

			for (int i = 0; i < messages.Length; i++) {
				if (flags[i] != MessageFlags.None)
					format += " " + ImapUtils.FormatFlagsList (flags[i]);

				if (dates != null)
					format += " \"" + ImapUtils.FormatInternalDate (dates[i]) + "\"";

				format += " %L";

				args.Add (messages[i]);
			}

			format += "\r\n";

			return Engine.QueueCommand (cancellationToken, null, format, args.ToArray ());
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public UniqueId[] Append (MimeMessage[] messages, MessageFlags[] flags)
		{
			return Append (messages, flags, CancellationToken.None);
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
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
		public UniqueId[] Append (MimeMessage[] messages, MessageFlags[] flags, CancellationToken cancellationToken)
		{
			if (messages == null)
				throw new ArgumentNullException ("messages");

			for (int i = 0; i < messages.Length; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException ("flags");

			if (messages.Length != flags.Length)
				throw new ArgumentException ("The number of messages and the number of flags must be equal.");

			CheckState (false, false);

			if (messages.Length == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.MultiAppend) != 0) {
				var ic = QueueMultiAppend (messages, flags, null, cancellationToken);

				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Result != ImapCommandResult.Ok)
					throw new ImapCommandException ("APPEND", ic.Result);

				foreach (var code in ic.RespCodes) {
					if (code.Type == ImapResponseCodeType.AppendUid)
						return code.DestUidSet;
				}

				return null;
			}

			var uids = new List<UniqueId> ();

			for (int i = 0; i < messages.Length; i++) {
				var uid = Append (messages[i], flags[i], cancellationToken);
				if (uids != null && uid.HasValue)
					uids.Add (uid.Value);
				else
					uids = null;
			}

			return uids != null ? uids.ToArray () : new UniqueId[0];
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
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
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The <see cref="ImapClient"/> is either not connected or not authenticated.
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
		public UniqueId[] Append (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates)
		{
			return Append (messages, flags, dates, CancellationToken.None);
		}

		/// <summary>
		/// Appends the specified messages to the folder.
		/// </summary>
		/// <returns>The UIDs of the appended messages, if available; otherwise, <c>null</c>.</returns>
		/// <param name="messages">The array of messages to append to the folder.</param>
		/// <param name="flags">The message flags to use for each of the messages.</param>
		/// <param name="dates">The received dates to use for each of the messages.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
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
		public UniqueId[] Append (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates, CancellationToken cancellationToken)
		{
			if (messages == null)
				throw new ArgumentNullException ("messages");

			for (int i = 0; i < messages.Length; i++) {
				if (messages[i] == null)
					throw new ArgumentException ("One or more of the messages is null.");
			}

			if (flags == null)
				throw new ArgumentNullException ("flags");

			if (dates == null)
				throw new ArgumentNullException ("dates");

			if (messages.Length != flags.Length || messages.Length != dates.Length)
				throw new ArgumentException ("The number of messages, the number of flags, and the number of dates must be equal.");

			CheckState (false, false);

			if (messages.Length == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.MultiAppend) != 0) {
				var ic = QueueMultiAppend (messages, flags, dates, cancellationToken);

				Engine.Wait (ic);

				ProcessResponseCodes (ic, null);

				if (ic.Result != ImapCommandResult.Ok)
					throw new ImapCommandException ("APPEND", ic.Result);

				foreach (var code in ic.RespCodes) {
					if (code.Type == ImapResponseCodeType.AppendUid)
						return code.DestUidSet;
				}

				return null;
			}

			var uids = new List<UniqueId> ();

			for (int i = 0; i < messages.Length; i++) {
				var uid = Append (messages[i], flags[i], dates[i], cancellationToken);
				if (uids != null && uid.HasValue)
					uids.Add (uid.Value);
				else
					uids = null;
			}

			return uids != null ? uids.ToArray () : new UniqueId[0];
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] CopyTo (UniqueId[] uids, IFolder destination)
		{
			return CopyTo (uids, destination, CancellationToken.None);
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		public UniqueId[] CopyTo (UniqueId[] uids, IFolder destination, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, false);

			if (uids.Length == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UIDPLUS extension.");

			var ic = Engine.QueueCommand (cancellationToken, this, "UID COPY %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("COPY", ic.Result);

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.CopyUid)
					return code.DestUidSet;
			}

			return new UniqueId[0];
		}

		/// <summary>
		/// Moves the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the MOVE command, then the MOVE command will be used. Otherwise,
		/// the messages will first be copied to the destination folder, then marked as \Deleted in the
		/// originating folder, and finally expunged. Since the server could disconnect at any point between
		/// those 3 operations, it may be advisable to implement your own logic for moving messages in this
		/// case in order to better handle spontanious server disconnects and other error conditions.</para>
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] MoveTo (UniqueId[] uids, IFolder destination)
		{
			return MoveTo (uids, destination, CancellationToken.None);
		}

		/// <summary>
		/// Moves the specified messages to the destination folder.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the MOVE command, then the MOVE command will be used. Otherwise,
		/// the messages will first be copied to the destination folder, then marked as \Deleted in the
		/// originating folder, and finally expunged. Since the server could disconnect at any point between
		/// those 3 operations, it may be advisable to implement your own logic for moving messages in this
		/// case in order to better handle spontanious server disconnects and other error conditions.</para>
		/// </remarks>
		/// <returns>The UIDs of the messages in the destination folder, if available; otherwise, <c>null</c>.</returns>
		/// <param name="uids">The UIDs of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		public UniqueId[] MoveTo (UniqueId[] uids, IFolder destination, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				var copied = CopyTo (uids, destination, cancellationToken);
				AddFlags (uids, MessageFlags.Deleted, true, cancellationToken);
				Expunge (uids, cancellationToken);
				return copied;
			}

			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, true);

			if (uids.Length == 0)
				return new UniqueId[0];

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UIDPLUS extension.");

			var ic = Engine.QueueCommand (cancellationToken, this, "UID MOVE %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("MOVE", ic.Result);

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.CopyUid)
					return code.DestUidSet;
			}

			return new UniqueId[0];
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void CopyTo (int[] indexes, IFolder destination)
		{
			CopyTo (indexes, destination, CancellationToken.None);
		}

		/// <summary>
		/// Copies the specified messages to the destination folder.
		/// </summary>
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		public void CopyTo (int[] indexes, IFolder destination, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatIndexSet (indexes);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true, false);

			if (indexes.Length == 0)
				return;

			var ic = Engine.QueueCommand (cancellationToken, this, "COPY %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("COPY", ic.Result);
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
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void MoveTo (int[] indexes, IFolder destination)
		{
			MoveTo (indexes, destination, CancellationToken.None);
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
		/// <param name="indexes">The indexes of the messages to copy.</param>
		/// <param name="destination">The destination folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="indexes"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="destination"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
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
		public void MoveTo (int[] indexes, IFolder destination, CancellationToken cancellationToken)
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

			if (indexes.Length == 0)
				return;

			var ic = Engine.QueueCommand (cancellationToken, this, "MOVE %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("MOVE", ic.Result);
		}

		static void FetchSummaryItems (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			var results = (SortedDictionary<int, MessageSummary>) ic.UserData;
			MessageSummary summary;

			if (!results.TryGetValue (index, out summary)) {
				summary = new MessageSummary (index);
				results.Add (index, summary);
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

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value) || value == 0)
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

						var message = MimeMessage.Load (engine.Stream, ic.CancellationToken);

						summary.References = message.References;
					} else {
						summary.Body = ImapUtils.ParseBody (engine, string.Empty, ic.CancellationToken);
					}
					break;
				case "ENVELOPE":
					summary.Envelope = ImapUtils.ParseEnvelope (engine, ic.CancellationToken);
					break;
				case "FLAGS":
					summary.Flags = ImapUtils.ParseFlagsList (engine, ic.CancellationToken);
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
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);
		}

		string FormatSummaryItems (MessageSummaryItems items)
		{
			string query;

			if ((items & MessageSummaryItems.BodyStructure) != 0 && (items & MessageSummaryItems.Body) != 0) {
				// don't query both the BODY and BODYSTRUCTURE, that's just dumb...
				items &= ~MessageSummaryItems.Body;
			}

			// Note: GMail doesn't properly handle aliases (or at least it doesn't handle "FULL")...
			if (!Engine.IsGMail) {
				// first, eliminate the aliases...
				if ((items & MessageSummaryItems.Full) == MessageSummaryItems.Full) {
					items &= ~MessageSummaryItems.Full;
					query = "FULL ";
				} else if ((items & MessageSummaryItems.All) == MessageSummaryItems.All) {
					items &= ~MessageSummaryItems.All;
					query = "ALL ";
				} else if ((items & MessageSummaryItems.Fast) == MessageSummaryItems.Fast) {
					items &= ~MessageSummaryItems.Fast;
					query = "FAST ";
				} else {
					query = string.Empty;
				}
			} else {
				query = string.Empty;
			}

			// now add on any additional summary items...
			if ((items & MessageSummaryItems.UniqueId) != 0)
				query += "UID ";
			if ((items & MessageSummaryItems.Flags) != 0)
				query += "FLAGS ";
			if ((items & MessageSummaryItems.InternalDate) != 0)
				query += "INTERNALDATE ";
			if ((items & MessageSummaryItems.MessageSize) != 0)
				query += "RFC822.SIZE ";
			if ((items & MessageSummaryItems.Envelope) != 0)
				query += "ENVELOPE ";
			if ((items & MessageSummaryItems.BodyStructure) != 0)
				query += "BODYSTRUCTURE ";
			if ((items & MessageSummaryItems.Body) != 0)
				query += "BODY ";

			if ((Engine.Capabilities & ImapCapabilities.CondStore) != 0) {
				if ((items & MessageSummaryItems.ModSeq) != 0)
					query += "MODSEQ ";
			}

			if ((Engine.Capabilities & ImapCapabilities.GMailExt1) != 0) {
				// now for the GMail extension items
				if ((items & MessageSummaryItems.GMailMessageId) != 0)
					query += "X-GM-MSGID ";
				if ((items & MessageSummaryItems.GMailThreadId) != 0)
					query += "X-GM-THRID ";
			}

			if ((items & MessageSummaryItems.References) != 0)
				query += "BODY.PEEK[HEADER.FIELDS (REFERENCES)]";

			return query.TrimEnd ();
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="items">The message summary items to fetch.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (UniqueId[] uids, MessageSummaryItems items)
		{
			return Fetch (uids, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
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
		public IEnumerable<MessageSummary> Fetch (UniqueId[] uids, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			var query = FormatSummaryItems (items);
			var set = ImapUtils.FormatUidSet (uids);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			if (uids.Length == 0)
				return new MessageSummary[0];

			var command = string.Format ("UID FETCH {0} ({1})\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="Vanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (UniqueId[] uids, ulong modseq, MessageSummaryItems items)
		{
			return Fetch (uids, modseq, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="Vanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
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
		public IEnumerable<MessageSummary> Fetch (UniqueId[] uids, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			var query = FormatSummaryItems (items);
			var set = ImapUtils.FormatUidSet (uids);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (uids.Length == 0)
				return new MessageSummary[0];

			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var command = string.Format ("UID FETCH {0} ({1}) (CHANGEDSINCE {2}{3})\r\n", set, query, modseq, vanished);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two UIDs, inclusive.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID, or <c>null</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="min"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (UniqueId min, UniqueId? max, MessageSummaryItems items)
		{
			return Fetch (min, max, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two UIDs, inclusive.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID, or <c>null</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="min"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
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
		public IEnumerable<MessageSummary> Fetch (UniqueId min, UniqueId? max, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			if (min.Id == 0)
				throw new ArgumentException ("The minimum uid is invalid.", "min");

			var query = FormatSummaryItems (items);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			var maxValue = max.HasValue ? max.Value.Id.ToString () : "*";
			var command = string.Format ("UID FETCH {0}:{1} ({2})\r\n", min.Id, maxValue, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two UIDs (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="Vanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="min"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (UniqueId min, UniqueId? max, ulong modseq, MessageSummaryItems items)
		{
			return Fetch (min, max, modseq, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two UIDs (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <remarks>
		/// <para>If the IMAP server supports the QRESYNC extension and the application has
		/// enabled this feature via <see cref="ImapClient.EnableQuickResync(CancellationToken)"/>,
		/// then this method will emit <see cref="Vanished"/> events for messages that have vanished
		/// since the specified mod-sequence value.</para>
		/// </remarks>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum UID.</param>
		/// <param name="max">The maximum UID.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="min"/> is invalid.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="items"/> is empty.
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
		public IEnumerable<MessageSummary> Fetch (UniqueId min, UniqueId? max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			if (min.Id == 0)
				throw new ArgumentException ("The minimum uid is invalid.", "min");

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			var query = FormatSummaryItems (items);
			var maxValue = max.HasValue ? max.Value.Id.ToString () : "*";
			var vanished = Engine.QResyncEnabled ? " VANISHED" : string.Empty;
			var command = string.Format ("UID FETCH {0}:{1} ({2}) (CHANGEDSINCE {3}{4})\r\n", min.Id, maxValue, query, modseq, vanished);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="items">The message summary items to fetch.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (int[] indexes, MessageSummaryItems items)
		{
			return Fetch (indexes, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes.
		/// </summary>
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
		public IEnumerable<MessageSummary> Fetch (int[] indexes, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatIndexSet (indexes);
			var query = FormatSummaryItems (items);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			if (indexes.Length == 0)
				return new MessageSummary[0];

			var command = string.Format ("FETCH {0} ({1})\r\n", set, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (int[] indexes, ulong modseq, MessageSummaryItems items)
		{
			return Fetch (indexes, modseq, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the specified message indexes that have a higher mod-sequence value than the one specified.
		/// </summary>
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
		public IEnumerable<MessageSummary> Fetch (int[] indexes, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatIndexSet (indexes);
			var query = FormatSummaryItems (items);

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			if (indexes.Length == 0)
				return new MessageSummary[0];

			var command = string.Format ("FETCH {0} ({1}) (CHANGEDSINCE {2})\r\n", set, query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="items">The message summary items to fetch.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (int min, int max, MessageSummaryItems items)
		{
			return Fetch (min, max, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes, inclusive.
		/// </summary>
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
		public IEnumerable<MessageSummary> Fetch (int min, int max, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			if (min < 0 || min >= Count)
				throw new ArgumentOutOfRangeException ("min");

			if ((max != -1 && max < min) || max >= Count)
				throw new ArgumentOutOfRangeException ("max");

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			CheckState (true, false);

			var query = FormatSummaryItems (items);
			var maxValue = max != -1 ? (max + 1).ToString () : "*";
			var command = string.Format ("FETCH {0}:{1} ({2})\r\n", min + 1, maxValue, query);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="min">The minimum index.</param>
		/// <param name="max">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="items">The message summary items to fetch.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public IEnumerable<MessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items)
		{
			return Fetch (min, max, modseq, items, CancellationToken.None);
		}

		/// <summary>
		/// Fetches the message summaries for the messages between the two indexes (inclusive) that have a higher mod-sequence value than the one specified.
		/// </summary>
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
		public IEnumerable<MessageSummary> Fetch (int min, int max, ulong modseq, MessageSummaryItems items, CancellationToken cancellationToken)
		{
			if (min < 0 || min >= Count)
				throw new ArgumentOutOfRangeException ("min");

			if ((max != -1 && max < min) || max >= Count)
				throw new ArgumentOutOfRangeException ("max");

			if (items == MessageSummaryItems.None)
				throw new ArgumentOutOfRangeException ("items");

			if (!SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, false);

			var query = FormatSummaryItems (items);
			var maxValue = max != -1 ? (max + 1).ToString () : "*";
			var command = string.Format ("FETCH {0}:{1} ({2}) (CHANGEDSINCE {3})\r\n", min + 1, maxValue, query, modseq);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchSummaryItems);
			ic.UserData = results;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		static void FetchMessageBody (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var streams = (Dictionary<string, Stream>) ic.UserData;
			var token = engine.ReadToken (ic.CancellationToken);
			var args = new MessageFlagsChangedEventArgs (index);
			var buf = new byte[4096];
			bool emit = false;
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

					if (token.Type != ImapTokenType.Literal)
						throw ImapEngine.UnexpectedToken (token, false);

					stream = new MemoryBlockStream ();

					ic.CancellationToken.ThrowIfCancellationRequested ();
					while ((nread = engine.Stream.Read (buf, 0, buf.Length)) > 0) {
						ic.CancellationToken.ThrowIfCancellationRequested ();
						stream.Write (buf, 0, nread);
					}

					streams[specifier] = stream;
					stream.Position = 0;
					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					args.UniqueId = new UniqueId (uid);
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

					args.ModSeq = modseq;
					break;
				case "FLAGS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message flags.
					args.Flags = ImapUtils.ParseFlagsList (engine, ic.CancellationToken);
					emit = true;
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			if (emit)
				ic.Folder.OnFlagsChanged (args);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public MimeMessage GetMessage (UniqueId uid)
		{
			return GetMessage (uid, CancellationToken.None);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
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
		public MimeMessage GetMessage (UniqueId uid, CancellationToken cancellationToken)
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
				throw new ImapCommandException ("FETCH", ic.Result);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return MimeMessage.Load (stream, cancellationToken);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="index">The index of the message.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public MimeMessage GetMessage (int index)
		{
			return GetMessage (index, CancellationToken.None);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
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
		public MimeMessage GetMessage (int index, CancellationToken cancellationToken)
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
				throw new ImapCommandException ("FETCH", ic.Result);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return MimeMessage.Load (stream, cancellationToken);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
		/// <returns>The body part.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="part">The body part.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public MimeEntity GetBodyPart (UniqueId uid, BodyPart part)
		{
			return GetBodyPart (uid, part, CancellationToken.None);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
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
		public MimeEntity GetBodyPart (UniqueId uid, BodyPart part, CancellationToken cancellationToken)
		{
			if (uid.Id == 0)
				throw new ArgumentException ("The uid is invalid.", "uid");

			if (part == null)
				throw new ArgumentNullException ("part");

			CheckState (true, false);

			var tags = new string[2];

			if (part.PartSpecifier.Length > 0) {
				tags[0] = part.PartSpecifier + ".MIME";
				tags[1] = part.PartSpecifier;
			} else {
				tags[0] = "HEADER";
				tags[1] = "TEXT";
			}

			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}] BODY.PEEK[{2}])\r\n", uid.Id, tags[0], tags[1]);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			var chained = new ChainedStream ();

			foreach (var tag in tags) {
				if (!streams.TryGetValue (tag, out stream))
					return null;

				chained.Add (stream);
			}

			var entity = MimeEntity.Load (chained, cancellationToken);

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
		/// <returns>The body part.</returns>
		/// <param name="index">The index of the message.</param>
		/// <param name="part">The body part.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public MimeEntity GetBodyPart (int index, BodyPart part)
		{
			return GetBodyPart (index, part, CancellationToken.None);
		}

		/// <summary>
		/// Gets the specified body part.
		/// </summary>
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
		public MimeEntity GetBodyPart (int index, BodyPart part, CancellationToken cancellationToken)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			if (part == null)
				throw new ArgumentNullException ("part");

			CheckState (true, false);

			var tags = new string[2];

			if (part.PartSpecifier.Length > 0) {
				tags[0] = part.PartSpecifier + ".MIME";
				tags[1] = part.PartSpecifier;
			} else {
				tags[0] = "HEADER";
				tags[1] = "TEXT";
			}

			var command = string.Format ("UID FETCH {0} (BODY.PEEK[{1}] BODY.PEEK[{2}])\r\n", index + 1, tags[0], tags[1]);
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			var chained = new ChainedStream ();

			foreach (var tag in tags) {
				if (!streams.TryGetValue (tag, out stream))
					return null;

				chained.Add (stream);
			}

			var entity = MimeEntity.Load (chained, cancellationToken);

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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public Stream GetStream (UniqueId uid, int offset, int count)
		{
			return GetStream (uid, offset, count, CancellationToken.None);
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
		public Stream GetStream (UniqueId uid, int offset, int count, CancellationToken cancellationToken)
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
				throw new ImapCommandException ("FETCH", ic.Result);

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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public Stream GetStream (int index, int offset, int count)
		{
			return GetStream (index, offset, count, CancellationToken.None);
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
		public Stream GetStream (int index, int offset, int count, CancellationToken cancellationToken)
		{
			if (index < 0)
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
				throw new ImapCommandException ("FETCH", ic.Result);

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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public Stream GetStream (UniqueId uid, BodyPart part, int offset, int count)
		{
			return GetStream (uid, part, offset, count, CancellationToken.None);
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
		public Stream GetStream (UniqueId uid, BodyPart part, int offset, int count, CancellationToken cancellationToken)
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

			var ic = new ImapCommand (Engine, cancellationToken, this, "UID FETCH %u (BODY.PEEK[%s]<%d.%d>)\r\n", uid.Id, part.PartSpecifier, offset, count);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public Stream GetStream (int index, BodyPart part, int offset, int count)
		{
			return GetStream (index, part, offset, count, CancellationToken.None);
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
		public Stream GetStream (int index, BodyPart part, int offset, int count, CancellationToken cancellationToken)
		{
			if (index < 0)
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

			var ic = new ImapCommand (Engine, cancellationToken, this, "FETCH %d (BODY.PEEK[%s]<%d.%d>)\r\n", index + 1, part.PartSpecifier, offset, count);
			var streams = new Dictionary<string, Stream> ();
			Stream stream;

			ic.RegisterUntaggedHandler ("FETCH", FetchMessageBody);
			ic.UserData = streams;

			Engine.QueueCommand (ic);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			if (!streams.TryGetValue (string.Empty, out stream))
				return null;

			return stream;
		}

		UniqueId[] ModifyFlags (UniqueId[] uids, ulong? modseq, MessageFlags flags, string action, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			var set = ImapUtils.FormatUidSet (uids);

			if (modseq.HasValue && !SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (uids.Length == 0)
				return new UniqueId[0];

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var format = string.Format ("UID STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STORE", ic.Result);

			if (modseq.HasValue) {
				foreach (var code in ic.RespCodes) {
					if (code.Type != ImapResponseCodeType.Modified)
						continue;

					return code.DestUidSet;
				}
			}

			return new UniqueId[0];
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void AddFlags (UniqueId[] uids, MessageFlags flags, bool silent)
		{
			AddFlags (uids, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		public void AddFlags (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, null, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void RemoveFlags (UniqueId[] uids, MessageFlags flags, bool silent)
		{
			RemoveFlags (uids, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		public void RemoveFlags (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, null, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void SetFlags (UniqueId[] uids, MessageFlags flags, bool silent)
		{
			SetFlags (uids, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		public void SetFlags (UniqueId[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, null, flags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] AddFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent)
		{
			return AddFlags (uids, modseq, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		public UniqueId[] AddFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			return ModifyFlags (uids, modseq, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] RemoveFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent)
		{
			return RemoveFlags (uids, modseq, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		public UniqueId[] RemoveFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			return ModifyFlags (uids, modseq, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] SetFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent)
		{
			return SetFlags (uids, modseq, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The unique IDs of the messages that were not updated.</returns>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains at least one invalid uid.
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
		public UniqueId[] SetFlags (UniqueId[] uids, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			return ModifyFlags (uids, modseq, flags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		int[] ModifyFlags (int[] indexes, ulong? modseq, MessageFlags flags, string action, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			var set = ImapUtils.FormatIndexSet (indexes);

			if (modseq.HasValue && !SupportsModSeq)
				throw new NotSupportedException ("The ImapFolder does not support mod-sequences.");

			CheckState (true, true);

			if (indexes.Length == 0)
				return new int[0];

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var format = string.Format ("STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STORE", ic.Result);

			if (modseq.HasValue) {
				foreach (var code in ic.RespCodes) {
					if (code.Type != ImapResponseCodeType.Modified)
						continue;

					var unmodified = new int[code.DestUidSet.Length];
					for (int i = 0; i < unmodified.Length; i++)
						unmodified[i] = (int) (code.DestUidSet[i].Id - 1);

					return unmodified;
				}
			}

			return new int[0];
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void AddFlags (int[] indexes, MessageFlags flags, bool silent)
		{
			AddFlags (indexes, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		public void AddFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, null, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void RemoveFlags (int[] indexes, MessageFlags flags, bool silent)
		{
			RemoveFlags (indexes, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		public void RemoveFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, null, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public void SetFlags (int[] indexes, MessageFlags flags, bool silent)
		{
			SetFlags (indexes, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		public void SetFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, null, flags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public int[] AddFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent)
		{
			return AddFlags (indexes, modseq, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		public int[] AddFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			return ModifyFlags (indexes, modseq, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public int[] RemoveFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent)
		{
			return RemoveFlags (indexes, modseq, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		public int[] RemoveFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			return ModifyFlags (indexes, modseq, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public int[] SetFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent)
		{
			return SetFlags (indexes, modseq, flags, silent, CancellationToken.None);
		}

		/// <summary>
		/// Sets the flags of the specified messages only if their mod-sequence value is less than the specified value.
		/// </summary>
		/// <returns>The indexes of the messages that were not updated.</returns>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="modseq">The mod-sequence value.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="MessageFlagsChanged"/> events will be emitted.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="indexes"/> contains at least one invalid index.
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
		public int[] SetFlags (int[] indexes, ulong modseq, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			return ModifyFlags (indexes, modseq, flags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		static bool IsAscii (string text)
		{
			for (int i = 0; i < text.Length; i++) {
				if (text[i] > 127)
					return false;
			}

			return true;
		}

		void BuildQuery (StringBuilder builder, SearchQuery query, List<string> args, bool parens, ref bool ascii)
		{
			TextSearchQuery text = null;
			NumericSearchQuery numeric;
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
				builder.AppendFormat ("SINCE {0}", date.Date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture));
				break;
			case SearchTerm.DeliveredBefore:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("BEFORE {0}", date.Date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture));
				break;
			case SearchTerm.DeliveredOn:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("ON {0}", date.Date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture));
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
				builder.AppendFormat ("SENTSINCE {0}", date.Date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture));
				break;
			case SearchTerm.SentBefore:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTBEFORE {0}", date.Date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture));
				break;
			case SearchTerm.SentOn:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTON {0}", date.Date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture));
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

		static string BuildSortOrder (OrderBy[] orderBy)
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			for (int i = 0; i < orderBy.Length; i++) {
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

		static void SearchMatches (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var matches = new HashSet<UniqueId> ();
			UniqueId[] uids;
			ImapToken token;
			uint uid;

			do {
				token = engine.PeekToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
					throw ImapEngine.UnexpectedToken (token, false);

				matches.Add (new UniqueId (uid));
			} while (true);

			uids = matches.ToArray ();
			Array.Sort (uids);

			ic.UserData = uids;
		}

		static void ESearchMatches (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var token = engine.ReadToken (ic.CancellationToken);
			UniqueId[] uids = null;
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
				var indexes = new int[uids.Length];
				for (int i = 0; i < uids.Length; i++)
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
		/// The returned array of unique identifiers can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] Search (SearchQuery query)
		{
			return Search (query, CancellationToken.None);
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
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
		public UniqueId[] Search (SearchQuery query, CancellationToken cancellationToken)
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

			if (args.Count > 0)
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
				throw new ImapCommandException ("SEARCH", ic.Result);

			return (UniqueId[]) ic.UserData;
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] Search (SearchQuery query, OrderBy[] orderBy)
		{
			return Search (query, orderBy, CancellationToken.None);
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
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
		public UniqueId[] Search (SearchQuery query, OrderBy[] orderBy, CancellationToken cancellationToken)
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			if (orderBy.Length == 0)
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
				throw new ImapCommandException ("SORT", ic.Result);

			return (UniqueId[]) ic.UserData;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains one or more invalid UIDs.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] Search (UniqueId[] uids, SearchQuery query)
		{
			return Search (uids, query, CancellationToken.None);
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
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
		/// <paramref name="uids"/> contains one or more invalid UIDs.
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
		public UniqueId[] Search (UniqueId[] uids, SearchQuery query, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true, false);

			if (uids.Length == 0)
				return new UniqueId[0];

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				command += "RETURN () ";

			if (args.Count > 0)
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
				throw new ImapCommandException ("SEARCH", ic.Result);

			return (UniqueId[]) ic.UserData;
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> contains one or more invalid UIDs.</para>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public UniqueId[] Search (UniqueId[] uids, SearchQuery query, OrderBy[] orderBy)
		{
			return Search (uids, query, orderBy, CancellationToken.None);
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query,
		/// returning them in the preferred sort order.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
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
		/// <para><paramref name="uids"/> contains one or more invalid UIDs.</para>
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
		public UniqueId[] Search (UniqueId[] uids, SearchQuery query, OrderBy[] orderBy, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException ("query");

			if (orderBy == null)
				throw new ArgumentNullException ("orderBy");

			if (orderBy.Length == 0)
				throw new ArgumentException ("No sort order provided.", "orderBy");

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			if (uids.Length == 0)
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
				throw new ImapCommandException ("SORT", ic.Result);

			return (UniqueId[]) ic.UserData;
		}

		static void ThreadMatches (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			ic.UserData = ImapUtils.ParseThreads (engine, ic.CancellationToken);
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public MessageThread[] Thread (ThreadingAlgorithm algorithm, SearchQuery query)
		{
			return Thread (algorithm, query, CancellationToken.None);
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
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
		public MessageThread[] Thread (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken)
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
				throw new ImapCommandException ("THREAD", ic.Result);

			return (MessageThread[]) ic.UserData;
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> contains one or more invalid UIDs.
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
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public MessageThread[] Thread (UniqueId[] uids, ThreadingAlgorithm algorithm, SearchQuery query)
		{
			return Thread (uids, algorithm, query, CancellationToken.None);
		}

		/// <summary>
		/// Threads the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with <see cref="IFolder.GetMessage(UniqueId,CancellationToken)"/>.
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
		/// <paramref name="uids"/> contains one or more invalid UIDs.
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
		public MessageThread[] Thread (UniqueId[] uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken)
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
				throw new ImapCommandException ("THREAD", ic.Result);

			return (MessageThread[]) ic.UserData;
		}

		#region Untagged response handlers called by ImapEngine

		internal void OnExists (int count)
		{
			if (Count == count)
				return;

			Count = count;

			OnCountChanged ();
		}

		internal void OnExpunge (int index)
		{
			var handler = Expunged;

			if (handler != null)
				handler (this, new MessageEventArgs (index));
		}

		internal void OnFetch (ImapEngine engine, int index, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			var args = new MessageFlagsChangedEventArgs (index);

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

					args.ModSeq = modseq;
					break;
				case "UID":
					token = engine.ReadToken (cancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					args.UniqueId = new UniqueId (uid);
					break;
				case "FLAGS":
					args.Flags = ImapUtils.ParseFlagsList (engine, cancellationToken);
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			OnFlagsChanged (args);
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
			bool earlier = false;
			UniqueId[] vanished;

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

			var handler = Vanished;

			if (handler != null)
				handler (this, new MessagesVanishedEventArgs (vanished, earlier));
		}

		internal void UpdateFirstUnread (int index)
		{
			FirstUnread = index;
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

		/// <summary>
		/// Occurs when the folder is deleted.
		/// </summary>
		public event EventHandler<EventArgs> Deleted;

		void OnDeleted ()
		{
			var handler = Deleted;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when the folder is renamed.
		/// </summary>
		public event EventHandler<FolderRenamedEventArgs> Renamed;

		void OnRenamed (string oldName, string newName)
		{
			var handler = Renamed;

			if (handler != null)
				handler (this, new FolderRenamedEventArgs (oldName, newName));
		}

		/// <summary>
		/// Occurs when the folder is subscribed.
		/// </summary>
		public event EventHandler<EventArgs> Subscribed;

		void OnSubscribed ()
		{
			var handler = Subscribed;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when the folder is unsubscribed.
		/// </summary>
		public event EventHandler<EventArgs> Unsubscribed;

		void OnUnsubscribed ()
		{
			var handler = Unsubscribed;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when a message is expunged from the folder.
		/// </summary>
		public event EventHandler<MessageEventArgs> Expunged;

		/// <summary>
		/// Occurs when a message vanishes from the folder.
		/// </summary>
		public event EventHandler<MessagesVanishedEventArgs> Vanished;

		/// <summary>
		/// Occurs when flags changed on a message.
		/// </summary>
		public event EventHandler<MessageFlagsChangedEventArgs> MessageFlagsChanged;

		void OnFlagsChanged (MessageFlagsChangedEventArgs args)
		{
			var handler = MessageFlagsChanged;

			if (handler != null)
				handler (this, args);
		}

		/// <summary>
		/// Occurs when the UID validity changes.
		/// </summary>
		public event EventHandler<EventArgs> UidValidityChanged;

		void OnUidValidityChanged ()
		{
			var handler = UidValidityChanged;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when the message count changes.
		/// </summary>
		public event EventHandler<EventArgs> CountChanged;

		void OnCountChanged ()
		{
			var handler = CountChanged;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		/// <summary>
		/// Occurs when the recent message count changes.
		/// </summary>
		public event EventHandler<EventArgs> RecentChanged;

		void OnRecentChanged ()
		{
			var handler = RecentChanged;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		#endregion

		#region IEnumerable<MimeMessage> implementation

		/// <summary>
		/// Gets an enumerator for the messages in the folder.
		/// </summary>
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
		public IEnumerator<MimeMessage> GetEnumerator ()
		{
			CheckState (true, false);

			for (int i = 0; i < Count; i++)
				yield return GetMessage (i, CancellationToken.None);

			yield break;
		}

		/// <summary>
		/// Gets an enumerator for the messages in the folder.
		/// </summary>
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
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return GetEnumerator ();
		}

		#endregion

		/// <summary>
		/// Returns a <see cref="System.String"/> that represents the current <see cref="MailKit.Net.Imap.ImapFolder"/>.
		/// </summary>
		/// <returns>A <see cref="System.String"/> that represents the current <see cref="MailKit.Net.Imap.ImapFolder"/>.</returns>
		public override string ToString ()
		{
			return FullName;
		}
	}
}
