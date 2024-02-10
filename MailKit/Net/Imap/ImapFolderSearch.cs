//
// ImapFolderSearch.cs
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

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

		bool IsBadCharset (ImapCommand ic, string charset)
		{
			// Note: if `charset` is null, then the charset is actually US-ASCII...
			return ic.Response == ImapCommandResponse.No &&
				ic.RespCodes.Any (rc => rc.Type == ImapResponseCodeType.BadCharset) &&
				charset != null && !Engine.SupportedCharsets.Contains (charset);
		}

		void AddTextArgument (StringBuilder builder, List<object> args, string text, ref string charset)
		{
			if (IsAscii (text)) {
				builder.Append ("%S");
				args.Add (text);
				return;
			}

			if (Engine.SupportedCharsets.Contains ("UTF-8")) {
				builder.Append ("%S");
				charset = "UTF-8";
				args.Add (text);
				return;
			}

			// force the text into US-ASCII...
			var buffer = new byte[text.Length];
			for (int i = 0; i < text.Length; i++)
				buffer[i] = (byte) text[i];

			builder.Append ("%L");
			args.Add (buffer);
		}

		void BuildQuery (StringBuilder builder, SearchQuery query, List<object> args, bool parens, ref string charset)
		{
			AnnotationSearchQuery annotation;
			NumericSearchQuery numeric;
			FilterSearchQuery filter;
			HeaderSearchQuery header;
			BinarySearchQuery binary;
			UnarySearchQuery unary;
			DateSearchQuery date;
			TextSearchQuery text;
			UidSearchQuery uid;

			switch (query.Term) {
			case SearchTerm.All:
				builder.Append ("ALL");
				break;
			case SearchTerm.And:
				binary = (BinarySearchQuery) query;
				if (parens)
					builder.Append ('(');
				BuildQuery (builder, binary.Left, args, false, ref charset);
				builder.Append (' ');
				BuildQuery (builder, binary.Right, args, false, ref charset);
				if (parens)
					builder.Append (')');
				break;
			case SearchTerm.Annotation:
				if ((Engine.Capabilities & ImapCapabilities.Annotate) == 0)
					throw new NotSupportedException ("The ANNOTATION search term is not supported by the IMAP server.");

				annotation = (AnnotationSearchQuery) query;
				builder.Append ("ANNOTATION ");
				builder.Append (annotation.Entry);
				builder.Append (' ');
				builder.Append (annotation.Attribute);
				builder.Append (" %S");
				args.Add (annotation.Value);
				break;
			case SearchTerm.Answered:
				builder.Append ("ANSWERED");
				break;
			case SearchTerm.BccContains:
				text = (TextSearchQuery) query;
				builder.Append ("BCC ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.BodyContains:
				text = (TextSearchQuery) query;
				builder.Append ("BODY ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.CcContains:
				text = (TextSearchQuery) query;
				builder.Append ("CC ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.Deleted:
				builder.Append ("DELETED");
				break;
			case SearchTerm.DeliveredAfter:
				date = (DateSearchQuery) query;
				builder.Append ("SINCE ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.DeliveredBefore:
				date = (DateSearchQuery) query;
				builder.Append ("BEFORE ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.DeliveredOn:
				date = (DateSearchQuery) query;
				builder.Append ("ON ");
				builder.Append (FormatDateTime (date.Date));
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
				builder.Append ("FROM ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.Fuzzy:
				if ((Engine.Capabilities & ImapCapabilities.FuzzySearch) == 0)
					throw new NotSupportedException ("The FUZZY search term is not supported by the IMAP server.");

				builder.Append ("FUZZY ");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args, true, ref charset);
				break;
			case SearchTerm.HeaderContains:
				header = (HeaderSearchQuery) query;
				builder.Append ("HEADER ");
				builder.Append (header.Field);
				builder.Append (' ');
				AddTextArgument (builder, args, header.Value, ref charset);
				break;
			case SearchTerm.Keyword:
				text = (TextSearchQuery) query;
				builder.Append ("KEYWORD ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.LargerThan:
				numeric = (NumericSearchQuery) query;
				builder.Append ("LARGER ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.MessageContains:
				text = (TextSearchQuery) query;
				builder.Append ("TEXT ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.ModSeq:
				numeric = (NumericSearchQuery) query;
				builder.Append ("MODSEQ ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.New:
				builder.Append ("NEW");
				break;
			case SearchTerm.Not:
				builder.Append ("NOT ");
				unary = (UnarySearchQuery) query;
				BuildQuery (builder, unary.Operand, args, true, ref charset);
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
				builder.Append ("UNKEYWORD ");
				AddTextArgument (builder, args, text.Text, ref charset);
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
				builder.Append ("OLDER ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.Or:
				builder.Append ("OR ");
				binary = (BinarySearchQuery) query;
				BuildQuery (builder, binary.Left, args, true, ref charset);
				builder.Append (' ');
				BuildQuery (builder, binary.Right, args, true, ref charset);
				break;
			case SearchTerm.Recent:
				builder.Append ("RECENT");
				break;
			case SearchTerm.SaveDateSupported:
				if ((Engine.Capabilities & ImapCapabilities.SaveDate) == 0)
					throw new NotSupportedException ("The SAVEDATESUPPORTED search term is not supported by the IMAP server.");

				builder.Append ("SAVEDATESUPPORTED");
				break;
			case SearchTerm.SavedBefore:
				if ((Engine.Capabilities & ImapCapabilities.SaveDate) == 0)
					throw new NotSupportedException ("The SAVEDBEFORE search term is not supported by the IMAP server.");

				date = (DateSearchQuery) query;
				builder.Append ("SAVEDBEFORE ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.SavedOn:
				if ((Engine.Capabilities & ImapCapabilities.SaveDate) == 0)
					throw new NotSupportedException ("The SAVEDON search term is not supported by the IMAP server.");

				date = (DateSearchQuery) query;
				builder.Append ("SAVEDON ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.SavedSince:
				if ((Engine.Capabilities & ImapCapabilities.SaveDate) == 0)
					throw new NotSupportedException ("The SAVEDSINCE search term is not supported by the IMAP server.");

				date = (DateSearchQuery) query;
				builder.Append ("SAVEDSINCE ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.Seen:
				builder.Append ("SEEN");
				break;
			case SearchTerm.SentBefore:
				date = (DateSearchQuery) query;
				builder.Append ("SENTBEFORE ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.SentOn:
				date = (DateSearchQuery) query;
				builder.Append ("SENTON ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.SentSince:
				date = (DateSearchQuery) query;
				builder.Append ("SENTSINCE ");
				builder.Append (FormatDateTime (date.Date));
				break;
			case SearchTerm.SmallerThan:
				numeric = (NumericSearchQuery) query;
				builder.Append ("SMALLER ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.SubjectContains:
				text = (TextSearchQuery) query;
				builder.Append ("SUBJECT ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.ToContains:
				text = (TextSearchQuery) query;
				builder.Append ("TO ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.Uid:
				uid = (UidSearchQuery) query;
				builder.Append ("UID ");
				builder.Append (UniqueIdSet.ToString (uid.Uids));
				break;
			case SearchTerm.Younger:
				if ((Engine.Capabilities & ImapCapabilities.Within) == 0)
					throw new NotSupportedException ("The YOUNGER search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.Append ("YOUNGER ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.GMailMessageId:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-MSGID search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.Append ("X-GM-MSGID ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.GMailThreadId:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-THRID search term is not supported by the IMAP server.");

				numeric = (NumericSearchQuery) query;
				builder.Append ("X-GM-THRID ");
				builder.Append (numeric.Value.ToString (CultureInfo.InvariantCulture));
				break;
			case SearchTerm.GMailLabels:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-LABELS search term is not supported by the IMAP server.");

				text = (TextSearchQuery) query;
				builder.Append ("X-GM-LABELS ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			case SearchTerm.GMailRaw:
				if ((Engine.Capabilities & ImapCapabilities.GMailExt1) == 0)
					throw new NotSupportedException ("The X-GM-RAW search term is not supported by the IMAP server.");

				text = (TextSearchQuery) query;
				builder.Append ("X-GM-RAW ");
				AddTextArgument (builder, args, text.Text, ref charset);
				break;
			}
		}

		string BuildQueryExpression (SearchQuery query, List<object> args, out string charset)
		{
			var builder = new StringBuilder ();

			charset = null;

			BuildQuery (builder, query, args, false, ref charset);

			return builder.ToString ();
		}

		string BuildSortOrder (IList<OrderBy> orderBy)
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			for (int i = 0; i < orderBy.Count; i++) {
				if (builder.Length > 1)
					builder.Append (' ');

				if (orderBy[i].Order == SortOrder.Descending)
					builder.Append ("REVERSE ");

				switch (orderBy[i].Type) {
				case OrderByType.Annotation:
					if ((Engine.Capabilities & ImapCapabilities.Annotate) == 0)
						throw new NotSupportedException ("The ANNOTATION search term is not supported by the IMAP server.");

					var annotation = (OrderByAnnotation) orderBy[i];
					builder.Append ("ANNOTATION ");
					builder.Append (annotation.Entry);
					builder.Append (' ');
					builder.Append (annotation.Attribute);
					break;
				case OrderByType.Arrival:     builder.Append ("ARRIVAL"); break;
				case OrderByType.Cc:          builder.Append ("CC"); break;
				case OrderByType.Date:        builder.Append ("DATE"); break;
				case OrderByType.DisplayFrom:
					if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");

					builder.Append ("DISPLAYFROM");
					break;
				case OrderByType.DisplayTo:
					if ((Engine.Capabilities & ImapCapabilities.SortDisplay) == 0)
						throw new NotSupportedException ("The IMAP server does not support the SORT=DISPLAY extension.");

					builder.Append ("DISPLAYTO");
					break;
				case OrderByType.From:        builder.Append ("FROM"); break;
				case OrderByType.Size:        builder.Append ("SIZE"); break;
				case OrderByType.Subject:     builder.Append ("SUBJECT"); break;
				case OrderByType.To:          builder.Append ("TO"); break;
				}
			}
			builder.Append (')');

			return builder.ToString ();
		}

		static void ParseESearchResults (ImapEngine engine, ImapCommand ic, SearchResults results)
		{
			var token = engine.ReadToken (ic.CancellationToken);
			UniqueId? minValue = null, maxValue = null;
			bool hasCount = false;
			int parenDepth = 0;
			//bool uid = false;
			string atom, tag;

			if (token.Type == ImapTokenType.OpenParen) {
				// optional search correlator
				do {
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					atom = (string) token.Value;

					if (atom == "TAG") {
						token = engine.ReadToken (ic.CancellationToken);

						ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						tag = (string) token.Value;

						if (tag != ic.Tag)
							throw new ImapProtocolException ("Unexpected TAG value in untagged ESEARCH response: " + tag);
					}
				} while (true);

				token = engine.ReadToken (ic.CancellationToken);
			}

			if (token.Type == ImapTokenType.Atom && ((string) token.Value) == "UID") {
				token = engine.ReadToken (ic.CancellationToken);
				//uid = true;
			}

			do {
				if (token.Type == ImapTokenType.CloseParen) {
					if (parenDepth == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					token = engine.ReadToken (ic.CancellationToken);
					parenDepth--;
				}

				if (token.Type == ImapTokenType.Eoln) {
					// unget the eoln token
					engine.Stream.UngetToken (token);
					break;
				}

				if (token.Type == ImapTokenType.OpenParen) {
					token = engine.ReadToken (ic.CancellationToken);
					parenDepth++;
				}

				ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

				atom = (string) token.Value;

				token = engine.ReadToken (ic.CancellationToken);

				if (atom.Equals ("RELEVANCY", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					results.Relevancy = new List<byte> ();

					do {
						token = engine.ReadToken (ic.CancellationToken);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						var score = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						if (score > 100)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						results.Relevancy.Add ((byte) score);
					} while (true);
				} else if (atom.Equals ("MODSEQ", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					results.ModSeq = ImapEngine.ParseNumber64 (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
				} else if (atom.Equals ("COUNT", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var count = ImapEngine.ParseNumber (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Count = (int) count;
					hasCount = true;
				} else if (atom.Equals ("MIN", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var min = ImapEngine.ParseNumber (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Min = new UniqueId (ic.Folder.UidValidity, min);
				} else if (atom.Equals ("MAX", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var max = ImapEngine.ParseNumber (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Max = new UniqueId (ic.Folder.UidValidity, max);
				} else if (atom.Equals ("ALL", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var uids = ImapEngine.ParseUidSet (token, ic.Folder.UidValidity, out minValue, out maxValue, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					if (!hasCount)
						results.Count = uids.Count;

					results.UniqueIds = uids;
				} else {
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);
				}

				token = engine.ReadToken (ic.CancellationToken);
			} while (true);

			if (!results.Min.HasValue)
				results.Min = minValue;

			if (!results.Max.HasValue)
				results.Max = maxValue;
		}

		static async Task ParseESearchResultsAsync (ImapEngine engine, ImapCommand ic, SearchResults results)
		{
			var token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
			UniqueId? minValue = null, maxValue = null;
			bool hasCount = false;
			int parenDepth = 0;
			//bool uid = false;
			string atom, tag;

			if (token.Type == ImapTokenType.OpenParen) {
				// optional search correlator
				do {
					token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					atom = (string) token.Value;

					if (atom == "TAG") {
						token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

						ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapTokenType.QString, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						tag = (string) token.Value;

						if (tag != ic.Tag)
							throw new ImapProtocolException ("Unexpected TAG value in untagged ESEARCH response: " + tag);
					}
				} while (true);

				token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
			}

			if (token.Type == ImapTokenType.Atom && ((string) token.Value) == "UID") {
				token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
				//uid = true;
			}

			do {
				if (token.Type == ImapTokenType.CloseParen) {
					if (parenDepth == 0)
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
					parenDepth--;
				}

				if (token.Type == ImapTokenType.Eoln) {
					// unget the eoln token
					engine.Stream.UngetToken (token);
					break;
				}

				if (token.Type == ImapTokenType.OpenParen) {
					token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
					parenDepth++;
				}

				ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

				atom = (string) token.Value;

				token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

				if (atom.Equals ("RELEVANCY", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.OpenParen, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					results.Relevancy = new List<byte> ();

					do {
						token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

						if (token.Type == ImapTokenType.CloseParen)
							break;

						var score = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						if (score > 100)
							throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

						results.Relevancy.Add ((byte) score);
					} while (true);
				} else if (atom.Equals ("MODSEQ", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					results.ModSeq = ImapEngine.ParseNumber64 (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
				} else if (atom.Equals ("COUNT", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var count = ImapEngine.ParseNumber (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Count = (int) count;
					hasCount = true;
				} else if (atom.Equals ("MIN", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var min = ImapEngine.ParseNumber (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Min = new UniqueId (ic.Folder.UidValidity, min);
				} else if (atom.Equals ("MAX", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var max = ImapEngine.ParseNumber (token, true, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					results.Max = new UniqueId (ic.Folder.UidValidity, max);
				} else if (atom.Equals ("ALL", StringComparison.OrdinalIgnoreCase)) {
					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);

					var uids = ImapEngine.ParseUidSet (token, ic.Folder.UidValidity, out minValue, out maxValue, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);

					if (!hasCount)
						results.Count = uids.Count;

					results.UniqueIds = uids;
				} else {
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ESEARCH", token);
				}

				token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);
			} while (true);

			if (!results.Min.HasValue)
				results.Min = minValue;

			if (!results.Max.HasValue)
				results.Max = maxValue;
		}

		static Task UntaggedESearchHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var results = (SearchResults) ic.UserData;

			if (doAsync)
				return ParseESearchResultsAsync (engine, ic, results);

			ParseESearchResults (engine, ic, results);

			return Task.CompletedTask;
		}

		static void ParseSearchResults (ImapEngine engine, ImapCommand ic, SearchResults results)
		{
			var uids = results.UniqueIds;
			uint min = uint.MaxValue;
			uint uid, max = 0;
			ImapToken token;

			do {
				token = engine.PeekToken (ic.CancellationToken);

				// keep reading UIDs until we get to the end of the line or until we get a "(MODSEQ ####)"
				if (token.Type == ImapTokenType.Eoln || token.Type == ImapTokenType.OpenParen)
					break;

				token = engine.ReadToken (ic.CancellationToken);

				uid = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);
				uids.Add (new UniqueId (ic.Folder.UidValidity, uid));
				min = Math.Min (min, uid);
				max = Math.Max (max, uid);
			} while (true);

			if (token.Type == ImapTokenType.OpenParen) {
				engine.ReadToken (ic.CancellationToken);

				do {
					token = engine.ReadToken (ic.CancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);

					var atom = (string) token.Value;

					if (atom.Equals ("MODSEQ", StringComparison.OrdinalIgnoreCase)) {
						token = engine.ReadToken (ic.CancellationToken);

						results.ModSeq = ImapEngine.ParseNumber64 (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					}

					token = engine.PeekToken (ic.CancellationToken);
				} while (token.Type != ImapTokenType.Eoln);
			}

			results.UniqueIds = uids;
			results.Count = uids.Count;
			if (uids.Count > 0) {
				results.Min = new UniqueId (ic.Folder.UidValidity, min);
				results.Max = new UniqueId (ic.Folder.UidValidity, max);
			}
		}

		static async Task ParseSearchResultsAsync (ImapEngine engine, ImapCommand ic, SearchResults results)
		{
			var uids = results.UniqueIds;
			uint min = uint.MaxValue;
			uint uid, max = 0;
			ImapToken token;

			do {
				token = await engine.PeekTokenAsync (ic.CancellationToken).ConfigureAwait (false);

				// keep reading UIDs until we get to the end of the line or until we get a "(MODSEQ ####)"
				if (token.Type == ImapTokenType.Eoln || token.Type == ImapTokenType.OpenParen)
					break;

				token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

				uid = ImapEngine.ParseNumber (token, true, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);
				uids.Add (new UniqueId (ic.Folder.UidValidity, uid));
				min = Math.Min (min, uid);
				max = Math.Max (max, uid);
			} while (true);

			if (token.Type == ImapTokenType.OpenParen) {
				await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

				do {
					token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					ImapEngine.AssertToken (token, ImapTokenType.Atom, ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "SEARCH", token);

					var atom = (string) token.Value;

					if (atom.Equals ("MODSEQ", StringComparison.OrdinalIgnoreCase)) {
						token = await engine.ReadTokenAsync (ic.CancellationToken).ConfigureAwait (false);

						results.ModSeq = ImapEngine.ParseNumber64 (token, false, ImapEngine.GenericItemSyntaxErrorFormat, atom, token);
					}

					token = await engine.PeekTokenAsync (ic.CancellationToken).ConfigureAwait (false);
				} while (token.Type != ImapTokenType.Eoln);
			}

			results.UniqueIds = uids;
			results.Count = uids.Count;
			if (uids.Count > 0) {
				results.Min = new UniqueId (ic.Folder.UidValidity, min);
				results.Max = new UniqueId (ic.Folder.UidValidity, max);
			}
		}

		static Task UntaggedSearchHandler (ImapEngine engine, ImapCommand ic, int index, bool doAsync)
		{
			var results = (SearchResults) ic.UserData;

			if (doAsync)
				return ParseSearchResultsAsync (engine, ic, results);

			ParseSearchResults (engine, ic, results);

			return Task.CompletedTask;
		}

		ImapCommand QueueSearchCommand (string query, CancellationToken cancellationToken)
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
				ic.RegisterUntaggedHandler ("ESEARCH", UntaggedESearchHandler);

			// Note: always register the untagged SEARCH handler because some servers will brokenly
			// respond with "* SEARCH ..." instead of "* ESEARCH ..." even when using the extended
			// search syntax.
			ic.RegisterUntaggedHandler ("SEARCH", UntaggedSearchHandler);
			ic.UserData = new SearchResults (UidValidity, SortOrder.Ascending);

			Engine.QueueCommand (ic);

			return ic;
		}

		SearchResults ProcessSearchResponse (ImapCommand ic)
		{
			ProcessResponseCodes (ic, null);

			ic.ThrowIfNotOk ("SEARCH");

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
		public virtual SearchResults Search (string query, CancellationToken cancellationToken = default)
		{
			var ic = QueueSearchCommand (query, cancellationToken);

			Engine.Run (ic);

			return ProcessSearchResponse (ic);
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
		public virtual async Task<SearchResults> SearchAsync (string query, CancellationToken cancellationToken = default)
		{
			var ic = QueueSearchCommand (query, cancellationToken);

			await Engine.RunAsync (ic).ConfigureAwait (false);

			return ProcessSearchResponse (ic);
		}

		ImapCommand QueueSearchCommand (SearchOptions options, SearchQuery query, CancellationToken cancellationToken, out string charset)
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			if (options != SearchOptions.None && (Engine.Capabilities & ImapCapabilities.ESearch) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESEARCH extension.");

			var args = new List<object> ();
			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID SEARCH ";

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0) {
				command += "RETURN (";

				if (options != SearchOptions.All && options != SearchOptions.None) {
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
				} else {
					command += "ALL";
				}

				command += ") ";
			}

			if (charset != null && args.Count > 0 && !Engine.UTF8Enabled)
				command += "CHARSET " + charset + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ()) {
				UserData = new SearchResults (UidValidity, SortOrder.Ascending)
			};

			if ((Engine.Capabilities & ImapCapabilities.ESearch) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", UntaggedESearchHandler);

			// Note: always register the untagged SEARCH handler because some servers will brokenly
			// respond with "* SEARCH ..." instead of "* ESEARCH ..." even when using the extended
			// search syntax.
			ic.RegisterUntaggedHandler ("SEARCH", UntaggedSearchHandler);

			Engine.QueueCommand (ic);

			return ic;
		}

		bool TryProcessSearchResponse (ImapCommand ic, string charset, bool retry, out SearchResults results)
		{
			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				if (retry && IsBadCharset (ic, charset)) {
					results = null;
					return false;
				}

				throw ImapCommandException.Create ("SEARCH", ic);
			}

			results = (SearchResults) ic.UserData;

			return true;
		}

		SearchResults Search (SearchOptions options, SearchQuery query, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueSearchCommand (options, query, cancellationToken, out string charset);

			Engine.Run (ic);

			if (TryProcessSearchResponse (ic, charset, retry, out var results))
				return results;

			return Search (options, query, false, cancellationToken);
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
		public override SearchResults Search (SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default)
		{
			return Search (options, query, true, cancellationToken);
		}

		async Task<SearchResults> SearchAsync (SearchOptions options, SearchQuery query, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueSearchCommand (options, query, cancellationToken, out string charset);

			await Engine.RunAsync (ic).ConfigureAwait (false);

			if (TryProcessSearchResponse (ic, charset, retry, out var results))
				return results;

			return await SearchAsync (options, query, false, cancellationToken).ConfigureAwait (false);
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
		public override Task<SearchResults> SearchAsync (SearchOptions options, SearchQuery query, CancellationToken cancellationToken = default)
		{
			return SearchAsync (options, query, true, cancellationToken);
		}

		ImapCommand QueueSortCommand (string query, CancellationToken cancellationToken)
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
				ic.RegisterUntaggedHandler ("ESEARCH", UntaggedESearchHandler);
			ic.RegisterUntaggedHandler ("SORT", UntaggedSearchHandler);
			ic.UserData = new SearchResults (UidValidity);

			Engine.QueueCommand (ic);

			return ic;
		}

		SearchResults ProcessSortResponse (ImapCommand ic)
		{
			ProcessResponseCodes (ic, null);

			ic.ThrowIfNotOk ("SORT");

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
		public virtual SearchResults Sort (string query, CancellationToken cancellationToken = default)
		{
			var ic = QueueSortCommand (query, cancellationToken);

			Engine.Run (ic);

			return ProcessSortResponse (ic);
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
		public virtual async Task<SearchResults> SortAsync (string query, CancellationToken cancellationToken = default)
		{
			var ic = QueueSortCommand (query, cancellationToken);

			await Engine.RunAsync (ic).ConfigureAwait (false);

			return ProcessSortResponse (ic);
		}

		ImapCommand QueueSortCommand (SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken, out string charset)
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if ((Engine.Capabilities & ImapCapabilities.Sort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the SORT extension.");

			var args = new List<object> ();
			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				command += "RETURN (ALL) ";

			command += order + " " + (charset ?? "US-ASCII") + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ()) {
				UserData = new SearchResults (UidValidity)
			};

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", UntaggedESearchHandler);
			else
				ic.RegisterUntaggedHandler ("SORT", UntaggedSearchHandler);

			Engine.QueueCommand (ic);

			return ic;
		}

		bool TryProcessSortResponse (ImapCommand ic, string charset, bool retry, out IList<UniqueId> results)
		{
			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				if (retry && IsBadCharset (ic, charset)) {
					results = null;
					return false;
				}

				throw ImapCommandException.Create ("SORT", ic);
			}

			results = ((SearchResults) ic.UserData).UniqueIds;

			return true;
		}

		IList<UniqueId> Sort (SearchQuery query, IList<OrderBy> orderBy, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueSortCommand (query, orderBy, cancellationToken, out string charset);

			Engine.Run (ic);

			if (TryProcessSortResponse (ic, charset, retry, out IList<UniqueId> results))
				return results;

			return Sort (query, orderBy, false, cancellationToken);
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
		public override IList<UniqueId> Sort (SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
		{
			return Sort (query, orderBy, true, cancellationToken);
		}

		async Task<IList<UniqueId>> SortAsync (SearchQuery query, IList<OrderBy> orderBy, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueSortCommand (query, orderBy, cancellationToken, out string charset);

			await Engine.RunAsync (ic).ConfigureAwait (false);

			if (TryProcessSortResponse (ic, charset, retry, out IList<UniqueId> results))
				return results;

			return await SortAsync (query, orderBy, false, cancellationToken).ConfigureAwait (false);
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
		public override Task<IList<UniqueId>> SortAsync (SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
		{
			return SortAsync (query, orderBy, true, cancellationToken);
		}

		ImapCommand QueueSortCommand (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken, out string charset)
		{
			if (query == null)
				throw new ArgumentNullException (nameof (query));

			if (orderBy == null)
				throw new ArgumentNullException (nameof (orderBy));

			if (orderBy.Count == 0)
				throw new ArgumentException ("No sort order provided.", nameof (orderBy));

			CheckState (true, false);

			if (options != SearchOptions.None && (Engine.Capabilities & ImapCapabilities.ESort) == 0)
				throw new NotSupportedException ("The IMAP server does not support the ESORT extension.");

			var args = new List<object> ();
			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var order = BuildSortOrder (orderBy);
			var command = "UID SORT ";

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0) {
				command += "RETURN (";

				if (options != SearchOptions.All && options != SearchOptions.None) {
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
				} else {
					command += "ALL";
				}

				command += ") ";
			}

			command += order + " " + (charset ?? "US-ASCII") + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ()) {
				UserData = new SearchResults (UidValidity)
			};

			if ((Engine.Capabilities & ImapCapabilities.ESort) != 0)
				ic.RegisterUntaggedHandler ("ESEARCH", UntaggedESearchHandler);
			else
				ic.RegisterUntaggedHandler ("SORT", UntaggedSearchHandler);

			Engine.QueueCommand (ic);

			return ic;
		}

		bool TryProcessSortResponse (ImapCommand ic, string charset, bool retry, out SearchResults results)
		{
			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				if (retry && IsBadCharset (ic, charset)) {
					results = null;
					return false;
				}

				throw ImapCommandException.Create ("SORT", ic);
			}

			results = (SearchResults) ic.UserData;

			return true;
		}

		SearchResults Sort (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueSortCommand (options, query, orderBy, cancellationToken, out string charset);

			Engine.Run (ic);

			if (TryProcessSortResponse (ic, charset, retry, out SearchResults results))
				return results;

			return Sort (options, query, orderBy, false, cancellationToken);
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
		public override SearchResults Sort (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
		{
			return Sort (options, query, orderBy, true, cancellationToken);
		}

		async Task<SearchResults> SortAsync (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueSortCommand (options, query, orderBy, cancellationToken, out string charset);

			await Engine.RunAsync (ic).ConfigureAwait (false);

			if (TryProcessSortResponse (ic, charset, retry, out SearchResults results))
				return results;

			return await SortAsync (options, query, orderBy, false, cancellationToken).ConfigureAwait (false);
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
		public override Task<SearchResults> SortAsync (SearchOptions options, SearchQuery query, IList<OrderBy> orderBy, CancellationToken cancellationToken = default)
		{
			return SortAsync (options, query, orderBy, true, cancellationToken);
		}

		ImapCommand QueueThreadCommand (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken, out string charset)
		{
			if ((Engine.Capabilities & ImapCapabilities.Thread) == 0)
				throw new NotSupportedException ("The IMAP server does not support the THREAD extension.");

			if (!Engine.ThreadingAlgorithms.Contains (algorithm))
				throw new ArgumentOutOfRangeException (nameof (algorithm), "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			var method = algorithm.ToString ().ToUpperInvariant ();
			var args = new List<object> ();
			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + (charset ?? "US-ASCII") + " ";

			command += expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ImapUtils.UntaggedThreadHandler);

			Engine.QueueCommand (ic);

			return ic;
		}

		bool TryProcessThreadResponse (ImapCommand ic, string charset, bool retry, out IList<MessageThread> threads)
		{
			ProcessResponseCodes (ic, null);

			if (ic.Response != ImapCommandResponse.Ok) {
				if (retry && IsBadCharset (ic, charset)) {
					threads = null;
					return false;
				}

				throw ImapCommandException.Create ("THREAD", ic);
			}

			threads = (IList<MessageThread>) ic.UserData ?? Array.Empty<MessageThread> ();

			return true;
		}

		IList<MessageThread> Thread (ThreadingAlgorithm algorithm, SearchQuery query, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueThreadCommand (algorithm, query, cancellationToken, out string charset);

			Engine.Run (ic);

			if (TryProcessThreadResponse (ic, charset, retry, out IList<MessageThread> threads))
				return threads;

			return Thread (algorithm, query, false, cancellationToken);
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
		public override IList<MessageThread> Thread (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
		{
			return Thread (algorithm, query, true, cancellationToken);
		}

		async Task<IList<MessageThread>> ThreadAsync (ThreadingAlgorithm algorithm, SearchQuery query, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueThreadCommand (algorithm, query, cancellationToken, out string charset);

			await Engine.RunAsync (ic).ConfigureAwait (false);

			if (TryProcessThreadResponse (ic, charset, retry, out IList<MessageThread> threads))
				return threads;

			return await ThreadAsync (algorithm, query, false, cancellationToken).ConfigureAwait (false);
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
		public override Task<IList<MessageThread>> ThreadAsync (ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
		{
			return ThreadAsync (algorithm, query, true, cancellationToken);
		}

		ImapCommand QueueThreadCommand (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken, out string charset)
		{
			if (uids == null)
				throw new ArgumentNullException (nameof (uids));

			if ((Engine.Capabilities & ImapCapabilities.Thread) == 0)
				throw new NotSupportedException ("The IMAP server does not support the THREAD extension.");

			if (!Engine.ThreadingAlgorithms.Contains (algorithm))
				throw new ArgumentOutOfRangeException (nameof (algorithm), "The specified threading algorithm is not supported.");

			if (query == null)
				throw new ArgumentNullException (nameof (query));

			CheckState (true, false);

			if (uids.Count == 0) {
				charset = null;
				return null;
			}

			var method = algorithm.ToString ().ToUpperInvariant ();
			var set = UniqueIdSet.ToString (uids);
			var args = new List<object> ();
			var optimized = query.Optimize (new ImapSearchQueryOptimizer ());
			var expr = BuildQueryExpression (optimized, args, out charset);
			var command = "UID THREAD " + method + " " + (charset ?? "US-ASCII") + " ";

			command += "UID " + set + " " + expr + "\r\n";

			var ic = new ImapCommand (Engine, cancellationToken, this, command, args.ToArray ());
			ic.RegisterUntaggedHandler ("THREAD", ImapUtils.UntaggedThreadHandler);

			Engine.QueueCommand (ic);

			return ic;
		}

		IList<MessageThread> Thread (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueThreadCommand (uids, algorithm, query, cancellationToken, out string charset);

			if (ic == null)
				return Array.Empty<MessageThread> ();

			Engine.Run (ic);

			if (TryProcessThreadResponse (ic, charset, retry, out IList<MessageThread> threads))
				return threads;

			return Thread (uids, algorithm, query, false, cancellationToken);
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
		public override IList<MessageThread> Thread (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
		{
			return Thread (uids, algorithm, query, true, cancellationToken);
		}

		async Task<IList<MessageThread>> ThreadAsync (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, bool retry, CancellationToken cancellationToken)
		{
			var ic = QueueThreadCommand (uids, algorithm, query, cancellationToken, out string charset);

			if (ic == null)
				return Array.Empty<MessageThread> ();

			await Engine.RunAsync (ic).ConfigureAwait (false);

			if (TryProcessThreadResponse (ic, charset, retry, out IList<MessageThread> threads))
				return threads;

			return await ThreadAsync (uids, algorithm, query, false, cancellationToken).ConfigureAwait (false);
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
		public override Task<IList<MessageThread>> ThreadAsync (IList<UniqueId> uids, ThreadingAlgorithm algorithm, SearchQuery query, CancellationToken cancellationToken = default)
		{
			return ThreadAsync (uids, algorithm, query, true, cancellationToken);
		}
	}
}
