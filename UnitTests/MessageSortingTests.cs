//
// MessageSortingTests.cs
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
using System.Collections.Generic;

using NUnit.Framework;

using MimeKit;

using MailKit.Search;
using MailKit;

namespace UnitTests {
	[TestFixture]
	public class MessageSortingTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			var messages = new List<MessageSummary> { new MessageSummary (0) };
			var orderBy = new OrderBy[] { OrderBy.Subject };
			var emptyOrderBy = new OrderBy[0];

			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort ((List<MessageSummary>) null, orderBy));
			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort (messages, null));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort (messages, emptyOrderBy));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort (messages, orderBy));

			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) null, orderBy));
			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) messages, null));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) messages, emptyOrderBy));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) messages, orderBy));
		}

		[Test]
		public void TestSorting ()
		{
			var messages = new List<MessageSummary> ();
			MessageSummary summary;

			summary = new MessageSummary (0);
			summary.Fields = MessageSummaryItems.Envelope | MessageSummaryItems.Size;
			summary.Envelope = new Envelope ();
			summary.Envelope.Date = DateTimeOffset.Now.AddSeconds (-2);
			summary.Envelope.Subject = "aaaa";
			summary.Envelope.From.Add (new MailboxAddress ("A", "a@a.com"));
			summary.Envelope.To.Add (new MailboxAddress ("A", "a@a.com"));
			summary.Envelope.Cc.Add (new MailboxAddress ("A", "a@a.com"));
			summary.Size = 520;
			messages.Add (summary);

			summary = new MessageSummary (1);
			summary.Fields = MessageSummaryItems.Envelope | MessageSummaryItems.Size;
			summary.Envelope = new Envelope ();
			summary.Envelope.Date = DateTimeOffset.Now.AddSeconds (-1);
			summary.Envelope.Subject = "bbbb";
			summary.Envelope.From.Add (new MailboxAddress ("B", "b@b.com"));
			summary.Envelope.To.Add (new MailboxAddress ("B", "b@b.com"));
			summary.Envelope.Cc.Add (new MailboxAddress ("B", "b@b.com"));
			summary.Size = 265;
			messages.Add (summary);

			summary = new MessageSummary (2);
			summary.Fields = MessageSummaryItems.Envelope | MessageSummaryItems.Size;
			summary.Envelope = new Envelope ();
			summary.Envelope.Date = DateTimeOffset.Now;
			summary.Envelope.Subject = "cccc";
			summary.Envelope.From.Add (new MailboxAddress ("C", "c@c.com"));
			summary.Envelope.To.Add (new MailboxAddress ("C", "c@c.com"));
			summary.Envelope.Cc.Add (new MailboxAddress ("C", "c@c.com"));
			summary.Size = 520;
			messages.Add (summary);

			messages.Sort (new[] { OrderBy.Arrival });
			Assert.AreEqual (0, messages[0].Index, "Sorting by arrival failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by arrival failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by arrival failed.");

			messages.Sort (new [] { OrderBy.ReverseArrival });
			Assert.AreEqual (2, messages[0].Index, "Sorting by reverse arrival failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by reverse arrival failed.");
			Assert.AreEqual (0, messages[2].Index, "Sorting by reverse arrival failed.");

			messages.Sort (new [] { OrderBy.Subject });
			Assert.AreEqual (0, messages[0].Index, "Sorting by subject failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by subject failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by subject failed.");

			messages.Sort (new [] { OrderBy.ReverseSubject });
			Assert.AreEqual (2, messages[0].Index, "Sorting by reverse subject failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by reverse subject failed.");
			Assert.AreEqual (0, messages[2].Index, "Sorting by reverse subject failed.");

			messages.Sort (new [] { OrderBy.Size, OrderBy.Arrival });
			Assert.AreEqual (1, messages[0].Index, "Sorting by size failed.");
			Assert.AreEqual (0, messages[1].Index, "Sorting by size failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by size failed.");

			messages.Sort (new [] { OrderBy.Date });
			Assert.AreEqual (0, messages[0].Index, "Sorting by date failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by date failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by date failed.");

			messages.Sort (new [] { OrderBy.Size, OrderBy.Subject });
			Assert.AreEqual (1, messages[0].Index, "Sorting by size+subject failed.");
			Assert.AreEqual (0, messages[1].Index, "Sorting by size+subject failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by size+subject failed.");

			messages.Sort (new [] { OrderBy.ReverseSize, OrderBy.ReverseSubject });
			Assert.AreEqual (2, messages[0].Index, "Sorting by reversed size+subject failed.");
			Assert.AreEqual (0, messages[1].Index, "Sorting by reversed size+subject failed.");
			Assert.AreEqual (1, messages[2].Index, "Sorting by reversed size+subject failed.");

			messages.Sort (new[] { OrderBy.DisplayFrom });
			Assert.AreEqual (0, messages[0].Index, "Sorting by display-from failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by display-from failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by display-from failed.");

			messages.Sort (new[] { OrderBy.From });
			Assert.AreEqual (0, messages[0].Index, "Sorting by from failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by from failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by from failed.");

			messages.Sort (new[] { OrderBy.DisplayTo });
			Assert.AreEqual (0, messages[0].Index, "Sorting by display-to failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by display-to failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by display-to failed.");

			messages.Sort (new[] { OrderBy.To });
			Assert.AreEqual (0, messages[0].Index, "Sorting by to failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by to failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by to failed.");

			messages.Sort (new[] { OrderBy.Cc });
			Assert.AreEqual (0, messages[0].Index, "Sorting by cc failed.");
			Assert.AreEqual (1, messages[1].Index, "Sorting by cc failed.");
			Assert.AreEqual (2, messages[2].Index, "Sorting by cc failed.");
		}
	}
}
