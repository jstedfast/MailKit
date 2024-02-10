//
// EventArgsTests.cs
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

using MimeKit;
using MailKit;

namespace UnitTests {
	[TestFixture]
	public class EventArgsTests
	{
		[Test]
		public void TestAlertEventArgs ()
		{
			var args = new AlertEventArgs ("Klingons on the starboard bow!");

			Assert.That (args.Message, Is.EqualTo ("Klingons on the starboard bow!"));

			Assert.Throws<ArgumentNullException> (() => new AlertEventArgs (null));
		}

		[Test]
		public void TestWebAlertEventArgs ()
		{
			var args = new WebAlertEventArgs (new Uri ("http://www.google.com/"), "Klingons on the starboard bow!");

			Assert.That (args.WebUri.AbsoluteUri, Is.EqualTo ("http://www.google.com/"));
			Assert.That (args.Message, Is.EqualTo ("Klingons on the starboard bow!"));

			Assert.Throws<ArgumentNullException> (() => new WebAlertEventArgs (null, "message text."));
			Assert.Throws<ArgumentNullException> (() => new WebAlertEventArgs (new Uri ("http://www.google.com/"), null));
		}

		[Test]
		public void TestAnnotationsChangedEventArgs ()
		{
			var annotations = new List<Annotation> ();
			var args = new AnnotationsChangedEventArgs (0, annotations);
			Assert.That (args.Annotations, Is.Empty);
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			Assert.Throws<ArgumentNullException> (() => new AnnotationsChangedEventArgs (0, null));
		}

		[Test]
		public void TestAuthenticatedEventArgs ()
		{
			var args = new AuthenticatedEventArgs ("Access Granted.");

			Assert.That (args.Message, Is.EqualTo ("Access Granted."));

			Assert.Throws<ArgumentNullException> (() => new AuthenticatedEventArgs (null));
		}

		[Test]
		public void TestFolderCreatedEventArgs ()
		{
			Assert.Throws<ArgumentNullException> (() => new FolderCreatedEventArgs (null));
		}

		[Test]
		public void TestFolderRenamedEventArgs ()
		{
			var args = new FolderRenamedEventArgs ("Istanbul", "Constantinople");

			Assert.That (args.OldName, Is.EqualTo ("Istanbul"));
			Assert.That (args.NewName, Is.EqualTo ("Constantinople"));

			Assert.Throws<ArgumentNullException> (() => new FolderRenamedEventArgs (null, "name"));
			Assert.Throws<ArgumentNullException> (() => new FolderRenamedEventArgs ("name", null));
		}

		[Test]
		public void TestMessageEventArgs ()
		{
			var args = new MessageEventArgs (0);

			Assert.That (args.Index, Is.EqualTo (0));

			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageEventArgs (-1));
		}

		[Test]
		public void TestMessageFlagsChangedEventArgs ()
		{
			var keywords = new HashSet<string> (new [] { "custom1", "custom2" });
			MessageFlagsChangedEventArgs args;
			var uid = new UniqueId (5);
			ulong modseq = 724;

			args = new MessageFlagsChangedEventArgs (0);
			Assert.That (args.Keywords, Is.Empty);
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.None));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered);
			Assert.That (args.Keywords, Is.Empty);
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, modseq);
			Assert.That (args.Keywords, Is.Empty);
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, keywords);
			Assert.That (args.Keywords, Has.Count.EqualTo (keywords.Count));
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, keywords, modseq);
			Assert.That (args.Keywords, Has.Count.EqualTo (keywords.Count));
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered);
			Assert.That (args.Keywords, Is.Empty);
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId, Is.EqualTo (uid));
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, modseq);
			Assert.That (args.Keywords, Is.Empty);
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId, Is.EqualTo (uid));
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, keywords);
			Assert.That (args.Keywords, Has.Count.EqualTo (keywords.Count));
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId, Is.EqualTo (uid));
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, keywords, modseq);
			Assert.That (args.Keywords, Has.Count.EqualTo (keywords.Count));
			Assert.That (args.Flags, Is.EqualTo (MessageFlags.Answered));
			Assert.That (args.UniqueId, Is.EqualTo (uid));
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, MessageFlags.Answered));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, MessageFlags.Answered, modseq));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, MessageFlags.Answered, keywords));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, MessageFlags.Answered, keywords, modseq));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, uid, MessageFlags.Answered));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, uid, MessageFlags.Answered, modseq));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, uid, MessageFlags.Answered, keywords));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageFlagsChangedEventArgs (-1, uid, MessageFlags.Answered, keywords, modseq));

			Assert.Throws<ArgumentNullException> (() => new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, null));
			Assert.Throws<ArgumentNullException> (() => new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, null, modseq));
			Assert.Throws<ArgumentNullException> (() => new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, null));
			Assert.Throws<ArgumentNullException> (() => new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, null, modseq));
		}

		[Test]
		public void TestMessageLabelsChangedEventArgs ()
		{
			var labels = new string[] { "label1", "label2" };
			MessageLabelsChangedEventArgs args;
			var uid = new UniqueId (5);
			ulong modseq = 724;

			args = new MessageLabelsChangedEventArgs (0);
			Assert.That (args.Labels, Is.Null);
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageLabelsChangedEventArgs (0, labels);
			Assert.That (args.Labels, Has.Count.EqualTo (labels.Length));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageLabelsChangedEventArgs (0, labels, modseq);
			Assert.That (args.Labels, Has.Count.EqualTo (labels.Length));
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageLabelsChangedEventArgs (0, uid, labels);
			Assert.That (args.Labels, Has.Count.EqualTo (labels.Length));
			Assert.That (args.UniqueId, Is.EqualTo (uid));
			Assert.That (args.ModSeq.HasValue, Is.False);
			Assert.That (args.Index, Is.EqualTo (0));

			args = new MessageLabelsChangedEventArgs (0, uid, labels, modseq);
			Assert.That (args.Labels, Has.Count.EqualTo (labels.Length));
			Assert.That (args.UniqueId, Is.EqualTo (uid));
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageLabelsChangedEventArgs (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageLabelsChangedEventArgs (-1, labels));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageLabelsChangedEventArgs (-1, labels, modseq));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageLabelsChangedEventArgs (-1, uid, labels));
			Assert.Throws<ArgumentOutOfRangeException> (() => new MessageLabelsChangedEventArgs (-1, uid, labels, modseq));

			Assert.Throws<ArgumentNullException> (() => new MessageLabelsChangedEventArgs (0, null));
			Assert.Throws<ArgumentNullException> (() => new MessageLabelsChangedEventArgs (0, null, modseq));
			Assert.Throws<ArgumentNullException> (() => new MessageLabelsChangedEventArgs (0, uid, null));
			Assert.Throws<ArgumentNullException> (() => new MessageLabelsChangedEventArgs (0, uid, null, modseq));
		}

		[Test]
		public void TestMessageSentEventArgs ()
		{
			var message = new MimeMessage ();
			MessageSentEventArgs args;

			args = new MessageSentEventArgs (message, "response");

			Assert.That (args.Message, Is.EqualTo (message));
			Assert.That (args.Response, Is.EqualTo ("response"));

			Assert.Throws<ArgumentNullException> (() => new MessageSentEventArgs (null, "response"));
			Assert.Throws<ArgumentNullException> (() => new MessageSentEventArgs (message, null));
		}

		[Test]
		public void TestMessageSummaryFetchedEventArgs ()
		{
			var message = new MessageSummary (0);
			MessageSummaryFetchedEventArgs args;

			args = new MessageSummaryFetchedEventArgs (message);

			Assert.That (args.Message, Is.EqualTo (message));

			Assert.Throws<ArgumentNullException> (() => new MessageSummaryFetchedEventArgs (null));
		}

		[Test]
		public void TestMessagesVanishedEventArgs ()
		{
			var uids = new UniqueIdRange (0, 5, 7);
			MessagesVanishedEventArgs args;

			args = new MessagesVanishedEventArgs (uids, true);

			Assert.That (args.UniqueIds, Is.EqualTo (uids));
			Assert.That (args.Earlier, Is.True);

			Assert.Throws<ArgumentNullException> (() => new MessagesVanishedEventArgs (null, false));
		}

		[Test]
		public void TestMetadataChangedEventArgs ()
		{
			var args = new MetadataChangedEventArgs (new Metadata (MetadataTag.PrivateComment, "this is a comment"));

			Assert.That (args.Metadata.Tag, Is.EqualTo (MetadataTag.PrivateComment), "Tag");
			Assert.That (args.Metadata.Value, Is.EqualTo ("this is a comment"), "Value");

			Assert.Throws<ArgumentNullException> (() => new MetadataChangedEventArgs (null));
		}

		[Test]
		public void TestModSeqChangedEventArgs ()
		{
			ModSeqChangedEventArgs args;
			ulong modseq = 724;

			args = new ModSeqChangedEventArgs (0);
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq, Is.EqualTo (0));
			Assert.That (args.Index, Is.EqualTo (0));

			args = new ModSeqChangedEventArgs (0, modseq);
			Assert.That (args.UniqueId.HasValue, Is.False);
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			args = new ModSeqChangedEventArgs (0, UniqueId.MinValue, modseq);
			Assert.That (args.UniqueId, Is.EqualTo (UniqueId.MinValue));
			Assert.That (args.ModSeq, Is.EqualTo (modseq));
			Assert.That (args.Index, Is.EqualTo (0));

			Assert.Throws<ArgumentOutOfRangeException> (() => new ModSeqChangedEventArgs (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new ModSeqChangedEventArgs (-1, modseq));
			Assert.Throws<ArgumentOutOfRangeException> (() => new ModSeqChangedEventArgs (-1, UniqueId.MinValue, modseq));
		}
	}
}
