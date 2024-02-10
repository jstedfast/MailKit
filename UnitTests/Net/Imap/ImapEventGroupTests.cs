//
// ImapEventGroupTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2013-2024 .NET Foundation and Contributors
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

using System.Text;

using MailKit;
using MailKit.Net.Imap;

using MimeKit;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapEventGroupTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new ImapEventGroup (null, new List<ImapEvent> ()));
			Assert.Throws<ArgumentNullException> (() => new ImapEventGroup (ImapMailboxFilter.Selected, (IList<ImapEvent>) null));

			Assert.Throws<ArgumentNullException> (() => new ImapEventGroup (null));
			Assert.Throws<ArgumentNullException> (() => new ImapEventGroup (ImapMailboxFilter.Selected, null));

			Assert.Throws<ArgumentNullException> (() => new ImapMailboxFilter.Mailboxes (null));
			Assert.Throws<ArgumentNullException> (() => new ImapMailboxFilter.Mailboxes ((IList<IMailFolder>) null));

			Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Mailboxes ());
			Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Mailboxes ((IList<IMailFolder>) Array.Empty<IMailFolder> ()));

			Assert.Throws<ArgumentNullException> (() => new ImapMailboxFilter.Subtree (null));
			Assert.Throws<ArgumentNullException> (() => new ImapMailboxFilter.Subtree ((IList<IMailFolder>) null));

			Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Subtree ());
			Assert.Throws<ArgumentException> (() => new ImapMailboxFilter.Subtree ((IList<IMailFolder>) Array.Empty<IMailFolder> ()));

			Assert.Throws<ArgumentNullException> (() => new ImapEvent.MessageNew (null));
		}

		static void AssertFormatEventGroup (ImapEventGroup eventGroup, string expected, bool expectedNotify)
		{
			using var engine = new ImapEngine (null);
			bool notifySelectedNewExpunge = false;
			var command = new StringBuilder ();
			var args = new List<object> ();

			if (expected == null) {
				Assert.Throws<InvalidOperationException> (() => eventGroup.Format (engine, command, args, ref notifySelectedNewExpunge));
			} else {
				eventGroup.Format (engine, command, args, ref notifySelectedNewExpunge);

				Assert.That (command.ToString (), Is.EqualTo (expected));
				Assert.That (notifySelectedNewExpunge, Is.EqualTo (expectedNotify), "notifySelectedNewExpunge");
			}
		}

		[Test]
		public void TestFormatEventGroup_None ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, Array.Empty<ImapEvent> ());

			AssertFormatEventGroup (eventGroup, "(INBOXES NONE)", false);
		}

		[Test]
		public void TestFormatEventGroup_AnnotationChange_Requires_MessageNew_And_MessageExpunge ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.AnnotationChange);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_AnnotationChange_MessageNew_Requires_MessageExpunge ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.AnnotationChange, new ImapEvent.MessageNew (MessageSummaryItems.None));

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_AnnotationChange_MessageExpunge_Requires_MessageNew ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.AnnotationChange, ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_FlagChange_Requires_MessageNew_And_MessageExpunge ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.FlagChange);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_FlagChange_MessageNew_Requires_MessageExpunge ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.FlagChange, new ImapEvent.MessageNew (MessageSummaryItems.None));

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_FlagChange_MessageExpunge_Requires_MessageNew ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.FlagChange, ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_FlagChange_MessageNew_MessageExpunge_AnnotationChange ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.FlagChange, new ImapEvent.MessageNew (MessageSummaryItems.None), ImapEvent.MessageExpunge, ImapEvent.AnnotationChange);

			AssertFormatEventGroup (eventGroup, "(INBOXES (FlagChange MessageNew MessageExpunge AnnotationChange))", false);
		}

		[Test]
		public void TestFormatEventGroup_MessageExpunge_Requires_MessageNew ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_MessageNew_Requires_MessageExpunge ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, new ImapEvent.MessageNew (MessageSummaryItems.None));

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_MessageNew_MessageExpunge ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, new ImapEvent.MessageNew (MessageSummaryItems.None), ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, "(INBOXES (MessageNew MessageExpunge))", false);
		}

		[Test]
		public void TestFormatEventGroup_MessageNew_Headers_Requires_Selected ()
		{
			var headers = new HashSet<HeaderId> (new HeaderId[] { HeaderId.From, HeaderId.Subject, HeaderId.Date });
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, new ImapEvent.MessageNew (MessageSummaryItems.None, headers), ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_MessageNew_Items_Requires_Selected ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Inboxes, new ImapEvent.MessageNew (MessageSummaryItems.Full), ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_MessageNew_WithSpecificHeaderIds ()
		{
			var headers = new HashSet<HeaderId> (new HeaderId[] { HeaderId.From, HeaderId.Subject, HeaderId.Date });
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Selected, new ImapEvent.MessageNew (MessageSummaryItems.None, headers), ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, "(SELECTED (MessageNew (BODY.PEEK[HEADER.FIELDS (FROM SUBJECT DATE)]) MessageExpunge))", true);
		}

		[Test]
		public void TestFormatEventGroup_MessageNew_WithSpecificHeaderNames ()
		{
			var headers = new HashSet<string> (new string[] { "From", "Subject", "Date" });
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Selected, new ImapEvent.MessageNew (MessageSummaryItems.None, headers), ImapEvent.MessageExpunge);

			AssertFormatEventGroup (eventGroup, "(SELECTED (MessageNew (BODY.PEEK[HEADER.FIELDS (FROM SUBJECT DATE)]) MessageExpunge))", true);
		}

		[Test]
		public void TestFormatEventGroup_Selected_Requires_OnlyMessageEvents ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.Selected, ImapEvent.ServerMetadataChange);

			AssertFormatEventGroup (eventGroup, null, false);
		}

		[Test]
		public void TestFormatEventGroup_SelectedDelayed_Requires_OnlyMessageEvents ()
		{
			var eventGroup = new ImapEventGroup (ImapMailboxFilter.SelectedDelayed, ImapEvent.ServerMetadataChange);

			AssertFormatEventGroup (eventGroup, null, false);
		}
	}
}
