//
// ImapSearchQueryOptimizerTests.cs
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
using System.IO;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

using NUnit.Framework;

using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;

namespace UnitTests.Net.Imap {
	[TestFixture]
	public class ImapSearchQueryOptimizerTests
	{
		ImapSearchQueryOptimizer optimizer = new ImapSearchQueryOptimizer ();

		[Test]
		public void TestReduceAnd ()
		{
			var query = optimizer.Reduce (SearchQuery.And (SearchQuery.All, SearchQuery.Answered));

			Assert.AreEqual (SearchQuery.Answered, query);

			query = optimizer.Reduce (SearchQuery.And (SearchQuery.Answered, SearchQuery.All));

			Assert.AreEqual (SearchQuery.Answered, query);
		}

		[Test]
		public void TestReduceOr ()
		{
			var query = optimizer.Reduce (SearchQuery.Or (SearchQuery.All, SearchQuery.Answered));

			Assert.AreEqual (SearchQuery.All, query);

			query = optimizer.Reduce (SearchQuery.Or (SearchQuery.Answered, SearchQuery.All));

			Assert.AreEqual (SearchQuery.All, query);
		}

		[Test]
		public void TestReduceNotFlags ()
		{
			foreach (MessageFlags flag in Enum.GetValues (typeof (MessageFlags))) {
				if (flag == MessageFlags.None || flag == MessageFlags.UserDefined)
					continue;

				var query = SearchQuery.Not (SearchQuery.HasFlags (flag));
				var optimized = optimizer.Reduce (query);

				Assert.AreEqual ("Not" + flag.ToString (), optimized.Term.ToString (), "NOT ({0})", flag);

				query = SearchQuery.Not (SearchQuery.DoesNotHaveFlags (flag));
				optimized = optimizer.Reduce (query);

				Assert.AreEqual (flag.ToString (), optimized.Term.ToString (), "NOT ({0})", query.Operand.Term);

				query = SearchQuery.Not (SearchQuery.Not (SearchQuery.HasFlags (flag)));
				optimized = optimizer.Reduce (query);

				Assert.AreEqual (flag.ToString (), optimized.Term.ToString (), "NOT (NOT ({0}))", flag);
			}
		}

		[Test]
		public void TestReduceNotFlag ()
		{
			var query = SearchQuery.Not (SearchQuery.HasCustomFlag ("custom"));
			var optimized = optimizer.Reduce (query);

			Assert.AreEqual (SearchTerm.NotKeyword, optimized.Term, "NOT KEYWORD");

			query = SearchQuery.Not (SearchQuery.DoesNotHaveCustomFlag ("custom"));
			optimized = optimizer.Reduce (query);

			Assert.AreEqual (SearchTerm.Keyword, optimized.Term, "NOT NOTKEYWORD", query.Operand.Term);

			query = SearchQuery.Not (SearchQuery.Not (SearchQuery.HasCustomFlag ("custom")));
			optimized = optimizer.Reduce (query);

			Assert.AreEqual (SearchTerm.Keyword, optimized.Term, "NOT NOT KEYWORD");
		}
	}
}
