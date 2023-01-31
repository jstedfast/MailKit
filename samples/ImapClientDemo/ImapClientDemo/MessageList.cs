using System;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms;

using MailKit;
using MailKit.Net.Imap;

namespace ImapClientDemo
{
	[ToolboxItem (true)]
	class MessageList : TreeView
	{
		static readonly FetchRequest request = new FetchRequest (MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.BodyStructure);
		const int BatchSize = 512;

		readonly Dictionary<MessageInfo, TreeNode> map = new Dictionary<MessageInfo, TreeNode> ();
		readonly List<MessageInfo> messages = new List<MessageInfo> ();
		IMailFolder folder;

		public MessageList ()
		{
			FullRowSelect = true;
		}

		void UpdateMessageNode (TreeNode node)
		{
			Debug.Assert (SynchronizationContext.Current == Program.GuiContext);

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

		void AddMessageSummaries (IMailFolder folder, IEnumerable<IMessageSummary> summaries)
		{
			Debug.Assert (SynchronizationContext.Current == Program.GuiContext);

			if (folder != this.folder)
				return;

			foreach (var message in summaries) {
				var info = new MessageInfo (message);
				var node = new TreeNode (message.Envelope.Subject) { Tag = info };
				UpdateMessageNode (node);
				messages.Add (info);
				Nodes.Add (node);
				map[info] = node;
			}

			if (messages.Count < folder.Count)
				FetchNewMessages (folder);
		}

		void OnFolderOpened (IMailFolder folder, IList<IMessageSummary> summaries)
		{
			if (this.folder != null) {
				this.folder.MessageFlagsChanged -= OnMessageFlagsChanged;
				this.folder.MessageExpunged -= OnMessageExpunged;
				this.folder.CountChanged -= OnCountChanged;
			}

			folder.MessageFlagsChanged += OnMessageFlagsChanged;
			folder.MessageExpunged += OnMessageExpunged;

			this.folder = folder;

			lock (messages) {
				messages.Clear ();
				Nodes.Clear ();
				map.Clear ();

				if (summaries != null)
					AddMessageSummaries (folder, summaries);
			}

			folder.CountChanged += OnCountChanged;
		}

		class OpenFolderCommand : ClientCommand<ImapClient>
		{
			readonly MessageList messageList;
			readonly IMailFolder folder;
			IList<IMessageSummary> summaries;

			public OpenFolderCommand (ClientConnection<ImapClient> connection, IMailFolder folder, MessageList messageList) : base (connection)
			{
				this.messageList = messageList;
				this.folder = folder;
			}

			public override void Run (CancellationToken cancellationToken)
			{
				if (!folder.IsOpen)
					folder.Open (FolderAccess.ReadWrite, cancellationToken);

				if (folder.Count > 0)
					summaries = folder.Fetch (0, BatchSize, request, cancellationToken);

				// Proxy the PostProcess() method call to the GUI thread.
				Program.RunOnMainThread (messageList, PostProcess);
			}

			void PostProcess ()
			{
				messageList.OnFolderOpened (folder, summaries);
			}
		}

		public void OpenFolder (IMailFolder folder)
		{
			var command = new OpenFolderCommand (Program.ImapClientConnection, folder, this);
			Program.ImapCommandPipeline.Enqueue (command);
		}

		void MessageFlagsChangedInGuiThread (object state)
		{
			Debug.Assert (SynchronizationContext.Current == Program.GuiContext);

			var e = (MessageFlagsChangedEventArgs) state;

			lock (messages) {
				if (e.Index < messages.Count) {
					var info = messages[e.Index];
					var node = map[info];

					info.Flags = e.Flags;

					UpdateMessageNode (node);
				}
			}
		}

		void OnMessageFlagsChanged (object sender, MessageFlagsChangedEventArgs e)
		{
			// This event is raised by the ImapFolder and will be running in the IMAP Command Pipeline thread. Defer this back to the GUI thread.
			Program.GuiContext.Send (MessageFlagsChangedInGuiThread, e);
		}

		void MessageExpungedInGuiThread (object state)
		{
			Debug.Assert (SynchronizationContext.Current == Program.GuiContext);

			var e = (MessageEventArgs) state;

			lock (messages) {
				if (e.Index < messages.Count) {
					var info = messages[e.Index];
					var node = map[info];

					messages.RemoveAt (e.Index);
					map.Remove (info);
					node.Remove ();
				}
			}
		}

		void OnMessageExpunged (object sender, MessageEventArgs e)
		{
			// This event is raised by the ImapFolder and will be running in the IMAP Command Pipeline thread. Defer this back to the GUI thread.
			Program.GuiContext.Send (MessageExpungedInGuiThread, e);
		}

		class FetchNewMessagesCommand : ClientCommand<ImapClient>
		{
			readonly MessageList messageList;
			readonly IMailFolder folder;
			IList<IMessageSummary> summaries;

			public FetchNewMessagesCommand (ClientConnection<ImapClient> connection, IMailFolder folder, MessageList messageList) : base (connection)
			{
				this.messageList = messageList;
				this.folder = folder;
			}

			public override void Run (CancellationToken cancellationToken)
			{
				if (!folder.IsOpen)
					folder.Open (FolderAccess.ReadWrite, cancellationToken);

				if (folder.Count > 0) {
					int currentCount;

					lock (messageList.messages) {
						currentCount = messageList.messages.Count;
					}

					summaries = folder.Fetch (currentCount, currentCount + BatchSize, request, cancellationToken);

					// Proxy the PostProcess() method call to the GUI thread.
					Program.RunOnMainThread (messageList, PostProcess);
				}
			}

			void PostProcess ()
			{
				lock (messageList.messages) {
					messageList.AddMessageSummaries (folder, summaries);
				}
			}
		}

		void FetchNewMessages (IMailFolder folder)
		{
			var command = new FetchNewMessagesCommand (Program.ImapClientConnection, folder, this);
			Program.ImapCommandPipeline.Enqueue (command);
		}

		void OnCountChanged (object sender, EventArgs e)
		{
			var folder = (IMailFolder) sender;

			FetchNewMessages (folder);
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
