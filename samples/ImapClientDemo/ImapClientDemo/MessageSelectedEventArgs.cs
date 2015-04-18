using System;

using MailKit;

namespace ImapClientDemo
{
	class MessageSelectedEventArgs : EventArgs
	{
		public MessageSelectedEventArgs (IMailFolder folder, UniqueId uid, BodyPart body)
		{
			Folder = folder;
			UniqueId = uid;
			Body = body;
		}

		public IMailFolder Folder {
			get; private set;
		}

		public UniqueId UniqueId {
			get; private set;
		}

		public BodyPart Body {
			get; private set;
		}
	}
}
