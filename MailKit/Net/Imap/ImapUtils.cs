//
// ImapUtils.cs
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

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using MimeKit;
using MimeKit.Utils;

#if NETFX_CORE
using Encoding = Portable.Text.Encoding;
#endif

namespace MailKit.Net.Imap {
	/// <summary>
	/// IMAP utility functions.
	/// </summary>
	static class ImapUtils
	{
		static readonly Encoding Latin1 = Encoding.GetEncoding (28591);
		const string QuotedSpecials = " \t()<>@,;:\\\"/[]?=";

		static readonly string[] Months = {
			"Jan", "Feb", "Mar", "Apr", "May", "Jun",
			"Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
		};

		/// <summary>
		/// Formats a date in a format suitable for use with the APPEND command.
		/// </summary>
		/// <returns>The formatted date string.</returns>
		/// <param name="date">The date.</param>
		public static string FormatInternalDate (DateTimeOffset date)
		{
			return string.Format ("{0:D2}-{1}-{2:D4} {3:D2}:{4:D2}:{5:D2} {6:+00;-00}{7:00}",
				date.Day, Months[date.Month - 1], date.Year, date.Hour, date.Minute, date.Second,
				date.Offset.Hours, date.Offset.Minutes);
		}

		static bool TryGetInt32 (string text, ref int index, out int value)
		{
			int startIndex = index;

			value = 0;

			while (index < text.Length && text[index] >= '0' && text[index] <= '9') {
				int digit = text[index] - '0';

				if (value > int.MaxValue / 10) {
					// integer overflow
					return false;
				}

				if (value == int.MaxValue / 10 && digit > int.MaxValue % 10) {
					// integer overflow
					return false;
				}

				value = (value * 10) + digit;
				index++;
			}

			return index > startIndex;
		}

		static bool TryGetInt32 (string text, ref int index, char delim, out int value)
		{
			return TryGetInt32 (text, ref index, out value) && index < text.Length && text[index] == delim;
		}

		static bool TryGetMonth (string text, ref int index, char delim, out int month)
		{
			int startIndex = index;

			month = 0;

			if ((index = text.IndexOf (delim, index)) == -1 || (index - startIndex) != 3)
				return false;

			for (int i = 0; i < Months.Length; i++) {
				if (string.Compare (Months[i], 0, text, startIndex, 3, StringComparison.OrdinalIgnoreCase) == 0) {
					month = i + 1;
					return true;
				}
			}

			return false;
		}

		static bool TryGetTimeZone (string text, ref int index, out TimeSpan timezone)
		{
			int tzone, sign = 1;

			if (index >= text.Length) {
				timezone = new TimeSpan ();
				return false;
			}

			if (text[index] == '-') {
				sign = -1;
				index++;
			} else if (text[index] == '+') {
				index++;
			}

			if (!TryGetInt32 (text, ref index, out tzone)) {
				timezone = new TimeSpan ();
				return false;
			}

			tzone *= sign;

			while (tzone < -1400)
				tzone += 2400;

			while (tzone > 1400)
				tzone -= 2400;

			int minutes = tzone % 100;
			int hours = tzone / 100;

			timezone = new TimeSpan (hours, minutes, 0);

			return true;
		}

		static Exception InvalidInternalDateFormat (string text)
		{
			return new FormatException ("Invalid INTERNALDATE format: " + text);
		}

		/// <summary>
		/// Parses the internal date string.
		/// </summary>
		/// <returns>The date.</returns>
		/// <param name="text">The text to parse.</param>
		public static DateTimeOffset ParseInternalDate (string text)
		{
			int day, month, year, hour, minute, second;
			TimeSpan timezone;
			int index = 0;

			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			if (index >= text.Length || !TryGetInt32 (text, ref index, '-', out day) || day < 1 || day > 31)
				throw InvalidInternalDateFormat (text);

			index++;
			if (index >= text.Length || !TryGetMonth (text, ref index, '-', out month))
				throw InvalidInternalDateFormat (text);

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ' ', out year) || year < 1969)
				throw InvalidInternalDateFormat (text);

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ':', out hour) || hour > 23)
				throw InvalidInternalDateFormat (text);

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ':', out minute) || minute > 59)
				throw InvalidInternalDateFormat (text);

			index++;
			if (index >= text.Length || !TryGetInt32 (text, ref index, ' ', out second) || second > 59)
				throw InvalidInternalDateFormat (text);

			index++;
			if (index >= text.Length || !TryGetTimeZone (text, ref index, out timezone))
				throw InvalidInternalDateFormat (text);

			while (index < text.Length && char.IsWhiteSpace (text[index]))
				index++;

			if (index < text.Length)
				throw InvalidInternalDateFormat (text);

			// return DateTimeOffset.ParseExact (text.Trim (), "d-MMM-yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture.DateTimeFormat);
			return new DateTimeOffset (year, month, day, hour, minute, second, timezone);
		}

		/// <summary>
		/// Attempts to parse an atom token as a set of UIDs.
		/// </summary>
		/// <returns><c>true</c> if the UIDs were successfully parsed, otherwise <c>false</c>.</returns>
		/// <param name="atom">The atom string.</param>
		/// <param name="uids">The UIDs.</param>
		public static bool TryParseUidSet (string atom, out IList<UniqueId> uids)
		{
			var ranges = atom.Split (new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			var list = new List<UniqueId> ();

			uids = null;

			for (int i = 0; i < ranges.Length; i++) {
				var minmax = ranges[i].Split (':');
				uint min;

				if (!uint.TryParse (minmax[0], out min) || min == 0)
					return false;

				if (minmax.Length == 2) {
					uint max;

					if (!uint.TryParse (minmax[1], out max) || max == 0)
						return false;

					for (uint uid = min; uid <= max; uid++)
						list.Add (new UniqueId (uid));
				} else if (minmax.Length == 1) {
					list.Add (new UniqueId (min));
				} else {
					return false;
				}
			}

			uids = new ReadOnlyCollection<UniqueId> (list);

			return true;
		}

		/// <summary>
		/// Formats the array of indexes as a string suitable for use with IMAP commands.
		/// </summary>
		/// <returns>The index set.</returns>
		/// <param name="indexes">The indexes.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="indexes"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// One or more of the indexes has a negative value.
		/// </exception>
		public static string FormatIndexSet (IList<int> indexes)
		{
			if (indexes == null)
				throw new ArgumentNullException ("indexes");

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes were specified.", "indexes");

			var builder = new StringBuilder ();
			int index = 0;

			while (index < indexes.Count) {
				if (indexes[index] < 0)
					throw new ArgumentException ("One or more of the indexes is negative.", "indexes");

				int begin = indexes[index];
				int end = indexes[index];
				int i = index + 1;

				if (i < indexes.Count) {
					if (indexes[i] == end + 1) {
						end = indexes[i++];

						while (i < indexes.Count && indexes[i] == end + 1) {
							end++;
							i++;
						}
					} else if (indexes[i] == end - 1) {
						end = indexes[i++];

						while (i < indexes.Count && indexes[i] == end - 1) {
							end--;
							i++;
						}
					}
				}

				if (builder.Length > 0)
					builder.Append (',');

				if (begin != end)
					builder.AppendFormat ("{0}:{1}", begin + 1, end + 1);
				else
					builder.Append ((begin + 1).ToString ());

				index = i;
			}

			return builder.ToString ();
		}

		/// <summary>
		/// Formats the array of UIDs as a string suitable for use with IMAP commands.
		/// </summary>
		/// <returns>The UID set.</returns>
		/// <param name="uids">The UIDs.</param>
		/// <exception cref="System.ArgumentNullException">
		/// <paramref name="uids"/> is <c>null</c>.
		/// </exception>
		/// <exception cref="System.ArgumentException">
		/// One or more of the UIDs is invalid.
		/// </exception>
		public static string FormatUidSet (IList<UniqueId> uids)
		{
			if (uids == null)
				throw new ArgumentNullException ("uids");

			var range = uids as UniqueIdRange;
			if (range != null)
				return range.ToString ();

			if (uids.Count == 0)
				throw new ArgumentException ("No uids were specified.", "uids");

			var set = uids as UniqueIdSet;
			if (set != null)
				return set.ToString ();

			var builder = new StringBuilder ();
			int index = 0;

			while (index < uids.Count) {
				if (uids[index].Id == 0)
					throw new ArgumentException ("One or more of the uids is invalid.", "uids");

				uint begin = uids[index].Id;
				uint end = uids[index].Id;
				int i = index + 1;

				if (i < uids.Count) {
					if (uids[i].Id == end + 1) {
						end = uids[i++].Id;

						while (i < uids.Count && uids[i].Id == end + 1) {
							end++;
							i++;
						}
					} else if (uids[i].Id == end - 1) {
						end = uids[i++].Id;

						while (i < uids.Count && uids[i].Id == end - 1) {
							end--;
							i++;
						}
					}
				}

				if (builder.Length > 0)
					builder.Append (',');

				if (begin != end)
					builder.AppendFormat ("{0}:{1}", begin, end);
				else
					builder.Append (begin.ToString ());

				index = i;
			}

			return builder.ToString ();
		}

		/// <summary>
		/// Parses an untagged LIST or LSUB response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		public static void ParseFolderList (ImapEngine engine, ImapCommand ic, int index)
		{
			var token = engine.ReadToken (ic.CancellationToken);
			var list = (List<ImapFolder>) ic.UserData;
			var attrs = FolderAttributes.None;
			string encodedName;
			ImapFolder folder;
			char delim;

			// parse the folder attributes list
			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			token = engine.ReadToken (ic.CancellationToken);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom) {
				string atom = (string) token.Value;

				switch (atom) {
				case "\\NoInferiors":   attrs |= FolderAttributes.NoInferiors; break;
				case "\\Noselect":      attrs |= FolderAttributes.NoSelect; break;
				case "\\Marked":        attrs |= FolderAttributes.Marked; break;
				case "\\Unmarked":      attrs |= FolderAttributes.Unmarked; break;
				case "\\NonExistent":   attrs |= FolderAttributes.NonExistent; break;
				case "\\Subscribed":    attrs |= FolderAttributes.Subscribed; break;
				case "\\Remote":        attrs |= FolderAttributes.Remote; break;
				case "\\HasChildren":   attrs |= FolderAttributes.HasChildren; break;
				case "\\HasNoChildren": attrs |= FolderAttributes.HasNoChildren; break;
				case "\\All":           attrs |= FolderAttributes.All; break;
				case "\\Archive":       attrs |= FolderAttributes.Archive; break;
				case "\\Drafts":        attrs |= FolderAttributes.Drafts; break;
				case "\\Flagged":       attrs |= FolderAttributes.Flagged; break;
				case "\\Junk":          attrs |= FolderAttributes.Junk; break;
				case "\\Sent":          attrs |= FolderAttributes.Sent; break;
				case "\\Trash":         attrs |= FolderAttributes.Trash; break;
					// XLIST flags:
				case "\\AllMail":       attrs |= FolderAttributes.All; break;
				case "\\Important":     attrs |= FolderAttributes.Flagged; break;
				case "\\Inbox":         break;
				case "\\Spam":          attrs |= FolderAttributes.Junk; break;
				case "\\Starred":       attrs |= FolderAttributes.Flagged; break;
				}

				token = engine.ReadToken (ic.CancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			// parse the path delimeter
			token = engine.ReadToken (ic.CancellationToken);

			if (token.Type == ImapTokenType.QString) {
				var qstring = (string) token.Value;

				delim = qstring[0];
			} else if (token.Type == ImapTokenType.Nil) {
				delim = '\0';
			} else {
				throw ImapEngine.UnexpectedToken (token, false);
			}

			// parse the folder name
			token = engine.ReadToken (ic.CancellationToken);

			switch (token.Type) {
			case ImapTokenType.Literal:
				encodedName = engine.ReadLiteral (ic.CancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				encodedName = (string) token.Value;
				break;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}

			if (engine.FolderCache.TryGetValue (encodedName, out folder)) {
				attrs |= (folder.Attributes & ~(FolderAttributes.Marked | FolderAttributes.Unmarked));
				folder.UpdateAttributes (attrs);
			} else {
				folder = new ImapFolder (engine, encodedName, attrs, delim);
				engine.FolderCache.Add (encodedName, folder);
			}

			list.Add (folder);
		}

		static string ReadStringToken (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.Literal:
				return engine.ReadLiteral (cancellationToken);
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				return (string) token.Value;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}
		}

		static string ReadNStringToken (ImapEngine engine, bool rfc2047, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			string value;

			switch (token.Type) {
			case ImapTokenType.Literal:
				value = engine.ReadLiteral (cancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				value = (string) token.Value;
				break;
			case ImapTokenType.Nil:
				return null;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}

			return rfc2047 ? Rfc2047.DecodeText (Latin1.GetBytes (value)) : value;
		}

		static uint ReadNumber (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			uint number;

			if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out number))
				throw ImapEngine.UnexpectedToken (token, false);

			return number;
		}

		static bool NeedsQuoting (string value)
		{
			for (int i = 0; i < value.Length; i++) {
				if (value[i] > 127 || char.IsControl (value[i]))
					return true;

				if (QuotedSpecials.IndexOf (value[i]) != -1)
					return true;
			}

			return false;
		}

		static void ParseParameterList (StringBuilder builder, ImapEngine engine, CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = engine.PeekToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				var name = ReadStringToken (engine, cancellationToken);
				var value = ReadStringToken (engine, cancellationToken);

				builder.Append ("; ").Append (name).Append ('=');

				if (NeedsQuoting (value))
					builder.Append (MimeUtils.Quote (value));
				else
					builder.Append (value);
			} while (true);

			// read the ')'
			engine.ReadToken (cancellationToken);
		}

		static ContentType ParseContentType (ImapEngine engine, CancellationToken cancellationToken)
		{
			var type = ReadStringToken (engine, cancellationToken);
			var subtype = ReadStringToken (engine, cancellationToken);
			var token = engine.ReadToken (cancellationToken);
			ContentType contentType;

			if (token.Type == ImapTokenType.Nil)
				return new ContentType (type, subtype);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			var builder = new StringBuilder ();
			builder.AppendFormat ("{0}/{1}", type, subtype);

			ParseParameterList (builder, engine, cancellationToken);

			if (!ContentType.TryParse (builder.ToString (), out contentType))
				contentType = new ContentType (type, subtype);

			return contentType;
		}

		static ContentDisposition ParseContentDisposition (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return null;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			var dsp = ReadStringToken (engine, cancellationToken);
			var builder = new StringBuilder (dsp);
			ContentDisposition disposition;

			token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.OpenParen)
				ParseParameterList (builder, engine, cancellationToken);
			else if (token.Type != ImapTokenType.Nil)
				throw ImapEngine.UnexpectedToken (token, false);

			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			if (!ContentDisposition.TryParse (builder.ToString (), out disposition))
				disposition = new ContentDisposition (dsp);

			return disposition;
		}

		static string[] ParseContentLanguage (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			var languages = new List<string> ();
			string language;

			switch (token.Type) {
			case ImapTokenType.Literal:
				language = engine.ReadLiteral (cancellationToken);
				languages.Add (language);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				language = (string) token.Value;
				languages.Add (language);
				break;
			case ImapTokenType.Nil:
				return null;
			case ImapTokenType.OpenParen:
				do {
					token = engine.PeekToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					language = ReadStringToken (engine, cancellationToken);
					languages.Add (language);
				} while (true);

				// read the ')'
				engine.ReadToken (cancellationToken);
				break;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}

			return languages.ToArray ();
		}

		static Uri ParseContentLocation (ImapEngine engine, CancellationToken cancellationToken)
		{
			var location = ReadNStringToken (engine, false, cancellationToken);

			if (string.IsNullOrWhiteSpace (location))
				return null;

			if (Uri.IsWellFormedUriString (location, UriKind.Absolute))
				return new Uri (location, UriKind.Absolute);

			if (Uri.IsWellFormedUriString (location, UriKind.Relative))
				return new Uri (location, UriKind.Relative);

			return null;
		}

		static void SkipBodyExtensions (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.OpenParen:
				do {
					token = engine.PeekToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					SkipBodyExtensions (engine, cancellationToken);
				} while (true);

				// read the ')'
				engine.ReadToken (cancellationToken);
				break;
			case ImapTokenType.Literal:
				engine.ReadLiteral (cancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
			case ImapTokenType.Nil:
				break;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}
		}

		static BodyPart ParseMultipart (ImapEngine engine, string path, CancellationToken cancellationToken)
		{
			var prefix = path.Length > 0 ? path + "." : string.Empty;
			var body = new BodyPartMultipart ();
			ImapToken token;
			int index = 1;

			do {
				body.BodyParts.Add (ParseBody (engine, prefix + index, cancellationToken));
				token = engine.PeekToken (cancellationToken);
				index++;
			} while (token.Type == ImapTokenType.OpenParen);

			var subtype = ReadStringToken (engine, cancellationToken);

			body.ContentType = new ContentType ("multipart", subtype);
			body.PartSpecifier = path;

			token = engine.PeekToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen) {
				token = engine.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.OpenParen)
					throw ImapEngine.UnexpectedToken (token, false);

				var builder = new StringBuilder ();
				ContentType contentType;

				builder.AppendFormat ("{0}/{1}", body.ContentType.MediaType, body.ContentType.MediaSubtype);
				ParseParameterList (builder, engine, cancellationToken);

				if (ContentType.TryParse (builder.ToString (), out contentType))
					body.ContentType = contentType;

				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentDisposition = ParseContentDisposition (engine, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLanguage = ParseContentLanguage (engine, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLocation = ParseContentLocation (engine, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				SkipBodyExtensions (engine, cancellationToken);

			// read the ')'
			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			return body;
		}

		public static BodyPart ParseBody (ImapEngine engine, string path, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return null;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			token = engine.PeekToken (cancellationToken);

			if (token.Type == ImapTokenType.OpenParen)
				return ParseMultipart (engine, path, cancellationToken);

			var type = ParseContentType (engine, cancellationToken);
			var id = ReadNStringToken (engine, false, cancellationToken);
			var desc = ReadNStringToken (engine, true, cancellationToken);
			// Note: technically, body-fld-enc, is not allowed to be NIL, but we need to deal with broken servers...
			var enc = ReadNStringToken (engine, false, cancellationToken);
			var octets = ReadNumber (engine, cancellationToken);
			BodyPartBasic body;

			if (type.Matches ("message", "rfc822")) {
				var mesg = new BodyPartMessage ();

				// Note: GMail's support for message/rfc822 body parts is broken. Essentially,
				// GMail treats message/rfc822 parts as if they were basic body parts.
				//
				// For examples, see issue #32 and issue #59.
				//
				// The workaround is to check for the expected '(' signifying an envelope token.
				// If we do not get an '(', then we are likely looking at the Content-MD5 token
				// which gets handled below.
				token = engine.PeekToken (cancellationToken);

				if (!engine.IsGMail || token.Type == ImapTokenType.OpenParen) {
					mesg.Envelope = ParseEnvelope (engine, cancellationToken);
					mesg.Body = ParseBody (engine, path, cancellationToken);
					mesg.Lines = ReadNumber (engine, cancellationToken);
				}

				body = mesg;
			} else if (type.Matches ("text", "*")) {
				var text = new BodyPartText ();
				text.Lines = ReadNumber (engine, cancellationToken);
				body = text;
			} else {
				body = new BodyPartBasic ();
			}

			body.ContentTransferEncoding = enc;
			body.ContentDescription = desc;
			body.PartSpecifier = path;
			body.ContentType = type;
			body.ContentId = id;
			body.Octets = octets;

			// if we are parsing a BODYSTRUCTURE, we may get some more tokens before the ')'
			token = engine.PeekToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentMd5 = ReadNStringToken (engine, false, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentDisposition = ParseContentDisposition (engine, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLanguage = ParseContentLanguage (engine, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLocation = ParseContentLocation (engine, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				SkipBodyExtensions (engine, cancellationToken);

			// read the ')'
			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			return body;
		}

		static void AddEnvelopeAddress (InternetAddressList list, ImapEngine engine, CancellationToken cancellationToken)
		{
			var values = new string[4];
			ImapToken token;
			int index = 0;

			do {
				token = engine.ReadToken (cancellationToken);

				switch (token.Type) {
				case ImapTokenType.Literal:
					values[index] = engine.ReadLiteral (cancellationToken);
					break;
				case ImapTokenType.QString:
				case ImapTokenType.Atom:
					values[index] = (string) token.Value;
					break;
				case ImapTokenType.Nil:
					break;
				default:
					throw ImapEngine.UnexpectedToken (token, false);
				}

				index++;
			} while (index < 4);

			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			string name = null;

			if (values[0] != null) {
				// Note: since the ImapEngine.ReadLiteral() uses iso-8859-1
				// to convert bytes to unicode, we can undo that here:
				name = Rfc2047.DecodePhrase (Latin1.GetBytes (values[0]));
			}

			string address = values[3] != null ? values[2] + "@" + values[3] : values[2];
			DomainList route;

			if (values[1] != null && DomainList.TryParse (values[1], out route))
				list.Add (new MailboxAddress (name, route, address));
			else
				list.Add (new MailboxAddress (name, address));
		}

		static void ParseEnvelopeAddressList (InternetAddressList list, ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type != ImapTokenType.OpenParen)
					throw ImapEngine.UnexpectedToken (token, false);

				AddEnvelopeAddress (list, engine, cancellationToken);
			} while (true);
		}

		static DateTimeOffset? ParseEnvelopeDate (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			DateTimeOffset date;
			string value;

			switch (token.Type) {
			case ImapTokenType.Literal:
				value = engine.ReadLiteral (cancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				value = (string) token.Value;
				break;
			case ImapTokenType.Nil:
				return null;
			default:
				throw ImapEngine.UnexpectedToken (token, false);
			}

			if (!DateUtils.TryParseDateTime (value, out date))
				return null;

			return date;
		}

		/// <summary>
		/// Parses the ENVELOPE parenthesized list.
		/// </summary>
		/// <returns>The envelope.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static Envelope ParseEnvelope (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			string nstring;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			var envelope = new Envelope ();
			envelope.Date = ParseEnvelopeDate (engine, cancellationToken);
			envelope.Subject = ReadNStringToken (engine, true, cancellationToken);
			ParseEnvelopeAddressList (envelope.From, engine, cancellationToken);
			ParseEnvelopeAddressList (envelope.Sender, engine, cancellationToken);
			ParseEnvelopeAddressList (envelope.ReplyTo, engine, cancellationToken);
			ParseEnvelopeAddressList (envelope.To, engine, cancellationToken);
			ParseEnvelopeAddressList (envelope.Cc, engine, cancellationToken);
			ParseEnvelopeAddressList (envelope.Bcc, engine, cancellationToken);

			if ((nstring = ReadNStringToken (engine, false, cancellationToken)) != null)
				envelope.InReplyTo = MimeUtils.EnumerateReferences (nstring).FirstOrDefault ();

			if ((nstring = ReadNStringToken (engine, false, cancellationToken)) != null)
				envelope.MessageId = MimeUtils.EnumerateReferences (nstring).FirstOrDefault ();

			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			return envelope;
		}

		/// <summary>
		/// Formats a flags list suitable for use with the APPEND command.
		/// </summary>
		/// <returns>The flags list string.</returns>
		/// <param name="flags">The message flags.</param>
		public static string FormatFlagsList (MessageFlags flags)
		{
			var builder = new StringBuilder ();

			builder.Append ('(');
			if ((flags & MessageFlags.Answered) != 0)
				builder.Append ("\\Answered ");
			if ((flags & MessageFlags.Deleted) != 0)
				builder.Append ("\\Deleted ");
			if ((flags & MessageFlags.Draft) != 0)
				builder.Append ("\\Draft ");
			if ((flags & MessageFlags.Flagged) != 0)
				builder.Append ("\\Flagged ");
			if ((flags & MessageFlags.Seen) != 0)
				builder.Append ("\\Seen ");
			if (builder.Length > 1)
				builder.Length--;
			builder.Append (')');

			return builder.ToString ();
		}

		/// <summary>
		/// Parses the flags list.
		/// </summary>
		/// <returns>The message flags.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static MessageFlags ParseFlagsList (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			var flags = MessageFlags.None;

			if (token.Type != ImapTokenType.OpenParen) {
				Debug.WriteLine ("Expected '(' at the start of the flags list, but got: {0}", token);
				throw ImapEngine.UnexpectedToken (token, false);
			}

			token = engine.ReadToken (cancellationToken);

			while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.Flag) {
				string flag = (string) token.Value;
				switch (flag) {
				case "\\Answered": flags |= MessageFlags.Answered; break;
				case "\\Deleted":  flags |= MessageFlags.Deleted; break;
				case "\\Draft":    flags |= MessageFlags.Draft; break;
				case "\\Flagged":  flags |= MessageFlags.Flagged; break;
				case "\\Seen":     flags |= MessageFlags.Seen; break;
				case "\\Recent":   flags |= MessageFlags.Recent; break;
				case "\\*":        flags |= MessageFlags.UserDefined; break;
				}

				token = engine.ReadToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				Debug.WriteLine ("Expected to find a ')' token terminating the flags list, but got: {0}", token);
				throw ImapEngine.UnexpectedToken (token, false);
			}

			return flags;
		}

		/// <summary>
		/// Parses the X-GM-LABELS list.
		/// </summary>
		/// <returns>The message labels.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static ReadOnlyCollection<string> ParseLabelsList (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			var labels = new List<string> ();

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (token, false);

			// Note: GMail's IMAP implementation is broken and does not quote strings with ']' like it should.
			token = engine.ReadToken (ImapStream.GMailLabelSpecials, cancellationToken);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString) {
				var label = engine.DecodeMailboxName ((string) token.Value);

				labels.Add (label);

				token = engine.ReadToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (token, false);

			return new ReadOnlyCollection<string> (labels);
		}

		static MessageThread ParseThread (ImapEngine engine, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			MessageThread thread, node, child;
			uint uid;

			if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid))
				throw ImapEngine.UnexpectedToken (token, false);

			node = thread = new MessageThread (new UniqueId (uid));

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type == ImapTokenType.OpenParen) {
					child = ParseThread (engine, cancellationToken);
					node.Add (child);
				} else {
					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid))
						throw ImapEngine.UnexpectedToken (token, false);

					child = new MessageThread (new UniqueId (uid));
					node.Add (child);
					node = child;
				}
			} while (true);

			return thread;
		}

		/// <summary>
		/// Parses the threads.
		/// </summary>
		/// <returns>The threads.</returns>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static IList<MessageThread> ParseThreads (ImapEngine engine, CancellationToken cancellationToken)
		{
			var threads = new List<MessageThread> ();
			ImapToken token;

			do {
				token = engine.PeekToken (cancellationToken);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = engine.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.OpenParen)
					throw ImapEngine.UnexpectedToken (token, false);

				threads.Add (ParseThread (engine, cancellationToken));
			} while (true);

			return threads;
		}
	}
}
