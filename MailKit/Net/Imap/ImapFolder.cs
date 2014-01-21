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
using System.Collections.Generic;

using MimeKit;

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
			DirectorySeparator = delim;
			EncodedName = encodedName;
			Attributes = attrs;
			Engine = engine;

			var names = FullName.Split (new [] { delim }, StringSplitOptions.RemoveEmptyEntries);
			Name = names.Length > 0 ? names[names.Length - 1] : FullName;
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
					FirstUnreadIndex = code.Index;
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

		public ulong HighestModSeq {
			get; private set;
		}

		/// <summary>
		/// Gets the UID validity.
		/// </summary>
		/// <remarks>
		/// A UID is only valid so long as the UID validity value remains unchanged.
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
		public int FirstUnreadIndex {
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
		/// <param name="access">The requested folder access.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="access"/> is not a valid value.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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

			if (IsOpen && Access == access)
				return access;

			string format = access == FolderAccess.ReadOnly ? "EXAMINE %F\r\n" : "SELECT %F\r\n";
			var ic = Engine.QueueCommand (cancellationToken, this, format, this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException (access == FolderAccess.ReadOnly ? "EXAMINE" : "SELECT", ic.Result);

			return Access;
		}

		/// <summary>
		/// Closes the folder, optionally expunging the messages marked for deletion.
		/// </summary>
		/// <param name="expunge">If set to <c>true</c>, expunge.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

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
		}

		public void Rename (string newName, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void Create (CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void Delete (CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Subscribes the folder.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
			var ic = Engine.QueueCommand (cancellationToken, null, "SUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("SUBSCRIBE", ic.Result);

			IsSubscribed = true;
		}

		/// <summary>
		/// Unsubscribes the folder.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
			var ic = Engine.QueueCommand (cancellationToken, null, "UNSUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("UNSUBSCRIBE", ic.Result);

			IsSubscribed = false;
		}

		/// <summary>
		/// Gets the subfolders.
		/// </summary>
		/// <returns>The subfolders.</returns>
		/// <param name="subscribedOnly">If set to <c>true</c>, only subscribed folders will be listed.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "CHECK\r\n");

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("CHECK", ic.Result);
		}

		/// <summary>
		/// Expunges the folder, permanently removing all messages marked for deletion.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

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
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

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
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
		/// <param name="message">The message.</param>
		/// <param name="flags">The message flags.</param>
		/// <param name="date">The received date of the message.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="message"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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
		/// <returns>The UIDs of the messages in the destination folder.</returns>
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
		/// <para>The destination folder is not an <see cref="ImapFolder"/>.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
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

			if (!(destination is ImapFolder))
				throw new ArgumentException ("The destination folder is not an ImapFolder.", "destination");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

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
		/// <returns>The UIDs of the messages in the destination folder.</returns>
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
		/// <para>The destination folder is not an <see cref="ImapFolder"/>.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
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

			if (!(destination is ImapFolder))
				throw new ArgumentException ("The destination folder is not an ImapFolder.", "destination");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

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
		/// <para>The destination folder is not an <see cref="ImapFolder"/>.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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

			if (!(destination is ImapFolder))
				throw new ArgumentException ("The destination folder is not an ImapFolder.", "destination");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

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
		/// <para>The destination folder is not an <see cref="ImapFolder"/>.</para>
		/// </exception>
		/// <exception cref="System.InvalidOperationException">
		/// The folder is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
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

			if (!(destination is ImapFolder))
				throw new ArgumentException ("The destination folder is not an ImapFolder.", "destination");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "MOVE %s %F\r\n", set, destination);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, "destination");

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("MOVE", ic.Result);
		}

		static void ValidateUid (string uid)
		{
			uint value;

			if (uid == null)
				throw new ArgumentNullException ("uid");

			if (!uint.TryParse (uid, out value) || value == 0)
				throw new ArgumentException ("The uid is invalid.");
		}

		public FetchResult Fetch (string uid, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public IEnumerable<FetchResult> Fetch (string[] uids, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public FetchResult Fetch (int index, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public IEnumerable<FetchResult> Fetch (int[] indexes, MessageAttributes attributes, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
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
				case "BODY[":
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

		public MimeMessage GetMessage (string uid, CancellationToken cancellationToken)
		{
			ValidateUid (uid);

			if (!IsOpen)
				throw new InvalidOperationException ("The folder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "UID FETCH %s BODY.PEEK[]\r\n", uid);
			ic.RegisterUntaggedHandler ("FETCH", FetchMessage);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return (MimeMessage) ic.UserData;
		}

		public MimeMessage GetMessage (int index, CancellationToken cancellationToken)
		{
			if (index < 0 || index >= Count)
				throw new ArgumentOutOfRangeException ("index");

			if (!IsOpen)
				throw new InvalidOperationException ("The folder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "FETCH %d BODY.PEEK[]\r\n", index + 1);
			ic.RegisterUntaggedHandler ("FETCH", FetchMessage);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("FETCH", ic.Result);

			return (MimeMessage) ic.UserData;
		}

		void ModifyFlags (string[] uids, MessageFlags flags, string action, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			var set = ImapUtils.FormatUidSet (uids);

			if (!IsOpen)
				throw new InvalidOperationException ("The folder is not currently open.");

			var format = string.Format ("UID STORE {0} {1} {2}\r\n", set, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STORE", ic.Result);
		}

		public void AddFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		public void RemoveFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		public void SetFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (uids, flags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
		}

		void ModifyFlags (int[] indexes, MessageFlags flags, string action, CancellationToken cancellationToken)
		{
			var flaglist = ImapUtils.FormatFlagsList (flags & AcceptedFlags);
			var set = ImapUtils.FormatUidSet (indexes);

			if (!IsOpen)
				throw new InvalidOperationException ("The folder is not currently open.");

			var format = string.Format ("STORE {0} {1} {2}\r\n", set, action, flaglist);
			var ic = Engine.QueueCommand (cancellationToken, this, format);

			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				throw new ImapCommandException ("STORE", ic.Result);
		}

		public void AddFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, flags, silent ? "+FLAGS.SILENT" : "+FLAGS", cancellationToken);
		}

		public void RemoveFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, flags, silent ? "-FLAGS.SILENT" : "-FLAGS", cancellationToken);
		}

		public void SetFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			ModifyFlags (indexes, flags, silent ? "FLAGS.SILENT" : "FLAGS", cancellationToken);
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
