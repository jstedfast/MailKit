using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ImapClientDemo
{
	public partial class MainWindow : Form
	{
		public MainWindow ()
		{
			InitializeComponent ();

			folderTreeView.FolderSelected += FolderSelected;
		}

		void FolderSelected (object sender, FolderSelectedEventArgs e)
		{
			messageList.OpenFolder (e.Folder);
		}

		public void LoadContent ()
		{
			folderTreeView.LoadFolders ();
		}

		protected override void OnClosed (EventArgs e)
		{
			base.OnClosed (e);

			Application.Exit ();
		}
	}
}
