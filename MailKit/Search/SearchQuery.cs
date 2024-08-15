//
// SearchQuery.cs
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

using System;
using System.Collections.Generic;

namespace MailKit.Search {
	/// <summary>
	/// A specialized query for searching messages in a <see cref="IMailFolder"/>.
	/// </summary>
	/// <remarks>
	/// A specialized query for searching messages in a <see cref="IMailFolder"/>.
	/// </remarks>
	public class SearchQuery
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.SearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SearchQuery"/> that matches all messages.
		/// </remarks>
		public SearchQuery () : this (SearchTerm.All)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="T:MailKit.Search.SearchQuery"/> class.
		/// </summary>
		/// <remarks>
		/// Creates a new <see cref="SearchQuery"/> with the specified search term.
		/// </remarks>
		/// <param name="term">The search term.</param>
		protected SearchQuery (SearchTerm term)
		{
			Term = term;
		}

		/// <summary>
		/// Get the search term used by the search query.
		/// </summary>
		/// <remarks>
		/// Gets the search term used by the search query.
		/// </remarks>
		/// <value>The term.</value>
		public SearchTerm Term {
			get; private set;
		}

		/// <summary>
		/// Match all messages in the folder.
		/// </summary>
		/// <remarks>
		/// <para>Matches all messages in the folder.</para>
		/// <note type="note">This is equivalent to the <c>ALL</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery All = new SearchQuery (SearchTerm.All);

		/// <summary>
		/// Create a conditional AND operation.
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
				throw new ArgumentNullException (nameof (left));

			if (right == null)
				throw new ArgumentNullException (nameof (right));

			return new BinarySearchQuery (SearchTerm.And, left, right);
		}

		/// <summary>
		/// Create a conditional AND operation.
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
				throw new ArgumentNullException (nameof (expr));

			return new BinarySearchQuery (SearchTerm.And, this, expr);
		}

		/// <summary>
		/// Match messages with the specified annotation.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the specified annotation.</para>
		/// <note type="note">This is equivalent to the <c>ANNOTATION</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc5257#section-4.8">rfc5257</a>
		/// and is therefor only available for use with IMAP servers that support the <c>ANNOTATE</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.Annotate"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <param name="entry">The annotation entry.</param>
		/// <param name="attribute">The annotation attribute.</param>
		/// <param name="value">The annotation attribute value.</param>
		/// <returns>A <see cref="AnnotationSearchQuery"/>.</returns>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="entry"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="attribute"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="attribute"/> is not a valid attribute for searching.
		/// </exception>
		public static AnnotationSearchQuery AnnotationsContain (AnnotationEntry entry, AnnotationAttribute attribute, string value)
		{
			return new AnnotationSearchQuery (entry, attribute, value);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Answered"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Answered"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>ANSWERED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Answered = new SearchQuery (SearchTerm.Answered);

		/// <summary>
		/// Match messages where the Bcc header contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the Bcc header contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>BCC</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.BccContains, text);
		}

		/// <summary>
		/// Match messages where the message body contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the message body contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>BODY</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.BodyContains, text);
		}

		/// <summary>
		/// Match messages where the Cc header contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the Cc header contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>CC</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.CcContains, text);
		}

		/// <summary>
		/// Match messages that have mod-sequence values greater than or equal to the specified mod-sequence value.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that have mod-sequence values greater than or equal to the specified mod-sequence value.</para>
		/// <note type="note">This is equivalent to the <c>MODSEQ</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc4551#section-3.4">rfc4551</a>
		/// and is therefor only available for use with IMAP servers that support the <c>CONDSTORE</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.CondStore"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="SearchQuery"/>.</returns>
		/// <param name="modseq">The mod-sequence value.</param>
		public static SearchQuery ChangedSince (ulong modseq)
		{
			return new NumericSearchQuery (SearchTerm.ModSeq, modseq);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Deleted"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Deleted"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>DELETED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Deleted = new SearchQuery (SearchTerm.Deleted);

		/// <summary>
		/// Match messages that were delivered after the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were delivered after the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SINCE</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery DeliveredAfter (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.DeliveredAfter, date);
		}

		/// <summary>
		/// Match messages that were delivered before the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were delivered before the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>BEFORE</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery DeliveredBefore (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.DeliveredBefore, date);
		}

		/// <summary>
		/// Match messages that were delivered on the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were delivered on the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>ON</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery DeliveredOn (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.DeliveredOn, date);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Draft"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Draft"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>DRAFT</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Draft = new SearchQuery (SearchTerm.Draft);

		/// <summary>
		/// Match messages using a saved search filter.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages using a saved search filter.</para>
		/// <note type="note">This is equivalent to the <c>FILTER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc5466">rfc5466</a> and is therefor only available
		/// for use with IMAP servers that support the <c>FILTERS</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.Filters"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="FilterSearchQuery"/>.</returns>
		/// <param name="name">The name of the saved search.</param>
		public static SearchQuery Filter (string name)
		{
			return new FilterSearchQuery (name);
		}

		/// <summary>
		/// Match messages using a saved search filter.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages using a saved search filter.</para>
		/// <note type="note">This is equivalent to the <c>FILTER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc5466">rfc5466</a> and is therefor only available
		/// for use with IMAP servers that support the <c>FILTERS</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.Filters"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="FilterSearchQuery"/>.</returns>
		/// <param name="filter">The name of the saved search.</param>
		public static SearchQuery Filter (MetadataTag filter)
		{
			return new FilterSearchQuery (filter);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Flagged"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Flagged"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>FLAGGED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Flagged = new SearchQuery (SearchTerm.Flagged);

		/// <summary>
		/// Match messages where the From header contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the From header contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>FROM</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.FromContains, text);
		}

		/// <summary>
		/// Apply a fuzzy matching algorithm to the specified expression.
		/// </summary>
		/// <remarks>
		/// <para>Applies a fuzzy matching algorithm to the specified expression.</para>
		/// <note type="note">This is equivalent to the <c>OLDER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc6203">rfc6203</a> and is therefor only available
		/// for use with IMAP servers that support the <c>SEARCH=FUZZY</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.FuzzySearch"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="UnarySearchQuery"/>.</returns>
		/// <param name="expr">The expression</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expr"/> is <c>null</c>.
		/// </exception>
		public static UnarySearchQuery Fuzzy (SearchQuery expr)
		{
			if (expr == null)
				throw new ArgumentNullException (nameof (expr));

			return new UnarySearchQuery (SearchTerm.Fuzzy, expr);
		}

		/// <summary>
		/// Match messages that have the specified flags set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that have the specified flag(s) set.</para>
		/// <note type="note">Maps each flag to the corresponding search key (<c>ANSWERED</c>, <c>DELETED</c>, <c>DRAFT</c>, <c>FLAGGED</c>,
		/// <c>RECENT</c> or <c>SEEN</c>) as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="SearchQuery"/>.</returns>
		/// <param name="flags">The message flags.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="flags"/> does not specify any valid message flags.
		/// </exception>
		public static SearchQuery HasFlags (MessageFlags flags)
		{
			var list = new List<SearchQuery> ();

			if ((flags & MessageFlags.Seen) != 0)
				list.Add (Seen);
			if ((flags & MessageFlags.Answered) != 0)
				list.Add (Answered);
			if ((flags & MessageFlags.Flagged) != 0)
				list.Add (Flagged);
			if ((flags & MessageFlags.Deleted) != 0)
				list.Add (Deleted);
			if ((flags & MessageFlags.Draft) != 0)
				list.Add (Draft);
			if ((flags & MessageFlags.Recent) != 0)
				list.Add (Recent);

			if (list.Count == 0)
				throw new ArgumentException ("No flags specified.", nameof (flags));

			var query = list[0];
			for (int i = 1; i < list.Count; i++)
				query = query.And (list[i]);

			return query;
		}

		/// <summary>
		/// Match messages that do not have any of the specified flags set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have any of the specified flags set.</para>
		/// <note type="note">Maps each flag to the corresponding search key (<c>UNANSWERED</c>, <c>UNDELETED</c>, <c>UNDRAFT</c>, <c>UNFLAGGED</c>,
		/// <c>OLD</c> or <c>UNSEEN</c>) as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="SearchQuery"/>.</returns>
		/// <param name="flags">The message flags.</param>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="flags"/> does not specify any valid message flags.
		/// </exception>
		public static SearchQuery NotFlags (MessageFlags flags)
		{
			var list = new List<SearchQuery> ();

			if ((flags & MessageFlags.Seen) != 0)
				list.Add (NotSeen);
			if ((flags & MessageFlags.Answered) != 0)
				list.Add (NotAnswered);
			if ((flags & MessageFlags.Flagged) != 0)
				list.Add (NotFlagged);
			if ((flags & MessageFlags.Deleted) != 0)
				list.Add (NotDeleted);
			if ((flags & MessageFlags.Draft) != 0)
				list.Add (NotDraft);
			if ((flags & MessageFlags.Recent) != 0)
				list.Add (NotRecent);

			if (list.Count == 0)
				throw new ArgumentException ("No flags specified.", nameof (flags));

			var query = list[0];
			for (int i = 1; i < list.Count; i++)
				query = query.And (list[i]);

			return query;
		}

		/// <summary>
		/// Match messages that have the specified keyword set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that have the specified keyword set.</para>
		/// <para>A keyword is a user-defined message flag that can be set (or unset) on a message.</para>
		/// <note type="note">This is equivalent to the <c>KEYWORD</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="keyword">The keyword.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="keyword"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="keyword"/> is empty.
		/// </exception>
		public static TextSearchQuery HasKeyword (string keyword)
		{
			if (keyword == null)
				throw new ArgumentNullException (nameof (keyword));

			if (keyword.Length == 0)
				throw new ArgumentException ("The keyword cannot be an empty string.", nameof (keyword));

			return new TextSearchQuery (SearchTerm.Keyword, keyword);
		}

		/// <summary>
		/// Match messages that have all of the specified keywords set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that have all of the specified keywords set.</para>
		/// <para>A keyword is a user-defined message flag that can be set (or unset) on a message.</para>
		/// <note type="note">This is equivalent to AND-ing multiple <c>KEYWORD</c> search keys as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="SearchQuery"/>.</returns>
		/// <param name="keywords">The keywords.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="keywords"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="keywords"/> is <c>null</c> or empty.</para>
		/// <para>-or-</para>
		/// <para>No keywords were given.</para>
		/// </exception>
		public static SearchQuery HasKeywords (IEnumerable<string> keywords)
		{
			if (keywords == null)
				throw new ArgumentNullException (nameof (keywords));

			var list = new List<SearchQuery> ();

			foreach (var keyword in keywords) {
				if (string.IsNullOrEmpty (keyword))
					throw new ArgumentException ("Cannot search for null or empty keywords.", nameof (keywords));

				list.Add (new TextSearchQuery (SearchTerm.Keyword, keyword));
			}

			if (list.Count == 0)
				throw new ArgumentException ("No keywords specified.", nameof (keywords));

			var query = list[0];
			for (int i = 1; i < list.Count; i++)
				query = query.And (list[i]);

			return query;
		}

		/// <summary>
		/// Match messages that do not have the specified keyword set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the specified keyword set.</para>
		/// <para>A keyword is a user-defined message flag that can be set (or unset) on a message.</para>
		/// <note type="note">This is equivalent to the <c>UNKEYWORD</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="keyword">The keyword.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="keyword"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="keyword"/> is empty.
		/// </exception>
		public static TextSearchQuery NotKeyword (string keyword)
		{
			if (keyword == null)
				throw new ArgumentNullException (nameof (keyword));

			if (keyword.Length == 0)
				throw new ArgumentException ("The keyword cannot be an empty string.", nameof (keyword));

			return new TextSearchQuery (SearchTerm.NotKeyword, keyword);
		}

		/// <summary>
		/// Match messages that do not have any of the specified keywords set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have any of the specified keywords set.</para>
		/// <para>A keyword is a user-defined message flag that can be set (or unset) on a message.</para>
		/// <note type="note">This is equivalent to AND-ing multiple <c>UNKEYWORD</c> search keys as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="SearchQuery"/>.</returns>
		/// <param name="keywords">The keywords.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="keywords"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para>One or more of the <paramref name="keywords"/> is <c>null</c> or empty.</para>
		/// <para>-or-</para>
		/// <para>No keywords were given.</para>
		/// </exception>
		public static SearchQuery NotKeywords (IEnumerable<string> keywords)
		{
			if (keywords == null)
				throw new ArgumentNullException (nameof (keywords));

			var list = new List<SearchQuery> ();

			foreach (var keyword in keywords) {
				if (string.IsNullOrEmpty (keyword))
					throw new ArgumentException ("Cannot search for null or empty keywords.", nameof (keywords));

				list.Add (new TextSearchQuery (SearchTerm.NotKeyword, keyword));
			}

			if (list.Count == 0)
				throw new ArgumentException ("No flags specified.", nameof (keywords));

			var query = list[0];
			for (int i = 1; i < list.Count; i++)
				query = query.And (list[i]);

			return query;
		}

		/// <summary>
		/// Match messages where the specified header contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the specified header contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>HEADER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="HeaderSearchQuery"/>.</returns>
		/// <param name="field">The header field to match against.</param>
		/// <param name="text">The text to match against.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="field"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="text"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="field"/> is empty.
		/// </exception>
		public static HeaderSearchQuery HeaderContains (string field, string text)
		{
			if (field == null)
				throw new ArgumentNullException (nameof (field));

			if (field.Length == 0)
				throw new ArgumentException ("Cannot search an empty header field name.", nameof (field));

			if (text == null)
				throw new ArgumentNullException (nameof (text));

			return new HeaderSearchQuery (field, text);
		}

		/// <summary>
		/// Match messages that are larger than the specified number of octets.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that are larger than the specified number of octets.</para>
		/// <note type="note">This is equivalent to the <c>LARGER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="octets">The number of octets.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="octets"/> is a negative value.
		/// </exception>
		public static NumericSearchQuery LargerThan (int octets)
		{
			if (octets < 0)
				throw new ArgumentOutOfRangeException (nameof (octets));

			return new NumericSearchQuery (SearchTerm.LargerThan, (ulong) octets);
		}

		/// <summary>
		/// Match messages that contain the specified text in either the header or the body.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that contain the specified text in either the header or the body.</para>
		/// <note type="note">This is equivalent to the <c>TEXT</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.MessageContains, text);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Recent"/> flag set but not the <see cref="MessageFlags.Seen"/>.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Recent"/> flag set but not the <see cref="MessageFlags.Seen"/>.</para>
		/// <note type="note">This is equivalent to the <c>NEW</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery New = new SearchQuery (SearchTerm.New);

		/// <summary>
		/// Create a logical negation of the specified expression.
		/// </summary>
		/// <remarks>
		/// <para>Creates a logical negation of the specified expression.</para>
		/// <note type="note">This is equivalent to the <c>NOT</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="UnarySearchQuery"/>.</returns>
		/// <param name="expr">The expression</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expr"/> is <c>null</c>.
		/// </exception>
		public static UnarySearchQuery Not (SearchQuery expr)
		{
			if (expr == null)
				throw new ArgumentNullException (nameof (expr));

			return new UnarySearchQuery (SearchTerm.Not, expr);
		}

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Answered"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Answered"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>UNANSWERED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery NotAnswered = new SearchQuery (SearchTerm.NotAnswered);

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Deleted"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Deleted"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>UNDELETED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery NotDeleted = new SearchQuery (SearchTerm.NotDeleted);

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Draft"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Draft"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>UNDRAFT</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery NotDraft = new SearchQuery (SearchTerm.NotDraft);

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Flagged"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Flagged"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>UNFLAGGED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery NotFlagged = new SearchQuery (SearchTerm.NotFlagged);

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Recent"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Recent"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>OLD</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery NotRecent = new SearchQuery (SearchTerm.NotRecent);

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Seen"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Seen"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>UNSEEN</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery NotSeen = new SearchQuery (SearchTerm.NotSeen);

		/// <summary>
		/// Match messages that do not have the <see cref="MessageFlags.Recent"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that do not have the <see cref="MessageFlags.Recent"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>OLD</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Old = new SearchQuery (SearchTerm.NotRecent);

		/// <summary>
		/// Match messages older than the specified number of seconds.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages older than the specified number of seconds.</para>
		/// <note type="note">This is equivalent to the <c>OLDER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc5032">rfc5032</a> and is therefor only available
		/// for use with IMAP servers that support the <c>WITHIN</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.Within"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="seconds">The number of seconds.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// The number of seconds cannot be less than <c>1</c>.
		/// </exception>
		public static NumericSearchQuery OlderThan (int seconds)
		{
			if (seconds < 1)
				throw new ArgumentOutOfRangeException (nameof (seconds));

			return new NumericSearchQuery (SearchTerm.Older, (ulong) seconds);
		}

		/// <summary>
		/// Create a conditional OR operation.
		/// </summary>
		/// <remarks>
		/// <para>A conditional OR operation only evaluates the second operand if the first operand evaluates to false.</para>
		/// <note type="note">This is equivalent to the <c>OR</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
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
				throw new ArgumentNullException (nameof (left));

			if (right == null)
				throw new ArgumentNullException (nameof (right));

			return new BinarySearchQuery (SearchTerm.Or, left, right);
		}

		/// <summary>
		/// Create a conditional OR operation.
		/// </summary>
		/// <remarks>
		/// <para>A conditional OR operation only evaluates the second operand if the first operand evaluates to true.</para>
		/// <note type="note">This is equivalent to the <c>OR</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="BinarySearchQuery"/> representing the conditional AND operation.</returns>
		/// <param name="expr">An additional query to execute.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expr"/> is <c>null</c>.
		/// </exception>
		public BinarySearchQuery Or (SearchQuery expr)
		{
			if (expr == null)
				throw new ArgumentNullException (nameof (expr));

			return new BinarySearchQuery (SearchTerm.Or, this, expr);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Recent"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Recent"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>RECENT</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Recent = new SearchQuery (SearchTerm.Recent);

		/// <summary>
		/// Match all messages in the mailbox when the underlying storage of that mailbox supports
		/// the save date attribute.
		/// </summary>
		/// <remarks>
		/// <para>Matches all messages in the mailbox when the underlying storage of that mailbox supports
		/// the save date attribute. Conversely, it matches no messages in the mailbox when the save
		/// date attribute is not supported.</para>
		/// <note type="note">This is equivalent to the <c>SAVEDATESUPPORTED</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc8514">rfc8514, section 4.3</a>
		/// and is therefor only available for use with IMAP servers that support the <c>SAVEDATE</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.SaveDate"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		public static readonly SearchQuery SaveDateSupported = new SearchQuery (SearchTerm.SaveDateSupported);

		/// <summary>
		/// Match messages that were saved to the mailbox before the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were saved to the mailbox before the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SAVEDBEFORE</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc8514">rfc8514, section 4.3</a>
		/// and is therefor only available for use with IMAP servers that support the <c>SAVEDATE</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.SaveDate"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SavedBefore (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SavedBefore, date);
		}

		/// <summary>
		/// Match messages that were saved to the mailbox on the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were saved to the mailbox on the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SAVEDON</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc8514">rfc8514, section 4.3</a>
		/// and is therefor only available for use with IMAP servers that support the <c>SAVEDATE</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.SaveDate"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SavedOn (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SavedOn, date);
		}

		/// <summary>
		/// Match messages that were saved to the mailbox since the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were saved to the mailbox since the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SAVEDSINCE</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc8514">rfc8514, section 4.3</a>
		/// and is therefor only available for use with IMAP servers that support the <c>SAVEDATE</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.SaveDate"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SavedSince (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SavedSince, date);
		}

		/// <summary>
		/// Match messages with the <see cref="MessageFlags.Seen"/> flag set.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages with the <see cref="MessageFlags.Seen"/> flag set.</para>
		/// <note type="note">This is equivalent to the <c>SEEN</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		public static readonly SearchQuery Seen = new SearchQuery (SearchTerm.Seen);

		/// <summary>
		/// Match messages that were sent before the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were sent before the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SENTBEFORE</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SentBefore (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SentBefore, date);
		}

		/// <summary>
		/// Match messages that were sent on the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were sent on the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SENTON</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SentOn (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SentOn, date);
		}

		/// <summary>
		/// Match messages that were sent since the specified date.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that were sent since the specified date.</para>
		/// <para>The resolution of this search query does not include the time.</para>
		/// <note type="note">This is equivalent to the <c>SENTSINCE</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="DateSearchQuery"/>.</returns>
		/// <param name="date">The date.</param>
		public static DateSearchQuery SentSince (DateTime date)
		{
			return new DateSearchQuery (SearchTerm.SentSince, date);
		}

		/// <summary>
		/// Match messages that are smaller than the specified number of octets.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that are smaller than the specified number of octets.</para>
		/// <note type="note">This is equivalent to the <c>SMALLER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="octets">The number of octets.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="octets"/> is a negative value.
		/// </exception>
		public static NumericSearchQuery SmallerThan (int octets)
		{
			if (octets < 0)
				throw new ArgumentOutOfRangeException (nameof (octets));

			return new NumericSearchQuery (SearchTerm.SmallerThan, (ulong) octets);
		}

		/// <summary>
		/// Match messages where the Subject header contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the Subject header contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>SUBJECT</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.SubjectContains, text);
		}

		/// <summary>
		/// Match messages where the To header contains the specified text.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages where the To header contains the specified text.</para>
		/// <note type="note">This is equivalent to the <c>TO</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
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
			return new TextSearchQuery (SearchTerm.ToContains, text);
		}

		/// <summary>
		/// Limit the search query to messages with the specified unique identifiers.
		/// </summary>
		/// <remarks>
		/// <para>Limits the search query to messages with the specified unique identifiers.</para>
		/// <note type="note">This is equivalent to the <c>UID</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc3501#section-6.4.4">rfc3501</a>.</note>
		/// </remarks>
		/// <returns>A <see cref="UidSearchQuery"/>.</returns>
		/// <param name="uids">The unique identifiers.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="uids"/> is empty.
		/// </exception>
		public static UidSearchQuery Uids (IList<UniqueId> uids)
		{
			return new UidSearchQuery (uids);
		}

		/// <summary>
		/// Match messages younger than the specified number of seconds.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages younger than the specified number of seconds.</para>
		/// <note type="note">This is equivalent to the <c>YOUNGER</c> search key as defined in <a href="https://datatracker.ietf.org/doc/html/rfc5032">rfc5032</a> and is therefor only available
		/// for use with IMAP servers that support the <c>WITHIN</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.Within"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="seconds">The number of seconds.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// The number of seconds cannot be less than <c>1</c>.
		/// </exception>
		public static NumericSearchQuery YoungerThan (int seconds)
		{
			if (seconds < 1)
				throw new ArgumentOutOfRangeException (nameof (seconds));

			return new NumericSearchQuery (SearchTerm.Younger, (ulong) seconds);
		}

		#region GMail extensions

		/// <summary>
		/// Match messages that have the specified GMail message identifier.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that have the specified GMail message identifier.</para>
		/// <note type="note">This is equivalent to the <c>X-GM-MSGID</c> search key as defined in <a href="https://developers.google.com/gmail/imap_extensions">Google's IMAP extensions</a> and is therefor only available
		/// for use with IMAP servers that support the <c>X-GM-EXT1</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.GMailExt1"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="id">The GMail message identifier.</param>
		public static NumericSearchQuery GMailMessageId (ulong id)
		{
			return new NumericSearchQuery (SearchTerm.GMailMessageId, id);
		}

		/// <summary>
		/// Match messages belonging to the specified GMail thread.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages belonging to the specified GMail thread.</para>
		/// <note type="note">This is equivalent to the <c>X-GM-THRID</c> search key as defined in <a href="https://developers.google.com/gmail/imap_extensions">Google's IMAP extensions</a> and is therefor only available
		/// for use with IMAP servers that support the <c>X-GM-EXT1</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.GMailExt1"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="NumericSearchQuery"/>.</returns>
		/// <param name="thread">The GMail thread.</param>
		public static NumericSearchQuery GMailThreadId (ulong thread)
		{
			return new NumericSearchQuery (SearchTerm.GMailThreadId, thread);
		}

		/// <summary>
		/// Match messages that have the specified GMail label.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages that have the specified GMail label.</para>
		/// <note type="note">This is equivalent to the <c>X-GM-LABELS</c> search key as defined in <a href="https://developers.google.com/gmail/imap_extensions">Google's IMAP extensions</a> and is therefor only available
		/// for use with IMAP servers that support the <c>X-GM-EXT1</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.GMailExt1"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="label">The GMail label.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="label"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="label"/> is empty.
		/// </exception>
		public static TextSearchQuery HasGMailLabel (string label)
		{
			if (label == null)
				throw new ArgumentNullException (nameof (label));

			if (label.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", nameof (label));

			return new TextSearchQuery (SearchTerm.GMailLabels, label);
		}

		/// <summary>
		/// Match messages using the GMail search expression.
		/// </summary>
		/// <remarks>
		/// <para>Matches messages using Google's custom message search syntax.</para>
		/// <note type="note">This is equivalent to the <c>X-GM-RAW</c> search key as defined in <a href="https://developers.google.com/gmail/imap_extensions">Google's IMAP extensions</a> and is therefor only available
		/// for use with IMAP servers that support the <c>X-GM-EXT1</c> extension.</note>
		/// <seealso cref="MailKit.Net.Imap.ImapCapabilities.GMailExt1"/>
		/// <seealso cref="MailKit.Net.Imap.ImapClient.Capabilities"/>
		/// </remarks>
		/// <returns>A <see cref="TextSearchQuery"/>.</returns>
		/// <param name="expression">The raw GMail search text.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="expression"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="expression"/> is empty.
		/// </exception>
		public static TextSearchQuery GMailRawSearch (string expression)
		{
			if (expression == null)
				throw new ArgumentNullException (nameof (expression));

			if (expression.Length == 0)
				throw new ArgumentException ("Cannot search for an empty string.", nameof (expression));

			return new TextSearchQuery (SearchTerm.GMailRaw, expression);
		}

		#endregion

		internal virtual SearchQuery Optimize (ISearchQueryOptimizer optimizer)
		{
			return optimizer.Reduce (this);
		}
	}
}
