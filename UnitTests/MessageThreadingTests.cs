//
// MessageThreadingTests.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc. (www.xamarin.com)
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

using MimeKit.Utils;
using MimeKit;

using MailKit;

namespace UnitTests {
	[TestFixture]
	public class MessageThreadingTests
	{
		readonly List<MessageSummary> summaries = new List<MessageSummary> ();
		int msgIndex = 0;

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

		void MakeThreadable (string subject, string msgid, string date, string refs)
		{
			DateTimeOffset value;

			DateUtils.TryParse (date, out value);

			var summary = new MessageSummary (msgIndex++);
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

			summaries.Add (summary);
		}

		void WriteMessageThread (StringBuilder builder, MessageThread thread, int depth)
		{
			builder.Append (new string (' ', depth * 3));

			if (thread.UniqueId.HasValue) {
				var summary = summaries[(int) thread.UniqueId.Value.Id];
				builder.Append (summary.Envelope.Subject);
			} else {
				builder.Append ("dummy");
			}

			builder.Append ('\n');

			foreach (var child in thread.Children)
				WriteMessageThread (builder, child, depth + 1);
		}

		[Test]
		public void TestThreading ()
		{
			const string defaultDate = "01 Jan 1997 12:00:00 -0400";

			// this test case was borrowed from Jamie Zawinski's TestThreader.java
			MakeThreadable ("A", "<1>", defaultDate, null);
			MakeThreadable ("B", "<2>", defaultDate, "<1>");
			MakeThreadable ("C", "<3>", defaultDate, "<1> <2>");
			MakeThreadable ("D", "<4>", defaultDate, "<1>");
			MakeThreadable ("E", "<5>", defaultDate, "<3> <x1> <x2> <x3>");
			MakeThreadable ("F", "<6>", defaultDate, "<2>");
			MakeThreadable ("G", "<7>", defaultDate, "<nonesuch>");
			MakeThreadable ("H", "<8>", defaultDate, "<nonesuch>");

			MakeThreadable ("Loop1", "<loop1>", defaultDate, "<loop2> <loop3>");
			MakeThreadable ("Loop2", "<loop2>", defaultDate, "<loop3> <loop1>");
			MakeThreadable ("Loop3", "<loop3>", defaultDate, "<loop1> <loop2>");

			MakeThreadable ("Loop4", "<loop4>", defaultDate, "<loop5>");
			MakeThreadable ("Loop5", "<loop5>", defaultDate, "<loop4>");

			MakeThreadable ("Loop6", "<loop6>", defaultDate, "<loop6>");

			MakeThreadable ("Loop7",  "<loop7>",  defaultDate, "<loop8>  <loop9>  <loop10> <loop8>  <loop9> <loop10>");
			MakeThreadable ("Loop8",  "<loop8>",  defaultDate, "<loop9>  <loop10> <loop7>  <loop9>  <loop10> <loop7>");
			MakeThreadable ("Loop8",  "<loop9>",  defaultDate, "<loop10> <loop7>  <loop8>  <loop10> <loop7>  <loop8>");
			MakeThreadable ("Loop10", "<loop10>", defaultDate, "<loop7>  <loop8>  <loop9>  <loop7>  <loop8>  <loop9>");

			MakeThreadable ("Ambig1",  "<ambig1>",  defaultDate, null);
			MakeThreadable ("Ambig2",  "<ambig2>",  defaultDate, "<ambig1>");
			MakeThreadable ("Ambig3",  "<ambig3>",  defaultDate, "<ambig1> <ambig2>");
			MakeThreadable ("Ambig4",  "<ambig4>",  defaultDate, "<ambig1> <ambig2> <ambig3>");
			MakeThreadable ("Ambig5a", "<ambig5a>", defaultDate, "<ambig1> <ambig2> <ambig3> <ambig4>");
			MakeThreadable ("Ambig5b", "<ambig5b>", defaultDate, "<ambig1> <ambig3> <ambig2> <ambig4>");

			MakeThreadable ("dup",       "<dup>",       defaultDate, null);
			MakeThreadable ("dup-kid",   "<dup-kid>",   defaultDate, "<dup>");
			MakeThreadable ("dup-kid",   "<dup-kid>",   defaultDate, "<dup>");
			MakeThreadable ("dup-kid-2", "<dup-kid-2>", defaultDate, "<dup>");
			MakeThreadable ("dup-kid-2", "<dup-kid-2>", defaultDate, "<dup>");
			MakeThreadable ("dup-kid-2", "<dup-kid-2>", defaultDate, "<dup>");

			MakeThreadable ("same subject 1", "<ss1.1>", defaultDate, null);
			MakeThreadable ("same subject 1", "<ss1.2>", defaultDate, null);

			MakeThreadable ("missingmessage", "<missa>", defaultDate, null);
			MakeThreadable ("missingmessage", "<missc>", defaultDate, "<missa> <missb>");

			MakeThreadable ("liar 1", "<liar.1>", defaultDate, "<liar.a> <liar.c>");
			MakeThreadable ("liar 2", "<liar.2>", defaultDate, "<liar.a> <liar.b> <liar.c>");

			MakeThreadable ("liar2 1", "<liar2.1>", defaultDate, "<liar2.a> <liar2.b> <liar2.c>");
			MakeThreadable ("liar2 2", "<liar2.2>", defaultDate, "<liar2.a> <liar2.c>");

			MakeThreadable ("xx", "<331F7D61.2781@netscape.com>", "Thu, 06 Mar 1997 18:28:50 -0800", null);
			MakeThreadable ("lkjhlkjh", "<3321E51F.41C6@netscape.com>", "Sat, 08 Mar 1997 14:15:59 -0800", null);
			MakeThreadable ("test 2", "<3321E5A6.41C6@netscape.com>", "Sat, 08 Mar 1997 14:18:14 -0800", null);
			MakeThreadable ("enc", "<3321E5C0.167E@netscape.com>", "Sat, 08 Mar 1997 14:18:40 -0800", null);
			MakeThreadable ("lkjhlkjh", "<3321E715.15FB@netscape.com>", "Sat, 08 Mar 1997 14:24:21 -0800", null);
			MakeThreadable ("eng", "<3321E7A4.59E2@netscape.com>", "Sat, 08 Mar 1997 14:26:44 -0800", null);
			MakeThreadable ("lkjhl", "<3321E7BB.1CFB@netscape.com>", "Sat, 08 Mar 1997 14:27:07 -0800", null);
			MakeThreadable ("Re: certs and signed messages", "<332230AA.41C6@netscape.com>", "Sat, 08 Mar 1997 19:38:18 -0800", "<33222A5E.ED4@netscape.com>");
			MakeThreadable ("from dogbert", "<3323546E.BEE44C78@netscape.com>", "Sun, 09 Mar 1997 16:23:10 -0800", null);
			MakeThreadable ("lkjhlkjhl", "<33321E2A.1C849A20@netscape.com>", "Thu, 20 Mar 1997 21:35:38 -0800", null);
			MakeThreadable ("le:/u/jwz/mime/smi", "<33323C9D.ADA4BCBA@netscape.com>", "Thu, 20 Mar 1997 23:45:33 -0800", null);
			MakeThreadable ("ile:/u/jwz", "<33323F62.402C573B@netscape.com>", "Thu, 20 Mar 1997 23:57:22 -0800", null);
			MakeThreadable ("ljkljhlkjhl", "<336FBAD0.864BC1F4@netscape.com>", "Tue, 06 May 1997 16:12:16 -0700", null);
			MakeThreadable ("lkjh", "<336FBB46.A0028A6D@netscape.com>", "Tue, 06 May 1997 16:14:14 -0700", null);
			MakeThreadable ("foo", "<337265C1.5C758C77@netscape.com>", "Thu, 08 May 1997 16:46:09 -0700", null);
			MakeThreadable ("Welcome to Netscape", "<337AAB3D.C8BCE069@netscape.com>", "Wed, 14 May 1997 23:20:45 -0700", null);
			MakeThreadable ("Re: Welcome to Netscape", "<337AAE46.903032E4@netscape.com>", "Wed, 14 May 1997 23:33:45 -0700", "<337AAB3D.C8BCE069@netscape.com>");
			MakeThreadable ("[Fwd: enc/signed test 1]", "<338B6EE2.BB26C74C@netscape.com>", "Tue, 27 May 1997 16:31:46 -0700", null);

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

			var threads = summaries.Thread (ThreadingAlgorithm.References);
			var builder = new StringBuilder ();

			foreach (var thread in threads)
				WriteMessageThread (builder, thread, 0);

			//Console.WriteLine (builder);

			Assert.AreEqual (expected, builder.ToString (), "Threading did not produce the expected results");
		}
	}
}
