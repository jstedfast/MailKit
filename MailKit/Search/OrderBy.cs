//
// OrderBy.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2013-2014 Xamarin Inc. (www.xamarin.com)
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
	/// Specifies a sort order for search results.
	/// </summary>
	/// <remarks>
	/// You can combine multiple <see cref="OrderBy"/> rules to specify the sort
	/// order that <see cref="IMailFolder.Search(SearchQuery,System.Collections.Generic.IList&lt;OrderBy&gt;,System.Threading.CancellationToken)"/>
	/// should return the results in.
	/// </remarks>
	public class OrderBy
	{
		OrderBy (OrderByType type, SortOrder order)
		{
			Order = order;
			Type = type;
		}

		internal OrderByType Type {
			get; private set;
		}

		internal SortOrder Order {
			get; private set;
		}

		/// <summary>
		/// Sort results by arrival date in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by arrival date in ascending order.
		/// </remarks>
		public static readonly OrderBy Arrival = new OrderBy (OrderByType.Arrival, SortOrder.Ascending);

		/// <summary>
		/// Sort results by arrival date in desending order.
		/// </summary>
		/// <remarks>
		/// Sort results by arrival date in desending order.
		/// </remarks>
		public static readonly OrderBy ReverseArrival = new OrderBy (OrderByType.Arrival, SortOrder.Descending);

		/// <summary>
		/// Sort results by the Cc header in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the Cc header in ascending order.
		/// </remarks>
		public static readonly OrderBy Cc = new OrderBy (OrderByType.Cc, SortOrder.Ascending);

		/// <summary>
		/// Sort results by the Cc header in descending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the Cc header in descending order.
		/// </remarks>
		public static readonly OrderBy ReverseCc = new OrderBy (OrderByType.Cc, SortOrder.Descending);

		/// <summary>
		/// Sort results by the sent date in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the sent date in ascending order.
		/// </remarks>
		public static readonly OrderBy Date = new OrderBy (OrderByType.Date, SortOrder.Ascending);

		/// <summary>
		/// Sort results by the sent date in descending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the sent date in descending order.
		/// </remarks>
		public static readonly OrderBy ReverseDate = new OrderBy (OrderByType.Date, SortOrder.Descending);

		/// <summary>
		/// Sort results by the From header in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the From header in ascending order.
		/// </remarks>
		public static readonly OrderBy From = new OrderBy (OrderByType.From, SortOrder.Ascending);

		/// <summary>
		/// Sort results by the From header in descending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the From header in descending order.
		/// </remarks>
		public static readonly OrderBy ReverseFrom = new OrderBy (OrderByType.From, SortOrder.Descending);

		/// <summary>
		/// Sort results by the message size in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the message size in ascending order.
		/// </remarks>
		public static readonly OrderBy Size = new OrderBy (OrderByType.Size, SortOrder.Ascending);

		/// <summary>
		/// Sort results by the message size in descending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the message size in descending order.
		/// </remarks>
		public static readonly OrderBy ReverseSize = new OrderBy (OrderByType.Size, SortOrder.Descending);

		/// <summary>
		/// Sort results by the Subject header in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the Subject header in ascending order.
		/// </remarks>
		public static readonly OrderBy Subject = new OrderBy (OrderByType.Subject, SortOrder.Ascending);

		/// <summary>
		/// Sort results by the Subject header in descending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the Subject header in descending order.
		/// </remarks>
		public static readonly OrderBy ReverseSubject = new OrderBy (OrderByType.Subject, SortOrder.Descending);

		/// <summary>
		/// Sort results by the To header in ascending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the To header in ascending order.
		/// </remarks>
		public static readonly OrderBy To = new OrderBy (OrderByType.To, SortOrder.Ascending);

		/// <summary>
		/// Sort results by the To header in descending order.
		/// </summary>
		/// <remarks>
		/// Sort results by the To header in descending order.
		/// </remarks>
		public static readonly OrderBy ReverseTo = new OrderBy (OrderByType.To, SortOrder.Descending);
	}
}
