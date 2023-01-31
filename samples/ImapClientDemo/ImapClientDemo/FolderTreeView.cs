using System;
using System.Linq;
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
	class FolderTreeView : TreeView
	{
		readonly Dictionary<IMailFolder, TreeNode> map = new Dictionary<IMailFolder, TreeNode> ();

		public FolderTreeView ()
		{
			FullRowSelect = true;

			ImageList = new ImageList ();
			ImageList.Images.Add ("folder", GetImageResource ("folder.png"));
			ImageList.Images.Add ("inbox", GetImageResource ("inbox.png"));
			ImageList.Images.Add ("archive", GetImageResource ("archive.png"));
			ImageList.Images.Add ("drafts", GetImageResource ("pencil.png"));
			ImageList.Images.Add ("flagged", GetImageResource ("flag.png"));
			ImageList.Images.Add ("important", GetImageResource ("important.png"));
			ImageList.Images.Add ("junk", GetImageResource ("junk.png"));
			ImageList.Images.Add ("sent", GetImageResource ("paper-plane.png"));
			ImageList.Images.Add ("trash-empty", GetImageResource ("trash-empty.png"));
			ImageList.Images.Add ("trash-full", GetImageResource ("trash-full.png"));
		}

		static Image GetImageResource (string name)
		{
			return Image.FromStream (typeof (FolderTreeView).Assembly.GetManifestResourceStream ("ImapClientDemo.Resources." + name));
		}

		static bool CheckFolderForChildren (ClientConnection<ImapClient> connection, IMailFolder folder)
		{
			if (connection.Client.Capabilities.HasFlag (ImapCapabilities.Children)) {
				if (folder.Attributes.HasFlag (FolderAttributes.HasChildren))
					return true;
			} else if (!folder.Attributes.HasFlag (FolderAttributes.NoInferiors)) {
				return true;
			}

			return false;
		}

		void UpdateFolderNode (IMailFolder folder)
		{
			Debug.Assert (SynchronizationContext.Current == Program.GuiContext);

			var node = map[folder];

			if (folder.Unread > 0) {
				node.Text = string.Format ("{0} ({1})", folder.Name, folder.Unread);
				node.NodeFont = new Font (node.NodeFont, FontStyle.Bold);
			} else {
				node.NodeFont = new Font (node.NodeFont, FontStyle.Regular);
				node.Text = folder.Name;
			}

			if (folder.Attributes.HasFlag (FolderAttributes.Trash))
				node.SelectedImageKey = node.ImageKey = folder.Count > 0 ? "trash-full" : "trash-empty";
		}

		TreeNode CreateFolderNode (ClientConnection<ImapClient> connection, IMailFolder folder)
		{
			Debug.Assert (SynchronizationContext.Current == Program.GuiContext);

			var node = new TreeNode (folder.Name) { Tag = folder, ToolTipText = folder.FullName };

			node.NodeFont = new Font (Font, FontStyle.Regular);

			if (folder == connection.Client.Inbox)
				node.SelectedImageKey = node.ImageKey = "inbox";
			else if (folder.Attributes.HasFlag (FolderAttributes.Archive))
				node.SelectedImageKey = node.ImageKey = "archive";
			else if (folder.Attributes.HasFlag (FolderAttributes.Drafts))
				node.SelectedImageKey = node.ImageKey = "drafts";
			else if (folder.Attributes.HasFlag (FolderAttributes.Flagged))
				node.SelectedImageKey = node.ImageKey = "flagged";
			else if (folder.FullName == "[Gmail]/Important")
				node.SelectedImageKey = node.ImageKey = "important";
			else if (folder.Attributes.HasFlag (FolderAttributes.Junk))
				node.SelectedImageKey = node.ImageKey = "junk";
			else if (folder.Attributes.HasFlag (FolderAttributes.Sent))
				node.SelectedImageKey = node.ImageKey = "sent";
			else if (folder.Attributes.HasFlag (FolderAttributes.Trash))
				node.SelectedImageKey = node.ImageKey = folder.Count > 0 ? "trash-full" : "trash-empty";
			else
				node.SelectedImageKey = node.ImageKey = "folder";

			if (CheckFolderForChildren (connection, folder))
				node.Nodes.Add ("Loading...");

			return node;
		}

		void LoadSubfolders (ClientConnection<ImapClient> connection, IMailFolder folder, IList<IMailFolder> subfolders)
		{
			TreeNodeCollection nodes;

			if (map.TryGetValue (folder, out var node)) {
				// removes the dummy "Loading..." folder
				nodes = node.Nodes;
				nodes.Clear ();
			} else {
				nodes = Nodes;
			}

			foreach (var subfolder in subfolders) {
				node = CreateFolderNode (connection, subfolder);
				map[subfolder] = node;
				nodes.Add (node);

				subfolder.MessageFlagsChanged += OnMessageFlagsChanged;
				subfolder.CountChanged += OnFolderCountChanged;

				if (!subfolder.Attributes.HasFlag (FolderAttributes.NonExistent) && !subfolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
					if (connection.Client.Capabilities.HasFlag (ImapCapabilities.ListStatus)) {
						// Note: If the IMAP server supports LIST-STATUS, then we obtained the STATUS information for each subfolder already.
						UpdateFolderNode (subfolder);
					} else {
						// ... If not, then we will queue a STATUS command ourselves.
						//
						// Note: Technically, ImapFolder.GetSubfolders(StatusItems, bool, CancellationToken) would send a STATUS command for us
						// for each subfolder if the IMAP server doesn't support LIST-STATUS, but I'm doing things this way so that we can display
						// a list of folders sooner and then asynchronously update each folder with an unread count as that information becomes
						// available.
						QueueUpdateUnreadCount (subfolder);
					}
				}
			}
		}

		class LoadSubfoldersCommand : ClientCommand<ImapClient>
		{
			List<IMailFolder> subfolders;

			protected LoadSubfoldersCommand (ClientConnection<ImapClient> connection, FolderTreeView treeView) : base (connection)
			{
				TreeView = treeView;
			}

			public LoadSubfoldersCommand (ClientConnection<ImapClient> connection, ImapFolder folder, FolderTreeView treeView) : this (connection, treeView)
			{
				Folder = folder;
			}

			protected FolderTreeView TreeView {
				get; private set;
			}

			protected IMailFolder Folder {
				get; set;
			}

			public override void Run (CancellationToken cancellationToken)
			{
				// Note: If the IMAP server supports LIST-STATUS, then we'll get the status of the subfolders as we get the list,
				// otherwise, we'll queue a StatusCommand for each subfolder in LoadSubfolders().
				if (Connection.Client.Capabilities.HasFlag (ImapCapabilities.ListStatus))
					subfolders = Folder.GetSubfolders (StatusItems.Unread, false, cancellationToken).ToList ();
				else
					subfolders = Folder.GetSubfolders (false, cancellationToken).ToList ();

				subfolders.Sort (FolderNameComparer.Default);

				// Proxy the PostProcess() method call to the GUI thread.
				Program.RunOnMainThread (TreeView, PostProcess);
			}

			protected virtual void PostProcess ()
			{
				TreeView.LoadSubfolders (Connection, Folder, subfolders);
			}
		}

		class LoadRootFoldersCommand : LoadSubfoldersCommand
		{
			public LoadRootFoldersCommand (ClientConnection<ImapClient> connection, FolderTreeView treeView) : base (connection, treeView)
			{
			}

			public override void Run (CancellationToken cancellationToken)
			{
				Folder = Connection.Client.GetFolder (Connection.Client.PersonalNamespaces[0]);

				base.Run (cancellationToken);
			}

			protected override void PostProcess ()
			{
				TreeView.PathSeparator = Folder.DirectorySeparator.ToString ();
				base.PostProcess ();
			}
		}

		class StatusCommand : ClientCommand<ImapClient>
		{
			readonly FolderTreeView treeView;
			readonly IMailFolder folder;

			public StatusCommand (ClientConnection<ImapClient> connection, IMailFolder folder, FolderTreeView treeView) : base (connection)
			{
				this.folder = folder;
				this.treeView = treeView;
			}

			public override void Run (CancellationToken cancellationToken)
			{
				if (!folder.IsOpen)
				folder.Status (StatusItems.Unread, cancellationToken);

				// Proxy the PostProcess() method call to the GUI thread.
				Program.RunOnMainThread (treeView, PostProcess);
			}

			void PostProcess ()
			{
				treeView.UpdateFolderNode (folder);
			}
		}

		public void LoadFolders ()
		{
			var command = new LoadRootFoldersCommand (Program.ImapClientConnection, this);
			Program.ImapCommandPipeline.Enqueue (command);
		}

		void QueueUpdateUnreadCount (IMailFolder folder)
		{
			if (!folder.IsOpen) {
				var command = new StatusCommand (Program.ImapClientConnection, folder, this);
				Program.ImapCommandPipeline.Enqueue (command);
			} else {
				Program.RunOnMainThread (this, () => UpdateFolderNode (folder));
			}
		}

		void OnFolderCountChanged (object sender, EventArgs e)
		{
			var folder = (IMailFolder) sender;

			QueueUpdateUnreadCount (folder);
		}

		void OnMessageFlagsChanged (object sender, MessageFlagsChangedEventArgs e)
		{
			var folder = (IMailFolder) sender;

			QueueUpdateUnreadCount (folder);
		}

		protected override void OnBeforeExpand (TreeViewCancelEventArgs e)
		{
			if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag == null) {
				// this folder has never been expanded before...
				var folder = (ImapFolder) e.Node.Tag;

				var command = new LoadSubfoldersCommand (Program.ImapClientConnection, folder, this);
				Program.ImapCommandPipeline.Enqueue (command);
			}

			base.OnBeforeExpand (e);
		}

		protected override void OnBeforeSelect (TreeViewCancelEventArgs e)
		{
			var folder = (IMailFolder) e.Node.Tag;

			// don't allow the user to select a folder with the \NoSelect or \NonExistent attribute
			if (folder == null || folder.Attributes.HasFlag (FolderAttributes.NoSelect) ||
				folder.Attributes.HasFlag (FolderAttributes.NonExistent)) {
				e.Cancel = true;
				return;
			}

			base.OnBeforeSelect (e);
		}

		public event EventHandler<FolderSelectedEventArgs> FolderSelected;

		protected override void OnAfterSelect (TreeViewEventArgs e)
		{
			FolderSelected?.Invoke (this, new FolderSelectedEventArgs ((IMailFolder) e.Node.Tag));

			base.OnAfterSelect (e);
		}
	}
}
