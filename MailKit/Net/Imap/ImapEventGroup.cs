//
// ImapFolderFetch.cs
//
// Authors: Steffen Kieß <s-kiess@web.de>
//          Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2020 .NET Foundation and Contributors
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
		/// MessageExpunged events for a SELECTED or SELECTED-DELAYED mailbox filter; otherwise it is left unchanged.</param>
		internal void Format (ImapEngine engine, StringBuilder command, IList<object> args, ref bool notifySelectedNewExpunge)
		{
			bool isSelectedFilter = MailboxFilter == ImapMailboxFilter.Selected || MailboxFilter == ImapMailboxFilter.SelectedDelayed;

			command.Append ("(");
			MailboxFilter.Format (engine, command, args);
			command.Append (" ");

			if (Events.Count > 0) {
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
			} else {
				command.Append ("NONE");
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
		/// An IMAP mailbox filter that specifies a list of folders to receive notifications about.
		/// </summary>
		/// <remarks>
		/// An IMAP mailbox filter that specifies a list of folders to receive notifications about.
		/// </remarks>
		public class Mailboxes : ImapMailboxFilter
		{
			readonly ImapFolder[] folders;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Mailboxes"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Mailboxes"/> class.
			/// </remarks>
			/// <param name="folders">The list of folders to watch for events.</param>
			/// <exception cref="System.ArgumentNullException">
			/// <paramref name="folders"/> is <c>null</c>.
			/// </exception>
			/// <exception cref="System.ArgumentException">
			/// <para>The list of <paramref name="folders"/> is empty.</para>
			/// <para>-or-</para>
			/// <para>The list of <paramref name="folders"/> contains folders that are not of
			/// type <see cref="ImapFolder"/>.</para>
			/// </exception>
			public Mailboxes (IList<IMailFolder> folders) : this ("MAILBOXES", folders)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Mailboxes"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Mailboxes"/> class.
			/// </remarks>
			/// <param name="folders">The list of folders to watch for events.</param>
			/// <exception cref="System.ArgumentNullException">
			/// <paramref name="folders"/> is <c>null</c>.
			/// </exception>
			/// <exception cref="System.ArgumentException">
			/// <para>The list of <paramref name="folders"/> is empty.</para>
			/// <para>-or-</para>
			/// <para>The list of <paramref name="folders"/> contains folders that are not of
			/// type <see cref="ImapFolder"/>.</para>
			/// </exception>
			public Mailboxes (params IMailFolder[] folders) : this ("MAILBOXES", folders)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Mailboxes"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Mailboxes"/> class.
			/// </remarks>
			/// <param name="name">The name of the mailbox filter.</param>
			/// <param name="folders">The list of folders to watch for events.</param>
			/// <exception cref="System.ArgumentNullException">
			/// <paramref name="folders"/> is <c>null</c>.
			/// </exception>
			/// <exception cref="System.ArgumentException">
			/// <para>The list of <paramref name="folders"/> is empty.</para>
			/// <para>-or-</para>
			/// <para>The list of <paramref name="folders"/> contains folders that are not of
			/// type <see cref="ImapFolder"/>.</para>
			/// </exception>
			internal Mailboxes (string name, IList<IMailFolder> folders) : base (name)
			{
				if (folders == null)
					throw new ArgumentNullException (nameof (folders));

				if (folders.Count == 0)
					throw new ArgumentException ("Must supply at least one folder.", nameof (folders));

				this.folders = new ImapFolder[folders.Count];
				for (int i = 0; i < folders.Count; i++) {
					if (!(folders[i] is ImapFolder folder))
						throw new ArgumentException ("All folders must be ImapFolders.", nameof (folders));

					this.folders[i] = folder;
				}
			}

			/// <summary>
			/// Format the IMAP NOTIFY command for this particular IMAP mailbox filter.
			/// </summary>
			/// <remarks>
			/// Formats the IMAP NOTIFY command for this particular IMAP mailbox filter.
			/// </remarks>
			/// <param name="engine">The IMAP engine.</param>
			/// <param name="command">The IMAP command builder.</param>
			/// <param name="args">The IMAP command argument builder.</param>
			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args)
			{
				command.Append (Name);
				command.Append (' ');

				// FIXME: should we verify that each ImapFolder belongs to this ImapEngine?

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
		/// An IMAP mailbox filter that specifies a list of folder subtrees to get notifications about.
		/// </summary>
		/// <remarks>
		/// <para>The client will receive notifications for each specified folder plus all selectable
		/// folders that are subordinate to any of the specified folders.</para>
		/// </remarks>
		public class Subtree : Mailboxes
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Subtree"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Subtree"/> class.
			/// </remarks>
			/// <param name="folders">The list of folders to watch for events.</param>
			/// <exception cref="System.ArgumentNullException">
			/// <paramref name="folders"/> is <c>null</c>.
			/// </exception>
			/// <exception cref="System.ArgumentException">
			/// <para>The list of <paramref name="folders"/> is empty.</para>
			/// <para>-or-</para>
			/// <para>The list of <paramref name="folders"/> contains folders that are not of
			/// type <see cref="ImapFolder"/>.</para>
			/// </exception>
			public Subtree (IList<IMailFolder> folders) : base ("SUBTREE", folders)
			{
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Subtree"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapMailboxFilter.Subtree"/> class.
			/// </remarks>
			/// <param name="folders">The list of folders to watch for events.</param>
			/// <exception cref="System.ArgumentNullException">
			/// <paramref name="folders"/> is <c>null</c>.
			/// </exception>
			/// <exception cref="System.ArgumentException">
			/// <para>The list of <paramref name="folders"/> is empty.</para>
			/// <para>-or-</para>
			/// <para>The list of <paramref name="folders"/> contains folders that are not of
			/// type <see cref="ImapFolder"/>.</para>
			/// </exception>
			public Subtree (params IMailFolder[] folders) : base ("SUBTREE", folders)
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

	/// <summary>
	/// An IMAP notification event.
	/// </summary>
	/// <remarks>
	/// An IMAP notification event.
	/// </remarks>
	public class ImapEvent
	{
		/// <summary>
		/// An IMAP event notification for expunged messages.
		/// </summary>
		/// <remarks>
		/// <para>If the expunged message or messages are in the selected mailbox, the server notifies the client
		/// using <see cref="IMailFolder.MessageExpunged"/> (or <see cref="IMailFolder.MessagesVanished"/> if
		/// the <a href="https://tools.ietf.org/html/rfc5162">QRESYNC</a> extension has been enabled via
		/// <see cref="ImapClient.EnableQuickResync(System.Threading.CancellationToken)"/> or
		/// <see cref="ImapClient.EnableQuickResyncAsync(System.Threading.CancellationToken)"/>).</para>
		/// <para>If the expunged message or messages are in another mailbox, the <see cref="IMailFolder.UidNext"/>
		/// and <see cref="IMailFolder.Count"/> properties will be updated and the appropriate
		/// <see cref="IMailFolder.UidNextChanged"/> and <see cref="IMailFolder.CountChanged"/> events will be
		/// emitted for the relevant folder. If the <a href="https://tools.ietf.org/html/rfc5162">QRESYNC</a>
		/// extension is enabled, the <see cref="IMailFolder.HighestModSeq"/> property will also be updated and
		/// the <see cref="IMailFolder.HighestModSeqChanged"/> event will be emitted.</para>
		/// <note type="note">if a client requests <see cref="MessageExpunge"/> with the <see cref="ImapMailboxFilter.Selected"/>
		/// mailbox specifier, the meaning of a message index can change at any time, so the client cannot use
		/// message indexes in commands anymore. The client MUST use API variants that take <see cref="UniqueId"/> or
		/// a <see cref="IList{UniqueId}"/>. The meaning of <c>*</c>* can also change when messages are added or expunged.
		/// A client wishing to keep using message indexes can either use the <see cref="ImapMailboxFilter.SelectedDelayed"/>
		/// mailbox specifier or can avoid using the <see cref="MessageExpunge"/> event entirely.</note>
		/// </remarks>
		public static readonly ImapEvent MessageExpunge = new ImapEvent ("MessageExpunge", true);

		/// <summary>
		/// An IMAP event notification for message flag changes.
		/// </summary>
		/// <remarks>
		/// <para>If the <see cref="FlagChange"/> notification arrives for a message located in the currently selected
		/// folder, then that folder will emit a <see cref="IMailFolder.MessageFlagsChanged"/> event as well as a
		/// <see cref="IMailFolder.MessageSummaryFetched"/> event with an appropriately populated
		/// <see cref="IMessageSummary"/>.</para>
		/// <para>On the other hand, if the <see cref="FlagChange"/> notification arrives for a message that is not
		/// located in the currently selected folder, then the events that are emitted will depend on the
		/// <see cref="ImapCapabilities"/> of the IMAP server.</para>
		/// <para>If the server supports the <see cref="ImapCapabilities.CondStore"/> capability (or the
		/// <see cref="ImapCapabilities.QuickResync"/> capability and the client has enabled it via
		/// <see cref="ImapClient.EnableQuickResync(System.Threading.CancellationToken)"/>), then the
		/// <see cref="IMailFolder.HighestModSeqChanged"/> event will be emitted as well as the
		/// <see cref="IMailFolder.UidValidityChanged"/> event (if the latter has changed). If the number of
		/// seen messages has changed, then the <see cref="IMailFolder.UnreadChanged"/> event may also be emitted.</para>
		/// <para>If the server does not support either the <see cref="ImapCapabilities.CondStore"/> capability nor
		/// the <see cref="ImapCapabilities.QuickResync"/> capability and the client has not enabled the later capability
		/// via <see cref="ImapClient.EnableQuickResync(System.Threading.CancellationToken)"/>, then the server may choose
		/// only to notify the client of <see cref="IMailFolder.UidValidity"/> changes by emitting the
		/// <see cref="IMailFolder.UidValidityChanged"/> event.</para>
		/// </remarks>
		public static readonly ImapEvent FlagChange = new ImapEvent ("FlagChange", true);

		/// <summary>
		/// An IMAP event notification for message annotation changes.
		/// </summary>
		/// <remarks>
		/// <para>If the <see cref="AnnotationChange"/> notification arrives for a message located in the currently selected
		/// folder, then that folder will emit a <see cref="IMailFolder.AnnotationsChanged"/> event as well as a
		/// <see cref="IMailFolder.MessageSummaryFetched"/> event with an appropriately populated
		/// <see cref="IMessageSummary"/>.</para>
		/// <para>On the other hand, if the <see cref="AnnotationChange"/> notification arrives for a message that is not
		/// located in the currently selected folder, then the events that are emitted will depend on the
		/// <see cref="ImapCapabilities"/> of the IMAP server.</para>
		/// <para>If the server supports the <see cref="ImapCapabilities.CondStore"/> capability (or the
		/// <see cref="ImapCapabilities.QuickResync"/> capability and the client has enabled it via
		/// <see cref="ImapClient.EnableQuickResync(System.Threading.CancellationToken)"/>), then the
		/// <see cref="IMailFolder.HighestModSeqChanged"/> event will be emitted as well as the
		/// <see cref="IMailFolder.UidValidityChanged"/> event (if the latter has changed). If the number of
		/// seen messages has changed, then the <see cref="IMailFolder.UnreadChanged"/> event may also be emitted.</para>
		/// <para>If the server does not support either the <see cref="ImapCapabilities.CondStore"/> capability nor
		/// the <see cref="ImapCapabilities.QuickResync"/> capability and the client has not enabled the later capability
		/// via <see cref="ImapClient.EnableQuickResync(System.Threading.CancellationToken)"/>, then the server may choose
		/// only to notify the client of <see cref="IMailFolder.UidValidity"/> changes by emitting the
		/// <see cref="IMailFolder.UidValidityChanged"/> event.</para>
		/// </remarks>
		public static readonly ImapEvent AnnotationChange = new ImapEvent ("AnnotationChange", true);

		/// <summary>
		/// AN IMAP event notification for folders that have been created, deleted, or renamed.
		/// </summary>
		/// <remarks>
		/// <para>These notifications are sent if an affected mailbox name was created, deleted, or renamed.</para>
		/// <para>As these notifications are received by the client, the apropriate will be emitted:
		/// <see cref="MailStore.FolderCreated"/>, <see cref="IMailFolder.Deleted"/>, or
		/// <see cref="IMailFolder.Renamed"/>, respectively.</para>
		/// <note type="info">If the server supports <see cref="ImapCapabilities.Acl"/>, granting or revocation of the
		/// <see cref="AccessRight.LookupFolder"/> right to the current user on the affected folder will also be
		/// considered folder creation or deletion, respectively. If a folder is created or deleted, the folder itself
		/// and its direct parent (whether it is an existing folder or not) are considered to be affected.</note>
		/// </remarks>
		public static readonly ImapEvent MailboxName = new ImapEvent ("MailboxName", false);

		/// <summary>
		/// An IMAP event notification for folders who have had their subscription status changed.
		/// </summary>
		/// <remarks>
		/// <para>This event requests that the server notifies the client of any subscription changes,
		/// causing the <see cref="IMailFolder.Subscribed"/> or <see cref="IMailFolder.Unsubscribed"/>
		/// events to be emitted accordingly on the affected <see cref="IMailFolder"/>.</para>
		/// </remarks>
		public static readonly ImapEvent SubscriptionChange = new ImapEvent ("SubscriptionChange", false);

		/// <summary>
		/// An IMAP event notification for changes to folder metadata.
		/// </summary>
		/// <remarks>
		/// <para>Support for this event type is OPTIONAL unless <see cref="ImapCapabilities.Metadata"/> is supported
		/// by the server, in which case support for this event type is REQUIRED.</para>
		/// <para>If the server does support this event, then the <see cref="IMailFolder.MetadataChanged"/> event
		/// will be emitted whenever metadata changes for any folder included in the <see cref="ImapMailboxFilter"/>.</para>
		/// </remarks>
		public static readonly ImapEvent MailboxMetadataChange = new ImapEvent ("MailboxMetadataChange", false);

		/// <summary>
		/// An IMAP event notification for changes to server metadata.
		/// </summary>
		/// <remarks>
		/// <para>Support for this event type is OPTIONAL unless <see cref="ImapCapabilities.Metadata"/> is supported
		/// by the server, in which case support for this event type is REQUIRED.</para>
		/// <para>If the server does support this event, then the <see cref="IMailStore.MetadataChanged"/> event
		/// will be emitted whenever metadata changes.</para>
		/// </remarks>
		public static readonly ImapEvent ServerMetadataChange = new ImapEvent ("ServerMetadataChange", false);

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent"/> class.
		/// </summary>
		/// <remarks>
		/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent"/> class.
		/// </remarks>
		/// <param name="name">The name of the IMAP event.</param>
		/// <param name="isMessageEvent"><c>true</c> if the event is a message event; otherwise, <c>false</c>.</param>
		internal ImapEvent (string name, bool isMessageEvent)
		{
			IsMessageEvent = isMessageEvent;
			Name = name;
		}

		/// <summary>
		/// Get whether or not this <see cref="T:MailKit.Net.Imap.ImapEvent"/> is a message event.
		/// </summary>
		/// <remarks>
		/// Gets whether or not this <see cref="T:MailKit.Net.Imap.ImapEvent"/> is a message event.
		/// </remarks>
		/// <value><c>true</c> if is message event; otherwise, <c>false</c>.</value>
		internal bool IsMessageEvent {
			get; private set;
		}

		/// <summary>
		/// Get the name of the IMAP event.
		/// </summary>
		/// <remarks>
		/// Gets the name of the IMAP event.
		/// </remarks>
		/// <value>The name of the IMAP event.</value>
		public string Name {
			get; private set;
		}

		/// <summary>
		/// Format the IMAP NOTIFY command for this particular IMAP mailbox filter.
		/// </summary>
		/// <remarks>
		/// Formats the IMAP NOTIFY command for this particular IMAP mailbox filter.
		/// </remarks>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="command">The IMAP command builder.</param>
		/// <param name="args">The IMAP command argument builder.</param>
		/// <param name="isSelectedFilter"><c>true</c> if the event is being registered for a
		/// <see cref="ImapMailboxFilter.Selected"/> or <see cref="ImapMailboxFilter.SelectedDelayed"/>
		/// mailbox filter.</param>
		internal virtual void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter)
		{
			command.Append (Name);
		}

		/// <summary>
		/// An IMAP event notification for new or appended messages.
		/// </summary>
		/// <remarks>
		/// <para>An IMAP event notification for new or appended messages.</para>
		/// <para>If the new or appended message is in the selected folder, the folder will emit the
		/// <see cref="IMailFolder.CountChanged"/> event, followed by a
		/// <see cref="IMailFolder.MessageSummaryFetched"/> event containing the information requested by the client.</para>
		/// <note type="note">These events will not be emitted for any message created by the client on this particular folder
		/// as a result of, for example, a call to
		/// <see cref="IMailFolder.Append(MimeMessage, MessageFlags, System.Threading.CancellationToken, ITransferProgress)"/>
		/// or <see cref="IMailFolder.CopyTo(IList{UniqueId}, IMailFolder, System.Threading.CancellationToken)"/>.</note>
		/// </remarks>
		public class MessageNew : ImapEvent
		{
			readonly MessageSummaryItems items;
			readonly HashSet<string> headers;

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent.MessageNew"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent.MessageNew"/> class.
			/// </remarks>
			/// <param name="items">The message summary items to automatically retrieve for new messages.</param>
			public MessageNew (MessageSummaryItems items = MessageSummaryItems.None) : base ("MessageNew", true)
			{
				headers = ImapFolder.EmptyHeaderFields;
				this.items = items;
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent.MessageNew"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent.MessageNew"/> class.
			/// </remarks>
			/// <param name="items">The message summary items to automatically retrieve for new messages.</param>
			/// <param name="headers">Additional message headers to retrieve for new messages.</param>
			public MessageNew (MessageSummaryItems items, HashSet<HeaderId> headers) : this (items)
			{
				this.headers = ImapUtils.GetUniqueHeaders (headers);
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent.MessageNew"/> class.
			/// </summary>
			/// <remarks>
			/// Initializes a new instance of the <see cref="T:MailKit.Net.Imap.ImapEvent.MessageNew"/> class.
			/// </remarks>
			/// <param name="items">The message summary items to automatically retrieve for new messages.</param>
			/// <param name="headers">Additional message headers to retrieve for new messages.</param>
			public MessageNew (MessageSummaryItems items, HashSet<string> headers) : this (items)
			{
				this.headers = ImapUtils.GetUniqueHeaders (headers);
			}

			/// <summary>
			/// Format the IMAP NOTIFY command for this particular IMAP mailbox filter.
			/// </summary>
			/// <remarks>
			/// Formats the IMAP NOTIFY command for this particular IMAP mailbox filter.
			/// </remarks>
			/// <param name="engine">The IMAP engine.</param>
			/// <param name="command">The IMAP command builder.</param>
			/// <param name="args">The IMAP command argument builder.</param>
			/// <param name="isSelectedFilter"><c>true</c> if the event is being registered for a
			/// <see cref="ImapMailboxFilter.Selected"/> or <see cref="ImapMailboxFilter.SelectedDelayed"/>
			/// mailbox filter.</param>
			internal override void Format (ImapEngine engine, StringBuilder command, IList<object> args, bool isSelectedFilter)
			{
				command.Append (Name);

				if (items == MessageSummaryItems.None && headers.Count == 0)
					return;

				if (!isSelectedFilter)
					throw new InvalidOperationException ("The MessageNew event cannot have any parameters for mailbox filters other than SELECTED and SELECTED-DELAYED.");

				var xitems = items;
				bool previewText;

				command.Append (" ");
				command.Append (ImapFolder.FormatSummaryItems (engine, ref xitems, headers, out previewText, isNotify: true));
			}
		}
	}
}
