//
// ImapFolderSearch.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;
using MimeKit.IO;
using MimeKit.Utils;
using MailKit.Search;

namespace MailKit.Net.Imap
{
	public partial class ImapFolder
	{
		static bool IsAscii (string text)
		{
			for (int i = 0; i < text.Length; i++) {
				if (text[i] > 127)
					return false;
			}

			return true;
		}

		static string FormatDateTime (DateTime date)
		{
			return date.ToString ("d-MMM-yyyy", CultureInfo.InvariantCulture);
		}

		void BuildQuery (StringBuilder builder, SearchQuery query, List<string> args, bool parens, ref bool ascii)
		{
			TextSearchQuery text = null;
			NumericSearchQuery numeric;
			FilterSearchQuery filter;
			HeaderSearchQuery header;
			BinarySearchQuery binary;
			UnarySearchQuery unary;
			DateSearchQuery date;
			UidSearchQuery uid;

			switch (query.Term) {
			case SearchTerm.All:
				builder.Append ("ALL");
				break;
			case SearchTerm.And:
				binary = (BinarySearchQuery) query;
				if (parens)
					builder.Append ('(');
				BuildQuery (builder, binary.Left, args, false, ref ascii);
				builder.Append (' ');
				BuildQuery (builder, binary.Right, args, false, ref ascii);
				if (parens)
					builder.Append (')');
				break;
			case SearchTerm.Answered:
				builder.Append ("ANSWERED");
				break;
			case SearchTerm.BccContains:
				text = (TextSearchQuery) query;
				builder.Append ("BCC %S");
				args.Add (text.Text);
				break;
			case SearchTerm.BodyContains:
				text = (TextSearchQuery) query;
				builder.Append ("BODY %S");
				args.Add (text.Text);
				break;
			case SearchTerm.CcContains:
				text = (TextSearchQuery) query;
				builder.Append ("CC %S");
				args.Add (text.Text);
				break;
			case SearchTerm.Deleted:
				builder.Append ("DELETED");
				break;
			case SearchTerm.DeliveredAfter:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SINCE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.DeliveredBefore:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("BEFORE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.DeliveredOn:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("ON {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.Draft:
				builder.Append ("DRAFT");
				break;
			case SearchTerm.Filter:
				if ((Engine.Capabilities & ImapCapabilities.Filters) == 0)
					throw new NotSupportedException ("The FILTER search term is not supported by the IMAP server.");

				filter = (FilterSearchQuery) query;
				builder.Append ("FILTER %S");
				args.Add (filter.Name);
				break;
			case SearchTerm.Flagged:
				builder.Append ("FLAGGED");
				break;
			case SearchTerm.FromContains:
				text = (TextSearchQuery) query;
				builder.Append ("FROM %S");
				args.Add (text.Text);
				break;
			case SearchTerm.Fuzzy:
				if ((Engine.Capabilities & ImapCapabilities.FuzzySearch) == 0)
					throw new NotSupportedException ("The FUZZY search term is not supported by the IMAP server.");

				builder.Append ("FUZZY ");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args, true, ref ascii);
				break;
			case SearchTerm.HeaderContains:
				header = (HeaderSearchQuery) query;
				builder.AppendFormat ("HEADER {0} %S", header.Field);
				args.Add (header.Value);
				break;
			case SearchTerm.Keyword:
				text = (TextSearchQuery) query;
				builder.Append ("KEYWORD %S");
				args.Add (text.Text);
				break;
			case SearchTerm.LargerThan:
				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("LARGER {0}", numeric.Value);
				break;
			case SearchTerm.MessageContains:
				text = (TextSearchQuery) query;
				builder.Append ("TEXT %S");
				args.Add (text.Text);
				break;
			case SearchTerm.ModSeq:
				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("MODSEQ {0}", numeric.Value);
				break;
			case SearchTerm.New:
				builder.Append ("NEW");
				break;
			case SearchTerm.Not:
				builder.Append ("NOT ");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args, true, ref ascii);
				break;
			case SearchTerm.NotAnswered:
				builder.Append ("UNANSWERED");
				break;
			case SearchTerm.NotDeleted:
				builder.Append ("UNDELETED");
				break;
			case SearchTerm.NotDraft:
				builder.Append ("UNDRAFT");
				break;
			case SearchTerm.NotFlagged:
				builder.Append ("UNFLAGGED");
				break;
			case SearchTerm.NotKeyword:
				text = (TextSearchQuery) query;
				builder.Append ("UNKEYWORD %S");
				args.Add (text.Text);
				break;
			case SearchTerm.NotRecent:
				builder.Append ("OLD");
				break;
			case SearchTerm.NotSeen:
				builder.Append ("UNSEEN");
				break;
			case SearchTerm.Older:
				if ((Engine.Capabilities & ImapCapabilities.Within) == 0)
					throw new NotSupportedException ("The OLDER search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("OLDER {0}", numeric.Value);
				break;
			case SearchTerm.Or:
				builder.Append ("OR ");
				binary = (BinarySearchQuery) query;
				BuildQuery (builder, binary.Left, args, true, ref ascii);
				builder.Append (' ');
				BuildQuery (builder, binary.Right, args, true, ref ascii);
				break;
			case SearchTerm.Recent:
				builder.Append ("RECENT");
				break;
			case SearchTerm.Seen:
				builder.Append ("SEEN");
				break;
			case SearchTerm.SentAfter:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTSINCE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.SentBefore:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTBEFORE {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.SentOn:
				date = (DateSearchQuery) query;
				builder.AppendFormat ("SENTON {0}", FormatDateTime (date.Date));
				break;
			case SearchTerm.SmallerThan:
				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("SMALLER {0}", numeric.Value);
				break;
			case SearchTerm.SubjectContains:
				text = (TextSearchQuery) query;
				builder.Append ("SUBJECT %S");
				args.Add (text.Text);
				break;
			case SearchTerm.ToContains:
				text = (TextSearchQuery) query;
				builder.Append ("TO %S");
				args.Add (text.Text);
				break;
			case SearchTerm.Uid:
				uid = (UidSearchQuery) query;
				builder.AppendFormat ("UID {0}", ImapUtils.FormatUidSet (uid.Uids));
				break;
			case SearchTerm.Younger:
				if ((Engine.Capabilities & ImapCapabilities.Within) == 0)
					throw new NotSupportedException ("The YOUNGER search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("YOUNGER {0}", numeric.Value);
				break;
			case SearchTerm.GMailMessageId:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-MSGID search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("X-GM-MSGID {0}", numeric.Value);
				break;
			case SearchTerm.GMailThreadId:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-THRID search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.AppendFormat ("X-GM-THRID {0}", numeric.Value);
				break;
			case SearchTerm.GMailLabels:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-LABELS search term is not supported by the IMAP server.");

				text = (TextSearchQuery) query;
				builder.Append ("X-GM-LABELS %S");
				args.Add (text.Text);
				break;
			case SearchTerm.GMailRaw:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-RAW search term is not supported by the IMAP server.");

				text = (TextSearchQuery) query;
				builder.Append ("X-GM-RAW %S");
				args.Add (text.Text);
				break;
			default:
				throw new ArgumentOutOfRangeException ();
			}

			if (text != null && !IsAscii (text.Text))
				ascii = false;
		}

		string BuildQueryExpression (SearchQuery query, List<string> args, out string charset)
		{
			var builder = new StringBuilder ();
			bool ascii = true;

			BuildQuery (builder, query, args, false, ref ascii);

			charset = ascii ? null : "UTF-8";

			return builder.ToString ();
		}

		static string BuildSortOrder (IList<OrderBy> orderBy)
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			for (int i = 0; i < orderBy.Count; i++) {
				if (builder.Length > 1)
					builder.Append (' ');

				if (orderBy[i].Order == SortOrder.Descending)
					builder.Append ("REVERSE ");

				switch (orderBy[i].Type) {
				case OrderByType.Arrival:     builder.Append ("ARRIVAL"); break;
				case OrderByType.Cc:          builder.Append ("CC"); break;
				case OrderByType.Date:        builder.Append ("DATE"); break;
				case OrderByType.DisplayFrom: builder.Append ("DISPLAYFROM"); break;
				case OrderByType.DisplayTo:   builder.Append ("DISPLAYTO"); break;
				case OrderByType.From:        builder.Append ("FROM"); break;
				case OrderByType.Size:        builder.Append ("SIZE"); break;
				case OrderByType.Subject:     builder.Append ("SUBJECT"); break;
				case OrderByType.To:          builder.Append ("TO"); break;
				default: throw new ArgumentOutOfRangeException ();
				}
			}
			builder.Append (')');

			return builder.ToString ();
		}

		static async Task SearchMatchesAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var uids = new UniqueIdSet (SortOrder.Ascending);
			var results = (SearchResults) ic.UserData;
			ImapToken token;
			ulong modseq;
			uint uid;

			do {
				token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				// keep reading UIDs until we get to the end of the line or until we get a "(MODSEQ ####)"
				if (token.Type == ImapTokenType.Eoln || token.Type == ImapTokenType.OpenParen)
					break;

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid) || uid == 0)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);

				uids.Add (new UniqueId (ic.Folder.UidValidity, uid));
			} while (true);

			if (token.Type == ImapTokenType.OpenParen) {
				await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				do {
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);

					var atom = (string) token.Value;

					switch (atom) {
					case "MODSEQ":
						token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

						if (token.Type != ImapTokenType.Atom || !ulong.TryParse ((string) token.Value, out modseq)) {
							Debug.WriteLine ("Expected 64-bit nz-number as the MODSEQ value, but got: {0}", token);
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
						}
						break;
					}

					token = await engine.PeekTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
				} while (token.Type != ImapTokenType.Eoln);
			}

			results.UniqueIds = uids;
		}

		static async Task ESearchMatchesAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			var results = (SearchResults) ic.UserData;
			UniqueIdSet uids = null;
			int parenDepth = 0;
			//bool uid = false;
			uint min, max;
			ulong modseq;
			string atom;
			string tag;
			int count;

			if (token.Type == ImapTokenType.OpenParen) {
				// optional search correlator
				do {
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					atom = (string) token.Value;

					if (atom == "TAG") {
						token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

						if (token.Type != ImapTokenType.Atom && token.Type != ImapTokenType.QString)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						tag = (string) token.Value;

						if (tag != ic.Tag)
							throw new ImapProtocolException ("Unexpected TAG value in untagged ESEARCH response: " + tag);
					}
				} while (true);

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			}

			if (token.Type == ImapTokenType.Atom && ((string) token.Value) == "UID") {
				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
				//uid = true;
			}

			do {
				if (token.Type == ImapTokenType.CloseParen) {
					if (parenDepth == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
					parenDepth--;
				}

				if (token.Type == ImapTokenType.Eoln) {
					// unget the eoln token
					engine.Stream.UngetToken (token);
					break;
				}

				if (token.Type == ImapTokenType.OpenParen) {
					token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
					parenDepth++;
				}

				if (token.Type != ImapTokenType.Atom)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

				atom = (string) token.Value;

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

				switch (atom) {
				case "RELEVANCY":
					if (token.Type != ImapTokenType.OpenParen)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					results.Relevancy = new List<byte> ();

					do {
						int score;

						token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						if (token.Type != ImapTokenType.Atom || !int.TryParse ((string) token.Value, out score) || score < 1 || score > 100)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						results.Relevancy.Add ((byte) score);
					} while (true);
					break;
				case "MODSEQ":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!ulong.TryParse ((string) token.Value, out modseq))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.ModSeq = modseq;
					break;
				case "COUNT":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!int.TryParse ((string) token.Value, out count))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Count = count;
					break;
				case "MIN":
					if (!uint.TryParse ((string) token.Value, out min) || min == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Min = new UniqueId (ic.Folder.UidValidity, min);
					break;
				case "MAX":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!uint.TryParse ((string) token.Value, out max) || max == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Max = new UniqueId (ic.Folder.UidValidity, max);
					break;
				case "ALL":
					if (token.Type != ImapTokenType.Atom)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					if (!UniqueIdSet.TryParse ((string) token.Value, ic.Folder.UidValidity, out uids))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Count = uids.Count;
					break;
				default:
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);
				}

				token = await engine.ReadTokenAsync (doAsync, ic.CancellationToken).ConfigureAwait (false);
			} while (true);

			results.UniqueIds = uids ?? new UniqueIdSet ();
		}

		async Task<SearchResults> SearchAsync (string query, bool doAsync, CancellationToken cancellationToken)
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));

			query = query.Trim ();

			if (query.Length == 0)
				throw new ArgumentException ("Cannot search using an empty query.", nameof (query));

			CheckState (true, false);

			var command = "UID SEARCH " + query + "\r\n";
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatchesAsync);
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatchesAsync);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Sends a <c>UID SEARCH</c> command with the specified query passed directly to the IMAP server
		/// with no interpretation by MailKit. This means that the query may contain any arguments that a
		/// <c>UID SEARCH</c> command is allowed to have according to the IMAP specifications and any
		/// extensions that are supported, including <c>RETURN</c> parameters.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="query"/> is an empty string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public virtual SearchResults Search (string query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SearchAsync (query, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Sends a <c>UID SEARCH</c> command with the specified query passed directly to the IMAP server
		/// with no interpretation by MailKit. This means that the query may contain any arguments that a
		/// <c>UID SEARCH</c> command is allowed to have according to the IMAP specifications and any
		/// extensions that are supported, including <c>RETURN</c> parameters.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="query"/> is an empty string.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public virtual Task<SearchResults> SearchAsync (string query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SearchAsync (query, true, cancellationToken);
		}

		async Task<IList<UniqueId>> SearchAsync (SearchQuery query, bool doAsync, CancellationToken cancellationToken)
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				command += "RETURN () ";

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatchesAsync);

			// Note: always register the untagged SEARCH handler because some servers will brokenly
			// respond with "* SEARCH ..." instead of "* ESEARCH ..." even when using the extended
			// search syntax.
			ic.RegisterUntaggedHandler ("SEARCH", SearchMatchesAsync);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return ((SearchResults) ic.UserData).UniqueIds;
		}

		/// <summary>
		/// Search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<UniqueId> Search (SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SearchAsync (query, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task<IList<UniqueId>> SearchAsync (SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SearchAsync (query, true, cancellationToken);
		}

		async Task<SearchResults> SearchAsync (SearchOptions options, SearchQuery query, bool doAsync, CancellationToken cancellationToken)
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.ESearch) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESEARCH extension.");

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH RETURN (";

			if (options != SearchOptions.All && options != 0) {
				if ((options & SearchOptions.All) != 0)
					command += "ALL ";
				if ((options & SearchOptions.Relevancy) != 0)
					command += "RELEVANCY ";
				if ((options & SearchOptions.Count) != 0)
					command += "COUNT ";
				if ((options & SearchOptions.Min) != 0)
					command += "MIN ";
				if ((options & SearchOptions.Max) != 0)
					command += "MAX ";
				command = command.TrimEnd ();
			}
			command += ") ";

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatchesAsync);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SEARCH", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query,
		/// returning only the specified search results.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESEARCH extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override SearchResults Search (SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SearchAsync (options, query, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously search the folder for messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query,
		/// returning only the specified search results.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESEARCH extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task<SearchResults> SearchAsync (SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SearchAsync (options, query, true, cancellationToken);
		}

		async Task<SearchResults> SortAsync (string query, bool doAsync, CancellationToken cancellationToken)
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));

			query = query.Trim ();

			if (query.Length == 0)
				throw new ArgumentException ("Cannot sort using an empty query.", nameof (query));

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			CheckState (true, false);

			var command = "UID SORT " + query + "\r\n";
			var ic = new ImapCommand (Engine, cancellationToken, this, command);
			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatchesAsync);
			ic.RegisterUntaggedHandler ("SORT", SearchMatchesAsync);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Sort messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Sends a <c>UID SORT</c> command with the specified query passed directly to the IMAP server
		/// with no interpretation by MailKit. This means that the query may contain any arguments that a
		/// <c>UID SORT</c> command is allowed to have according to the IMAP specifications and any
		/// extensions that are supported, including <c>RETURN</c> parameters.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="query"/> is an empty string.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the SORT extension.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public virtual SearchResults Sort (string query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SortAsync (query, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously sort messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Sends a <c>UID SORT</c> command with the specified query passed directly to the IMAP server
		/// with no interpretation by MailKit. This means that the query may contain any arguments that a
		/// <c>UID SORT</c> command is allowed to have according to the IMAP specifications and any
		/// extensions that are supported, including <c>RETURN</c> parameters.
		/// </remarks>
		/// <returns>An array of matching UIDs.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="query"/> is an empty string.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// The IMAP server does not support the SORT extension.
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public virtual Task<SearchResults> SortAsync (string query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SortAsync (query, true, cancellationToken);
		}

		async Task<IList<UniqueId>> SortAsync (SearchQuery query, IList<OrderBy> orderBy, bool doAsync, CancellationToken cancellationToken)
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0) {
				for (int i = 0; i < orderBy.Count; i++) {
					if (orderBy [i].Type == OrderByType.DisplayFrom || orderBy [i].Type == OrderByType.DisplayTo)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");
				}
			}

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				command += "RETURN () ";

			command += order + " " + (charset ?? "US-ASCII") + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatchesAsync);
			else
				ic.RegisterUntaggedHandler ("SORT", SearchMatchesAsync);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return ((SearchResults) ic.UserData).UniqueIds;
		}

		/// <summary>
		/// Sort messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="orderBy"/> is empty.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the SORT extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<UniqueId> Sort (SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SortAsync (query, orderBy, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously sort messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// The returned array of unique identifiers will be sorted in the preferred order and
		/// can be used with <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of matching UIDs in the specified sort order.</returns>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="orderBy"/> is empty.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the SORT extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task<IList<UniqueId>> SortAsync (SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SortAsync (query, orderBy, true, cancellationToken);
		}

		async Task<SearchResults> SortAsync (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, bool doAsync, CancellationToken cancellationToken)
		{
			var args = new List<string> ();
			string charset;

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.ESort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESORT extension.");

			if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0) {
				for (int i = 0; i < orderBy.Count; i++) {
					if (orderBy[i].Type == OrderByType.DisplayFrom || orderBy[i].Type == OrderByType.DisplayTo)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");
				}
			}

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);

			var command = "UID SORT RETURN (";
			if (options != SearchOptions.All && options != 0) {
				if ((options & SearchOptions.All) != 0)
					command += "ALL ";
				if ((options & SearchOptions.Relevancy) != 0)
					command += "RELEVANCY ";
				if ((options & SearchOptions.Count) != 0)
					command += "COUNT ";
				if ((options & SearchOptions.Min) != 0)
					command += "MIN ";
				if ((options & SearchOptions.Max) != 0)
					command += "MAX ";
				command = command.TrimEnd ();
			}
			command += ") ";

			command += order + " " + (charset ?? "US-ASCII") + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("ESEARCH", ESearchMatchesAsync);
			ic.UserData = new SearchResults ();

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("SORT", ic);

			return (SearchResults) ic.UserData;
		}

		/// <summary>
		/// Sort messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query, returning the search results in the specified sort order.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="orderBy"/> is empty.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESORT extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override SearchResults Sort (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SortAsync (options, query, orderBy, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously sort messages matching the specified query.
		/// </summary>
		/// <remarks>
		/// Searches the folder for messages matching the specified query, returning the search results in the specified sort order.
		/// </remarks>
		/// <returns>The search results.</returns>
		/// <param name="options">The search options.</param>
		/// <param name="query">The search query.</param>
		/// <param name="orderBy">The sort order.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="orderBy"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <paramref name="orderBy"/> is empty.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The IMAP server does not support the ESORT extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task<SearchResults> SortAsync (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default (CancellationToken))
		{
			return SortAsync (options, query, orderBy, true, cancellationToken);
		}

		static async Task ThreadMatchesAsync (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			ic.UserData = await ImapUtils.ParseThreadsAsync (engine, ic.Folder.UidValidity, doAsync, ic.CancellationToken).ConfigureAwait (false);
		}

		async Task<IList<MessageThread>> ThreadAsync (ThreadingAlgorithm algorithm, SearchQuery query, bool doAsync, CancellationToken cancellationToken)
		{
			var method = algorithm.ToString ().ToUpperInvariant ();
			var args = new List<string> ();
			string charset;

			if ((Engine.Capabilities & ImapCapabilities.Thread) == 0)
				throw new NotSupportedException ("The IMAP server does not support the THREAD extension.");

			if (!Engine.ThreadingAlgorithms.Contains (algorithm))
				throw new ArgumentOutOfRangeException (nameof (algorithm), "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + (charset ?? "US-ASCII") + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ThreadMatchesAsync);

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("THREAD", ic);

			var threads = (IList<MessageThread>) ic.UserData;

			if (threads == null)
				return new MessageThread[0];

			return threads;
		}

		/// <summary>
		/// Thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the THREAD extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<MessageThread> Thread (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ThreadAsync (algorithm, query, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="query"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the THREAD extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task<IList<MessageThread>> ThreadAsync (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ThreadAsync (algorithm, query, true, cancellationToken);
		}

		async Task<IList<MessageThread>> ThreadAsync (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, bool doAsync, CancellationToken cancellationToken)
		{
			var method = algorithm.ToString ().ToUpperInvariant ();
			var set = ImapUtils.FormatUidSet (uids);
			var args = new List<string> ();
			string charset;

			if ((Engine.Capabilities & ImapCapabilities.Thread) == 0)
				throw new NotSupportedException ("The IMAP server does not support the THREAD extension.");

			if (!Engine.ThreadingAlgorithms.Contains (algorithm))
				throw new ArgumentOutOfRangeException (nameof (algorithm), "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + (charset ?? "US-ASCII") + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ThreadMatchesAsync);

			Engine.QueueCommand (ic);

			await Engine.RunAsync (ic, doAsync).ConfigureAwait (false);

			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok)
				throw ImapCommandException.Create ("THREAD", ic);

			var threads = (IList<MessageThread>) ic.UserData;

			if (threads == null)
				return new MessageThread[0];

			return threads;
		}

		/// <summary>
		/// Thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the THREAD extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override IList<MessageThread> Thread (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ThreadAsync (uids, algorithm, query, false, cancellationToken).GetAwaiter ().GetResult ();
		}

		/// <summary>
		/// Asynchronously thread the messages in the folder that match the search query using the specified threading algorithm.
		/// </summary>
		/// <remarks>
		/// The <see cref="MessageThread.UniqueId"/> can be used with methods such as
		/// <see cref="IMailFolder.GetMessage(UniqueId,CancellationToken,ITransferProgress)"/>.
		/// </remarks>
		/// <returns>An array of message threads.</returns>
		/// <param name="uids">The subset of UIDs</param>
		/// <param name="algorithm">The threading algorithm to use.</param>
		/// <param name="query">The search query.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// <paramref name="algorithm"/> is not supported.
		/// </exception>
		/// <exception cref="System.ArgumentNullException">
		/// <para><paramref name="uids"/> is <c>null</c>.</para>
		/// <para>-or-</para>
		/// <para><paramref name="query"/> is <c>null</c>.</para>
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// <para><paramref name="uids"/> is empty.</para>
		/// <para>-or-</para>
		/// <para>One or more of the <paramref name="uids"/> is invalid.</para>
		/// </exception>
		/// <exception cref="System.NotSupportedException">
		/// <para>One or more search terms in the <paramref name="query"/> are not supported by the IMAP server.</para>
		/// <para>-or-</para>
		/// <para>The server does not support the THREAD extension.</para>
		/// </exception>
		/// <exception cref="System.ObjectDisposedException">
		/// The <see cref="ImapClient"/> has been disposed.
		/// </exception>
		/// <exception cref="ServiceNotConnectedException">
		/// The <see cref="ImapClient"/> is not connected.
		/// </exception>
		/// <exception cref="ServiceNotAuthenticatedException">
		/// The <see cref="ImapClient"/> is not authenticated.
		/// </exception>
		/// <exception cref="FolderNotOpenException">
		/// The <see cref="ImapFolder"/> is not currently open.
		/// </exception>
		/// <exception cref="System.OperationCanceledException">
		/// The operation was canceled via the cancellation token.
		/// </exception>
		/// <exception cref="System.IO.IOException">
		/// An I/O error occurred.
		/// </exception>
		/// <exception cref="ImapProtocolException">
		/// The server's response contained unexpected tokens.
		/// </exception>
		/// <exception cref="ImapCommandException">
		/// The server replied with a NO or BAD response.
		/// </exception>
		public override Task<IList<MessageThread>> ThreadAsync (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default (CancellationToken))
		{
			return ThreadAsync (uids, algorithm, query, true, cancellationToken);
		}
	}
}
