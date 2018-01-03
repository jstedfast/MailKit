//
// UnarySearchQuery.cs
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

namespace MailKit.Search
{
	/// <summary>
	/// A unary search query such as a NOT expression.
	/// </summary>
	/// <remarks>
	/// A unary search query such as a NOT expression.
	/// </remarks>
	public class UnarySearchQuery : SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.UnarySearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new unary search query.
		/// </remarks>
		/// <param name="term">The search term.</param>
		/// <param name="operand">The operand.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="operand"/> is <c>null</c>.
		/// </exception>
		public UnarySearchQuery (SearchTerm term, SearchQuery operand) : base (term)
		{
			if (operand == null)
				throw new ArgumentNullException (nameof (operand));

			Operand = operand;
		}

		/// <summary>
		/// Gets the inner operand.
		/// </summary>
		/// <remarks>
		/// Gets the inner operand.
		/// </remarks>
		/// <value>The operand.</value>
		public SearchQuery Operand {
			get; private set;
		}

		internal override SearchQuery Optimize (ISearchQueryOptimizer optimizer)
		{
			var operand = Operand.Optimize (optimizer);
			SearchQuery unary;

			if (operand != Operand)
				unary = new UnarySearchQuery (Term, operand);
			else
				unary = this;

			return optimizer.Reduce (unary);
		}
	}
}
