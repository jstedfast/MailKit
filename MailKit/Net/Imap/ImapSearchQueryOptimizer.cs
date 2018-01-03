//
// ImapSearchQueryOptimizer.cs
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

using MailKit.Search;

namespace MailKit.Net.Imap {
	class ImapSearchQueryOptimizer : ISearchQueryOptimizer
	{
		#region ISearchQueryOptimizer implementation

		public SearchQuery Reduce (SearchQuery expr)
		{
			if (expr.Term == SearchTerm.And) {
				var and = (BinarySearchQuery) expr;

				if (and.Left.Term == SearchTerm.All)
					return and.Right;

				if (and.Right.Term == SearchTerm.All)
					return and.Left;
			} else if (expr.Term == SearchTerm.Or) {
				var or = (BinarySearchQuery) expr;

				if (or.Left.Term == SearchTerm.All)
					return SearchQuery.All;

				if (or.Right.Term == SearchTerm.All)
					return SearchQuery.All;
			} else if (expr.Term == SearchTerm.Not) {
				var unary = (UnarySearchQuery) expr;

				switch (unary.Operand.Term) {
				case SearchTerm.NotAnswered: return SearchQuery.Answered;
				case SearchTerm.Answered: return SearchQuery.NotAnswered;
				case SearchTerm.NotDeleted: return SearchQuery.Deleted;
				case SearchTerm.Deleted: return SearchQuery.NotDeleted;
				case SearchTerm.NotDraft: return SearchQuery.Draft;
				case SearchTerm.Draft: return SearchQuery.NotDraft;
				case SearchTerm.NotFlagged: return SearchQuery.Flagged;
				case SearchTerm.Flagged: return SearchQuery.NotFlagged;
				case SearchTerm.NotRecent: return SearchQuery.Recent;
				case SearchTerm.Recent: return SearchQuery.NotRecent;
				case SearchTerm.NotSeen: return SearchQuery.Seen;
				case SearchTerm.Seen: return SearchQuery.NotSeen;
				}

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
