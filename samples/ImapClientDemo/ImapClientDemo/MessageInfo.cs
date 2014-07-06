using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

using MailKit;

namespace ImapClientDemo
{
	class MessageInfo
	{
		public readonly MessageSummary Summary;
		public MessageFlags Flags;

		public MessageInfo (MessageSummary summary)
		{
			Summary = summary;

			if (summary.Flags.HasValue)
				Flags = summary.Flags.Value;
		}
	}
}
