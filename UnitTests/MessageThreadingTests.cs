//
// MessageThreadingTests.cs
//
// Author: Jeffrey Stedfast <jestedfa@microsoft.com>
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
using System.Linq;
using System.Text;
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;
using MimeKit.Utils;

using MailKit;
using MailKit.Search;

namespace UnitTests {
	[TestFixture]
	public class MessageThreadingTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var orderBy = new OrderBy[] { OrderBy.Arrival };
			var messagesMissingInfo = new [] { new MessageSummary (0) };
			var emptyOrderBy = new OrderBy[0];
			int depth;

			var summary = new MessageSummary (0);
			summary.UniqueId = UniqueId.MinValue;
			summary.Envelope = new Envelope ();
			summary.References = new MessageIdList ();
			summary.Envelope.MessageId = "xyz@mimekit.org";
			summary.Envelope.Subject = "This is the subject";
			summary.Envelope.Date = DateTimeOffset.Now;
			summary.Size = 0;

			var messages = new MessageSummary[] { summary };

			Assert.Throws<ArgumentNullException> (() => MessageThreader.GetThreadableSubject (null, out depth));
			Assert.Throws<ArgumentNullException> (() => MessageThreader.Thread ((IEnumerable<MessageSummary>) null, ThreadingAlgorithm.References));
			Assert.Throws<ArgumentNullException> (() => MessageThreader.Thread ((IEnumerable<MessageSummary>) null, ThreadingAlgorithm.References, orderBy));
			Assert.Throws<ArgumentException> (() => MessageThreader.Thread (messagesMissingInfo, ThreadingAlgorithm.References));
			Assert.Throws<ArgumentNullException> (() => MessageThreader.Thread (messages, ThreadingAlgorithm.References, null));
			Assert.Throws<ArgumentException> (() => MessageThreader.Thread (messages, ThreadingAlgorithm.References, emptyOrderBy));
		}

		[Test]
		public void TestThreadableSubject ()
		{
			string result;
			int depth;

			result = MessageThreader.GetThreadableSubject ("Re: simple subject", out depth);
			Assert.AreEqual ("simple subject", result, "#1a");
			Assert.AreEqual (1, depth, "#1b");

			result = MessageThreader.GetThreadableSubject ("Re: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#2a");
			Assert.AreEqual (1, depth, "#2b");

			result = MessageThreader.GetThreadableSubject ("Re: Re: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#3a");
			Assert.AreEqual (2, depth, "#3b");

			result = MessageThreader.GetThreadableSubject ("Re: Re[4]: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#4a");
			Assert.AreEqual (5, depth, "#4b");

			result = MessageThreader.GetThreadableSubject ("Re: [Mailing-List] Re[4]: simple subject  ", out depth);
			Assert.AreEqual ("simple subject", result, "#5a");
			Assert.AreEqual (5, depth, "#5b");
		}

		MessageSummary MakeThreadable (ref int index, string subject, string msgid, string date, string refs)
		{
			DateTimeOffset value;

			DateUtils.TryParse (date, out value);

			var summary = new MessageSummary (++index);
			summary.UniqueId = new UniqueId ((uint) summary.Index);
			summary.Envelope = new Envelope ();
			summary.References = new MessageIdList ();
			if (refs != null) {
				foreach (var id in refs.Split (new [] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
					summary.References.Add (id);
			}
			summary.Envelope.MessageId = MimeUtils.EnumerateReferences (msgid).FirstOrDefault ();
			summary.Envelope.Subject = subject;
			summary.Envelope.Date = value;
			summary.Size = 0;

			return summary;
		}

		void WriteMessageThread (StringBuilder builder, IList<MessageSummary> messages, MessageThread thread, int depth)
		{
			builder.Append (new string (' ', depth * 3));

			if (thread.UniqueId.HasValue) {
				var summary = messages[(int) thread.UniqueId.Value.Id - 1];
				builder.Append (summary.Envelope.Subject);
			} else {
				builder.Append ("dummy");
			}

			builder.Append ('\n');

			foreach (var child in thread.Children)
				WriteMessageThread (builder, messages, child, depth + 1);
		}

		[Test]
		public void TestThreadBySubject ()
		{
			const string defaultDate = "01 Jan 1997 12:00:00 -0400";
			var messages = new List<MessageSummary> ();
			int index = 0;

			// this test case was borrowed from Jamie Zawinski's TestThreader.java
			messages.Add (MakeThreadable (ref index, "Subject", "<1>", defaultDate, null));
			messages.Add (MakeThreadable (ref index, "Re[2]: Subject", "<2>", defaultDate, "<1>"));
			messages.Add (MakeThreadable (ref index, "Re: Subject", "<3>", defaultDate, "<1> <2>"));
			messages.Add (MakeThreadable (ref index, "Re: Re: Subject", "<4>", defaultDate, "<1>"));
			messages.Add (MakeThreadable (ref index, "Re:RE:rE[3]: Subject", "<5>", defaultDate, "<3> <x1> <x2> <x3>"));

			string expected = @"Subject
   Re[2]: Subject
   Re: Subject
   Re: Re: Subject
   Re:RE:rE[3]: Subject
".Replace ("\r\n", "\n");

			var threads = messages.Thread (ThreadingAlgorithm.OrderedSubject);
			var builder = new StringBuilder ();

			foreach (var thread in threads)
				WriteMessageThread (builder, messages, thread, 0);

			//Console.WriteLine (builder);

			Assert.AreEqual (expected, builder.ToString (), "Threading did not produce the expected results");
		}

		[Test]
		public void TestThreadByReferences ()
		{
			const string defaultDate = "01 Jan 1997 12:00:00 -0400";
			var messages = new List<MessageSummary> ();
			int index = 0;

			// this test case was borrowed from Jamie Zawinski's TestThreader.java
			messages.Add (MakeThreadable (ref index, "A", "<1>", defaultDate, null));
			messages.Add (MakeThreadable (ref index, "B", "<2>", defaultDate, "<1>"));
			messages.Add (MakeThreadable (ref index, "C", "<3>", defaultDate, "<1> <2>"));
			messages.Add (MakeThreadable (ref index, "D", "<4>", defaultDate, "<1>"));
			messages.Add (MakeThreadable (ref index, "E", "<5>", defaultDate, "<3> <x1> <x2> <x3>"));
			messages.Add (MakeThreadable (ref index, "F", "<6>", defaultDate, "<2>"));
			messages.Add (MakeThreadable (ref index, "G", "<7>", defaultDate, "<nonesuch>"));
			messages.Add (MakeThreadable (ref index, "H", "<8>", defaultDate, "<nonesuch>"));

			messages.Add (MakeThreadable (ref index, "Loop1", "<loop1>", defaultDate, "<loop2> <loop3>"));
			messages.Add (MakeThreadable (ref index, "Loop2", "<loop2>", defaultDate, "<loop3> <loop1>"));
			messages.Add (MakeThreadable (ref index, "Loop3", "<loop3>", defaultDate, "<loop1> <loop2>"));

			messages.Add (MakeThreadable (ref index, "Loop4", "<loop4>", defaultDate, "<loop5>"));
			messages.Add (MakeThreadable (ref index, "Loop5", "<loop5>", defaultDate, "<loop4>"));

			messages.Add (MakeThreadable (ref index, "Loop6", "<loop6>", defaultDate, "<loop6>"));

			messages.Add (MakeThreadable (ref index, "Loop7",  "<loop7>",  defaultDate, "<loop8>  <loop9>  <loop10> <loop8>  <loop9> <loop10>"));
			messages.Add (MakeThreadable (ref index, "Loop8",  "<loop8>",  defaultDate, "<loop9>  <loop10> <loop7>  <loop9>  <loop10> <loop7>"));
			messages.Add (MakeThreadable (ref index, "Loop8",  "<loop9>",  defaultDate, "<loop10> <loop7>  <loop8>  <loop10> <loop7>  <loop8>"));
			messages.Add (MakeThreadable (ref index, "Loop10", "<loop10>", defaultDate, "<loop7>  <loop8>  <loop9>  <loop7>  <loop8>  <loop9>"));

			messages.Add (MakeThreadable (ref index, "Ambig1",  "<ambig1>",  defaultDate, null));
			messages.Add (MakeThreadable (ref index, "Ambig2",  "<ambig2>",  defaultDate, "<ambig1>"));
			messages.Add (MakeThreadable (ref index, "Ambig3",  "<ambig3>",  defaultDate, "<ambig1> <ambig2>"));
			messages.Add (MakeThreadable (ref index, "Ambig4",  "<ambig4>",  defaultDate, "<ambig1> <ambig2> <ambig3>"));
			messages.Add (MakeThreadable (ref index, "Ambig5a", "<ambig5a>", defaultDate, "<ambig1> <ambig2> <ambig3> <ambig4>"));
			messages.Add (MakeThreadable (ref index, "Ambig5b", "<ambig5b>", defaultDate, "<ambig1> <ambig3> <ambig2> <ambig4>"));

			messages.Add (MakeThreadable (ref index, "dup",       "<dup>",       defaultDate, null));
			messages.Add (MakeThreadable (ref index, "dup-kid",   "<dup-kid>",   defaultDate, "<dup>"));
			messages.Add (MakeThreadable (ref index, "dup-kid",   "<dup-kid>",   defaultDate, "<dup>"));
			messages.Add (MakeThreadable (ref index, "dup-kid-2", "<dup-kid-2>", defaultDate, "<dup>"));
			messages.Add (MakeThreadable (ref index, "dup-kid-2", "<dup-kid-2>", defaultDate, "<dup>"));
			messages.Add (MakeThreadable (ref index, "dup-kid-2", "<dup-kid-2>", defaultDate, "<dup>"));

			messages.Add (MakeThreadable (ref index, "same subject 1", "<ss1.1>", defaultDate, null));
			messages.Add (MakeThreadable (ref index, "same subject 1", "<ss1.2>", defaultDate, null));

			messages.Add (MakeThreadable (ref index, "missingmessage", "<missa>", defaultDate, null));
			messages.Add (MakeThreadable (ref index, "missingmessage", "<missc>", defaultDate, "<missa> <missb>"));

			messages.Add (MakeThreadable (ref index, "liar 1", "<liar.1>", defaultDate, "<liar.a> <liar.c>"));
			messages.Add (MakeThreadable (ref index, "liar 2", "<liar.2>", defaultDate, "<liar.a> <liar.b> <liar.c>"));

			messages.Add (MakeThreadable (ref index, "liar2 1", "<liar2.1>", defaultDate, "<liar2.a> <liar2.b> <liar2.c>"));
			messages.Add (MakeThreadable (ref index, "liar2 2", "<liar2.2>", defaultDate, "<liar2.a> <liar2.c>"));

			messages.Add (MakeThreadable (ref index, "xx", "<331F7D61.2781@netscape.com>", "Thu, 06 Mar 1997 18:28:50 -0800", null));
			messages.Add (MakeThreadable (ref index, "lkjhlkjh", "<3321E51F.41C6@netscape.com>", "Sat, 08 Mar 1997 14:15:59 -0800", null));
			messages.Add (MakeThreadable (ref index, "test 2", "<3321E5A6.41C6@netscape.com>", "Sat, 08 Mar 1997 14:18:14 -0800", null));
			messages.Add (MakeThreadable (ref index, "enc", "<3321E5C0.167E@netscape.com>", "Sat, 08 Mar 1997 14:18:40 -0800", null));
			messages.Add (MakeThreadable (ref index, "lkjhlkjh", "<3321E715.15FB@netscape.com>", "Sat, 08 Mar 1997 14:24:21 -0800", null));
			messages.Add (MakeThreadable (ref index, "eng", "<3321E7A4.59E2@netscape.com>", "Sat, 08 Mar 1997 14:26:44 -0800", null));
			messages.Add (MakeThreadable (ref index, "lkjhl", "<3321E7BB.1CFB@netscape.com>", "Sat, 08 Mar 1997 14:27:07 -0800", null));
			messages.Add (MakeThreadable (ref index, "Re: certs and signed messages", "<332230AA.41C6@netscape.com>", "Sat, 08 Mar 1997 19:38:18 -0800", "<33222A5E.ED4@netscape.com>"));
			messages.Add (MakeThreadable (ref index, "from dogbert", "<3323546E.BEE44C78@netscape.com>", "Sun, 09 Mar 1997 16:23:10 -0800", null));
			messages.Add (MakeThreadable (ref index, "lkjhlkjhl", "<33321E2A.1C849A20@netscape.com>", "Thu, 20 Mar 1997 21:35:38 -0800", null));
			messages.Add (MakeThreadable (ref index, "le:/u/jwz/mime/smi", "<33323C9D.ADA4BCBA@netscape.com>", "Thu, 20 Mar 1997 23:45:33 -0800", null));
			messages.Add (MakeThreadable (ref index, "ile:/u/jwz", "<33323F62.402C573B@netscape.com>", "Thu, 20 Mar 1997 23:57:22 -0800", null));
			messages.Add (MakeThreadable (ref index, "ljkljhlkjhl", "<336FBAD0.864BC1F4@netscape.com>", "Tue, 06 May 1997 16:12:16 -0700", null));
			messages.Add (MakeThreadable (ref index, "lkjh", "<336FBB46.A0028A6D@netscape.com>", "Tue, 06 May 1997 16:14:14 -0700", null));
			messages.Add (MakeThreadable (ref index, "foo", "<337265C1.5C758C77@netscape.com>", "Thu, 08 May 1997 16:46:09 -0700", null));
			messages.Add (MakeThreadable (ref index, "Welcome to Netscape", "<337AAB3D.C8BCE069@netscape.com>", "Wed, 14 May 1997 23:20:45 -0700", null));
			messages.Add (MakeThreadable (ref index, "Re: Welcome to Netscape", "<337AAE46.903032E4@netscape.com>", "Wed, 14 May 1997 23:33:45 -0700", "<337AAB3D.C8BCE069@netscape.com>"));
			messages.Add (MakeThreadable (ref index, "[Fwd: enc/signed test 1]", "<338B6EE2.BB26C74C@netscape.com>", "Tue, 27 May 1997 16:31:46 -0700", null));

			string expected = @"A
   B
      C
         E
      F
   D
dummy
   G
   H
Loop5
   Loop4
Loop6
Ambig1
   Ambig2
      Ambig3
         Ambig4
            Ambig5a
            Ambig5b
dup
   dup-kid
   dup-kid
   dup-kid-2
   dup-kid-2
   dup-kid-2
dummy
   same subject 1
   same subject 1
missingmessage
   missingmessage
dummy
   liar 1
   liar 2
dummy
   liar2 1
   liar2 2
xx
dummy
   lkjhlkjh
   lkjhlkjh
test 2
enc
eng
lkjhl
Re: certs and signed messages
from dogbert
lkjhlkjhl
le:/u/jwz/mime/smi
ile:/u/jwz
ljkljhlkjhl
lkjh
foo
Welcome to Netscape
   Re: Welcome to Netscape
[Fwd: enc/signed test 1]
".Replace ("\r\n", "\n");

			var threads = messages.Thread (ThreadingAlgorithm.References);
			var builder = new StringBuilder ();

			foreach (var thread in threads)
				WriteMessageThread (builder, messages, thread, 0);

			//Console.WriteLine (builder);

			Assert.AreEqual (expected, builder.ToString (), "Threading did not produce the expected results");
		}
	}
}
