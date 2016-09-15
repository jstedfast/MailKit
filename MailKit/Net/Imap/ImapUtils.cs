//
// ImapUtils.cs
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
		const string QuotedSpecials = " \t()<>@,;:\\\"/[]?=";
		static int InboxLength = "INBOX".Length;

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
				throw new ArgumentNullException (nameof (indexes));

			if (indexes.Count == 0)
				throw new ArgumentException ("No indexes were specified.", nameof (indexes));

			var builder = new StringBuilder ();
			int index = 0;

			while (index < indexes.Count) {
				if (indexes[index] < 0)
					throw new ArgumentException ("One or more of the indexes is negative.", nameof (indexes));

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
				throw new ArgumentNullException (nameof (uids));

			if (uids.Count == 0)
				throw new ArgumentException ("No uids were specified.", nameof (uids));

			return UniqueIdSet.ToString (uids);
		}

		/// <summary>
		/// Parses an untagged ID response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		public static void ParseImplementation (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "ID", "{0}");
			var token = engine.ReadToken (ic.CancellationToken);
			ImapImplementation implementation;

			if (token.Type == ImapTokenType.Nil)
				return;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			token = engine.PeekToken (ic.CancellationToken);

			implementation = new ImapImplementation ();

			while (token.Type != ImapTokenType.CloseParen) {
				var property = ReadStringToken (engine, format, ic.CancellationToken);
				var value = ReadNStringToken (engine, format, false, ic.CancellationToken);

				implementation.Properties[property] = value;

				token = engine.PeekToken (ic.CancellationToken);
			}

			ic.UserData = implementation;

			// read the ')' token
			engine.ReadToken (ic.CancellationToken);
		}

		/// <summary>
		/// Canonicalize the name of the mailbox.
		/// </summary>
		/// <remarks>
		/// Canonicalizes the name of the mailbox by replacing various
		/// capitalizations of "INBOX" with the literal "INBOX" string.
		/// </remarks>
		/// <returns>The mailbox name.</returns>
		/// <param name="mailboxName">The encoded mailbox name.</param>
		/// <param name="directorySeparator">The directory separator.</param>
		public static string CanonicalizeMailboxName (string mailboxName, char directorySeparator)
		{
			if (!mailboxName.StartsWith ("INBOX", StringComparison.OrdinalIgnoreCase))
				return mailboxName;

			if (mailboxName.Length > InboxLength && mailboxName[InboxLength] == directorySeparator)
				return "INBOX" + mailboxName.Substring (InboxLength);

			if (mailboxName.Length == InboxLength)
				return "INBOX";

			return mailboxName;
		}

		/// <summary>
		/// Determines whether the specified mailbox is the Inbox.
		/// </summary>
		/// <returns><c>true</c> if the specified mailbox name is the Inbox; otherwise, <c>false</c>.</returns>
		/// <param name="mailboxName">The mailbox name.</param>
		public static bool IsInbox (string mailboxName)
		{
			return string.Compare (mailboxName, "INBOX", StringComparison.OrdinalIgnoreCase) == 0;
		}

		/// <summary>
		/// Parses an untagged LIST or LSUB response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		public static void ParseFolderList (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "LIST", "{0}");
			var token = engine.ReadToken (ic.CancellationToken);
			var list = (List<ImapFolder>) ic.UserData;
			var attrs = FolderAttributes.None;
			string encodedName;
			ImapFolder folder;
			char delim;

			// parse the folder attributes list
			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			token = engine.ReadToken (ic.CancellationToken);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom) {
				var atom = (string) token.Value;

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
				case "\\Inbox":         attrs |= FolderAttributes.Inbox; break;
				case "\\Spam":          attrs |= FolderAttributes.Junk; break;
				case "\\Starred":       attrs |= FolderAttributes.Flagged; break;
				}

				token = engine.ReadToken (ic.CancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (format, token);

			// parse the path delimeter
			token = engine.ReadToken (ic.CancellationToken);

			if (token.Type == ImapTokenType.QString) {
				var qstring = (string) token.Value;

				delim = qstring[0];
			} else if (token.Type == ImapTokenType.Nil) {
				delim = '\0';
			} else {
				throw ImapEngine.UnexpectedToken (format, token);
			}

			// parse the folder name
			token = engine.ReadToken (ImapStream.AtomSpecials, ic.CancellationToken);

			switch (token.Type) {
			case ImapTokenType.Literal:
				encodedName = engine.ReadLiteral (ic.CancellationToken);
				break;
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				encodedName = (string) token.Value;
				break;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}

			if (IsInbox (encodedName))
				attrs |= FolderAttributes.Inbox;

			if (engine.GetCachedFolder (encodedName, out folder)) {
				attrs |= (folder.Attributes & ~(FolderAttributes.Marked | FolderAttributes.Unmarked));
				folder.UpdateAttributes (attrs);
			} else {
				folder = engine.CreateImapFolder (encodedName, attrs, delim);
				engine.CacheFolder (folder);
			}

			list.Add (folder);
		}

		/// <summary>
		/// Parses an untagged METADATA response.
		/// </summary>
		/// <param name="engine">The IMAP engine.</param>
		/// <param name="ic">The IMAP command.</param>
		/// <param name="index">The index.</param>
		public static void ParseMetadata (ImapEngine engine, ImapCommand ic, int index)
		{
			string format = string.Format (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "METADATA", "{0}");
			var encodedName = ReadStringToken (engine, format, ic.CancellationToken);
			var metadata = (MetadataCollection)ic.UserData;
			ImapFolder folder;

			engine.GetCachedFolder (encodedName, out folder);

			var token = engine.ReadToken (ic.CancellationToken);

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			while (token.Type != ImapTokenType.CloseParen) {
				var tag = ReadStringToken (engine, format, ic.CancellationToken);
				var value = ReadStringToken (engine, format, ic.CancellationToken);

				metadata.Add (new Metadata (MetadataTag.Create (tag), value));

				token = engine.PeekToken (ic.CancellationToken);
			}

			// read the closing paren
			engine.ReadToken (ic.CancellationToken);
		}

		static string ReadStringToken (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.Literal:
				return engine.ReadLiteral (cancellationToken);
			case ImapTokenType.QString:
			case ImapTokenType.Atom:
				return (string) token.Value;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		static string ReadNStringToken (ImapEngine engine, string format, bool rfc2047, CancellationToken cancellationToken)
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
				throw ImapEngine.UnexpectedToken (format, token);
			}

			return rfc2047 ? Rfc2047.DecodeText (ImapEngine.Latin1.GetBytes (value)) : value;
		}

		static uint ReadNumber (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			uint number;

			if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out number))
				throw ImapEngine.UnexpectedToken (format, token);

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

		static void ParseParameterList (StringBuilder builder, ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			ImapToken token;

			do {
				token = engine.PeekToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				var name = ReadStringToken (engine, format, cancellationToken);

				// Note: technically, the value should also be a 'string' token and not an 'nstring',
				// but issue #124 reveals a server that is sending NIL for boundary values.
				var value = ReadNStringToken (engine, format, false, cancellationToken) ?? string.Empty;

				builder.Append ("; ").Append (name).Append ('=');

				if (NeedsQuoting (value))
					builder.Append (MimeUtils.Quote (value));
				else
					builder.Append (value);
			} while (true);

			// read the ')'
			engine.ReadToken (cancellationToken);
		}

		static bool ParseContentType (ImapEngine engine, string format, CancellationToken cancellationToken, out ContentType contentType, out string value)
		{
			var type = ReadNStringToken (engine, format, false, cancellationToken) ?? "application";
			var token = engine.PeekToken (cancellationToken);
			string subtype;

			value = null;

			// Note: work around broken IMAP server implementations...
			if (token.Type == ImapTokenType.OpenParen) {
				if (engine.IsGMail) {
					// Note: GMail's IMAP server implementation breaks when it encounters
					// nested multiparts with the same boundary and returns a BODYSTRUCTURE
					// like the example in https://github.com/jstedfast/MailKit/issues/205
					contentType = null;
					value = type;
					return false;
				}

				// Note: In other IMAP server implementations, such as the one found in
				// https://github.com/jstedfast/MailKit/issues/371, if the server comes
				// across something like "Content-Type: X-ZIP", it will only send a
				// media-subtype token and completely fail to send a media-type token.
				subtype = type;
				type = "application";
			} else {
				subtype = ReadNStringToken (engine, format, false, cancellationToken) ?? string.Empty;
			}

			token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil) {
				contentType = new ContentType (type, subtype);
				return true;
			}

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			var builder = new StringBuilder ();
			builder.AppendFormat ("{0}/{1}", type, subtype);

			ParseParameterList (builder, engine, format, cancellationToken);

			if (!ContentType.TryParse (builder.ToString (), out contentType))
				contentType = new ContentType (type, subtype);

			return true;
		}

		static ContentDisposition ParseContentDisposition (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return null;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			var dsp = ReadStringToken (engine, format, cancellationToken);
			var builder = new StringBuilder (dsp);
			ContentDisposition disposition;

			token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.OpenParen)
				ParseParameterList (builder, engine, format, cancellationToken);
			else if (token.Type != ImapTokenType.Nil)
				throw ImapEngine.UnexpectedToken (format, token);

			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (format, token);

			if (!ContentDisposition.TryParse (builder.ToString (), out disposition))
				disposition = new ContentDisposition (dsp);

			return disposition;
		}

		static string[] ParseContentLanguage (ImapEngine engine, string format, CancellationToken cancellationToken)
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

					language = ReadStringToken (engine, format, cancellationToken);
					languages.Add (language);
				} while (true);

				// read the ')'
				engine.ReadToken (cancellationToken);
				break;
			default:
				throw ImapEngine.UnexpectedToken (format, token);
			}

			return languages.ToArray ();
		}

		static Uri ParseContentLocation (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var location = ReadNStringToken (engine, format, false, cancellationToken);

			if (string.IsNullOrWhiteSpace (location))
				return null;

			if (Uri.IsWellFormedUriString (location, UriKind.Absolute))
				return new Uri (location, UriKind.Absolute);

			if (Uri.IsWellFormedUriString (location, UriKind.Relative))
				return new Uri (location, UriKind.Relative);

			return null;
		}

		static void SkipBodyExtensions (ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			switch (token.Type) {
			case ImapTokenType.OpenParen:
				do {
					token = engine.PeekToken (cancellationToken);

					if (token.Type == ImapTokenType.CloseParen)
						break;

					SkipBodyExtensions (engine, format, cancellationToken);
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
				throw ImapEngine.UnexpectedToken (format, token);
			}
		}

		static BodyPart ParseMultipart (ImapEngine engine, string format, string path, string subtype, CancellationToken cancellationToken)
		{
			var prefix = path.Length > 0 ? path + "." : string.Empty;
			var body = new BodyPartMultipart ();
			ImapToken token;
			int index = 1;

			// Note: if subtype is not null, then we are working around a GMail bug...
			if (subtype == null) {
				do {
					body.BodyParts.Add (ParseBody (engine, format, prefix + index, cancellationToken));
					token = engine.PeekToken (cancellationToken);
					index++;
				} while (token.Type == ImapTokenType.OpenParen);

				subtype = ReadStringToken (engine, format, cancellationToken);
			}

			body.ContentType = new ContentType ("multipart", subtype);
			body.PartSpecifier = path;

			token = engine.PeekToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen) {
				token = engine.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.OpenParen)
					throw ImapEngine.UnexpectedToken (format, token);

				var builder = new StringBuilder ();
				ContentType contentType;

				builder.AppendFormat ("{0}/{1}", body.ContentType.MediaType, body.ContentType.MediaSubtype);
				ParseParameterList (builder, engine, format, cancellationToken);

				if (ContentType.TryParse (builder.ToString (), out contentType))
					body.ContentType = contentType;

				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentDisposition = ParseContentDisposition (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLanguage = ParseContentLanguage (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLocation = ParseContentLocation (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				SkipBodyExtensions (engine, format, cancellationToken);

			// read the ')'
			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (format, token);

			return body;
		}

		public static BodyPart ParseBody (ImapEngine engine, string format, string path, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return null;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			token = engine.PeekToken (cancellationToken);

			if (token.Type == ImapTokenType.OpenParen)
				return ParseMultipart (engine, format, path, null, cancellationToken);

			ContentType type;
			string value;

			if (!ParseContentType (engine, format, cancellationToken, out type, out value)) {
				// GMail breakage... yay! What we have is a nested multipart with
				// the same boundary as its parent.
				return ParseMultipart (engine, format, path, value, cancellationToken);
			}

			var id = ReadNStringToken (engine, format, false, cancellationToken);
			var desc = ReadNStringToken (engine, format, true, cancellationToken);
			// Note: technically, body-fld-enc, is not allowed to be NIL, but we need to deal with broken servers...
			var enc = ReadNStringToken (engine, format, false, cancellationToken);
			var octets = ReadNumber (engine, format, cancellationToken);
			BodyPartBasic body;

			if (type.IsMimeType ("message", "rfc822")) {
				var mesg = new BodyPartMessage ();

				// Note: GMail (and potentially other IMAP servers) will send body-part-basic
				// expressions instead of body-part-msg expressions when they encounter
				// message/rfc822 MIME parts that are illegally encoded using base64 (or
				// quoted-printable?). According to rfc3501, IMAP servers are REQUIRED to
				// send body-part-msg expressions for message/rfc822 parts, however, it is
				// understandable why GMail (and other IMAP servers?) do what they do in this
				// particular case.
				//
				// For examples, see issue #32 and issue #59.
				//
				// The workaround is to check for the expected '(' signifying an envelope token.
				// If we do not get an '(', then we are likely looking at the Content-MD5 token
				// which gets handled below.
				token = engine.PeekToken (cancellationToken);

				if (token.Type == ImapTokenType.OpenParen) {
					mesg.Envelope = ParseEnvelope (engine, cancellationToken);
					mesg.Body = ParseBody (engine, format, path, cancellationToken);
					mesg.Lines = ReadNumber (engine, format, cancellationToken);
				}

				body = mesg;
			} else if (type.IsMimeType ("text", "*")) {
				var text = new BodyPartText ();
				text.Lines = ReadNumber (engine, format, cancellationToken);
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
				body.ContentMd5 = ReadNStringToken (engine, format, false, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentDisposition = ParseContentDisposition (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLanguage = ParseContentLanguage (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				body.ContentLocation = ParseContentLocation (engine, format, cancellationToken);
				token = engine.PeekToken (cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				SkipBodyExtensions (engine, format, cancellationToken);

			// read the ')'
			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (format, token);

			return body;
		}

		struct EnvelopeAddress
		{
			public readonly string Name;
			public readonly string Route;
			public readonly string Mailbox;
			public readonly string Domain;

			public EnvelopeAddress (string[] values)
			{
				Name = values[0];
				Route = values[1];
				Mailbox = values[2];
				Domain = values[3];
			}

			public bool IsGroupStart {
				get { return Domain == null; }
			}

			public bool IsGroupEnd {
				get { return Mailbox == null; }
			}

			public MailboxAddress ToMailboxAddress ()
			{
				var domain = Domain;
				string name = null;

				if (Name != null) {
					// Note: since the ImapEngine.ReadLiteral() uses iso-8859-1
					// to convert bytes to unicode, we can undo that here:
					name = Rfc2047.DecodePhrase (ImapEngine.Latin1.GetBytes (Name));
				}

				// Note: When parsing mailbox addresses w/o a domain, Dovecot will
				// use "MISSING_DOMAIN" as the domain string to prevent it from
				// appearing as a group address in the IMAP ENVELOPE response.
				if (domain == "MISSING_DOMAIN")
					domain = null;

				string address = domain != null ? Mailbox + "@" + domain : Mailbox;
				DomainList route;

				if (Route != null && DomainList.TryParse (Route, out route))
					return new MailboxAddress (name, route, address);

				return new MailboxAddress (name, address);
			}

			public GroupAddress ToGroupAddress ()
			{
				var name = string.Empty;

				if (Mailbox != null) {
					// Note: since the ImapEngine.ReadLiteral() uses iso-8859-1
					// to convert bytes to unicode, we can undo that here:
					name = Rfc2047.DecodePhrase (ImapEngine.Latin1.GetBytes (Mailbox));
				}

				return new GroupAddress (name);
			}
		}

		static EnvelopeAddress ParseEnvelopeAddress (ImapEngine engine, string format, CancellationToken cancellationToken)
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
					throw ImapEngine.UnexpectedToken (format, token);
				}

				index++;
			} while (index < 4);

			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (format, token);

			return new EnvelopeAddress (values);
		}

		static void ParseEnvelopeAddressList (InternetAddressList list, ImapEngine engine, string format, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);

			if (token.Type == ImapTokenType.Nil)
				return;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			GroupAddress group = null;

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type != ImapTokenType.OpenParen)
					throw ImapEngine.UnexpectedToken (format, token);

				var item = ParseEnvelopeAddress (engine, format, cancellationToken);

				if (item.IsGroupStart && !engine.IsGMail && group == null) {
					group = item.ToGroupAddress ();
					list.Add (group);
				} else if (item.IsGroupEnd) {
					group = null;
				} else if (group != null) {
					group.Members.Add (item.ToMailboxAddress ());
				} else {
					list.Add (item.ToMailboxAddress ());
				}
			} while (true);
		}

		static DateTimeOffset? ParseEnvelopeDate (ImapEngine engine, string format, CancellationToken cancellationToken)
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
				throw ImapEngine.UnexpectedToken (format, token);
			}

			if (!DateUtils.TryParse (value, out date))
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
			string format = string.Format (ImapEngine.GenericItemSyntaxErrorFormat, "ENVELOPE", "{0}");
			var token = engine.ReadToken (cancellationToken);
			string nstring;

			if (token.Type != ImapTokenType.OpenParen)
				throw ImapEngine.UnexpectedToken (format, token);

			var envelope = new Envelope ();
			envelope.Date = ParseEnvelopeDate (engine, format, cancellationToken);
			envelope.Subject = ReadNStringToken (engine, format, true, cancellationToken);
			ParseEnvelopeAddressList (envelope.From, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.Sender, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.ReplyTo, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.To, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.Cc, engine, format, cancellationToken);
			ParseEnvelopeAddressList (envelope.Bcc, engine, format, cancellationToken);

			if ((nstring = ReadNStringToken (engine, format, false, cancellationToken)) != null)
				envelope.InReplyTo = MimeUtils.EnumerateReferences (nstring).FirstOrDefault ();

			if ((nstring = ReadNStringToken (engine, format, false, cancellationToken)) != null)
				envelope.MessageId = MimeUtils.ParseMessageId (nstring);

			token = engine.ReadToken (cancellationToken);

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (format, token);

			return envelope;
		}

		/// <summary>
		/// Formats a flags list suitable for use with the APPEND command.
		/// </summary>
		/// <returns>The flags list string.</returns>
		/// <param name="flags">The message flags.</param>
		/// <param name="numUserFlags">The number of user-defined flags.</param>
		public static string FormatFlagsList (MessageFlags flags, int numUserFlags)
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

			for (int i = 0; i < numUserFlags; i++)
				builder.Append ("%S ");

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
		/// <param name="name">The name of the flags being parsed.</param>
		/// <param name="userFlags">A hash set of user-defined message flags that will be populated if non-null.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static MessageFlags ParseFlagsList (ImapEngine engine, string name, HashSet<string> userFlags, CancellationToken cancellationToken)
		{
			var specials = engine.IsGMail ? ImapStream.GMailLabelSpecials : ImapStream.AtomSpecials;
			var token = engine.ReadToken (cancellationToken);
			var flags = MessageFlags.None;

			if (token.Type != ImapTokenType.OpenParen) {
				Debug.WriteLine ("Expected '(' at the start of the {0} list, but got: {1}", name, token);
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, name, token);
			}

			token = engine.ReadToken (specials, cancellationToken);

			while (token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.Flag) {
				var flag = (string) token.Value;

				switch (flag) {
				case "\\Answered": flags |= MessageFlags.Answered; break;
				case "\\Deleted":  flags |= MessageFlags.Deleted; break;
				case "\\Draft":    flags |= MessageFlags.Draft; break;
				case "\\Flagged":  flags |= MessageFlags.Flagged; break;
				case "\\Seen":     flags |= MessageFlags.Seen; break;
				case "\\Recent":   flags |= MessageFlags.Recent; break;
				case "\\*":        flags |= MessageFlags.UserDefined; break;
				default:
					if (userFlags != null)
						userFlags.Add (flag);
					break;
				}

				token = engine.ReadToken (specials, cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen) {
				Debug.WriteLine ("Expected to find a ')' token terminating the {0} list, but got: {1}", name, token);
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, name, token);
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
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, "X-GM-LABELS", token);

			// Note: GMail's IMAP implementation is broken and does not quote strings with ']' like it should.
			token = engine.ReadToken (ImapStream.GMailLabelSpecials, cancellationToken);

			while (token.Type == ImapTokenType.Flag || token.Type == ImapTokenType.Atom || token.Type == ImapTokenType.QString || token.Type == ImapTokenType.Nil) {
				// Apparently it's possible to set a NIL label in GMail...
				//
				// See https://github.com/jstedfast/MailKit/issues/244 for an example.
				if (token.Type != ImapTokenType.Nil) {
					var label = engine.DecodeMailboxName ((string) token.Value);

					labels.Add (label);
				} else {
					labels.Add (null);
				}

				token = engine.ReadToken (ImapStream.GMailLabelSpecials, cancellationToken);
			}

			if (token.Type != ImapTokenType.CloseParen)
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericItemSyntaxErrorFormat, "X-GM-LABELS", token);

			return new ReadOnlyCollection<string> (labels);
		}

		static MessageThread ParseThread (ImapEngine engine, uint uidValidity, CancellationToken cancellationToken)
		{
			var token = engine.ReadToken (cancellationToken);
			MessageThread thread, node, child;
			uint uid;

			if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid))
				throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);

			node = thread = new MessageThread (new UniqueId (uidValidity, uid));

			do {
				token = engine.ReadToken (cancellationToken);

				if (token.Type == ImapTokenType.CloseParen)
					break;

				if (token.Type == ImapTokenType.OpenParen) {
					child = ParseThread (engine, uidValidity, cancellationToken);
					node.Children.Add (child);
				} else {
					if (token.Type != ImapTokenType.Atom || !uint.TryParse ((string) token.Value, out uid))
						throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);

					child = new MessageThread (new UniqueId (uidValidity, uid));
					node.Children.Add (child);
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
		/// <param name="uidValidity">The UIDVALIDITY of the folder.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		public static IList<MessageThread> ParseThreads (ImapEngine engine, uint uidValidity, CancellationToken cancellationToken)
		{
			var threads = new List<MessageThread> ();
			ImapToken token;

			do {
				token = engine.PeekToken (cancellationToken);

				if (token.Type == ImapTokenType.Eoln)
					break;

				token = engine.ReadToken (cancellationToken);

				if (token.Type != ImapTokenType.OpenParen)
					throw ImapEngine.UnexpectedToken (ImapEngine.GenericUntaggedResponseSyntaxErrorFormat, "THREAD", token);

				threads.Add (ParseThread (engine, uidValidity, cancellationToken));
			} while (true);

			return threads;
		}
	}
}
