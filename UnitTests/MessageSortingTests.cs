//
// MessageSortingTests.cs
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
			var emptyOrderBy = Array.Empty<OrderBy> ();

			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort ((List<MessageSummary>) null, orderBy));
			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort (messages, null));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort (messages, emptyOrderBy));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort (messages, orderBy));

			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) null, orderBy));
			Assert.Throws<ArgumentNullException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) messages, null));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) messages, emptyOrderBy));
			Assert.Throws<ArgumentException> (() => MessageSorter.Sort ((IEnumerable<MessageSummary>) messages, orderBy));
		}

		static List<MessageSummary> Create ()
		{
			var messages = new List<MessageSummary> ();
			MessageSummary summary;

			summary = new MessageSummary (0);
			summary.Fields = MessageSummaryItems.Annotations | MessageSummaryItems.Envelope | MessageSummaryItems.Size | MessageSummaryItems.ModSeq;
			summary.Annotations = new List<Annotation> (new [] {
				new Annotation (AnnotationEntry.AltSubject)
			});
			summary.Annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "aaaa");
			summary.Envelope = new Envelope ();
			summary.Envelope.Date = DateTimeOffset.Now.AddSeconds (-2);
			summary.Envelope.Subject = "aaaa";
			summary.Envelope.From.Add (new MailboxAddress ("A", "a@a.com"));
			summary.Envelope.To.Add (new MailboxAddress ("A", "a@a.com"));
			summary.Envelope.Cc.Add (new MailboxAddress ("A", "a@a.com"));
			summary.ModSeq = 80290;
			summary.Size = 520;
			messages.Add (summary);

			summary = new MessageSummary (1);
			summary.Fields = MessageSummaryItems.Annotations | MessageSummaryItems.Envelope | MessageSummaryItems.Size | MessageSummaryItems.ModSeq;
			summary.Annotations = new List<Annotation> (new [] {
				new Annotation (AnnotationEntry.AltSubject)
			});
			summary.Annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "bbbb");
			summary.Envelope = new Envelope ();
			summary.Envelope.Date = DateTimeOffset.Now.AddSeconds (-1);
			summary.Envelope.Subject = "bbbb";
			summary.Envelope.From.Add (new MailboxAddress ("B", "b@b.com"));
			summary.Envelope.To.Add (new MailboxAddress ("B", "b@b.com"));
			summary.Envelope.Cc.Add (new MailboxAddress ("B", "b@b.com"));
			summary.ModSeq = 70642;
			summary.Size = 265;
			messages.Add (summary);

			summary = new MessageSummary (2);
			summary.Fields = MessageSummaryItems.Annotations | MessageSummaryItems.Envelope | MessageSummaryItems.Size | MessageSummaryItems.ModSeq;
			summary.Annotations = new List<Annotation> (new [] {
				new Annotation (AnnotationEntry.AltSubject)
			});
			summary.Annotations[0].Properties.Add (AnnotationAttribute.SharedValue, "cccc");
			summary.Envelope = new Envelope ();
			summary.Envelope.Date = DateTimeOffset.Now;
			summary.Envelope.Subject = "cccc";
			summary.Envelope.From.Add (new MailboxAddress ("C", "c@c.com"));
			summary.Envelope.To.Add (new MailboxAddress ("C", "c@c.com"));
			summary.Envelope.Cc.Add (new MailboxAddress ("C", "c@c.com"));
			summary.ModSeq = 80290;
			summary.Size = 520;
			messages.Add (summary);

			return messages;
		}

		[Test]
		public void TestSorting ()
		{
			var messages = Create ();

			messages.Sort (new[] { OrderBy.Arrival });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by arrival failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by arrival failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by arrival failed.");

			messages.Sort (new [] { OrderBy.ReverseArrival });
			Assert.That (messages[0].Index, Is.EqualTo (2), "Sorting by reverse arrival failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by reverse arrival failed.");
			Assert.That (messages[2].Index, Is.EqualTo (0), "Sorting by reverse arrival failed.");

			messages.Sort (new [] { OrderBy.Subject });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by subject failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by subject failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by subject failed.");

			messages.Sort (new [] { OrderBy.ReverseSubject });
			Assert.That (messages[0].Index, Is.EqualTo (2), "Sorting by reverse subject failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by reverse subject failed.");
			Assert.That (messages[2].Index, Is.EqualTo (0), "Sorting by reverse subject failed.");

			messages.Sort (new [] { OrderBy.Size, OrderBy.Arrival });
			Assert.That (messages[0].Index, Is.EqualTo (1), "Sorting by size failed.");
			Assert.That (messages[1].Index, Is.EqualTo (0), "Sorting by size failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by size failed.");

			messages.Sort (new [] { OrderBy.Date });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by date failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by date failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by date failed.");

			messages.Sort (new [] { OrderBy.Size, OrderBy.Subject });
			Assert.That (messages[0].Index, Is.EqualTo (1), "Sorting by size+subject failed.");
			Assert.That (messages[1].Index, Is.EqualTo (0), "Sorting by size+subject failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by size+subject failed.");

			messages.Sort (new [] { OrderBy.ReverseSize, OrderBy.ReverseSubject });
			Assert.That (messages[0].Index, Is.EqualTo (2), "Sorting by reversed size+subject failed.");
			Assert.That (messages[1].Index, Is.EqualTo (0), "Sorting by reversed size+subject failed.");
			Assert.That (messages[2].Index, Is.EqualTo (1), "Sorting by reversed size+subject failed.");

			messages.Sort (new[] { OrderBy.DisplayFrom });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by display-from failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by display-from failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by display-from failed.");

			messages.Sort (new[] { OrderBy.From });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by from failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by from failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by from failed.");

			messages.Sort (new[] { OrderBy.DisplayTo });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by display-to failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by display-to failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by display-to failed.");

			messages.Sort (new[] { OrderBy.To });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by to failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by to failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by to failed.");

			messages.Sort (new[] { OrderBy.Cc });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by cc failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by cc failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by cc failed.");

			messages.Sort (new [] { new OrderBy (OrderByType.ModSeq, SortOrder.Ascending), OrderBy.Arrival });
			Assert.That (messages[0].Index, Is.EqualTo (1), "Sorting by modseq failed.");
			Assert.That (messages[1].Index, Is.EqualTo (0), "Sorting by modseq failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by modseq failed.");

			messages.Sort (new[] { new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Ascending) });
			Assert.That (messages[0].Index, Is.EqualTo (0), "Sorting by altsubject failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by altsubject failed.");
			Assert.That (messages[2].Index, Is.EqualTo (2), "Sorting by altsubject failed.");

			messages.Sort (new[] { new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Descending) });
			Assert.That (messages[0].Index, Is.EqualTo (2), "Sorting by reverse altsubject failed.");
			Assert.That (messages[1].Index, Is.EqualTo (1), "Sorting by reverse altsubject failed.");
			Assert.That (messages[2].Index, Is.EqualTo (0), "Sorting by reverse altsubject failed.");
		}

		[Test]
		public void TestSortingEnumerable ()
		{
			var messages = Create ();
			IEnumerable<MessageSummary> enumerable = messages;
			IList<MessageSummary> sorted;

			sorted = enumerable.Sort (new [] { OrderBy.Arrival });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by arrival failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by arrival failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by arrival failed.");

			sorted = enumerable.Sort (new [] { OrderBy.ReverseArrival });
			Assert.That (sorted[0].Index, Is.EqualTo (2), "Sorting by reverse arrival failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by reverse arrival failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (0), "Sorting by reverse arrival failed.");

			sorted = enumerable.Sort (new [] { OrderBy.Subject });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by subject failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by subject failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by subject failed.");

			sorted = enumerable.Sort (new [] { OrderBy.ReverseSubject });
			Assert.That (sorted[0].Index, Is.EqualTo (2), "Sorting by reverse subject failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by reverse subject failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (0), "Sorting by reverse subject failed.");

			sorted = enumerable.Sort (new [] { OrderBy.Size, OrderBy.Arrival });
			Assert.That (sorted[0].Index, Is.EqualTo (1), "Sorting by size failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (0), "Sorting by size failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by size failed.");

			sorted = enumerable.Sort (new [] { OrderBy.Date });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by date failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by date failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by date failed.");

			sorted = enumerable.Sort (new [] { OrderBy.Size, OrderBy.Subject });
			Assert.That (sorted[0].Index, Is.EqualTo (1), "Sorting by size+subject failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (0), "Sorting by size+subject failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by size+subject failed.");

			sorted = enumerable.Sort (new [] { OrderBy.ReverseSize, OrderBy.ReverseSubject });
			Assert.That (sorted[0].Index, Is.EqualTo (2), "Sorting by reversed size+subject failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (0), "Sorting by reversed size+subject failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (1), "Sorting by reversed size+subject failed.");

			sorted = enumerable.Sort (new [] { OrderBy.DisplayFrom });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by display-from failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by display-from failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by display-from failed.");

			sorted = enumerable.Sort (new [] { OrderBy.From });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by from failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by from failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by from failed.");

			sorted = enumerable.Sort (new [] { OrderBy.DisplayTo });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by display-to failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by display-to failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by display-to failed.");

			sorted = enumerable.Sort (new [] { OrderBy.To });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by to failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by to failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by to failed.");

			sorted = enumerable.Sort (new [] { OrderBy.Cc });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by cc failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by cc failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by cc failed.");

			sorted = enumerable.Sort (new [] { new OrderBy (OrderByType.ModSeq, SortOrder.Ascending), OrderBy.Arrival });
			Assert.That (sorted[0].Index, Is.EqualTo (1), "Sorting by modseq failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (0), "Sorting by modseq failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by modseq failed.");

			sorted = enumerable.Sort (new[] { new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Ascending) });
			Assert.That (sorted[0].Index, Is.EqualTo (0), "Sorting by subject failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by subject failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (2), "Sorting by subject failed.");

			sorted = enumerable.Sort (new[] { new OrderByAnnotation (AnnotationEntry.AltSubject, AnnotationAttribute.SharedValue, SortOrder.Descending) });
			Assert.That (sorted[0].Index, Is.EqualTo (2), "Sorting by reverse subject failed.");
			Assert.That (sorted[1].Index, Is.EqualTo (1), "Sorting by reverse subject failed.");
			Assert.That (sorted[2].Index, Is.EqualTo (0), "Sorting by reverse subject failed.");
		}
	}
}
