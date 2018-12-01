using System;
using System.Drawing;
using System.ComponentModel;
using System.Collections.Generic;
using System.Threading.Tasks;
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

			if (CheckFolderForChildren (folder))
				node.Nodes.Add ("Loading...");

			return node;
		}

		async Task LoadSubfoldersAsync (IMailFolder folder, IList<IMailFolder> subfolders)
		{
			TreeNodeCollection nodes;
			TreeNode node;

			if (map.TryGetValue (folder, out node)) {
				// removes the dummy "Loading..." folder
				nodes = node.Nodes;
				nodes.Clear ();
			} else {
				nodes = Nodes;
			}

			foreach (var subfolder in subfolders) {
				node = CreateFolderNode (subfolder);
				map[subfolder] = node;
				nodes.Add (node);

				subfolder.MessageFlagsChanged += UpdateUnreadCount;
				subfolder.CountChanged += UpdateUnreadCount;

				if (!subfolder.Attributes.HasFlag (FolderAttributes.NonExistent) && !subfolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
					await subfolder.StatusAsync (StatusItems.Unread);
					UpdateFolderNode (subfolder);
				}
			}
		}

		class FolderComparer : IComparer<IMailFolder>
		{
			public int Compare (IMailFolder x, IMailFolder y)
			{
				return string.Compare (x.Name, y.Name, StringComparison.CurrentCulture);
			}
		}

		async Task LoadSubfoldersAsync (IMailFolder folder)
		{
			var subfolders = await folder.GetSubfoldersAsync ();
			var sorted = new List<IMailFolder> (subfolders);
			
			sorted.Sort (new FolderComparer ());

			await LoadSubfoldersAsync (folder, sorted);
		}

		public Task LoadFoldersAsync ()
		{
			var personal = Program.Client.GetFolder (Program.Client.PersonalNamespaces[0]);

			PathSeparator = personal.DirectorySeparator.ToString ();

			return LoadSubfoldersAsync (personal);
		}

		async Task UpdateUnreadCountAsync (Task task, object state)
		{
			var folder = (IMailFolder) state;

			await task;

			await folder.StatusAsync (StatusItems.Unread);
			UpdateFolderNode (folder);
		}

		void UpdateUnreadCount (object sender, EventArgs e)
		{
			Program.Queue (UpdateUnreadCountAsync, sender);
		}

		async Task ExpandFolderAsync (Task task, object state)
		{
			var folder = (IMailFolder) state;

			await task;

			await LoadSubfoldersAsync (folder);
		}

		protected override void OnBeforeExpand (TreeViewCancelEventArgs e)
		{
			if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag == null) {
				// this folder has never been expanded before...
				var folder = e.Node.Tag;

				Program.Queue (ExpandFolderAsync, folder);
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
