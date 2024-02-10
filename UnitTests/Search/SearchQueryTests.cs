//
// SearchQueryTests.cs
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

using MailKit;
using MailKit.Search;

namespace UnitTests.Search {
	[TestFixture]
	public class SearchQueryTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => SearchQuery.And (null, SearchQuery.All));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.And (SearchQuery.All, null));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.AnnotationsContain (null, AnnotationAttribute.Value, "value"));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.AnnotationsContain (AnnotationEntry.AltSubject, null, "value"));
			Assert.Throws<ArgumentException> (() => SearchQuery.AnnotationsContain (AnnotationEntry.AltSubject, AnnotationAttribute.Size, "value"));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.BccContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.BccContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.BodyContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.BodyContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.CcContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.CcContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.NotKeyword (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotKeyword (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.NotKeywords (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotKeywords (Array.Empty<string> ()));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotFlags (MessageFlags.None));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Filter (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.Filter (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.FromContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.FromContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Fuzzy (null));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.GMailRawSearch (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.GMailRawSearch (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasKeyword (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasKeyword (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasKeywords (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasKeywords (Array.Empty<string> ()));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasFlags (MessageFlags.None));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasKeyword (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasKeyword (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasKeywords (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasKeywords (Array.Empty<string> ()));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasKeywords (new string[] { "keyword", null }));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotFlags (MessageFlags.None));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.NotKeyword (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotKeyword (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.NotKeywords (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotKeywords (Array.Empty<string> ()));
			Assert.Throws<ArgumentException> (() => SearchQuery.NotKeywords (new string [] { "keyword", null }));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasGMailLabel (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasGMailLabel (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HeaderContains (null, "text"));
			Assert.Throws<ArgumentException> (() => SearchQuery.HeaderContains (string.Empty, "text"));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HeaderContains ("name", null));
			Assert.Throws<ArgumentOutOfRangeException> (() => SearchQuery.LargerThan (-1));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.MessageContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.MessageContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Not (null));
			Assert.Throws<ArgumentOutOfRangeException> (() => SearchQuery.OlderThan (-1));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Or (null, SearchQuery.All));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Or (SearchQuery.All, null));
			Assert.Throws<ArgumentOutOfRangeException> (() => SearchQuery.SmallerThan (-1));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.SubjectContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.SubjectContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.ToContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.ToContains (string.Empty));
			Assert.Throws<ArgumentOutOfRangeException> (() => SearchQuery.YoungerThan (-1));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.All.And (null));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.All.Or (null));

			Assert.Throws<ArgumentNullException> (() => new BinarySearchQuery (SearchTerm.And, null, SearchQuery.All));
			Assert.Throws<ArgumentNullException> (() => new BinarySearchQuery (SearchTerm.And, SearchQuery.All, null));
			Assert.Throws<ArgumentNullException> (() => new FilterSearchQuery (null));
			Assert.Throws<ArgumentException> (() => new FilterSearchQuery (string.Empty));
			Assert.Throws<ArgumentException> (() => new FilterSearchQuery (MetadataTag.Create ("/dev/null")));
			Assert.Throws<ArgumentNullException> (() => new HeaderSearchQuery (null, "text"));
			Assert.Throws<ArgumentException> (() => new HeaderSearchQuery (string.Empty, "text"));
			Assert.Throws<ArgumentNullException> (() => new HeaderSearchQuery ("name", null));
			Assert.Throws<ArgumentNullException> (() => new TextSearchQuery (SearchTerm.BodyContains, null));
			Assert.Throws<ArgumentNullException> (() => new UidSearchQuery (null));
			Assert.Throws<ArgumentException> (() => new UidSearchQuery (new UniqueIdSet ()));
			Assert.Throws<ArgumentException> (() => new UidSearchQuery (UniqueId.Invalid));
			Assert.Throws<ArgumentNullException> (() => new UnarySearchQuery (SearchTerm.Not, null));
		}

		[Test]
		public void TestFilterSearchQuery ()
		{
			var query = new FilterSearchQuery (MetadataTag.Create ("/private/filters/values/private-unread"));

			Assert.That (query.Name, Is.EqualTo ("private-unread"));

			query = new FilterSearchQuery (MetadataTag.Create ("/shared/filters/values/shared-unread"));

			Assert.That (query.Name, Is.EqualTo ("shared-unread"));

			query = (FilterSearchQuery) SearchQuery.Filter ("basic");

			Assert.That (query.Name, Is.EqualTo ("basic"));
		}

		[Test]
		public void TestUidSearchQuery ()
		{
			var query = new UidSearchQuery (new UniqueId (5));

			Assert.That (query.Uids, Has.Count.EqualTo (1));
			Assert.That (query.Uids[0].Id, Is.EqualTo ((uint) 5));

			query = SearchQuery.Uids (new UniqueId[] { new UniqueId (5) });

			Assert.That (query.Uids, Has.Count.EqualTo (1));
			Assert.That (query.Uids [0].Id, Is.EqualTo ((uint)5));
		}

		[Test]
		public void TestDefaultSearchQuery ()
		{
			var query = new SearchQuery ();

			Assert.That (query.Term, Is.EqualTo (SearchTerm.All), "Default .ctor");
		}

		[Test]
		public void TestKeywordsQueries ()
		{
			BinarySearchQuery binary;
			TextSearchQuery text;

			var query = SearchQuery.HasKeywords (new [] { "custom1" });
			Assert.That (query, Is.InstanceOf<TextSearchQuery> ());
			text = (TextSearchQuery)query;
			Assert.That (text.Term, Is.EqualTo (SearchTerm.Keyword));
			Assert.That (text.Text, Is.EqualTo ("custom1"));

			query = SearchQuery.HasKeywords (new [] { "custom1", "custom2" });
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery)query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left, Is.InstanceOf<TextSearchQuery> ());
			Assert.That (binary.Right, Is.InstanceOf<TextSearchQuery> ());
			Assert.That (((TextSearchQuery)binary.Left).Text, Is.EqualTo ("custom1"));
			Assert.That (((TextSearchQuery)binary.Right).Text, Is.EqualTo ("custom2"));

			query = SearchQuery.NotKeywords (new [] { "custom1" });
			Assert.That (query, Is.InstanceOf<TextSearchQuery> ());
			text = (TextSearchQuery) query;
			Assert.That (text.Term, Is.EqualTo (SearchTerm.NotKeyword));
			Assert.That (text.Text, Is.EqualTo ("custom1"));

			query = SearchQuery.NotKeywords (new [] { "custom1", "custom2" });
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left, Is.InstanceOf<TextSearchQuery> ());
			Assert.That (binary.Right, Is.InstanceOf<TextSearchQuery> ());
			Assert.That (((TextSearchQuery) binary.Left).Text, Is.EqualTo ("custom1"));
			Assert.That (((TextSearchQuery) binary.Right).Text, Is.EqualTo ("custom2"));
		}

		[Test]
		public void TestMessageFlagsQueries ()
		{
			BinarySearchQuery binary;

			var query = SearchQuery.HasFlags (MessageFlags.Answered | MessageFlags.Seen);
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left.Term, Is.EqualTo (SearchQuery.Seen.Term));
			Assert.That (binary.Right.Term, Is.EqualTo (SearchQuery.Answered.Term));

			query = SearchQuery.HasFlags (MessageFlags.Flagged | MessageFlags.Deleted);
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left.Term, Is.EqualTo (SearchQuery.Flagged.Term));
			Assert.That (binary.Right.Term, Is.EqualTo (SearchQuery.Deleted.Term));

			query = SearchQuery.HasFlags (MessageFlags.Draft | MessageFlags.Recent);
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left.Term, Is.EqualTo (SearchQuery.Draft.Term));
			Assert.That (binary.Right.Term, Is.EqualTo (SearchQuery.Recent.Term));

			query = SearchQuery.NotFlags (MessageFlags.Answered | MessageFlags.Seen);
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left.Term, Is.EqualTo (SearchQuery.NotSeen.Term));
			Assert.That (binary.Right.Term, Is.EqualTo (SearchQuery.NotAnswered.Term));

			query = SearchQuery.NotFlags (MessageFlags.Flagged | MessageFlags.Deleted);
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left.Term, Is.EqualTo (SearchQuery.NotFlagged.Term));
			Assert.That (binary.Right.Term, Is.EqualTo (SearchQuery.NotDeleted.Term));

			query = SearchQuery.NotFlags (MessageFlags.Draft | MessageFlags.Recent);
			Assert.That (query, Is.InstanceOf<BinarySearchQuery> ());
			binary = (BinarySearchQuery) query;
			Assert.That (binary.Term, Is.EqualTo (SearchTerm.And));
			Assert.That (binary.Left.Term, Is.EqualTo (SearchQuery.NotDraft.Term));
			Assert.That (binary.Right.Term, Is.EqualTo (SearchQuery.NotRecent.Term));
		}

		[Test]
		public void TestGMailExtensionQueries ()
		{
			var numeric = SearchQuery.GMailMessageId (512);

			Assert.That (numeric.Term, Is.EqualTo (SearchTerm.GMailMessageId));
			Assert.That (numeric.Value, Is.EqualTo (512));

			numeric = SearchQuery.GMailThreadId (1024);

			Assert.That (numeric.Term, Is.EqualTo (SearchTerm.GMailThreadId));
			Assert.That (numeric.Value, Is.EqualTo (1024));

			var text = SearchQuery.HasGMailLabel ("label");

			Assert.That (text.Term, Is.EqualTo (SearchTerm.GMailLabels));
			Assert.That (text.Text, Is.EqualTo ("label"));

			text = SearchQuery.GMailRawSearch ("raw");

			Assert.That (text.Term, Is.EqualTo (SearchTerm.GMailRaw));
			Assert.That (text.Text, Is.EqualTo ("raw"));
		}
	}
}
