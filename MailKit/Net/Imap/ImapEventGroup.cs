using System;
using System.Text;
using System.Collections.Generic;

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

	public abstract class ImapMailboxFilter
	{
		sealed class Simple : ImapMailboxFilter
		{
			public string Name { get; private set; }

			public Simple (string name)
			{
				Name = name;
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args)
			{
				command.Append (Name);
			}
		}

		internal ImapMailboxFilter ()
		{
		}

		internal abstract void Format (ImapEngine engine, StringBuilder command, IList<object> args);

		public static readonly ImapMailboxFilter Selected = new Simple ("SELECTED");
		public static readonly ImapMailboxFilter SelectedDelayed = new Simple ("SELECTED-DELAYED");
		public static readonly ImapMailboxFilter Inboxes = new Simple ("INBOXES");
		public static readonly ImapMailboxFilter Personal = new Simple ("PERSONAL");
		public static readonly ImapMailboxFilter Subscribed = new Simple ("SUBSCRIBED");

		public sealed class Subtree : ImapMailboxFilter
		{
			public IList<ImapFolder> Folders { get; private set; }

			public Subtree (IList<ImapFolder> folders)
			{
				if (folders == null)
					throw new ArgumentNullException (nameof (folders));

				if (folders.Count == 0)
					throw new ArgumentException ("Must supply at least one folder.", nameof (folders));

				Folders = folders;
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args)
			{
				command.Append ("SUBTREE ");

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

		public sealed class Mailboxes : ImapMailboxFilter
		{
			public IList<ImapFolder> Folders { get; private set; }

			public Mailboxes (IList<ImapFolder> folders)
			{
				if (folders == null)
					throw new ArgumentNullException (nameof (folders));

				if (folders.Count == 0)
					throw new ArgumentException ("Must supply at least one folder.", nameof (folders));

				Folders = folders;
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args)
			{
				command.Append ("MAILBOXES ");

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
	}

	public abstract class ImapEvent
	{
		sealed class Simple : ImapEvent
		{
			internal string Name { get; private set; }

			internal Simple (string name, bool isMessageEvent) : base (isMessageEvent)
			{
				Name = name;
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter)
			{
				command.Append (Name);
			}
		}

		internal ImapEvent (bool isMessageEvent)
		{
			IsMessageEvent = isMessageEvent;
		}

		internal bool IsMessageEvent { get; private set; }

		internal abstract void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter);

		public static readonly ImapEvent MessageExpunge = new Simple ("MessageExpunge", true);
		public static readonly ImapEvent FlagChange = new Simple ("FlagChange", true);
		public static readonly ImapEvent AnnotationChange = new Simple ("AnnotationChange", true);

		public static readonly ImapEvent MailboxName = new Simple ("MailboxName", false);
		public static readonly ImapEvent SubscriptionChange = new Simple ("SubscriptionChange", false);
		public static readonly ImapEvent MailboxMetadataChange = new Simple ("MailboxMetadataChange", false);
		public static readonly ImapEvent ServerMetadataChange = new Simple ("ServerMetadataChange", false);

		public sealed class MessageNew : ImapEvent
		{
			public MessageSummaryItems MessageSummaryItems { get; private set; }
			public HashSet<string> Fields { get; private set; }

			public MessageNew (MessageSummaryItems messageSummaryItems = MessageSummaryItems.None, HashSet<string> fields = null) : base (true)
			{
				MessageSummaryItems = messageSummaryItems;
				Fields = fields;
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter)
			{
				command.Append ("MessageNew");

				if (MessageSummaryItems == MessageSummaryItems.None && Fields == null)
					return;

				if (!isSelectedFilter)
					throw new InvalidOperationException ("The MessageNew event cannot have any parameters for mailbox filters other than SELECTED and SELECTED-DELAYED.");

				var items = MessageSummaryItems;
				bool previewText;

				command.Append (" ");
				command.Append (ImapFolder.FormatSummaryItems (engine, ref items, Fields, out previewText, isNotify: true));
			}
		}
	}
}
