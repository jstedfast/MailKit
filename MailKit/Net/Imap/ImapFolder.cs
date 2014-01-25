//
// ImapFolder.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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
using System.Collections;
using System.Globalization;
using System.Collections.Generic;

using MimeKit;
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

		void CheckState (bool open)
		{
			if (Engine.IsDisposed)
				throw new ObjectDisposedException ("ImapClient");

			if (!Engine.IsConnected)
				throw new InvalidOperationException ("The ImapClient is not connected.");

			if (Engine.State < ImapEngineState.Authenticated)
				throw new InvalidOperationException ("The ImapClient is not authenticated.");

			if (open && !IsOpen)
				throw new InvalidOperationException ("The folder is not currently open.");
		}

		static void ValidateUid (string uid)
		{
			uint value;

			if (uid == null)
				throw new ArgumentNullException ("uid");

			if (!uint.TryParse (uid, out value) || value == 0)
				throw new ArgumentException ("The uid is invalid.");
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
					UidNext = code.Uid.ToString ();
					break;
				case ImapResponseCodeType.UidValidity:
					UidValidity = code.UidValidity.ToString ();
					break;
				case ImapResponseCodeType.Unseen:
					FirstUnread = code.Index;
					break;
				case ImapResponseCodeType.HighestModSeq:
					HighestModSeq = code.HighestModSeq;
					break;
				case ImapResponseCodeType.NoModSeq:
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
			get; internal set;
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
		/// UIDs are only valid so long as the UID validity value remains unchanged.
		/// </remarks>
		/// <value>The UID validity.</value>
		public string UidValidity {
			get; private set;
		}

		/// <summary>
		/// Gets the UID that the next message that is added to the folder will be assigned.
		/// </summary>
		/// <value>The next UID.</value>
		public string UidNext {
			get; private set;
		}

		/// <summary>
		/// Gets the index of the first unread message in the folder.
		/// </summary>
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

			CheckState (false);

			if (IsOpen && Access == access)
				return access;

			string format = access == FolderAccess.ReadOnly ? "EXAMINE %F\r\n" : "SELECT %F\r\n";
			var ic = Engine.QueueCommand (cancellationToken, this, format, this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic.Result);

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			return Access;
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
			CheckState (true);

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
			Engine.Selected = null;
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
		public IFolder Create (string name, bool isMessageFolder, CancellationToken cancellationToken)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (string.IsNullOrEmpty (name) || name.IndexOf (DirectorySeparator) != -1)
				throw new ArgumentException ("The name is not a legal folder name.", "name");

			CheckState (false);

			var fullName = FullName + DirectorySeparator + name;
			var encodedName = ImapEncoding.Encode (fullName);
			var list = new List<ImapFolder> ();
			var createName = encodedName;

			if (!isMessageFolder)
				createName += DirectorySeparator;

			var ic = Engine.QueueCommand (cancellationToken, null, "CREATE %S\r\n", createName);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("CREATE", ic.Result);

			ic = Engine.QueueCommand (cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.HandleUntaggedListResponse);
			ic.UserData = list;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("LIST", ic.Result);

			return list.Count > 0 ? list[0] : null;
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

			CheckState (false);

			var encodedName = ImapEncoding.Encode (parent.FullName + parent.DirectorySeparator + name);
			var ic = Engine.QueueCommand (cancellationToken, null, "RENAME %F %S\r\n", this, encodedName);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("RENAME", ic.Result);

			// FIXME: what to do about all child folders? :-(

			var oldFullName = FullName;

			Name = GetBaseName (name, parent.DirectorySeparator);
			EncodedName = encodedName;
			ParentFolder = parent;
			FullName = name;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Engine.Selected = null;
			}

			Engine.FolderCache.Remove (EncodedName);
			Engine.FolderCache[encodedName] = this;

			OnRenamed (oldFullName, FullName);
		}

		/// <summary>
		/// Deletes the folder on the IMAP server.
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
		public void Delete (CancellationToken cancellationToken)
		{
			var ic = Engine.QueueCommand (cancellationToken, null, "DELETE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("DELETE", ic.Result);

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Engine.Selected = null;
			}

			Attributes |= FolderAttributes.NonExistent;
			Exists = false;
			OnDeleted ();
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
			CheckState (false);

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
			CheckState (false);

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
			CheckState (false);

			var pattern = EncodedName.Length > 0 ? EncodedName + DirectorySeparator + "%" : "%";
			var command = subscribedOnly ? "LSUB" : "LIST";

			var ic = Engine.QueueCommand (cancellationToken, null, "%s \"\" %S\r\n", command, pattern);
			var list = new List<ImapFolder> ();

			ic.RegisterUntaggedHandler (command, ImapUtils.HandleUntaggedListResponse);
			ic.UserData = list;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException (subscribedOnly ? "LSUB" : "LIST", ic.Result);

			return list;
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
			CheckState (true);

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
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the STATUS command.
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
		public void Status (StatusItems items, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Status) != 0)
				throw new NotSupportedException ();

			CheckState (false);

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
		public void Expunge (CancellationToken cancellationToken)
		{
			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "EXPUNGE\r\n");

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("EXPUNGE", ic.Result);
		}

		/// <summary>
		/// Expunges the specified uids, permanently removing them from the folder.
		/// </summary>
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
		public void Expunge (string[] uids, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "UID EXPUNGE %s\r\n", set);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("EXPUNGE", ic.Result);
		}

		ImapCommand QueueAppend (MimeMessage message, MessageFlags flags, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			string format = string.Empty;

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) != 0)
				format = "UID ";

			format += "APPEND %F";

			if (date.HasValue)
				format += " \"" + ImapUtils.FormatInternalDate (date.Value) + "\"";

			format += " " + ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			format += " %L\r\n";

			return Engine.QueueCommand (cancellationToken, null, format, this, message);
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
		public string Append (MimeMessage message, MessageFlags flags, CancellationToken cancellationToken)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			CheckState (false);

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
		public string Append (MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			CheckState (false);

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

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) != 0)
				format = "UID ";

			format += "APPEND %F";
			args.Add (this);

			for (int i = 0; i < messages.Length; i++) {
				if (dates != null)
					format += " \"" + ImapUtils.FormatInternalDate (dates[i]) + "\"";

				format += " " + ImapUtils.FormatFlagsList (flags[i] & AcceptedFlags);
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
		public string[] Append (MimeMessage[] messages, MessageFlags[] flags, CancellationToken cancellationToken)
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

			CheckState (false);

			if (messages.Length == 0)
				return new string[0];

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

			var uids = new List<string> ();

			for (int i = 0; i < messages.Length; i++) {
				var uid = Append (messages[i], flags[i], cancellationToken);
				if (uids != null && uid != null)
					uids.Add (uid);
				else
					uids = null;
			}

			return uids != null ? uids.ToArray () : null;
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
		public string[] Append (MimeMessage[] messages, MessageFlags[] flags, DateTimeOffset[] dates, CancellationToken cancellationToken)
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

			CheckState (false);

			if (messages.Length == 0)
				return new string[0];

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

			var uids = new List<string> ();

			for (int i = 0; i < messages.Length; i++) {
				var uid = Append (messages[i], flags[i], dates[i], cancellationToken);
				if (uids != null && uid != null)
					uids.Add (uid);
				else
					uids = null;
			}

			return uids != null ? uids.ToArray () : null;
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
		public string[] CopyTo (string[] uids, IFolder destination, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true);

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

			return null;
		}

		/// <summary>
		/// Moves the specified messages to the destination folder.
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
		public string[] MoveTo (string[] uids, IFolder destination, CancellationToken cancellationToken)
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

			CheckState (true);

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

			return null;
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
			var set = ImapUtils.FormatUidSet (indexes);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "COPY %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("COPY", ic.Result);
		}

		/// <summary>
		/// Moves the specified messages to the destination folder.
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
		public void MoveTo (int[] indexes, IFolder destination, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				CopyTo (indexes, destination, cancellationToken);
				AddFlags (indexes, MessageFlags.Deleted, true, cancellationToken);
				return;
			}

			var set = ImapUtils.FormatUidSet (indexes);

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder) || ((ImapFolder) destination).Engine != Engine)
				throw new ArgumentException ("The destination folder does not belong to this ImapClient.", "destination");

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "MOVE %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("MOVE", ic.Result);
		}

		static void FetchAttributes (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
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
				case "BODY":
					summary.Body = ImapUtils.ParseBody (engine, ic.CancellationToken);
					break;
				case "ENVELOPE":
					summary.Envelope = ImapUtils.ParseEnvelope (engine, ic.CancellationToken);
					break;
				case "FLAGS":
					summary.Flags = ImapUtils.ParseFlagsList (engine, ic.CancellationToken);
					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out value) || value == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					summary.Uid = value.ToString ();
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);
		}

		static string FormatAttributeQuery (MessageAttributes attrs)
		{
			string query;

			if ((attrs & MessageAttributes.BodyStructure) != 0 && (attrs & MessageAttributes.Body) != 0) {
				// don't query both the BODY and BODYSTRUCTURE, that's just dumb...
				attrs &= ~MessageAttributes.Body;
			}

			// first, eliminate the aliases...
			if ((attrs & MessageAttributes.Full) == MessageAttributes.Full) {
				attrs &= ~MessageAttributes.Full;
				query = "FULL ";
			} else if ((attrs & MessageAttributes.All) == MessageAttributes.All) {
				attrs &= ~MessageAttributes.All;
				query = "ALL ";
			} else if ((attrs & MessageAttributes.Fast) == MessageAttributes.Fast) {
				attrs &= ~MessageAttributes.Fast;
				query = "FAST ";
			} else {
				query = string.Empty;
			}

			// now add on any additional attributes...
			if ((attrs & MessageAttributes.Uid) != 0)
				query += "UID ";
			if ((attrs & MessageAttributes.Flags) != 0)
				query += "FLAGS ";
			if ((attrs & MessageAttributes.InternalDate) != 0)
				query += "INTERNALDATE ";
			if ((attrs & MessageAttributes.MessageSize) != 0)
				query += "RFC822.SIZE ";
			if ((attrs & MessageAttributes.Envelope) != 0)
				query += "ENVELOPE ";
			if ((attrs & MessageAttributes.BodyStructure) != 0)
				query += "BODYSTRUCTURE ";
			if ((attrs & MessageAttributes.Body) != 0)
				query += "BODY ";

			return query.TrimEnd ();
		}

		/// <summary>
		/// Fetches the message summaries for the specified message UIDs.
		/// </summary>
		/// <returns>An enumeration of summaries for the requested messages.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <param name="attributes">The message attributes to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="attributes"/> is empty.
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
		public IEnumerable<MessageSummary> Fetch (string[] uids, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			var query = FormatAttributeQuery (attributes);
			var set = ImapUtils.FormatUidSet (uids);

			if (attributes == MessageAttributes.None)
				throw new ArgumentOutOfRangeException ("attributes");

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "UID FETCH %s (%s)\r\n", set, query);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchAttributes);
			ic.UserData = results;

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
		/// <param name="attributes">The message attributes to fetch.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="attributes"/> is empty.
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
		public IEnumerable<MessageSummary> Fetch (int[] indexes, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			var query = FormatAttributeQuery (attributes);
			var set = ImapUtils.FormatUidSet (indexes);

			if (attributes == MessageAttributes.None)
				throw new ArgumentOutOfRangeException ("attributes");

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "FETCH %s (%s)\r\n", set, query);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchAttributes);
			ic.UserData = results;

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
		/// <param name="minIndex">The minimum index.</param>
		/// <param name="maxIndex">The maximum index, or <c>-1</c> to specify no upper bound.</param>
		/// <param name="attributes">The message attributes.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <para><paramref name="minIndex"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="maxIndex"/> is out of range.</para>
		/// <para>-or-</para>
		/// <para><paramref name="attributes"/> is empty.</para>
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
		public IEnumerable<MessageSummary> Fetch (int minIndex, int maxIndex, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			if (minIndex < 0 || minIndex >= Count)
				throw new ArgumentOutOfRangeException ("minIndex");

			if ((maxIndex != -1 && maxIndex < minIndex) || maxIndex >= Count)
				throw new ArgumentOutOfRangeException ("maxIndex");

			if (attributes == MessageAttributes.None)
				throw new ArgumentOutOfRangeException ("attributes");

			var set = string.Format ("{0}:{1}", minIndex + 1, maxIndex != -1 ? (maxIndex + 1).ToString () : "*");
			var query = FormatAttributeQuery (attributes);

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "FETCH %s (%s)\r\n", set, query);
			var results = new SortedDictionary<int, MessageSummary> ();
			ic.RegisterUntaggedHandler ("FETCH", FetchAttributes);
			ic.UserData = results;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return results.Values;
		}

		static void FetchMessage (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			do {
				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (token, false);

				var atom = (string) token.Value;
				uint uid;

				switch (atom) {
				case "BODY":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.OpenBracket)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.CloseBracket)
						throw ImapEngine.UnexpectedToken (token, false);

					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Literal)
						throw ImapEngine.UnexpectedToken (token, false);

					ic.UserData = MimeMessage.Load (engine.Stream, ic.CancellationToken);
					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					break;
				case "FLAGS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message flags.
					var flags = ImapUtils.ParseFlagsList (engine, ic.CancellationToken);

					ic.Folder.OnFlagsChanged (index, flags);
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);
		}

		/// <summary>
		/// Gets the specified message.
		/// </summary>
		/// <returns>The message.</returns>
		/// <param name="uid">The UID of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uid"/> is <c>null</c>.
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
		public MimeMessage GetMessage (string uid, CancellationToken cancellationToken)
		{
			ValidateUid (uid);
			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "UID FETCH %s (BODY.PEEK[])\r\n", uid);
			ic.RegisterUntaggedHandler ("FETCH", FetchMessage);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return (MimeMessage) ic.UserData;
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

			CheckState (true);

			var ic = Engine.QueueCommand (cancellationToken, this, "FETCH %d (BODY.PEEK[])\r\n", index + 1);
			ic.RegisterUntaggedHandler ("FETCH", FetchMessage);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return (MimeMessage) ic.UserData;
		}

		static void FetchModSeq (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var token = engine.ReadToken (ic.CancellationToken);

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

					break;
				case "UID":
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (token, false);

					break;
				case "FLAGS":
					// even though we didn't request this piece of information, the IMAP server
					// may send it if another client has recently modified the message flags.
					var flags = ImapUtils.ParseFlagsList (engine, ic.CancellationToken);

					ic.Folder.OnFlagsChanged (index, flags);
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}
			} while (true);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);
		}

		void ModifyFlags (string[] uids, MessageFlags flags, string action, ulong? modseq, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			var set = ImapUtils.FormatUidSet (uids);

			CheckState (true);

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var format = string.Format ("UID STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format);
			ic.RegisterUntaggedHandler ("FETCH", FetchModSeq);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STORE", ic.Result);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="FlagsChanged"/> events will be emitted.</param>
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
		public void AddFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", null, cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="FlagsChanged"/> events will be emitted.</param>
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
		public void RemoveFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", null, cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <param name="uids">The UIDs of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="FlagsChanged"/> events will be emitted.</param>
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
		public void SetFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, flags, silent ? "FLAGS.SILENT" : "FLAGS", null, cancellationToken);
		}

		void ModifyFlags (int[] indexes, MessageFlags flags, string action, ulong? modseq, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			var set = ImapUtils.FormatUidSet (indexes);

			CheckState (true);

			string @params = string.Empty;
			if (modseq.HasValue)
				@params = string.Format (" (UNCHANGEDSINCE {0})", modseq.Value);

			var format = string.Format ("STORE {0}{1} {2} {3}\r\n", set, @params, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format);
			ic.RegisterUntaggedHandler ("FETCH", FetchModSeq);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STORE", ic.Result);
		}

		/// <summary>
		/// Adds a set of flags to the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to add.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="FlagsChanged"/> events will be emitted.</param>
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
		public void AddFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", null, cancellationToken);
		}

		/// <summary>
		/// Removes a set of flags from the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to remove.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="FlagsChanged"/> events will be emitted.</param>
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
		public void RemoveFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", null, cancellationToken);
		}

		/// <summary>
		/// Sets the flags of the specified messages.
		/// </summary>
		/// <param name="indexes">The indexes of the messages.</param>
		/// <param name="flags">The message flags to set.</param>
		/// <param name="silent">If set to <c>true</c>, no <see cref="FlagsChanged"/> events will be emitted.</param>
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
		public void SetFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, flags, silent ? "FLAGS.SILENT" : "FLAGS", null, cancellationToken);
		}

		void BuildQuery (StringBuilder builder, SearchQuery query, List<string> args)
		{
			NumericSearchQuery numeric;
			HeaderSearchQuery header;
			BinarySearchQuery binary;
			UnarySearchQuery unary;
			DateSearchQuery date;
			TextSearchQuery text;

			if (builder.Length > 0)
				builder.Append (' ');

			switch (query.Term) {
			case SearchTerm.All:
				builder.Append ("ALL");
				break;
			case SearchTerm.And:
				binary = (BinarySearchQuery) query;
				BuildQuery (builder, binary.Left, args);
				BuildQuery (builder, binary.Right, args);
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
			case SearchTerm.New:
				builder.Append ("NEW");
				break;
			case SearchTerm.Not:
				builder.Append ("NOT");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args);
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
			case SearchTerm.NotRecent:
				builder.Append ("OLD");
				break;
			case SearchTerm.NotSeen:
				builder.Append ("UNSEEN");
				break;
			case SearchTerm.Or:
				builder.Append ("OR");
				binary = (BinarySearchQuery) query;
				BuildQuery (builder, binary.Left, args);
				BuildQuery (builder, binary.Right, args);
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
			case SearchTerm.Unkeyword:
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}
		}

		string BuildQueryExpression (SearchQuery query, List<string> args)
		{
			var builder = new StringBuilder ();

			BuildQuery (builder, query, args);

			return builder.ToString ();
		}

		static void SearchMatches (ImapEngine engine, ImapCommand ic, int index, ImapToken tok)
		{
			var uids = (HashSet<uint>) ic.UserData;
			ImapToken token;
			uint uid;

			do {
				token = engine.PeekToken (ic.CancellationToken);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = engine.ReadToken (ic.CancellationToken);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
					throw ImapEngine.UnexpectedToken (token, false);

				uids.Add (uid);
			} while (true);
		}

		/// <summary>
		/// Searches the subset of UIDs in the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of UID strings can be used with <see cref="IFolder.GetMessage(string,CancellationToken)"/>.
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
		public string[] Search (string[] uids, SearchQuery query, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);
			List<string> args = new List<string> ();

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args);
			var command = args.Count > 0 ? "UID SEARCH CHARSET UTF-8 " : "UID SEARCH ";
			var matches = new HashSet<uint> ();

			command += "UID " + set + " " + expr + "\r\n";

			var ic = Engine.QueueCommand (cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);
			ic.UserData = matches;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("SEARCH", ic.Result);

			var array = new uint[matches.Count];
			int i = 0;

			foreach (var match in matches)
				array[i++] = match;

			Array.Sort (array);

			var results = new string[array.Length];
			for (i = 0; i < array.Length; i++)
				results[i] = array[i].ToString ();

			return results;
		}

		/// <summary>
		/// Searches the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of UID strings can be used with <see cref="IFolder.GetMessage(string,CancellationToken)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
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
		public string[] Search (SearchQuery query, CancellationToken cancellationToken)
		{
			List<string> args = new List<string> ();

			if (query == null)
				throw new ArgumentNullException ("query");

			CheckState (true);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args);
			var command = args.Count > 0 ? "UID SEARCH CHARSET UTF-8 " : "UID SEARCH ";
			var matches = new HashSet<uint> ();

			command += expr + "\r\n";

			var ic = Engine.QueueCommand (cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatches);
			ic.UserData = matches;

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("SEARCH", ic.Result);

			var array = new uint[matches.Count];
			int i = 0;

			foreach (var match in matches)
				array[i++] = match;

			Array.Sort (array);

			var results = new string[array.Length];
			for (i = 0; i < array.Length; i++)
				results[i] = array[i].ToString ();

			return results;
		}

		public event EventHandler<EventArgs> Deleted;

		void OnDeleted ()
		{
			var handler = Deleted;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		public event EventHandler<FolderRenamedEventArgs> Renamed;

		void OnRenamed (string oldName, string newName)
		{
			var handler = Renamed;

			if (handler != null)
				handler (this, new FolderRenamedEventArgs (oldName, newName));
		}

		public event EventHandler<EventArgs> Subscribed;

		void OnSubscribed ()
		{
			var handler = Subscribed;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> Unsubscribed;

		void OnUnsubscribed ()
		{
			var handler = Unsubscribed;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		public event EventHandler<MessageEventArgs> Expunged;

		internal void OnExpunged (int index)
		{
			var handler = Expunged;

			if (handler != null)
				handler (this, new MessageEventArgs (index));
		}

		public event EventHandler<FlagsChangedEventArgs> FlagsChanged;

		internal void OnFlagsChanged (int index, MessageFlags flags)
		{
			var handler = FlagsChanged;

			if (handler != null)
				handler (this, new FlagsChangedEventArgs (index, flags));
		}

		public event EventHandler<EventArgs> UidValidityChanged;

		void OnUidValidityChanged ()
		{
			var handler = UidValidityChanged;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

		public event EventHandler<EventArgs> CountChanged;

		void OnCountChanged ()
		{
			var handler = CountChanged;

			if (handler != null)
				handler (this, EventArgs.Empty);
		}

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
			CheckState (true);

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

		internal void UpdateCount (int count)
		{
			if (Count == count)
				return;

			Count = count;

			OnCountChanged ();
		}

		internal void UpdateRecent (int count)
		{
			if (Recent == count)
				return;

			Recent = count;

			OnRecentChanged ();
		}

		internal void UpdateFirstUnread (int index)
		{
			FirstUnread = index;
		}

		internal void UpdateUidNext (string value)
		{
			UidNext = value;
		}

		internal void UpdateUidValidity (string value)
		{
			if (UidValidity == value)
				return;

			UidValidity = value;

			OnUidValidityChanged ();
		}
	}
}
