using System;
using System.Collections.Generic;

using MailKit;

namespace ImapClientDemo
{
	class FolderNameComparer : IComparer<IMailFolder>
	{
		public static readonly FolderNameComparer Default = new FolderNameComparer ();

		public int Compare (IMailFolder x, IMailFolder y)
		{
			return string.Compare (x.Name, y.Name, StringComparison.CurrentCulture);
		}
	}
}
