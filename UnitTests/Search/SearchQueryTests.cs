//
// SearchQueryTests.cs
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

using NUnit.Framework;

using MailKit;
using MailKit.Search;

namespace UnitTests.Search
{
	[TestFixture]
	public class SearchQueryTests
	{
		[Test]
		public void TestArgumentExceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => SearchQuery.And (null, SearchQuery.All));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.And (SearchQuery.All, null));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.BccContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.BccContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.BodyContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.BodyContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.CcContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.CcContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.DoesNotHaveCustomFlag (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.DoesNotHaveCustomFlag (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.DoesNotHaveCustomFlags (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.DoesNotHaveFlags (MessageFlags.None));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Filter (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.Filter (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.FromContains (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.FromContains (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.Fuzzy (null));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.GMailRawSearch (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.GMailRawSearch (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasCustomFlag (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasCustomFlag (string.Empty));
			Assert.Throws<ArgumentNullException> (() => SearchQuery.HasCustomFlags (null));
			Assert.Throws<ArgumentException> (() => SearchQuery.HasFlags (MessageFlags.None));
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
			Assert.Throws<ArgumentNullException> (() => new HeaderSearchQuery (null, "text"));
			Assert.Throws<ArgumentException> (() => new HeaderSearchQuery (string.Empty, "text"));
			Assert.Throws<ArgumentNullException> (() => new HeaderSearchQuery ("name", null));
			Assert.Throws<ArgumentNullException> (() => new TextSearchQuery (SearchTerm.BodyContains, null));
			Assert.Throws<ArgumentNullException> (() => new UidSearchQuery (null));
			Assert.Throws<ArgumentException> (() => new UidSearchQuery (new UniqueIdSet ()));
			Assert.Throws<ArgumentException> (() => new UidSearchQuery (UniqueId.Invalid));
			Assert.Throws<ArgumentNullException> (() => new UnarySearchQuery (SearchTerm.Not, null));
		}
	}
}
