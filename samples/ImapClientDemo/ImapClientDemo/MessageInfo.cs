using MailKit;

namespace ImapClientDemo
{
	class MessageInfo
	{
		public readonly IMessageSummary Summary;
		public MessageFlags Flags;

		public MessageInfo (IMessageSummary summary)
		{
			Summary = summary;

			if (summary.Flags.HasValue)
				Flags = summary.Flags.Value;
		}
	}
}
