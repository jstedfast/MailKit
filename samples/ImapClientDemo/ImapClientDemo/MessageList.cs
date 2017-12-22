using System;
using System.Drawing;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms;

using MailKit;

namespace ImapClientDemo
{
	[ToolboxItem (true)]
	class MessageList : TreeView
	{
		readonly Dictionary<MessageInfo, TreeNode> map = new Dictionary<MessageInfo, TreeNode> ();
		readonly List<MessageInfo> messages = new List<MessageInfo> ();
		IMailFolder folder;

		public MessageList ()
		{
		}

		void UpdateMessageNode (TreeNode node)
		{
			var info = (MessageInfo) node.Tag;
			FontStyle style;

			if (info.Flags.HasFlag (MessageFlags.Deleted))
				style = FontStyle.Strikeout;
			else if (!info.Flags.HasFlag (MessageFlags.Seen))
				style = FontStyle.Bold;
			else
				style = FontStyle.Regular;

			node.NodeFont = new Font (Font, style);
		}

		void AddMessageSummaries (IEnumerable<IMessageSummary> summaries)
		{
			foreach (var message in summaries) {
				var info = new MessageInfo (message);
				var node = new TreeNode (message.Envelope.Subject) { Tag = info };
				UpdateMessageNode (node);
				Nodes.Add (node);
				map[info] = node;
			}
		}

		async void LoadMessages ()
		{
			messages.Clear ();
			Nodes.Clear ();
			map.Clear ();

			if (folder.Count > 0) {
				var summaries = await folder.FetchAsync (0, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId);

				AddMessageSummaries (summaries);
			}

			folder.CountChanged += CountChanged_TaskThread;
		}

		public async void OpenFolder (IMailFolder folder)
		{
			if (this.folder == folder)
				return;

			if (this.folder != null) {
				this.folder.MessageFlagsChanged -= MessageFlagsChanged_TaskThread;
				this.folder.MessageExpunged -= MessageExpunged_TaskThread;
				this.folder.CountChanged -= CountChanged_TaskThread;
			}

			// Note: because we are using the *Async() methods, these events will fire
			// in another thread so we'll have to proxy them back to the main thread.
			folder.MessageFlagsChanged += MessageFlagsChanged_TaskThread;
			folder.MessageExpunged += MessageExpunged_TaskThread;

			this.folder = folder;

			if (folder.IsOpen) {
				LoadMessages ();
				return;
			}

			await folder.OpenAsync (FolderAccess.ReadOnly);
			LoadMessages ();
		}

		void MessageFlagsChanged (object sender, MessageFlagsChangedEventArgs e)
		{
			if (e.Index < messages.Count) {
				var info = messages[e.Index];
				var node = map[info];

				info.Flags = e.Flags;

				UpdateMessageNode (node);
			}
		}

		void MessageFlagsChanged_TaskThread (object sender, MessageFlagsChangedEventArgs e)
		{
			// proxy back to the main thread
			Invoke (new EventHandler<MessageFlagsChangedEventArgs> (MessageFlagsChanged), sender, e);
		}

		void MessageExpunged (object sender, MessageEventArgs e)
		{
			if (e.Index < messages.Count) {
				var info = messages[e.Index];
				var node = map[info];

				messages.RemoveAt (e.Index);
				map.Remove (info);
				node.Remove ();
			}
		}

		void MessageExpunged_TaskThread (object sender, MessageEventArgs e)
		{
			// proxy back to the main thread
			Invoke (new EventHandler<MessageEventArgs> (MessageExpunged), sender, e);
		}

		void CountChanged (object sender, EventArgs e)
		{
			var folder = (IMailFolder) sender;

			// Note: we can't call back into the ImapFolder in this event handler since another command is still processing.
			// TODO: queue this operation for later...

			//var summaries = await folder.FetchAsync (messages.Count, -1, MessageSummaryItems.Full | MessageSummaryItems.UniqueId);

			//AddMessageSummaries (summaries);
		}

		void CountChanged_TaskThread (object sender, EventArgs e)
		{
			// proxy back to the main thread
			Invoke (new EventHandler<EventArgs> (CountChanged), sender, e);
		}

		public event EventHandler<MessageSelectedEventArgs> MessageSelected;

		protected override void OnAfterSelect (TreeViewEventArgs e)
		{
			var handler = MessageSelected;

			if (handler != null) {
				var info = (MessageInfo) e.Node.Tag;

				if (info.Summary.UniqueId.IsValid)
					handler (this, new MessageSelectedEventArgs (folder, info.Summary.UniqueId, info.Summary.Body));
			}

			base.OnAfterSelect (e);
		}
	}
}
