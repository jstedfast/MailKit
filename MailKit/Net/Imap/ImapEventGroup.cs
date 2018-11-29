using System;
using System.Text;
using System.Collections.Generic;

using MimeKit;

namespace MailKit.Net.Imap {
	public sealed class ImapEventGroup
	{
		public ImapMailboxFilter MailboxFilter { get; private set; }

		public IList<ImapEvent> Events { get; private set; }

		public ImapEventGroup (ImapMailboxFilter mailboxFilter, IList<ImapEvent> events)
		{
			if (mailboxFilter == null)
				throw new ArgumentNullException (nameof (mailboxFilter));

			if (events == null)
				throw new ArgumentNullException (nameof (events));

			MailboxFilter = mailboxFilter;
			Events = events;
		}

		internal void Format (ImapEngine engine, StringBuilder command, IList<object> args, out bool notifySelectedNewExpunge)
		{
			bool isSelectedFilter = MailboxFilter == ImapMailboxFilter.Selected || MailboxFilter == ImapMailboxFilter.SelectedDelayed;

			command.Append ("(");
			MailboxFilter.Format (engine, command, args);
			command.Append (" ");

			if (Events.Count == 0) {
				command.Append ("NONE");
				notifySelectedNewExpunge = false;
			} else {
				var haveAnnotationChange = false;
				var haveMessageExpunge = false;
				var haveMessageNew = false;
				var haveFlagChange = false;

				command.Append ("(");

				for (int i = 0; i < Events.Count; i++) {
					var @event = Events[i];

					if (isSelectedFilter && !@event.IsMessageEvent)
						throw new InvalidOperationException ("Only message events may be specified when SELECTED or SELECTED-DELAYED is used.");

					if (@event is ImapEvent.MessageNew)
						haveMessageNew = true;
					else if (@event == ImapEvent.MessageExpunge)
						haveMessageExpunge = true;
					else if (@event == ImapEvent.FlagChange)
						haveFlagChange = true;
					else if (@event == ImapEvent.AnnotationChange)
						haveAnnotationChange = true;

					if (i > 0)
						command.Append (" ");

					@event.Format (engine, command, args, isSelectedFilter);
				}
				command.Append (")");

				// https://tools.ietf.org/html/rfc5465#section-5
				if ((haveMessageNew && !haveMessageExpunge) || (!haveMessageNew && haveMessageExpunge))
					throw new InvalidOperationException ("If MessageNew or MessageExpunge is specified, both must be specified.");

				if ((haveFlagChange || haveAnnotationChange) && (!haveMessageNew || !haveMessageExpunge))
					throw new InvalidOperationException ("If FlagChange and/or AnnotationChange are specified, MessageNew and MessageExpunge must also be specified.");

				notifySelectedNewExpunge = (haveMessageNew || haveMessageExpunge) && MailboxFilter == ImapMailboxFilter.Selected;
			}
			command.Append (")");
		}
	}

	public class ImapMailboxFilter
	{
		public static readonly ImapMailboxFilter Selected = new ImapMailboxFilter ("SELECTED");
		public static readonly ImapMailboxFilter SelectedDelayed = new ImapMailboxFilter ("SELECTED-DELAYED");
		public static readonly ImapMailboxFilter Inboxes = new ImapMailboxFilter ("INBOXES");
		public static readonly ImapMailboxFilter Personal = new ImapMailboxFilter ("PERSONAL");
		public static readonly ImapMailboxFilter Subscribed = new ImapMailboxFilter ("SUBSCRIBED");

		public abstract class MultiMailboxFilter : ImapMailboxFilter
		{
			protected MultiMailboxFilter (string name, IEnumerable<ImapFolder> folders) : base (name)
			{
				if (folders == null)
					throw new ArgumentNullException (nameof (folders));

				Folders = new List<ImapFolder> (folders);

				if (Folders.Count == 0)
					throw new ArgumentException ("Must supply at least one folder.", nameof (folders));
			}

			public List<ImapFolder> Folders { get; private set; }

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args)
			{
				command.Append (Name);
				command.Append (' ');

				if (Folders.Count == 1) {
					command.Append ("%F");
					args.Add (Folders[0]);
				} else {
					command.Append ("(");

					for (int i = 0; i < Folders.Count; i++) {
						if (i > 0)
							command.Append (" ");
						command.Append ("%F");
						args.Add (Folders[i]);
					}

					command.Append (")");
				}
			}
		}

		public sealed class Subtree : MultiMailboxFilter
		{
			public Subtree (IList<ImapFolder> folders) : base ("SUBTREE", folders)
			{
			}
		}

		public sealed class Mailboxes : MultiMailboxFilter
		{
			public Mailboxes (IList<ImapFolder> folders) : base ("MAILBOXES", folders)
			{
			}
		}

		internal ImapMailboxFilter (string name)
		{
			Name = name;
		}

		public string Name { get; private set; }

		internal virtual void Format (ImapEngine engine, StringBuilder command, IList<object> args)
		{
			command.Append (Name);
		}
	}

	public class ImapEvent
	{
		public static readonly ImapEvent MessageExpunge = new ImapEvent ("MessageExpunge", true);
		public static readonly ImapEvent FlagChange = new ImapEvent ("FlagChange", true);
		public static readonly ImapEvent AnnotationChange = new ImapEvent ("AnnotationChange", true);

		public static readonly ImapEvent MailboxName = new ImapEvent ("MailboxName", false);
		public static readonly ImapEvent SubscriptionChange = new ImapEvent ("SubscriptionChange", false);
		public static readonly ImapEvent MailboxMetadataChange = new ImapEvent ("MailboxMetadataChange", false);
		public static readonly ImapEvent ServerMetadataChange = new ImapEvent ("ServerMetadataChange", false);

		internal ImapEvent (string name, bool isMessageEvent)
		{
			IsMessageEvent = isMessageEvent;
			Name = name;
		}

		public string Name { get; private set; }

		internal bool IsMessageEvent { get; private set; }

		internal virtual void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter)
		{
			command.Append (Name);
		}

		public sealed class MessageNew : ImapEvent
		{
			public MessageSummaryItems MessageSummaryItems { get; private set; }
			public HashSet<string> Headers { get; private set; }

			public MessageNew (MessageSummaryItems messageSummaryItems = MessageSummaryItems.None, HashSet<string> fields = null) : base ("MessageNew", true)
			{
				MessageSummaryItems = messageSummaryItems;
				Headers = fields;
			}

			public MessageNew (MessageSummaryItems messageSummaryItems, HashSet<HeaderId> fields) : base ("MessageNew", true)
			{
				MessageSummaryItems = messageSummaryItems;
				Headers = ImapFolder.GetHeaderNames (fields);
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter)
			{
				command.Append (Name);

				if (MessageSummaryItems == MessageSummaryItems.None && (Headers == null || Headers.Count == 0))
					return;

				if (!isSelectedFilter)
					throw new InvalidOperationException ("The MessageNew event cannot have any parameters for mailbox filters other than SELECTED and SELECTED-DELAYED.");

				var items = MessageSummaryItems;
				bool previewText;

				command.Append (" ");
				command.Append (ImapFolder.FormatSummaryItems (engine, ref items, Headers, out previewText, isNotify: true));
			}
		}
	}
}
