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

			var names = FullName.Split (new char[] { delim }, StringSplitOptions.RemoveEmptyEntries);
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

		public IFolder ParentFolder {
			get; internal set;
		}

		public FolderAttributes Attributes {
			get; internal set;
		}

		public MessageFlags PermanentFlags {
			get; internal set;
		}

		public MessageFlags AcceptedFlags {
			get; internal set;
		}

		public char DirectorySeparator { 
			get; private set;
		}

		public FolderAccess Access {
			get; internal set;
		}

		public string FullName {
			get; private set;
		}

		public string Name {
			get; private set;
		}

		public bool IsSubscribed {
			get; private set;
		}

		public bool IsOpen {
			get { return Engine.Selected == this; }
		}

		public bool Exists {
			get; internal set;
		}

		public ulong HighestModSeq {
			get; private set;
		}

		public string UidValidity {
			get; private set;
		}

		public string UidNext {
			get; private set;
		}

		public int FirstUnreadIndex {
			get; private set;
		}

		public int Recent {
			get; private set;
		}

		public int Count {
			get; private set;
		}

		public FolderAccess Open (FolderAccess access, CancellationToken cancellationToken)
		{
			if (access != FolderAccess.ReadOnly && access != FolderAccess.ReadWrite)
				throw new ArgumentOutOfRangeException ("access");

			if (IsOpen && Access == access)
				throw new InvalidOperationException ("The ImapFolder is already open with the specified access.");

			string format;
			if (access == FolderAccess.ReadOnly)
				format = "EXAMINE %F\r\n";
			else
				format = "SELECT %F\r\n";

			var ic = Engine.QueueCommand (cancellationToken, this, format, this);
			Engine.Wait (ic);

			ProcessResponseCodes (ic, null);

			if (ic.Result != ImapCommandResult.Ok)
				return FolderAccess.None;

			return Access;
		}

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

		public void Subscribe (CancellationToken cancellationToken)
		{
			var ic = Engine.QueueCommand (cancellationToken, null, "SUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);

			if (ic.Result == ImapCommandResult.Ok)
				IsSubscribed = true;
		}

		public void Unsubscribe (CancellationToken cancellationToken)
		{
			var ic = Engine.QueueCommand (cancellationToken, null, "UNSUBSCRIBE %F\r\n", this);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);

			if (ic.Result == ImapCommandResult.Ok)
				IsSubscribed = false;
		}

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

			return list;
		}

		public void Check (CancellationToken cancellationToken)
		{
			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "CHECK\r\n");

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);
		}

		public void Expunge (CancellationToken cancellationToken)
		{
			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "EXPUNGE\r\n");

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);
		}

		public void Expunge (string[] uids, CancellationToken cancellationToken)
		{
			var set = ImapUtils.FormatUidSet (uids);

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = Engine.QueueCommand (cancellationToken, this, "UID EXPUNGE %s\r\n", set);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);
		}

		static string GetFlagsList (MessageFlags flags)
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			if ((flags & MessageFlags.Answered) != 0)
				builder.Append ("\\Answered ");
			if ((flags & MessageFlags.Deleted) != 0)
				builder.Append ("\\Deleted ");
			if ((flags & MessageFlags.Draft) != 0)
				builder.Append ("\\Draft ");
			if ((flags & MessageFlags.Flagged) != 0)
				builder.Append ("\\Flagged ");
			if ((flags & MessageFlags.Seen) != 0)
				builder.Append ("\\Seen ");
			if (builder.Length > 1)
				builder.Length--;
			builder.Append (')');

			return builder.ToString ();
		}

		ImapCommand QueueAppend (MimeMessage message, MessageFlags flags, DateTimeOffset? date, CancellationToken cancellationToken)
		{
			string format = string.Empty;

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) != 0)
				format = "UID ";

			format += "APPEND %F";

			if (date.HasValue)
				format += " \"" + ImapUtils.FormatInternalDate (date.Value) + "\"";

			format += " " + GetFlagsList (flags & AcceptedFlags);
			format += " %L\r\n";

			return Engine.QueueCommand (cancellationToken, this, format, this, message);
		}

		public string Append (MimeMessage message, MessageFlags flags, CancellationToken cancellationToken)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = QueueAppend (message, flags, null, cancellationToken);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.AppendUid)
					return code.DestUidSet[0];
			}

			return null;
		}

		public string Append (MimeMessage message, MessageFlags flags, DateTimeOffset date, CancellationToken cancellationToken)
		{
			if (message == null)
				throw new ArgumentNullException ("message");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			var ic = QueueAppend (message, flags, date, cancellationToken);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, null);

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

				format += " " + GetFlagsList (flags[i] & AcceptedFlags);
				format += " %L";

				args.Add (messages[i]);
			}

			format += "\r\n";

			return Engine.QueueCommand (cancellationToken, this, format, args.ToArray ());
		}

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

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			if (messages.Length == 0)
				return new string[0];

			if ((Engine.Capabilities & ImapCapabilities.MultiAppend) != 0) {
				var ic = QueueMultiAppend (messages, flags, null, cancellationToken);

				Engine.Wait (ic);
				ProcessResponseCodes (ic, null);

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

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			if (messages.Length == 0)
				return new string[0];

			if ((Engine.Capabilities & ImapCapabilities.MultiAppend) != 0) {
				var ic = QueueMultiAppend (messages, flags, dates, cancellationToken);

				Engine.Wait (ic);
				ProcessResponseCodes (ic, null);

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

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.CopyUid)
					return code.DestUidSet;
			}

			return null;
		}

		public string[] MoveTo (string[] uids, IFolder destination, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				var copied = CopyTo (uids, destination, cancellationToken);
				AddFlags (uids, MessageFlags.Deleted, true, cancellationToken);
				Expunge (uids, cancellationToken);
				return copied;
			}

			var set = ImapUtils.FormatUidSet (uids);
			ImapCommand ic;

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder))
				throw new ArgumentException ("The destination folder is not an ImapFolder.", "destination");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			if ((Engine.Capabilities & ImapCapabilities.UidPlus) == 0)
				throw new NotSupportedException ("The IMAP server does not support the UIDPLUS extension.");

			ic = Engine.QueueCommand (cancellationToken, this, "UID MOVE %s %F\r\n", set, destination);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, "destination");

			foreach (var code in ic.RespCodes) {
				if (code.Type == ImapResponseCodeType.CopyUid)
					return code.DestUidSet;
			}

			return null;
		}

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
		}

		public void MoveTo (int[] indexes, IFolder destination, CancellationToken cancellationToken)
		{
			if ((Engine.Capabilities & ImapCapabilities.Move) == 0) {
				CopyTo (indexes, destination, cancellationToken);
				AddFlags (indexes, MessageFlags.Deleted, true, cancellationToken);
			}

			var set = ImapUtils.FormatUidSet (indexes);
			ImapCommand ic;

			if (destination == null)
				throw new ArgumentNullException ("destination");

			if (!(destination is ImapFolder))
				throw new ArgumentException ("The destination folder is not an ImapFolder.", "destination");

			if (!IsOpen)
				throw new InvalidOperationException ("The ImapFolder is not currently open.");

			ic = Engine.QueueCommand (cancellationToken, this, "MOVE %s %F\r\n", set, destination);

			Engine.Wait (ic);
			ProcessResponseCodes (ic, "destination");
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

		public MimeMessage GetMessage (string uid, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public MimeMessage GetMessage (int index, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void AddFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void RemoveFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void SetFlags (string[] uids, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void AddFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void RemoveFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void SetFlags (int[] indexes, MessageFlags flags, bool silent, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
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
