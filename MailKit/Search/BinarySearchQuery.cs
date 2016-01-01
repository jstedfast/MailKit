//
// BinarySearchQuery.cs
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

namespace MailKit.Search {
	/// <summary>
	/// A binary search query such as an AND or OR expression.
	/// </summary>
	/// <remarks>
	/// A binary search query such as an AND or OR expression.
	/// </remarks>
	public class BinarySearchQuery : SearchQuery
	{
		internal BinarySearchQuery (SearchTerm term, SearchQuery left, SearchQuery right) : base (term)
		{
			Right = right;
			Left = left;
		}

		/// <summary>
		/// Gets the left operand of the expression.
		/// </summary>
		/// <remarks>
		/// Gets the left operand of the expression.
		/// </remarks>
		/// <value>The left operand.</value>
		public SearchQuery Left {
			get; private set;
		}

		/// <summary>
		/// Gets the right operand of the expression.
		/// </summary>
		/// <remarks>
		/// Gets the right operand of the expression.
		/// </remarks>
		/// <value>The right operand.</value>
		public SearchQuery Right {
			get; private set;
		}

		internal override SearchQuery Optimize (ISearchQueryOptimizer optimizer)
		{
			SearchQuery binary = null;
			SearchQuery right = null;
			SearchQuery left = null;

			if (optimizer.CanReduce (Left))
				left = optimizer.Reduce (Left);

			if (optimizer.CanReduce (Right))
				right = optimizer.Reduce (Right);

			if (left != null || right != null)
				binary = new BinarySearchQuery (Term, left ?? Left, right ?? Right);
			else
				binary = this;

			if (optimizer.CanReduce (binary))
				return optimizer.Reduce (binary);

			return binary;
		}
	}
}
