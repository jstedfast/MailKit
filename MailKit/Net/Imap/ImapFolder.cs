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
	public class ImapFolder : IFolder
	{
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

		internal ImapEngine Engine {
			get; private set;
		}

		internal string EncodedName {
			get; private set;
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
			get; internal set;
		}

		public bool IsOpen {
			get; private set;
		}

		public bool Exists {
			get; internal set;
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
			throw new NotImplementedException ();
		}

		public void Close (bool expunge, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
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
			throw new NotImplementedException ();
		}

		public void Unsubscribe (CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public IEnumerable<IFolder> GetSubfolders (string pattern, bool subscribedOnly, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public IEnumerable<IFolder> GetSubfolders (bool subscribedOnly, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void Check (CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void Expunge (CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void Expunge (string[] uids, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public string Append (MimeMessage message, MessageFlags flags, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public string Append (MimeMessage message, MessageFlags flags, DateTime date, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public string[] Append (MimeMessage[] messages, MessageFlags[] flags, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public string[] Append (MimeMessage[] messages, MessageFlags[] flags, DateTime[] dates, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void CopyTo (string[] uids, IFolder destination, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void MoveTo (string[] uids, IFolder destination, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void CopyTo (int[] indexes, IFolder destination, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
		}

		public void MoveTo (int[] indexes, IFolder destination, CancellationToken cancellationToken)
		{
			throw new NotImplementedException ();
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
