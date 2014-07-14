namespace ImapClientDemo
{
	partial class MainWindow
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose (bool disposing)
		{
			if (disposing && (components != null)) {
				components.Dispose ();
			}
			base.Dispose (disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent ()
		{
			this.components = new System.ComponentModel.Container();
			this.folderPanel = new System.Windows.Forms.Panel();
			this.folderTreeView = new ImapClientDemo.FolderTreeView();
			this.messageListPanel = new System.Windows.Forms.Panel();
			this.messageList = new ImapClientDemo.MessageList();
			this.webBrowser = new System.Windows.Forms.WebBrowser();
			this.folderPanel.SuspendLayout();
			this.messageListPanel.SuspendLayout();
			this.SuspendLayout();
			// 
			// folderPanel
			// 
			this.folderPanel.Controls.Add(this.folderTreeView);
			this.folderPanel.Location = new System.Drawing.Point(0, 0);
			this.folderPanel.Name = "folderPanel";
			this.folderPanel.Size = new System.Drawing.Size(200, 487);
			this.folderPanel.TabIndex = 0;
			// 
			// folderTreeView
			// 
			this.folderTreeView.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.folderTreeView.ImageIndex = 0;
			this.folderTreeView.Location = new System.Drawing.Point(0, 0);
			this.folderTreeView.Name = "folderTreeView";
			this.folderTreeView.PathSeparator = "/";
			this.folderTreeView.SelectedImageIndex = 0;
			this.folderTreeView.Size = new System.Drawing.Size(200, 487);
			this.folderTreeView.TabIndex = 0;
			// 
			// messageListPanel
			// 
			this.messageListPanel.Controls.Add(this.messageList);
			this.messageListPanel.Location = new System.Drawing.Point(206, 0);
			this.messageListPanel.Name = "messageListPanel";
			this.messageListPanel.Size = new System.Drawing.Size(328, 487);
			this.messageListPanel.TabIndex = 1;
			// 
			// messageList
			// 
			this.messageList.Font = new System.Drawing.Font("Segoe UI", 9.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.messageList.Location = new System.Drawing.Point(0, 0);
			this.messageList.Name = "messageList";
			this.messageList.Size = new System.Drawing.Size(328, 487);
			this.messageList.TabIndex = 0;
			// 
			// webBrowser
			// 
			this.webBrowser.Location = new System.Drawing.Point(540, 0);
			this.webBrowser.MinimumSize = new System.Drawing.Size(20, 20);
			this.webBrowser.Name = "webBrowser";
			this.webBrowser.Size = new System.Drawing.Size(391, 487);
			this.webBrowser.TabIndex = 2;
			// 
			// MainWindow
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(933, 487);
			this.Controls.Add(this.webBrowser);
			this.Controls.Add(this.messageListPanel);
			this.Controls.Add(this.folderPanel);
			this.Name = "MainWindow";
			this.Text = "MainWindow";
			this.folderPanel.ResumeLayout(false);
			this.messageListPanel.ResumeLayout(false);
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.Panel folderPanel;
		private FolderTreeView folderTreeView;
		private System.Windows.Forms.Panel messageListPanel;
		private MessageList messageList;
		private System.Windows.Forms.WebBrowser webBrowser;

	}
}