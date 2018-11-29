//
// ImapFolderFetch.cs
//
// Authors: Steffen Kieß <s-kiess@web.de>
//          Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2018 Xamarin Inc. (www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Text;
using System.Collections.Generic;

using MimeKit;

namespace MailKit.Net.Imap {
	/// <summary>
	/// An IMAP event group used with the NOTIFY command.
	/// </summary>
	/// <remarks>
	/// An IMAP event group used with the NOTIFY command.
	/// </remarks>
	public sealed class ImapEventGroup
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEventGroup"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEventGroup"/> class.
		/// </remarks>
		/// <param name="mailboxFilter">The mailbox filter.</param>
		/// <param name="events">The list of IMAP events.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="mailboxFilter"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="events"/> is <c>null</c>.</para>
		/// </exception>
		public ImapEventGroup (ImapMailboxFilter mailboxFilter, IList<ImapEvent> events)
		{
			if (mailboxFilter == null)
				throw new ArgumentNullException (nameof (mailboxFilter));

			if (events == null)
				throw new ArgumentNullException (nameof (events));

			MailboxFilter = mailboxFilter;
			Events = events;
		}

		/// <summary>
		/// Get the mailbox filter.
		/// </summary>
		/// <remarks>
		/// Gets the mailbox filter.
		/// </remarks>
		/// <value>The mailbox filter.</value>
		public ImapMailboxFilter MailboxFilter {
			get; private set;
		}

		/// <summary>
		/// Get the list of IMAP events.
		/// </summary>
		/// <remarks>
		/// Gets the list of IMAP events.
		/// </remarks>
		/// <value>The events.</value>
		public IList<ImapEvent> Events {
			get; private set;
		}

		/// <summary>
		/// Format the IMAP NOTIFY command for this particular IMAP event group.
		/// </summary>
		/// <remarks>
		/// Formats the IMAP NOTIFY command for this particular IMAP event group.
		/// </remarks>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="command">The IMAP command builder.</param>
		/// <param name="args">The IMAP command argument builder.</param>
		/// <param name="notifySelectedNewExpunge">Gets set to <c>true</c> if the NOTIFY command requests the MessageNew or
		/// MessageExpunged events for a SELECTED or SELECTED-DELAYED mailbox filter.</param>
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

	/// <summary>
	/// An IMAP mailbox filter for use with the NOTIFY command.
	/// </summary>
	/// <remarks>
	/// An IMAP mailbox filter for use with the NOTIFY command.
	/// </remarks>
	public class ImapMailboxFilter
	{
		/// <summary>
		/// An IMAP mailbox filter specifying that the client wants immediate notifications for
		/// the currently selected folder.
		/// </summary>
		/// <remarks>
		/// The <c>SELECTED</c> mailbox specifier requires the server to send immediate
		/// notifications for the currently selected mailbox about all specified
		/// message events.
		/// </remarks>
		public static readonly ImapMailboxFilter Selected = new ImapMailboxFilter ("SELECTED");

		/// <summary>
		/// An IMAP mailbox filter specifying the currently selected folder but delays notifications
		/// until a command has been issued.
		/// </summary>
		/// <remarks>
		/// The <c>SELECTED-DELAYED</c> mailbox specifier requires the server to delay a
		/// <see cref="ImapEvent.MessageExpunge"/> event until the client issues a command that allows
		/// returning information about expunged messages (see
		/// <a href="https://tools.ietf.org/html/rfc3501#section-7.4.1">Section 7.4.1 of RFC3501]</a>
		/// for more details), for example, till a <c>NOOP</c> or an <c>IDLE</c> command has been issued.
		/// When <c>SELECTED-DELAYED</c> is specified, the server MAY also delay returning other message
		/// events until the client issues one of the commands specified above, or it MAY return them
		/// immediately.
		/// </remarks>
		public static readonly ImapMailboxFilter SelectedDelayed = new ImapMailboxFilter ("SELECTED-DELAYED");

		/// <summary>
		/// An IMAP mailbox filter specifying the currently selected folder.
		/// </summary>
		/// <remarks>
		/// <para>The <c>INBOXES</c> mailbox specifier refers to all selectable mailboxes in the user's
		/// personal namespace(s) to which messages may be delivered by a Message Delivery Agent (MDA).
		/// </para>
		/// <para>If the IMAP server cannot easily compute this set, it MUST treat <see cref="Inboxes"/>
		/// as equivalent to <see cref="Personal"/>.</para>
		/// </remarks>
		public static readonly ImapMailboxFilter Inboxes = new ImapMailboxFilter ("INBOXES");

		/// <summary>
		/// An IMAP mailbox filter specifying all selectable folders within the user's personal namespace.
		/// </summary>
		/// <remarks>
		/// The <c>PERSONAL</c> mailbox specifier refers to all selectable folders within the user's personal namespace.
		/// </remarks>
		public static readonly ImapMailboxFilter Personal = new ImapMailboxFilter ("PERSONAL");

		/// <summary>
		/// An IMAP mailbox filter that refers to all subscribed folders.
		/// </summary>
		/// <remarks>
		/// <para>The <c>SUBSCRIBED</c> mailbox specifier refers to all folders subscribed to by the user.</para>
		/// <para>If the subscription list changes, the server MUST reevaluate the list.</para>
		/// </remarks>
		public static readonly ImapMailboxFilter Subscribed = new ImapMailboxFilter ("SUBSCRIBED");

		/// <summary>
		/// An IMAP mailbox filter that specifies a custom list of folders.
		/// </summary>
		/// <remarks>
		/// An IMAP mailbox filter that specifies a custom list of folders.
		/// </remarks>
		public abstract class MultiMailboxFilter : ImapMailboxFilter
		{
			readonly ImapFolder[] folders;

			protected MultiMailboxFilter (string name, IList<ImapFolder> folders) : base (name)
			{
				if (folders == null)
					throw new ArgumentNullException (nameof (folders));

				if (folders.Count == 0)
					throw new ArgumentException ("Must supply at least one folder.", nameof (folders));

				this.folders = new ImapFolder[folders.Count];
				for (int i = 0; i < folders.Count; i++) {
					if (folders[i] == null)
						throw new ArgumentException ("The list of folders cannot contain null.", nameof (folders));

					this.folders[i] = folders[i];
				}
			}

			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args)
			{
				command.Append (Name);
				command.Append (' ');

				if (folders.Length == 1) {
					command.Append ("%F");
					args.Add (folders[0]);
				} else {
					command.Append ("(");

					for (int i = 0; i < folders.Length; i++) {
						if (i > 0)
							command.Append (" ");
						command.Append ("%F");
						args.Add (folders[i]);
					}

					command.Append (")");
				}
			}
		}

		/// <summary>
		/// An IMAP mailbox filter that specifies a list of folders to receive notifications about.
		/// </summary>
		/// <remarks>
		/// An IMAP mailbox filter that specifies a list of folders to receive notifications about.
		/// </remarks>
		public sealed class Mailboxes : MultiMailboxFilter
		{
			public Mailboxes (IList<ImapFolder> folders) : base ("MAILBOXES", folders)
			{
			}

			public Mailboxes (params ImapFolder[] folders) : base ("MAILBOXES", folders)
			{
			}
		}

		/// <summary>
		/// An IMAP mailbox filter that specifies a list of folder subtrees to get notifications about.
		/// </summary>
		/// <remarks>
		/// <para>The client will receive notifications for each specified folder plus all selectable
		/// folders that are subordinate to any of the specified folders.</para>
		/// </remarks>
		public sealed class Subtree : MultiMailboxFilter
		{
			public Subtree (IList<ImapFolder> folders) : base ("SUBTREE", folders)
			{
			}

			public Subtree (params ImapFolder[] folders) : base ("SUBTREE", folders)
			{
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter"/> class.
		/// </remarks>
		/// <param name="name">The name of the mailbox filter.</param>
		internal ImapMailboxFilter (string name)
		{
			Name = name;
		}

		/// <summary>
		/// Get the name of the mailbox filter.
		/// </summary>
		/// <remarks>
		/// Gets the name of the mailbox filter.
		/// </remarks>
		/// <value>The name.</value>
		public string Name { get; private set; }

		/// <summary>
		/// Format the IMAP NOTIFY command for this particular IMAP mailbox filter.
		/// </summary>
		/// <remarks>
		/// Formats the IMAP NOTIFY command for this particular IMAP mailbox filter.
		/// </remarks>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="command">The IMAP command builder.</param>
		/// <param name="args">The IMAP command argument builder.</param>
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
