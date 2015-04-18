using System;
using System.Drawing;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms;

using MailKit.Net.Imap;
using MailKit;

namespace ImapClientDemo
{
	[ToolboxItem (true)]
	class FolderTreeView : TreeView
	{
		readonly Dictionary<IMailFolder, TreeNode> map = new Dictionary<IMailFolder, TreeNode> ();

		public FolderTreeView ()
		{
			ImageList = new ImageList ();
			ImageList.Images.Add ("folder", GetImageResource ("folder.png"));
			ImageList.Images.Add ("inbox", GetImageResource ("inbox.png"));
			ImageList.Images.Add ("archive", GetImageResource ("archive.png"));
			ImageList.Images.Add ("drafts", GetImageResource ("pencil.png"));
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

		static bool CheckFolderForChildren (IMailFolder folder)
		{
			if (Program.Client.Capabilities.HasFlag (ImapCapabilities.Children)) {
				if (folder.Attributes.HasFlag (FolderAttributes.HasChildren))
					return true;
			} else if (!folder.Attributes.HasFlag (FolderAttributes.NoInferiors)) {
				return true;
			}

			return false;
		}

		void UpdateFolderNode (IMailFolder folder)
		{
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

		delegate void UpdateFolderNodeDelegate (IMailFolder folder);

		TreeNode CreateFolderNode (IMailFolder folder)
		{
			var node = new TreeNode (folder.Name) { Tag = folder, ToolTipText = folder.FullName };

			node.NodeFont = new Font (Font, FontStyle.Regular);

			if (folder == Program.Client.Inbox)
				node.SelectedImageKey = node.ImageKey = "inbox";
			else if (folder.Attributes.HasFlag (FolderAttributes.Archive))
				node.SelectedImageKey = node.ImageKey = "archive";
			else if (folder.Attributes.HasFlag (FolderAttributes.Drafts))
				node.SelectedImageKey = node.ImageKey = "drafts";
			else if (folder.Attributes.HasFlag (FolderAttributes.Flagged))
				node.SelectedImageKey = node.ImageKey = "important";
			else if (folder.Attributes.HasFlag (FolderAttributes.Junk))
				node.SelectedImageKey = node.ImageKey = "junk";
			else if (folder.Attributes.HasFlag (FolderAttributes.Sent))
				node.SelectedImageKey = node.ImageKey = "sent";
			else if (folder.Attributes.HasFlag (FolderAttributes.Trash))
				node.SelectedImageKey = node.ImageKey = folder.Count > 0 ? "trash-full" : "trash-empty";
			else
				node.SelectedImageKey = node.ImageKey = "folder";

			if (CheckFolderForChildren (folder))
				node.Nodes.Add ("Loading...");

			return node;
		}

		void LoadChildFolders (IMailFolder folder, IEnumerable<IMailFolder> children)
		{
			TreeNodeCollection nodes;
			TreeNode node;

			if (map.TryGetValue (folder, out node)) {
				nodes = node.Nodes;
				nodes.Clear ();
			} else {
				nodes = Nodes;
			}

			foreach (var child in children) {
				node = CreateFolderNode (child);
				map[child] = node;
				nodes.Add (node);

				// Note: because we are using the *Async() methods, these events will fire
				// in another thread so we'll have to proxy them back to the main thread.
				child.MessageFlagsChanged += UpdateUnreadCount_TaskThread;
				child.CountChanged += UpdateUnreadCount_TaskThread;

				if (!child.Attributes.HasFlag (FolderAttributes.NonExistent) && !child.Attributes.HasFlag (FolderAttributes.NoSelect)) {
					child.StatusAsync (StatusItems.Unread).ContinueWith (task => {
						Invoke (new UpdateFolderNodeDelegate (UpdateFolderNode), child);
					});
				}
			}
		}

		async void LoadChildFolders (IMailFolder folder)
		{
			var children = await folder.GetSubfoldersAsync ();

			LoadChildFolders (folder, children);
		}

		public void LoadFolders ()
		{
			var personal = Program.Client.GetFolder (Program.Client.PersonalNamespaces[0]);

			PathSeparator = personal.DirectorySeparator.ToString ();

			LoadChildFolders (personal);
		}

		async void UpdateUnreadCount (object sender, EventArgs e)
		{
			var folder = (IMailFolder) sender;

			await folder.StatusAsync (StatusItems.Unread);
			UpdateFolderNode (folder);
		}

		void UpdateUnreadCount_TaskThread (object sender, EventArgs e)
		{
			// proxy to the main thread
			Invoke (new EventHandler<EventArgs> (UpdateUnreadCount), sender, e);
		}

		protected override void OnBeforeExpand (TreeViewCancelEventArgs e)
		{
			if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag == null) {
				// this folder has never been expanded before...
				var folder = (IMailFolder) e.Node.Tag;

				LoadChildFolders (folder);
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
			var handler = FolderSelected;

			if (handler != null)
				handler (this, new FolderSelectedEventArgs ((IMailFolder) e.Node.Tag));

			base.OnAfterSelect (e);
		}
	}
}
