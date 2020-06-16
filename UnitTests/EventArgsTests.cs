//
// EventArgsTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
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
using System.Collections;
using System.Collections.Generic;

using NUnit.Framework;

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

			Assert.AreEqual ("Klingons on the starboard bow!", args.Message);

			Assert.Throws<ArgumentNullException> (() => new AlertEventArgs (null));
		}

		[Test]
		public void TestAnnotationsChangedEventArgs ()
		{
			var annotations = new List<Annotation> ();
			var args = new AnnotationsChangedEventArgs (0, annotations);
			Assert.AreEqual (0, args.Annotations.Count);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			Assert.Throws<ArgumentNullException> (() => new AnnotationsChangedEventArgs (0, null));
		}

		[Test]
		public void TestAuthenticatedEventArgs ()
		{
			var args = new AuthenticatedEventArgs ("Access Granted.");

			Assert.AreEqual ("Access Granted.", args.Message);

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

			Assert.AreEqual ("Istanbul", args.OldName);
			Assert.AreEqual ("Constantinople", args.NewName);

			Assert.Throws<ArgumentNullException> (() => new FolderRenamedEventArgs (null, "name"));
			Assert.Throws<ArgumentNullException> (() => new FolderRenamedEventArgs ("name", null));
		}

		[Test]
		public void TestMessageEventArgs ()
		{
			var args = new MessageEventArgs (0);

			Assert.AreEqual (0, args.Index);

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
			Assert.AreEqual (0, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.None, args.Flags);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered);
			Assert.AreEqual (0, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, modseq);
			Assert.AreEqual (0, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, keywords);
			Assert.AreEqual (keywords.Count, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, MessageFlags.Answered, keywords, modseq);
			Assert.AreEqual (keywords.Count, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered);
			Assert.AreEqual (0, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.AreEqual (uid, args.UniqueId);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, modseq);
			Assert.AreEqual (0, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.AreEqual (uid, args.UniqueId);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, keywords);
			Assert.AreEqual (keywords.Count, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.AreEqual (uid, args.UniqueId);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageFlagsChangedEventArgs (0, uid, MessageFlags.Answered, keywords, modseq);
			Assert.AreEqual (keywords.Count, args.UserFlags.Count);
			Assert.AreEqual (MessageFlags.Answered, args.Flags);
			Assert.AreEqual (uid, args.UniqueId);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

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
			Assert.IsNull (args.Labels);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageLabelsChangedEventArgs (0, labels);
			Assert.AreEqual (labels.Length, args.Labels.Count);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageLabelsChangedEventArgs (0, labels, modseq);
			Assert.AreEqual (labels.Length, args.Labels.Count);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			args = new MessageLabelsChangedEventArgs (0, uid, labels);
			Assert.AreEqual (labels.Length, args.Labels.Count);
			Assert.AreEqual (uid, args.UniqueId);
			Assert.IsFalse (args.ModSeq.HasValue);
			Assert.AreEqual (0, args.Index);

			args = new MessageLabelsChangedEventArgs (0, uid, labels, modseq);
			Assert.AreEqual (labels.Length, args.Labels.Count);
			Assert.AreEqual (uid, args.UniqueId);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

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

			Assert.AreEqual (message, args.Message);
			Assert.AreEqual ("response", args.Response);

			Assert.Throws<ArgumentNullException> (() => new MessageSentEventArgs (null, "response"));
			Assert.Throws<ArgumentNullException> (() => new MessageSentEventArgs (message, null));
		}

		[Test]
		public void TestMessageSummaryFetchedEventArgs ()
		{
			var message = new MessageSummary (0);
			MessageSummaryFetchedEventArgs args;

			args = new MessageSummaryFetchedEventArgs (message);

			Assert.AreEqual (message, args.Message);

			Assert.Throws<ArgumentNullException> (() => new MessageSummaryFetchedEventArgs (null));
		}

		[Test]
		public void TestMessagesVanishedEventArgs ()
		{
			var uids = new UniqueIdRange (0, 5, 7);
			MessagesVanishedEventArgs args;

			args = new MessagesVanishedEventArgs (uids, true);

			Assert.AreEqual (uids, args.UniqueIds);
			Assert.IsTrue (args.Earlier);

			Assert.Throws<ArgumentNullException> (() => new MessagesVanishedEventArgs (null, false));
		}

		[Test]
		public void TestMetadataChangedEventArgs ()
		{
			var args = new MetadataChangedEventArgs (new Metadata (MetadataTag.PrivateComment, "this is a comment"));

			Assert.AreEqual (MetadataTag.PrivateComment, args.Metadata.Tag, "Tag");
			Assert.AreEqual ("this is a comment", args.Metadata.Value, "Value");

			Assert.Throws<ArgumentNullException> (() => new MetadataChangedEventArgs (null));
		}

		[Test]
		public void TestModSeqChangedEventArgs ()
		{
			ModSeqChangedEventArgs args;
			ulong modseq = 724;

			args = new ModSeqChangedEventArgs (0);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.AreEqual (0, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			args = new ModSeqChangedEventArgs (0, modseq);
			Assert.IsFalse (args.UniqueId.HasValue);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			args = new ModSeqChangedEventArgs (0, UniqueId.MinValue, modseq);
			Assert.AreEqual (UniqueId.MinValue, args.UniqueId);
			Assert.AreEqual (modseq, args.ModSeq);
			Assert.AreEqual (0, args.Index);

			Assert.Throws<ArgumentOutOfRangeException> (() => new ModSeqChangedEventArgs (-1));
			Assert.Throws<ArgumentOutOfRangeException> (() => new ModSeqChangedEventArgs (-1, modseq));
			Assert.Throws<ArgumentOutOfRangeException> (() => new ModSeqChangedEventArgs (-1, UniqueId.MinValue, modseq));
		}
	}
}
