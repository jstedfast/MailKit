using System;

using MailKit;

namespace ImapClientDemo
{
	class FolderSelectedEventArgs : EventArgs
	{
		public FolderSelectedEventArgs (IMailFolder folder)
		{
			Folder = folder;
		}

		public IMailFolder Folder {
			get; private set;
		}
	}
}
