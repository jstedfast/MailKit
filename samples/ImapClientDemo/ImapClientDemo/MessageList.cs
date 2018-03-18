using System;
using System.Drawing;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

using MailKit;

namespace ImapClientDemo
{
	[ToolboxItem (true)]
	class MessageList : TreeView
	{
		const MessageSummaryItems SummaryItems = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.BodyStructure;
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

		async Task LoadMessagesAsync (Task task)
		{
			await task;

			messages.Clear ();
			Nodes.Clear ();
			map.Clear ();

			if (!folder.IsOpen)
				await folder.OpenAsync (FolderAccess.ReadOnly);

			if (folder.Count > 0) {
				var summaries = await folder.FetchAsync (0, -1, SummaryItems);

				AddMessageSummaries (summaries);
			}

			folder.CountChanged += CountChanged;
		}

		public void OpenFolder (IMailFolder folder)
		{
			if (this.folder == folder)
				return;

			if (this.folder != null) {
				this.folder.MessageFlagsChanged -= MessageFlagsChanged;
				this.folder.MessageExpunged -= MessageExpunged;
				this.folder.CountChanged -= CountChanged;
			}

			folder.MessageFlagsChanged += MessageFlagsChanged;
			folder.MessageExpunged += MessageExpunged;

			this.folder = folder;
			
			Program.Queue (LoadMessagesAsync);
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

		async Task UpdateMessageListAsync (Task task)
		{
			await task;

			if (folder.Count > messages.Count) {
				var summaries = await folder.FetchAsync (messages.Count, -1, SummaryItems);

				AddMessageSummaries (summaries);
			}
		}

		void CountChanged (object sender, EventArgs e)
		{
			// Note: we can't call back into the ImapFolder in this event handler since another command is still processing,
			// so queue it to run after our current command...
			Program.Queue (UpdateMessageListAsync);
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
