//
// ImapFolder.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
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
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadMessages"/>
	/// </example>
	/// <example>
	/// <code language="c#" source="Examples\ImapExamples.cs" region="DownloadBodyParts"/>
	/// </example>
	public partial class ImapFolder : MailFolder
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
			Access = FolderAccess.None;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
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

		static Task QResyncFetchAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			return ic.Folder.OnFetchAsync (engine, index, doAsync, ic.CancellationToken);
		}

		async Task<FolderAccess> OpenAsync (FolderAccess access, uint uidValidity, ulong highestModSeq, IList<UniqueId> uids, bool doAsync, CancellationToken cancellationToken)
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
			ic.RegisterUntaggedHandler ("FETCH", QResyncFetchAsync);

			if (access == FolderAccess.ReadWrite) {
				// Note: if the server does not respond with a PERMANENTFLAGS response,
				// then we need to assume all flags are permanent.
				PermanentFlags = SettableFlags | MessageFlags.UserDefined;
			} else {
				PermanentFlags = MessageFlags.None;
			}

			try {
				Engine.QueueCommand (ic);

				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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

			if (ic.Bye)
				return FolderAccess.None;

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			OnOpened ();

			return Access;
		}

		/// <summary>
		/// Open the folder using the requested folder access.
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
			return OpenAsync (access, uidValidity, highestModSeq, uids, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously open the folder using the requested folder access.
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
		public override Task<FolderAccess> OpenAsync (FolderAccess access, uint uidValidity, ulong highestModSeq, IList<UniqueId> uids, CancellationToken cancellationToken = default (CancellationToken))
		{
			return OpenAsync (access, uidValidity, highestModSeq, uids, true, cancellationToken);
		}

		async Task<FolderAccess> OpenAsync (FolderAccess access, bool doAsync, CancellationToken cancellationToken)
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

				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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

			if (ic.Bye)
				return FolderAccess.None;

			Engine.State = ImapEngineState.Selected;
			Engine.Selected = this;

			OnOpened ();

			return Access;
		}

		/// <summary>
		/// Open the folder using the requested folder access.
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
			return OpenAsync (access, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously open the folder using the requested folder access.
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
		public override Task<FolderAccess> OpenAsync (FolderAccess access, CancellationToken cancellationToken = default (CancellationToken))
		{
			return OpenAsync (access, true, cancellationToken);
		}

		async Task CloseAsync (bool expunge, bool doAsync, CancellationToken cancellationToken)
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
				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

				ProcessResponseCodes (ic, null);

				if (ic.Response != ImapCommandResponse.Ok)
					throw ImapCommandException.Create (expunge ? "CLOSE" : "UNSELECT", ic);
			}

			Access = FolderAccess.None;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Engine.Selected = null;
				OnClosed ();
			}
		}

		/// <summary>
		/// Close the folder, optionally expunging the messages marked for deletion.
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
			CloseAsync (expunge, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously close the folder, optionally expunging the messages marked for deletion.
		/// </summary>
		/// <remarks>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task CloseAsync (bool expunge = false, CancellationToken cancellationToken = default (CancellationToken))
		{
			return CloseAsync (expunge, true, cancellationToken);
		}

		async Task<IMailFolder> CreateAsync (string name, bool isMessageFolder, bool doAsync, CancellationToken cancellationToken)
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("CREATE", ic);

			ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
			ic.UserData = list;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if ((folder = list.FirstOrDefault ()) != null)
				folder.ParentFolder = this;

			return folder;
		}

		/// <summary>
		/// Create a new subfolder with the given name.
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
			return CreateAsync (name, isMessageFolder, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously create a new subfolder with the given name.
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
		public override Task<IMailFolder> CreateAsync (string name, bool isMessageFolder, CancellationToken cancellationToken = default (CancellationToken))
		{
			return CreateAsync (name, isMessageFolder, true, cancellationToken);
		}

		async Task<IMailFolder> CreateAsync (string name, IEnumerable<SpecialFolder> specialUses, bool doAsync, CancellationToken cancellationToken)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (!Engine.IsValidMailboxName (name, DirectorySeparator))
				throw new ArgumentException ("The name is not a legal folder name.", nameof (name));

			if (specialUses == null)
				throw new ArgumentNullException (nameof (specialUses));

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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				var useAttr = ic.RespCodes.FirstOrDefault (rc => rc.Type == ImapResponseCodeType.UseAttr);

				if (useAttr != null)
					throw new ImapCommandException (ic.Response, useAttr.Message);

				throw ImapCommandException.Create ("CREATE", ic);
			}

			ic = new ImapCommand (Engine, cancellationToken, null, "LIST \"\" %S\r\n", encodedName);
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
			ic.UserData = list;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if ((folder = list.FirstOrDefault ()) != null)
				folder.ParentFolder = this;

			Engine.AssignSpecialFolders (new [] { folder });

			return folder;
		}

		/// <summary>
		/// Create a new subfolder with the given name.
		/// </summary>
		/// <remarks>
		/// Creates a new subfolder with the given name.
		/// </remarks>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="specialUses">A list of special uses for the folder being created.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="specialUses"/> is <c>null</c>.</para>
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
			return CreateAsync (name, specialUses, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously create a new subfolder with the given name.
		/// </summary>
		/// <remarks>
		/// Creates a new subfolder with the given name.
		/// </remarks>
		/// <returns>The created folder.</returns>
		/// <param name="name">The name of the folder to create.</param>
		/// <param name="specialUses">A list of special uses for the folder being created.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="name"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="specialUses"/> is <c>null</c>.</para>
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
		public override Task<IMailFolder> CreateAsync (string name, IEnumerable<SpecialFolder> specialUses, CancellationToken cancellationToken = default (CancellationToken))
		{
			return CreateAsync (name, specialUses, true, cancellationToken);
		}

		async Task RenameAsync (IMailFolder parent, string name, bool doAsync, CancellationToken cancellationToken)
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("RENAME", ic);

			Engine.FolderCache.Remove (EncodedName);
			Engine.FolderCache[encodedName] = this;

			ParentFolder = parent;

			FullName = Engine.DecodeMailboxName (encodedName);
			Access = FolderAccess.None;
			EncodedName = encodedName;
			Name = name;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Engine.Selected = null;
				OnClosed ();
			}

			OnRenamed (oldFullName, FullName);
		}

		/// <summary>
		/// Rename the folder to exist with a new name under a new parent folder.
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
			RenameAsync (parent, name, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously rename the folder to exist with a new name under a new parent folder.
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
		public override Task RenameAsync (IMailFolder parent, string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			return RenameAsync (parent, name, true, cancellationToken);
		}

		async Task DeleteAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if (IsNamespace || (Attributes & FolderAttributes.Inbox) != 0)
				throw new InvalidOperationException ("Cannot delete this folder.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "DELETE %F\r\n", this);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("DELETE", ic);

			Access = FolderAccess.None;

			if (Engine.Selected == this) {
				Engine.State = ImapEngineState.Authenticated;
				Engine.Selected = null;
				OnClosed ();
			}

			Attributes |= FolderAttributes.NonExistent;
			OnDeleted ();
		}

		/// <summary>
		/// Delete the folder on the IMAP server.
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
			DeleteAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously delete the folder on the IMAP server.
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
		public override Task DeleteAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return DeleteAsync (true, cancellationToken);
		}

		async Task SubscribeAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "SUBSCRIBE %F\r\n", this);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SUBSCRIBE", ic);

			Attributes |= FolderAttributes.Subscribed;

			OnSubscribed ();
		}

		/// <summary>
		/// Subscribe the folder.
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
			SubscribeAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously subscribe the folder.
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
		public override Task SubscribeAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return SubscribeAsync (true, cancellationToken);
		}

		async Task UnsubscribeAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "UNSUBSCRIBE %F\r\n", this);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("UNSUBSCRIBE", ic);

			Attributes &= ~FolderAttributes.Subscribed;

			OnUnsubscribed ();
		}

		/// <summary>
		/// Unsubscribe the folder.
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
			UnsubscribeAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously unsubscribe the folder.
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
		public override Task UnsubscribeAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return UnsubscribeAsync (true, cancellationToken);
		}

		async Task<IEnumerable<IMailFolder>> GetSubfoldersAsync (StatusItems items, bool subscribedOnly, bool doAsync, CancellationToken cancellationToken)
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
			ic.RegisterUntaggedHandler (lsub ? "LSUB" : "LIST", ImapUtils.ParseFolderListAsync);
			ic.UserData = list;
			ic.Lsub = lsub;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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
		/// Get the subfolders.
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
			return GetSubfoldersAsync (items, subscribedOnly, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the subfolders.
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
		public override Task<IEnumerable<IMailFolder>> GetSubfoldersAsync (StatusItems items, bool subscribedOnly = false, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetSubfoldersAsync (items, subscribedOnly, true, cancellationToken);
		}

		async Task<IMailFolder> GetSubfolderAsync (string name, bool doAsync, CancellationToken cancellationToken)
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
			ic.RegisterUntaggedHandler ("LIST", ImapUtils.ParseFolderListAsync);
			ic.UserData = list = new List<ImapFolder> ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LIST", ic);

			if (list.Count == 0)
				throw new FolderNotFoundException (fullName);

			return list[0];
		}

		/// <summary>
		/// Get the specified subfolder.
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
			return GetSubfolderAsync (name, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified subfolder.
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
		public override Task<IMailFolder> GetSubfolderAsync (string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetSubfolderAsync (name, true, cancellationToken);
		}

		async Task CheckAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (true, false);

			var ic = Engine.QueueCommand (cancellationToken, this, "CHECK\r\n");

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("CHECK", ic);
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
			CheckAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously force the server to sync its in-memory state with its disk state.
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
		public override Task CheckAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return CheckAsync (true, cancellationToken);
		}

		async Task StatusAsync (StatusItems items, bool doAsync, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Status) == 0)
				throw new NotSupportedException ("The IMAP server does not support the STATUS command.");

			CheckState (false, false);

			if (items == StatusItems.None)
				return;

			var command = string.Format ("STATUS %F ({0})\r\n", Engine.GetStatusQuery (items));
			var ic = Engine.QueueCommand (cancellationToken, null, command, this);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("STATUS", ic);
		}

		/// <summary>
		/// Update the values of the specified items.
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
			StatusAsync (items, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously update the values of the specified items.
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
		public override Task StatusAsync (StatusItems items, CancellationToken cancellationToken = default (CancellationToken))
		{
			return StatusAsync (items, true, cancellationToken);
		}

		static async Task<string> ReadStringTokenAsync (ImapEngine engine, string format, bool doAsync, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (ImapStream.AtomSpecials, doAsync, cancellationToken).ConfigureAwait (false);

			switch (token.Type) {
			case ImapTokenType.Literal: return await engine.ReadLiteralAsync (doAsync, cancellationToken).ConfigureAwait (false);
			case ImapTokenType.QString: return (string) token.Value;
			case ImapTokenType.Atom:    return (string) token.Value;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		static async Task UntaggedAclAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ACL", "{0}");
			var acl = (AccessControlList) ic.UserData;
			string name, rights;
			ImapToken token;

			// read the mailbox name
			await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

			do {
				name = await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);
				rights = await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

				acl.Add (new AccessControl (name, rights));

				token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			} while (token.Type != ImapTokenType.Eoln);
		}

		async Task<AccessControlList> GetAccessControlListAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = new ImapCommand (Engine, cancellationToken, null, "GETACL %F\r\n", this);
			ic.RegisterUntaggedHandler ("ACL", UntaggedAclAsync);
			ic.UserData = new AccessControlList ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("GETACL", ic);

			return (AccessControlList) ic.UserData;
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
			return GetAccessControlListAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the complete access control list for the folder.
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
		public override Task<AccessControlList> GetAccessControlListAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetAccessControlListAsync (true, cancellationToken);
		}

		static async Task UntaggedListRightsAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "LISTRIGHTS", "{0}");
			var access = (AccessRights) ic.UserData;
			ImapToken token;

			// read the mailbox name
			await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

			// read the identity name
			await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

			do {
				var rights = await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

				access.AddRange (rights);

				token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			} while (token.Type != ImapTokenType.Eoln);
		}

		async Task<AccessRights> GetAccessRightsAsync (string name, bool doAsync, CancellationToken cancellationToken)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = new ImapCommand (Engine, cancellationToken, null, "LISTRIGHTS %F %S\r\n", this, name);
			ic.RegisterUntaggedHandler ("LISTRIGHTS", UntaggedListRightsAsync);
			ic.UserData = new AccessRights ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("LISTRIGHTS", ic);

			return (AccessRights) ic.UserData;
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
			return GetAccessRightsAsync (name, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the access rights for a particular identifier.
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
		public override Task<AccessRights> GetAccessRightsAsync (string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetAccessRightsAsync (name, true, cancellationToken);
		}

		static async Task UntaggedMyRightsAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "MYRIGHTS", "{0}");
			var access = (AccessRights) ic.UserData;

			// read the mailbox name
			await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

			// read the access rights
			access.AddRange (await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false));
		}

		async Task<AccessRights> GetMyAccessRightsAsync (bool doAsync, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = new ImapCommand (Engine, cancellationToken, null, "MYRIGHTS %F\r\n", this);
			ic.RegisterUntaggedHandler ("MYRIGHTS", UntaggedMyRightsAsync);
			ic.UserData = new AccessRights ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("MYRIGHTS", ic);

			return (AccessRights) ic.UserData;
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
			return GetMyAccessRightsAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the access rights for the current authenticated user.
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
		public override Task<AccessRights> GetMyAccessRightsAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMyAccessRightsAsync (true, cancellationToken);
		}

		async Task ModifyAccessRightsAsync (string name, AccessRights rights, string action, bool doAsync, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "SETACL %F %S %S\r\n", this, name, action + rights);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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

			ModifyAccessRightsAsync (name, rights, "+", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously add access rights for the specified identity.
		/// </summary>
		/// <remarks>
		/// Adds the given access rights for the specified identity.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task AddAccessRightsAsync (string name, AccessRights rights, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			if (rights.Count == 0)
				throw new ArgumentException ("No rights were specified.", nameof (rights));

			return ModifyAccessRightsAsync (name, rights, "+", true, cancellationToken);
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

			ModifyAccessRightsAsync (name, rights, "-", false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously remove access rights for the specified identity.
		/// </summary>
		/// <remarks>
		/// Removes the given access rights for the specified identity.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task RemoveAccessRightsAsync (string name, AccessRights rights, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			if (rights.Count == 0)
				throw new ArgumentException ("No rights were specified.", nameof (rights));

			return ModifyAccessRightsAsync (name, rights, "-", true, cancellationToken);
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

			ModifyAccessRightsAsync (name, rights, string.Empty, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the access rights for the specified identity.
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
		public override Task SetAccessRightsAsync (string name, AccessRights rights, CancellationToken cancellationToken = default (CancellationToken))
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if (rights == null)
				throw new ArgumentNullException (nameof (rights));

			return ModifyAccessRightsAsync (name, rights, string.Empty, true, cancellationToken);
		}

		async Task RemoveAccessAsync (string name, bool doAsync, CancellationToken cancellationToken)
		{
			if (name == null)
				throw new ArgumentNullException (nameof (name));

			if ((Engine.Capabilities & ImapCapabilities.Acl) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ACL extension.");

			CheckState (false, false);

			var ic = Engine.QueueCommand (cancellationToken, null, "DELETEACL %F %S\r\n", this, name);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("DELETEACL", ic);
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
			RemoveAccessAsync (name, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously remove all access rights for the given identity.
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
		public override Task RemoveAccessAsync (string name, CancellationToken cancellationToken = default (CancellationToken))
		{
			return RemoveAccessAsync (name, true, cancellationToken);
		}

		async Task<string> GetMetadataAsync (MetadataTag tag, bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Metadata) == 0)
				throw new NotSupportedException ("The IMAP server does not support the METADATA extension.");

			var ic = new ImapCommand (Engine, cancellationToken, null, "GETMETADATA %F %S\r\n", this, tag.Id);
			ic.RegisterUntaggedHandler ("METADATA", ImapUtils.ParseMetadataAsync);
			var metadata = new MetadataCollection ();
			ic.UserData = metadata;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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
		/// Get the specified metadata.
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
			return GetMetadataAsync (tag, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified metadata.
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
		public override Task<string> GetMetadataAsync (MetadataTag tag, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMetadataAsync (tag, true, cancellationToken);
		}

		async Task<MetadataCollection> GetMetadataAsync (MetadataOptions options, IEnumerable<MetadataTag> tags, bool doAsync, CancellationToken cancellationToken)
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
			ic.RegisterUntaggedHandler ("METADATA", ImapUtils.ParseMetadataAsync);
			ic.UserData = new MetadataCollection ();
			options.LongEntries = 0;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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
		/// Get the specified metadata.
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
			return GetMetadataAsync (options, tags, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the specified metadata.
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
		public override Task<MetadataCollection> GetMetadataAsync (MetadataOptions options, IEnumerable<MetadataTag> tags, CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetMetadataAsync (options, tags, true, cancellationToken);
		}

		async Task SetMetadataAsync (MetadataCollection metadata, bool doAsync, CancellationToken cancellationToken)
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SETMETADATA", ic);
		}

		/// <summary>
		/// Set the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
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
			SetMetadataAsync (metadata, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously set the specified metadata.
		/// </summary>
		/// <remarks>
		/// Sets the specified metadata.
		/// </remarks>
		/// <returns>An asynchronous task context.</returns>
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
		public override Task SetMetadataAsync (MetadataCollection metadata, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SetMetadataAsync (metadata, true, cancellationToken);
		}

		class Quota
		{
			public uint? MessageLimit;
			public uint? StorageLimit;
			public uint? CurrentMessageCount;
			public uint? CurrentStorageSize;
		}

		class QuotaContext
		{
			public QuotaContext ()
			{
				Quotas = new Dictionary<string, Quota> ();
				QuotaRoots = new List<string> ();
			}

			public IList<string> QuotaRoots {
				get; private set;
			}

			public IDictionary<string, Quota> Quotas {
				get; private set;
			}
		}

		static async Task UntaggedQuotaRootAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "QUOTAROOT", "{0}");
			var ctx = (QuotaContext) ic.UserData;

			// The first token should be the mailbox name
			await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);

			// ...followed by 0 or more quota roots
			var token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

			while (token.Type != ImapTokenType.Eoln) {
				var root = await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);
				ctx.QuotaRoots.Add (root);

				token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			}
		}

		static async Task UntaggedQuotaAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "QUOTA", "{0}");
			var quotaRoot = await ReadStringTokenAsync (engine, format, doAsync, ic.CancellationToken).ConfigureAwait (false);
			var ctx = (QuotaContext) ic.UserData;
			var quota = new Quota ();

			var token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			while (token.Type != ImapTokenType.CloseParen) {
				uint used, limit;
				string resource;

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (format, token);

				resource = (string) token.Value;

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out used))
					throw ImapEngine.UnexpectedToken (format, token);

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

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

				token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			}

			// read the closing paren
			await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

			ctx.Quotas[quotaRoot] = quota;
		}

		async Task<FolderQuota> GetQuotaAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Quota) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QUOTA extension.");

			var ic = new ImapCommand (Engine, cancellationToken, null, "GETQUOTAROOT %F\r\n", this);
			var ctx = new QuotaContext ();

			ic.RegisterUntaggedHandler ("QUOTAROOT", UntaggedQuotaRootAsync);
			ic.RegisterUntaggedHandler ("QUOTA", UntaggedQuotaAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("GETQUOTAROOT", ic);

			for (int i = 0; i < ctx.QuotaRoots.Count; i++) {
				var encodedName = ctx.QuotaRoots[i];
				ImapFolder quotaRoot;
				Quota quota;

				if (!ctx.Quotas.TryGetValue (encodedName, out quota))
					continue;

				quotaRoot = await Engine.GetQuotaRootFolderAsync (encodedName, doAsync, cancellationToken).ConfigureAwait (false);

				return new FolderQuota (quotaRoot) {
					CurrentMessageCount = quota.CurrentMessageCount,
					CurrentStorageSize = quota.CurrentStorageSize,
					MessageLimit = quota.MessageLimit,
					StorageLimit = quota.StorageLimit
				};
			}

			return new FolderQuota (null);
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
			return GetQuotaAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously get the quota information for the folder.
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
		public override Task<FolderQuota> GetQuotaAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return GetQuotaAsync (true, cancellationToken);
		}

		async Task<FolderQuota> SetQuotaAsync (uint? messageLimit, uint? storageLimit, bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (false, false);

			if ((Engine.Capabilities & ImapCapabilities.Quota) == 0)
				throw new NotSupportedException ("The IMAP server does not support the QUOTA extension.");

			var command = new StringBuilder ("SETQUOTA %F (");
			if (messageLimit.HasValue)
				command.AppendFormat ("MESSAGE {0} ", messageLimit.Value);
			if (storageLimit.HasValue)
				command.AppendFormat ("STORAGE {0} ", storageLimit.Value);
			command[command.Length - 1] = ')';
			command.Append ("\r\n");

			var ic = new ImapCommand (Engine, cancellationToken, null, command.ToString (), this);
			var ctx = new QuotaContext ();
			Quota quota;

			ic.RegisterUntaggedHandler ("QUOTA", UntaggedQuotaAsync);
			ic.UserData = ctx;

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SETQUOTA", ic);

			if (ctx.Quotas.TryGetValue (EncodedName, out quota)) {
				return new FolderQuota (this) {
					CurrentMessageCount = quota.CurrentMessageCount,
					CurrentStorageSize = quota.CurrentStorageSize,
					MessageLimit = quota.MessageLimit,
					StorageLimit = quota.StorageLimit
				};
			}

			return new FolderQuota (null);
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
			return SetQuotaAsync (messageLimit, storageLimit, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously set the quota limits for the folder.
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
		public override Task<FolderQuota> SetQuotaAsync (uint? messageLimit, uint? storageLimit, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SetQuotaAsync (messageLimit, storageLimit, true, cancellationToken);
		}

		async Task ExpungeAsync (bool doAsync, CancellationToken cancellationToken)
		{
			CheckState (true, true);

			var ic = Engine.QueueCommand (cancellationToken, this, "EXPUNGE\r\n");

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("EXPUNGE", ic);
		}

		/// <summary>
		/// Expunge the folder, permanently removing all messages marked for deletion.
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
			ExpungeAsync (false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously expunge the folder, permanently removing all messages marked for deletion.
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
		/// <returns>An asynchronous task context.</returns>
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
		public override Task ExpungeAsync (CancellationToken cancellationToken = default (CancellationToken))
		{
			return ExpungeAsync (true, cancellationToken);
		}

		async Task ExpungeAsync (IList<UniqueId> uids, bool doAsync, CancellationToken cancellationToken)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			CheckState (true, true);

			if (uids.Count == 0)
				return;

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				// get the list of messages marked for deletion that should not be expunged
				var query = SearchQuery.Deleted.And (SearchQuery.Not (SearchQuery.Uids (uids)));
				var unmark = await SearchAsync (query, doAsync, cancellationToken).ConfigureAwait (false);

				if (unmark.Count > 0) {
					// clear the \Deleted flag on all messages except the ones that are to be expunged
					if (doAsync)
						await RemoveFlagsAsync (unmark, MessageFlags.Deleted, true, cancellationToken);
					else
						RemoveFlags (unmark, MessageFlags.Deleted, true, cancellationToken);
				}

				// expunge the folder
				await ExpungeAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (unmark.Count > 0) {
					// restore the \Deleted flags
					if (doAsync)
						await AddFlagsAsync (unmark, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait (false);
					else
						AddFlags (unmark, MessageFlags.Deleted, true, cancellationToken);
				}

				return;
			}

			var set = ImapUtils.FormatUidSet (uids);
			var command = string.Format ("UID EXPUNGE {0}\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
			ExpungeAsync (uids, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously expunge the specified uids, permanently removing them from the folder.
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
		/// <returns>An asynchronous task context.</returns>
		/// <param name="uids">The message uids.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task ExpungeAsync (IList<UniqueId> uids, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ExpungeAsync (uids, true, cancellationToken);
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

		async Task<UniqueId?> AppendAsync (FormatOptions options, MimeMessage message, MessageFlags flags, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("APPEND", ic);

			var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

			if (append != null)
				return append.UidSet[0];

			return null;
		}

		/// <summary>
		/// Append the specified message to the folder.
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
			return AppendAsync (options, message, flags, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
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
		public override Task<UniqueId?> AppendAsync (FormatOptions options, MimeMessage message, MessageFlags flags = MessageFlags.None, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return AppendAsync (options, message, flags, true, cancellationToken, progress);
		}

		async Task<UniqueId?> AppendAsync (FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset date, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, this);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("APPEND", ic);

			var append = ic.RespCodes.OfType<AppendUidResponseCode> ().FirstOrDefault ();

			if (append != null)
				return append.UidSet[0];

			return null;
		}

		/// <summary>
		/// Append the specified message to the folder.
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
			return AppendAsync (options, message, flags, date, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously append the specified message to the folder.
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
		public override Task<UniqueId?> AppendAsync (FormatOptions options, MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return AppendAsync (options, message, flags, date, true, cancellationToken, progress);
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

		async Task<IList<UniqueId>> AppendAsync (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
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

				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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
				var uid = await AppendAsync (format, messages[i], flags[i], doAsync, cancellationToken, progress).ConfigureAwait (false);
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
		/// Append the specified messages to the folder.
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
			return AppendAsync (options, messages, flags, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
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
		public override Task<IList<UniqueId>> AppendAsync (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return AppendAsync (options, messages, flags, true, cancellationToken, progress);
		}

		async Task<IList<UniqueId>> AppendAsync (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, bool doAsync, CancellationToken cancellationToken, ITransferProgress progress)
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

				await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

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
				var uid = await AppendAsync (format, messages[i], flags[i], dates[i], doAsync, cancellationToken, progress);
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
		/// Append the specified messages to the folder.
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
			return AppendAsync (options, messages, flags, dates, false, cancellationToken, progress).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously append the specified messages to the folder.
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
		public override Task<IList<UniqueId>> AppendAsync (FormatOptions options, IList<MimeMessage> messages, IList<MessageFlags> flags, IList<DateTimeOffset> dates, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
		{
			return AppendAsync (options, messages, flags, dates, true, cancellationToken, progress);
		}

		async Task<UniqueIdMap> CopyToAsync (IList<UniqueId> uids, IMailFolder destination, bool doAsync, CancellationToken cancellationToken)
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
				var indexes = (await FetchAsync (uids, MessageSummaryItems.UniqueId, doAsync, cancellationToken).ConfigureAwait (false)).Select (x => x.Index).ToList ();
				await CopyToAsync (indexes, destination, doAsync, cancellationToken).ConfigureAwait (false);
				return UniqueIdMap.Empty;
			}

			var command = string.Format ("UID COPY {0} %F\r\n", set);
			var ic = Engine.QueueCommand (cancellationToken, this, command, destination);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("COPY", ic);

			var copy = ic.RespCodes.OfType<CopyUidResponseCode> ().FirstOrDefault ();

			if (copy != null)
				return new UniqueIdMap (copy.SrcUidSet, copy.DestUidSet);

			return UniqueIdMap.Empty;
		}

		/// <summary>
		/// Copy the specified messages to the destination folder.
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
			return CopyToAsync (uids, destination, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously copy the specified messages to the destination folder.
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
		public override Task<UniqueIdMap> CopyToAsync (IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			return CopyToAsync (uids, destination, true, cancellationToken);
		}

		async Task<UniqueIdMap> MoveToAsync (IList<UniqueId> uids, IMailFolder destination, bool doAsync, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				var copied = await CopyToAsync (uids, destination, doAsync, cancellationToken).ConfigureAwait (false);
				if (doAsync)
					await AddFlagsAsync (uids, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait (false);
				else
					AddFlags (uids, MessageFlags.Deleted, true, cancellationToken);
				await ExpungeAsync (uids, doAsync, cancellationToken).ConfigureAwait (false);
				return copied;
			}

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0) {
				var indexes = (await FetchAsync (uids, MessageSummaryItems.UniqueId, doAsync, cancellationToken).ConfigureAwait (false)).Select (x => x.Index).ToList ();
				await MoveToAsync (indexes, destination, doAsync, cancellationToken).ConfigureAwait (false);
				await ExpungeAsync (uids, doAsync, cancellationToken).ConfigureAwait (false);
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("MOVE", ic);

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
			return MoveToAsync (uids, destination, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously move the specified messages to the destination folder.
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
		public override Task<UniqueIdMap> MoveToAsync (IList<UniqueId> uids, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			return MoveToAsync (uids, destination, true, cancellationToken);
		}

		async Task CopyToAsync (IList<int> indexes, IMailFolder destination, bool doAsync, CancellationToken cancellationToken)
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("COPY", ic);
		}

		/// <summary>
		/// Copy the specified messages to the destination folder.
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
			CopyToAsync (indexes, destination, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously copy the specified messages to the destination folder.
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
		public override Task CopyToAsync (IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			return CopyToAsync (indexes, destination, true, cancellationToken);
		}

		async Task MoveToAsync (IList<int> indexes, IMailFolder destination, bool doAsync, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				await CopyToAsync (indexes, destination, doAsync, cancellationToken).ConfigureAwait (false);
				if (doAsync)
					await AddFlagsAsync (indexes, MessageFlags.Deleted, true, cancellationToken).ConfigureAwait (false);
				else
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

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, destination);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("MOVE", ic);
		}

		/// <summary>
		/// Move the specified messages to the destination folder.
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
			MoveToAsync (indexes, destination, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously move the specified messages to the destination folder.
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
		public override Task MoveToAsync (IList<int> indexes, IMailFolder destination, CancellationToken cancellationToken = default (CancellationToken))
		{
			return MoveToAsync (indexes, destination, true, cancellationToken);
		}

		#region IEnumerable<MimeMessage> implementation

		/// <summary>
		/// Get an enumerator for the messages in the folder.
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
			Count--;
			
			OnMessageExpunged (new MessageEventArgs (index));
			OnCountChanged ();
		}

		internal async Task OnFetchAsync (ImapEngine engine, int index, bool doAsync, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
			var labelsChangedEventArgs = new MessageLabelsChangedEventArgs (index);
			var flagsChangedEventArgs = new MessageFlagsChangedEventArgs (index);
			var modSeqChangedEventArgs = new ModSeqChangedEventArgs (index);
			bool modSeqChanged = false;
			bool labelsChanged = false;
			bool flagsChanged = false;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

			do {
				token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

				if (token.Type == ImapTokenType.CloseParen || token.Type == ImapTokenType.Eoln)
					break;

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "FETCH", token);

				var atom = (string) token.Value;
				ulong modseq;
				uint uid;

				switch (atom) {
				case "MODSEQ":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type != ImapTokenType.CloseParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					if (modseq > HighestModSeq)
						UpdateHighestModSeq (modseq);

					modSeqChangedEventArgs.ModSeq = modseq;
					labelsChangedEventArgs.ModSeq = modseq;
					flagsChangedEventArgs.ModSeq = modseq;
					modSeqChanged = true;
					break;
				case "UID":
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					modSeqChangedEventArgs.UniqueId = new UniqueId (UidValidity, uid);
					labelsChangedEventArgs.UniqueId = new UniqueId (UidValidity, uid);
					flagsChangedEventArgs.UniqueId = new UniqueId (UidValidity, uid);
					break;
				case "FLAGS":
					flagsChangedEventArgs.Flags = await ImapUtils.ParseFlagsListAsync (engine, atom, flagsChangedEventArgs.UserFlags, doAsync, cancellationToken).ConfigureAwait (false);
					flagsChanged = true;
					break;
				case "X-GM-LABELS":
					labelsChangedEventArgs.Labels = await ImapUtils.ParseLabelsListAsync (engine, doAsync, cancellationToken).ConfigureAwait (false);
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

		internal async Task OnVanishedAsync (ImapEngine engine, bool doAsync, CancellationToken cancellationToken)
		{
			var token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
			UniqueIdSet vanished;
			bool earlier = false;

			if (token.Type == ImapTokenType.OpenParen) {
				do {
					token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "VANISHED", token);

					var atom = (string) token.Value;

					if (atom == "EARLIER")
						earlier = true;
				} while (true);

				token = await engine.ReadTokenAsync (doAsync, cancellationToken).ConfigureAwait (false);
			}

			if (token.Type != ImapTokenType.Atom || !UniqueIdSet.TryParse ((string) token.Value, UidValidity, out vanished))
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "VANISHED", token);

			OnMessagesVanished (new MessagesVanishedEventArgs (vanished, earlier));

			if (!earlier) {
				Count -= vanished.Count;

				OnCountChanged ();
			}
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
	}
}
