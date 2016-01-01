//
// ImapSearchQueryOptimizer.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2016 Xamarin Inc. (www.xamarin.com)
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

using MailKit.Search;

namespace MailKit.Net.Imap {
	class ImapSearchQueryOptimizer : ISearchQueryOptimizer
	{
		#region ISearchQueryOptimizer implementation

		public bool CanReduce (SearchQuery expr)
		{
			if (expr.Term == SearchTerm.Not) {
				var unary = (UnarySearchQuery) expr;

				if (unary.Operand == SearchQuery.Answered)
					return true;

				if (unary.Operand == SearchQuery.Deleted)
					return true;

				if (unary.Operand == SearchQuery.Draft)
					return true;

				if (unary.Operand == SearchQuery.Flagged)
					return true;

				if (unary.Operand == SearchQuery.Recent)
					return true;

				if (unary.Operand == SearchQuery.Seen)
					return true;

				if (unary.Operand.Term == SearchTerm.Keyword)
					return true;

				if (unary.Operand.Term == SearchTerm.NotKeyword)
					return true;
			}

			return false;
		}

		public SearchQuery Reduce (SearchQuery expr)
		{
			if (expr.Term == SearchTerm.Not) {
				var unary = (UnarySearchQuery) expr;

				if (unary.Operand == SearchQuery.Answered)
					return SearchQuery.NotAnswered;

				if (unary.Operand == SearchQuery.Deleted)
					return SearchQuery.NotDeleted;

				if (unary.Operand == SearchQuery.Draft)
					return SearchQuery.NotDraft;

				if (unary.Operand == SearchQuery.Flagged)
					return SearchQuery.NotFlagged;

				if (unary.Operand == SearchQuery.Recent)
					return SearchQuery.NotRecent;

				if (unary.Operand == SearchQuery.Seen)
					return SearchQuery.NotSeen;

				if (unary.Operand.Term == SearchTerm.Keyword)
					return new TextSearchQuery (SearchTerm.NotKeyword, ((TextSearchQuery) unary.Operand).Text);

				if (unary.Operand.Term == SearchTerm.NotKeyword)
					return new TextSearchQuery (SearchTerm.Keyword, ((TextSearchQuery) unary.Operand).Text);
			}

			return expr;
		}

		#endregion
	}
}
