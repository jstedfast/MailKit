//
// SearchQuery.cs
//
// Author: Jeffrey Stedfast <jeff@xamarin.com>
//
// Copyright (c) 2014 Jeffrey Stedfast
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

namespace MailKit.Search {
	/// <summary>
	/// A specialized query for searching messages in a <see cref="IFolder"/>.
	/// </summary>
	public class SearchQuery
	{
		internal SearchQuery (SearchTerm term)
		{
			Term = term;
		}

		internal SearchTerm Term {
			get; private set;
		}

		/// <summary>
		/// Matches all messages in the folder.
		/// </summary>
		public static readonly SearchQuery All = new SearchQuery (SearchTerm.All);

		/// <summary>
		/// Creates a conditional AND operation.
		/// </summary>
		/// <remarks>
		/// A conditional AND operation only evaluates the second operand if the first operand evaluates to true.
		/// </remarks>
		/// <returns>A <see cref="BinarySearchQuery"/> representing the conditional AND operation.</returns>
		/// <param name="left">The first operand.</param>
		/// <param name="right">The second operand.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="left"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="right"/> is <c>null</c>.</para>
		/// </exception>
		public static BinarySearchQuery And (SearchQuery left, SearchQuery right)
		{
			if (left == null)
				throw new ArgumentNullException ("left");

			if (right == null)
				throw new ArgumentNullException ("right");

			return new BinarySearchQuery (SearchTerm.And, left, right);
		}

		/// <summary>
		/// Creates a conditional AND operation.
		/// </summary>
		/// <remarks>
		/// A conditional AND operation only evaluates the second operand if the first operand evaluates to true.
		/// </remarks>
		/// <returns>A <see cref="BinarySearchQuery"/> representing the conditional AND operation.</returns>
		/// <param name="expr">An additional query to execute.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expr"/> is <c>null</c>.
		/// </exception>
		public BinarySearchQuery And (SearchQuery expr)
		{
			if (expr == null)
				throw new ArgumentNullException ("expr");

			return new BinarySearchQuery (SearchTerm.And, this, expr);
		}

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Answered"/> flag set.
		/// </summary>
		public static readonly SearchQuery Answered = new SearchQuery (SearchTerm.Answered);

		/// <summary>
		/// Matches messages where the Bcc header contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery BccContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.BccContains, text);
		}

		/// <summary>
		/// Matches messages where the message body contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery BodyContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.BodyContains, text);
		}

		/// <summary>
		/// Matches messages where the Cc header contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery CcContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.CcContains, text);
		}

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Deleted"/> flag set.
		/// </summary>
		public static readonly SearchQuery Deleted = new SearchQuery (SearchTerm.Deleted);

		/// <summary>
		/// Matches messages that were delivered after the specified date.
		/// </summary>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery DeliveredAfter (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.DeliveredAfter, date);
		}

		/// <summary>
		/// Matches messages that were delivered before the specified date.
		/// </summary>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery DeliveredBefore (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.DeliveredBefore, date);
		}

		/// <summary>
		/// Matches messages that were delivered on the specified date.
		/// </summary>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery DeliveredOn (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.DeliveredOn, date);
		}

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Draft"/> flag set.
		/// </summary>
		public static readonly SearchQuery Draft = new SearchQuery (SearchTerm.Draft);

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Flagged"/> flag set.
		/// </summary>
		public static readonly SearchQuery Flagged = new SearchQuery (SearchTerm.Flagged);

		/// <summary>
		/// Matches messages where the From header contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery FromContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.FromContains, text);
		}

		/// <summary>
		/// Matches messages where the specified header contains the specified text.
		/// </summary>
		/// <returns>A <see cref="HeaderSearchQuery"/>.</returns>
		/// <param name="field">The header field to match against.</param>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="field"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="text"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="field"/> is empty.</para>
		/// <para>-or-</para>
		/// <para><paramref name="text"/> is empty.</para>
		/// </exception>
		public static HeaderSearchQuery Header (string field, string text)
		{
			if (field == null)
				throw new ArgumentNullException ("field");

			if (field.Length == 0)
				throw new ArgumentException ("Cannot search an empty header field name.", "field");

			if (text == null)
				throw new ArgumentNullException ("value");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty header value.", "value");

			return new HeaderSearchQuery (field, text);
		}

		// FIXME: KEYWORD???

		/// <summary>
		/// Matches messages that are larger than the specified number of octets.
		/// </summary>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="octets">The number of octets.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="octets"/> is a negative value.
		/// </exception>
		public static NumericSearchQuery LargerThan (int octets)
		{
			if (octets < 0)
				throw new ArgumentOutOfRangeException ("octets");

			return new NumericSearchQuery (SearchTerm.LargerThan, octets);
		}

		/// <summary>
		/// Matches messages where the raw message contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery MessageContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.MessageContains, text);
		}

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Recent"/> flag set but not the <see cref="MessageFlags.Seen"/>.
		/// </summary>
		public static readonly SearchQuery New = new SearchQuery (SearchTerm.New);

		/// <summary>
		/// Creates a logical negation of the specified expression.
		/// </summary>
		/// <returns>A <see cref="UnarySearchQuery"/>.</returns>
		/// <param name="expr">The expression</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expr"/> is <c>null</c>.
		/// </exception>
		public static UnarySearchQuery Not (SearchQuery expr)
		{
			if (expr == null)
				throw new ArgumentNullException ("expr");

			return new UnarySearchQuery (SearchTerm.Not, expr);
		}

		/// <summary>
		/// Matches messages that do not have the <see cref="MessageFlags.Answered"/> flag set.
		/// </summary>
		public static readonly SearchQuery NotAnswered = new SearchQuery (SearchTerm.NotAnswered);

		/// <summary>
		/// Matches messages that do not have the <see cref="MessageFlags.Deleted"/> flag set.
		/// </summary>
		public static readonly SearchQuery NotDeleted = new SearchQuery (SearchTerm.NotDeleted);

		/// <summary>
		/// Matches messages that do not have the <see cref="MessageFlags.Draft"/> flag set.
		/// </summary>
		public static readonly SearchQuery NotDraft = new SearchQuery (SearchTerm.NotDraft);

		/// <summary>
		/// Matches messages that do not have the <see cref="MessageFlags.Flagged"/> flag set.
		/// </summary>
		public static readonly SearchQuery NotFlagged = new SearchQuery (SearchTerm.NotFlagged);

		/// <summary>
		/// Matches messages that do not have the <see cref="MessageFlags.Recent"/> flag set.
		/// </summary>
		public static readonly SearchQuery NotRecent = new SearchQuery (SearchTerm.NotRecent);

		/// <summary>
		/// Matches messages that do not have the <see cref="MessageFlags.Seen"/> flag set.
		/// </summary>
		public static readonly SearchQuery NotSeen = new SearchQuery (SearchTerm.NotSeen);

		/// <summary>
		/// Creates a conditional OR operation.
		/// </summary>
		/// <remarks>
		/// A conditional OR operation only evaluates the second operand if the first operand evaluates to false.
		/// </remarks>
		/// <returns>A <see cref="BinarySearchQuery"/> representing the conditional OR operation.</returns>
		/// <param name="left">The first operand.</param>
		/// <param name="right">The second operand.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="left"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="right"/> is <c>null</c>.</para>
		/// </exception>
		public static BinarySearchQuery Or (SearchQuery left, SearchQuery right)
		{
			if (left == null)
				throw new ArgumentNullException ("left");

			if (right == null)
				throw new ArgumentNullException ("right");

			return new BinarySearchQuery (SearchTerm.Or, left, right);
		}

		/// <summary>
		/// Creates a conditional OR operation.
		/// </summary>
		/// <remarks>
		/// A conditional OR operation only evaluates the second operand if the first operand evaluates to true.
		/// </remarks>
		/// <returns>A <see cref="BinarySearchQuery"/> representing the conditional AND operation.</returns>
		/// <param name="expr">An additional query to execute.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expr"/> is <c>null</c>.
		/// </exception>
		public BinarySearchQuery Or (SearchQuery expr)
		{
			if (expr == null)
				throw new ArgumentNullException ("expr");

			return new BinarySearchQuery (SearchTerm.Or, this, expr);
		}

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Recent"/> flag set.
		/// </summary>
		public static readonly SearchQuery Recent = new SearchQuery (SearchTerm.Recent);

		/// <summary>
		/// Matches messages with the <see cref="MessageFlags.Seen"/> flag set.
		/// </summary>
		public static readonly SearchQuery Seen = new SearchQuery (SearchTerm.Seen);

		/// <summary>
		/// Matches messages that were sent after the specified date.
		/// </summary>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SentAfter (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SentAfter, date);
		}

		/// <summary>
		/// Matches messages that were sent before the specified date.
		/// </summary>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SentBefore (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SentBefore, date);
		}

		/// <summary>
		/// Matches messages that were sent on the specified date.
		/// </summary>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SentOn (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SentOn, date);
		}

		/// <summary>
		/// Matches messages that are smaller than the specified number of octets.
		/// </summary>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="octets">The number of octets.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="octets"/> is a negative value.
		/// </exception>
		public static NumericSearchQuery SmallerThan (int octets)
		{
			if (octets < 0)
				throw new ArgumentOutOfRangeException ("octets");

			return new NumericSearchQuery (SearchTerm.SmallerThan, octets);
		}

		/// <summary>
		/// Matches messages where the Subject header contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery SubjectContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.SubjectContains, text);
		}

		/// <summary>
		/// Matches messages where the To header contains the specified text.
		/// </summary>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="text"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="text"/> is empty.
		/// </exception>
		public static TextSearchQuery ToContains (string text)
		{
			if (text == null)
				throw new ArgumentNullException ("text");

			if (text.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", "text");

			return new TextSearchQuery (SearchTerm.ToContains, text);
		}

		// FIXME: UID???

		// FIXME: UNKEYWORD???

		internal virtual SearchQuery Optimize (ISearchQueryOptimizer optimizer)
		{
			if (optimizer.CanReduce (this))
				return optimizer.Reduce (this);

			return this;
		}
	}
}
