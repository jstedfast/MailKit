using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

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
